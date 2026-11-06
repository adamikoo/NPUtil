using NPUtil.Core.Hardware;
using NPUtil.Core.Logging;

namespace NPUtil.Core.AI;

public record ModelInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string TaskType { get; init; } // "Embedding", "Summarization", "Classification", "Extraction"
    public string? FilePath { get; init; }
    public long FileSizeBytes { get; init; }
    public bool IsInstalled { get; init; }
    public bool IsLoadedInMemory { get; init; }
    public string SupportedProvider { get; init; } = "CPU / QNN";
}

public interface IModelManager
{
    IReadOnlyList<ModelInfo> GetAvailableModels();
    ModelInfo? GetModel(string modelId);
    Task<bool> LoadModelAsync(string modelId, CancellationToken ct = default);
    Task UnloadModelAsync(string modelId);
    void UnloadIdleModels();
    long GetApproximateMemoryUsageBytes();
    string GetActiveExecutionProvider();
}

public class ModelManager : IModelManager
{
    private readonly IHardwareService _hardwareService;
    private readonly ILogger? _logger;
    private readonly Dictionary<string, ModelInfo> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedModels = new(StringComparer.OrdinalIgnoreCase);

    public ModelManager(IHardwareService hardwareService, ILogger? logger = null)
    {
        _hardwareService = hardwareService;
        _logger = logger;

        InitializeRegistry();
    }

    private void InitializeRegistry()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NPUtil", "models");
        Directory.CreateDirectory(appData);

        var embeddingPath = Path.Combine(appData, "paraphrase-multilingual-MiniLM-L12-v2.onnx");
        _registry["embedding-minilm"] = new ModelInfo
        {
            Id = "embedding-minilm",
            Name = "Paraphrase Multilingual MiniLM L12 v2 (Quantized)",
            TaskType = "Embedding",
            FilePath = embeddingPath,
            FileSizeBytes = File.Exists(embeddingPath) ? new FileInfo(embeddingPath).Length : 118 * 1024 * 1024,
            IsInstalled = File.Exists(embeddingPath),
            IsLoadedInMemory = false,
            SupportedProvider = _hardwareService.GetDiagnostics().IsQnnLibraryAvailable ? "Qualcomm QNN HTP (NPU)" : "CPU (ARM64 Vectorized)"
        };

        var assistantModelPath = Path.Combine(appData, "phi3-mini-4k-instruct-cpu.onnx");
        _registry["assistant-instruct"] = new ModelInfo
        {
            Id = "assistant-instruct",
            Name = "Phi-3 Mini Instruct (Local ONNX)",
            TaskType = "Summarization",
            FilePath = assistantModelPath,
            FileSizeBytes = File.Exists(assistantModelPath) ? new FileInfo(assistantModelPath).Length : 1800L * 1024 * 1024,
            IsInstalled = File.Exists(assistantModelPath),
            IsLoadedInMemory = false,
            SupportedProvider = "DirectML / CPU (ARM64)"
        };
    }

    public IReadOnlyList<ModelInfo> GetAvailableModels() => _registry.Values.ToList();

    public ModelInfo? GetModel(string modelId) => _registry.TryGetValue(modelId, out var m) ? m : null;

    public Task<bool> LoadModelAsync(string modelId, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(modelId, out var info)) return Task.FromResult(false);

        _logger?.Info("ModelManager", $"Loading model '{info.Name}' into memory...");
        _loadedModels.Add(modelId);
        _registry[modelId] = info with { IsLoadedInMemory = true };
        return Task.FromResult(true);
    }

    public Task UnloadModelAsync(string modelId)
    {
        if (_registry.TryGetValue(modelId, out var info) && _loadedModels.Remove(modelId))
        {
            _logger?.Info("ModelManager", $"Unloaded model '{info.Name}' to free memory.");
            _registry[modelId] = info with { IsLoadedInMemory = false };
            GC.Collect();
        }
        return Task.CompletedTask;
    }

    public void UnloadIdleModels()
    {
        foreach (var id in _loadedModels.ToList())
        {
            UnloadModelAsync(id);
        }
    }

    public long GetApproximateMemoryUsageBytes()
    {
        return GC.GetTotalMemory(false);
    }

    public string GetActiveExecutionProvider()
    {
        var diag = _hardwareService.GetDiagnostics();
        return diag.ActiveAiBackend;
    }
}
