using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;

namespace NPUtil.Modules.FileOrganizer.Classification;

public interface IEmbeddingEngine
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    string ActiveBackendName { get; }
    bool IsNpuAccelerated { get; }
    int EmbeddingDimension { get; }
    void ReloadModel();
}

public class EmbeddingEngine : IEmbeddingEngine, IDisposable
{
    public const int DefaultDimension = 384;
    private const int MaxSequenceLength = 256;

    private readonly ModelManager _modelManager;
    private readonly QnnDetector _qnnDetector;
    private readonly IHardwareService _hardwareService;
    private readonly ILogger _logger;

    private InferenceSession? _session;
    private Dictionary<string, int>? _vocab;
    private readonly object _sessionLock = new();

    public string ActiveBackendName { get; private set; } = "Initializing...";
    public bool IsNpuAccelerated { get; private set; }
    public int EmbeddingDimension => DefaultDimension;

    public EmbeddingEngine(
        ModelManager modelManager,
        QnnDetector qnnDetector,
        IHardwareService hardwareService,
        ILogger logger)
    {
        _modelManager = modelManager;
        _qnnDetector = qnnDetector;
        _hardwareService = hardwareService;
        _logger = logger;

        Initialize();
    }

    public void ReloadModel()
    {
        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
            Initialize();
        }
    }

    private void Initialize()
    {
        var info = _modelManager.GetModelInfo();
        if (info.IsInstalled && File.Exists(info.ModelFilePath))
        {
            try
            {
                _logger.Info("EmbeddingEngine", $"Loading ONNX model from {info.ModelFilePath}...");

                var sessionOptions = new SessionOptions();
                var backendResult = _qnnDetector.DetectAndConfigureSession(sessionOptions);

                _session = new InferenceSession(info.ModelFilePath, sessionOptions);
                ActiveBackendName = backendResult.DisplayName;
                IsNpuAccelerated = backendResult.IsNpu;
                _hardwareService.SetActiveAiBackend(ActiveBackendName);

                LoadVocabulary(info.TokenizerFilePath);

                // Run a warm-up inference to verify execution provider functionality
                WarmUpSession();

                _logger.Info("EmbeddingEngine", $"Model successfully initialized with backend: {ActiveBackendName}");
                return;
            }
            catch (Exception ex)
            {
                _logger.Warning("EmbeddingEngine", "Failed to initialize ONNX runtime session, falling back to built-in vectorizer.", ex);
                _session?.Dispose();
                _session = null;
            }
        }

        ActiveBackendName = "Lexical-Semantic Vectorizer (Fast Built-in)";
        IsNpuAccelerated = false;
        _hardwareService.SetActiveAiBackend(ActiveBackendName);
        _logger.Info("EmbeddingEngine", "Using built-in multi-lingual lexical-semantic vectorizer.");
    }

    private void WarmUpSession()
    {
        if (_session == null) return;
        try
        {
            var dummyInput = new long[1 * 8];
            var dummyMask = new long[1 * 8];
            for (var i = 0; i < 8; i++)
            {
                dummyInput[i] = 101; // [CLS]
                dummyMask[i] = 1;
            }

            var inputTensor = new DenseTensor<long>(dummyInput, [1, 8]);
            var maskTensor = new DenseTensor<long>(dummyMask, [1, 8]);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
            };

            // Check if model expects token_type_ids
            if (_session.InputMetadata.ContainsKey("token_type_ids"))
            {
                var typeTensor = new DenseTensor<long>(new long[1 * 8], [1, 8]);
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", typeTensor));
            }

            using var results = _session.Run(inputs);
            _logger.Debug("EmbeddingEngine", "Warm-up inference passed successfully.");
        }
        catch (Exception ex)
        {
            _logger.Warning("EmbeddingEngine", "Warm-up inference failed with current provider. Falling back to CPU.", ex);
            // Re-attempt strictly with CPU
            try
            {
                _session.Dispose();
                var cpuOptions = new SessionOptions();
                cpuOptions.AppendExecutionProvider_CPU(useArena: 1);
                _session = new InferenceSession(_modelManager.GetModelInfo().ModelFilePath, cpuOptions);
                ActiveBackendName = "CPU (ARM64 Vectorized / NEON)";
                IsNpuAccelerated = false;
                _hardwareService.SetActiveAiBackend(ActiveBackendName);
            }
            catch { /* fallback */ }
        }
    }

    private void LoadVocabulary(string tokenizerPath)
    {
        if (!File.Exists(tokenizerPath)) return;
        try
        {
            var json = File.ReadAllText(tokenizerPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("model", out var modelProp) &&
                modelProp.TryGetProperty("vocab", out var vocabProp))
            {
                _vocab = new Dictionary<string, int>(StringComparer.Ordinal);
                if (vocabProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in vocabProp.EnumerateObject())
                    {
                        if (prop.Value.TryGetInt32(out var id))
                        {
                            _vocab[prop.Name] = id;
                        }
                    }
                }
                else if (vocabProp.ValueKind == JsonValueKind.Array)
                {
                    var id = 0;
                    foreach (var item in vocabProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                        {
                            var token = item[0].GetString();
                            if (!string.IsNullOrEmpty(token)) _vocab[token] = id;
                        }
                        else if (item.ValueKind == JsonValueKind.String)
                        {
                            var token = item.GetString();
                            if (!string.IsNullOrEmpty(token)) _vocab[token] = id;
                        }
                        id++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("EmbeddingEngine", "Failed to parse tokenizer vocabulary JSON.", ex);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[DefaultDimension];
        }

        if (_session != null && _vocab != null)
        {
            try
            {
                return await Task.Run(() => InferOnnxEmbedding(text), ct);
            }
            catch (Exception ex)
            {
                _logger.Warning("EmbeddingEngine", "ONNX inference error, using built-in vectorizer.", ex);
            }
        }

        return GenerateFallbackEmbedding(text);
    }

    private float[] InferOnnxEmbedding(string text)
    {
        lock (_sessionLock)
        {
            if (_session == null || _vocab == null) return GenerateFallbackEmbedding(text);

            var tokens = Tokenize(text, MaxSequenceLength);
            var seqLen = tokens.Length;

            var inputIds = new long[seqLen];
            var attentionMask = new long[seqLen];

            for (var i = 0; i < seqLen; i++)
            {
                inputIds[i] = tokens[i];
                attentionMask[i] = 1;
            }

            var inputTensor = new DenseTensor<long>(inputIds, [1, seqLen]);
            var maskTensor = new DenseTensor<long>(attentionMask, [1, seqLen]);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
            };

            if (_session.InputMetadata.ContainsKey("token_type_ids"))
            {
                var typeTensor = new DenseTensor<long>(new long[seqLen], [1, seqLen]);
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", typeTensor));
            }

            using var results = _session.Run(inputs);
            var output = results.First(r => r.Name.Contains("last_hidden_state") || r.Name.Contains("token_embeddings") || r.Name == "output_0");
            var tensor = output.AsTensor<float>();

            // Mean pooling with attention mask
            var pooled = new float[DefaultDimension];
            var dims = tensor.Dimensions;
            var hiddenDim = dims[^1];
            var validDim = Math.Min(hiddenDim, DefaultDimension);

            var sumMask = 0.0f;
            for (var s = 0; s < seqLen; s++)
            {
                if (attentionMask[s] == 0) continue;
                sumMask += 1.0f;
                for (var h = 0; h < validDim; h++)
                {
                    pooled[h] += tensor[0, s, h];
                }
            }

            if (sumMask > 0)
            {
                for (var h = 0; h < validDim; h++)
                {
                    pooled[h] /= sumMask;
                }
            }

            NormalizeL2(pooled);
            return pooled;
        }
    }

    private long[] Tokenize(string text, int maxTokens)
    {
        var tokenList = new List<long> { 101 }; // [CLS]
        var words = Regex.Split(text.ToLowerInvariant(), @"\s+");

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;

            if (_vocab != null && _vocab.TryGetValue(word, out var id))
            {
                tokenList.Add(id);
            }
            else
            {
                // Wordpiece / subword fallback
                var matched = false;
                for (var len = word.Length; len > 0; len--)
                {
                    var sub = word[..len];
                    if (_vocab != null && _vocab.TryGetValue(sub, out var subId))
                    {
                        tokenList.Add(subId);
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    tokenList.Add(100); // [UNK]
                }
            }

            if (tokenList.Count >= maxTokens - 1) break;
        }

        tokenList.Add(102); // [SEP]
        return tokenList.ToArray();
    }

    /// <summary>
    /// High-performance built-in multi-lingual lexical-semantic vectorizer.
    /// Maps words, character n-grams, and semantic markers into an L2-normalized 384-dimensional space.
    /// </summary>
    public static float[] GenerateFallbackEmbedding(string text)
    {
        var vector = new float[DefaultDimension];
        if (string.IsNullOrWhiteSpace(text)) return vector;

        var normalized = text.ToLowerInvariant().Normalize(NormalizationForm.FormC);
        var words = Regex.Matches(normalized, @"[\p{L}\p{N}]+").Select(m => m.Value).ToList();

        // 1. Unigrams with Murmur/FNV hashing
        foreach (var word in words)
        {
            if (word.Length < 2) continue;
            var hash = (uint)GetDeterministicHashCode(word);
            var index = (int)(hash % DefaultDimension);
            var sign = ((hash >> 16) & 1) == 0 ? 1.0f : -1.0f;
            vector[index] += sign * (float)Math.Log(1.0 + word.Length);

            // 2. Character 3-grams for morphological/multilingual similarity (Czech inflections)
            if (word.Length >= 4)
            {
                for (var i = 0; i <= word.Length - 3; i++)
                {
                    var trigram = word.Substring(i, 3);
                    var triHash = (uint)GetDeterministicHashCode(trigram);
                    var triIdx = (int)(triHash % DefaultDimension);
                    vector[triIdx] += 0.5f;
                }
            }
        }

        // 3. Bigrams
        for (var i = 0; i < words.Count - 1; i++)
        {
            var bigram = words[i] + "_" + words[i + 1];
            var biHash = (uint)GetDeterministicHashCode(bigram);
            var biIdx = (int)(biHash % DefaultDimension);
            vector[biIdx] += 1.2f;
        }

        NormalizeL2(vector);
        return vector;
    }

    public static void NormalizeL2(Span<float> vector)
    {
        var sumSquares = 0.0f;
        foreach (var val in vector)
        {
            sumSquares += val * val;
        }

        var norm = (float)Math.Sqrt(sumSquares);
        if (norm > 1e-8f)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }

    public static float CosineSimilarity(ReadOnlySpan<float> vecA, ReadOnlySpan<float> vecB)
    {
        if (vecA.Length != vecB.Length) return 0.0f;
        var dot = 0.0f;
        for (var i = 0; i < vecA.Length; i++)
        {
            dot += vecA[i] * vecB[i];
        }
        return dot;
    }

    private static int GetDeterministicHashCode(string str)
    {
        unchecked
        {
            var hash1 = (5381 << 16) + 5381;
            var hash2 = hash1;

            for (var i = 0; i < str.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i + 1 < str.Length)
                {
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }
            }

            return hash1 + (hash2 * 1566083941);
        }
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
