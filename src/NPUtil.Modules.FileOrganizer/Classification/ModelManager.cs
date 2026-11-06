using NPUtil.Core.Logging;

namespace NPUtil.Modules.FileOrganizer.Classification;

public record ModelInfo(
    string Name,
    string ModelId,
    string SourceUrl,
    string License,
    long ExpectedSizeBytes,
    string LocalDirectory,
    string ModelFilePath,
    string TokenizerFilePath,
    bool IsInstalled
);

public class ModelDownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100.0 : 0.0;
    public string StatusMessage { get; set; } = string.Empty;
}

public class ModelManager
{
    private const string ModelId = "Xenova/paraphrase-multilingual-MiniLM-L12-v2";
    private const string ModelFileName = "model_quantized.onnx";
    private const string TokenizerFileName = "tokenizer.json";
    private const string BaseDownloadUrl = "https://huggingface.co/" + ModelId + "/resolve/main/";
    private const long ModelSize = 118_308_126; // ~112 MB

    private readonly string _modelsDir;
    private readonly ILogger _logger;

    public event EventHandler<ModelDownloadProgress>? DownloadProgress;

    public ModelManager(ILogger logger, string? customDir = null)
    {
        _logger = logger;
        _modelsDir = customDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Models", "paraphrase-multilingual-MiniLM-L12-v2");
        Directory.CreateDirectory(_modelsDir);
    }

    public ModelInfo GetModelInfo()
    {
        var modelPath = Path.Combine(_modelsDir, ModelFileName);
        var tokenizerPath = Path.Combine(_modelsDir, TokenizerFileName);
        var isInstalled = File.Exists(modelPath) && File.Exists(tokenizerPath);

        return new ModelInfo(
            Name: "Paraphrase Multilingual MiniLM L12 v2 (Quantized ONNX)",
            ModelId: ModelId,
            SourceUrl: "https://huggingface.co/" + ModelId,
            License: "Apache 2.0 (Permissive Commercial & Personal Use)",
            ExpectedSizeBytes: ModelSize,
            LocalDirectory: _modelsDir,
            ModelFilePath: modelPath,
            TokenizerFilePath: tokenizerPath,
            IsInstalled: isInstalled
        );
    }

    public async Task DownloadModelAsync(CancellationToken ct = default)
    {
        var info = GetModelInfo();
        if (info.IsInstalled)
        {
            _logger.Info("ModelManager", "Model is already installed locally.");
            return;
        }

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        var files = new[]
        {
            (Url: BaseDownloadUrl + "onnx/" + ModelFileName, Path: info.ModelFilePath, Size: ModelSize),
            (Url: BaseDownloadUrl + TokenizerFileName, Path: info.TokenizerFilePath, Size: 9_000_000L)
        };

        var totalBytes = files.Sum(f => f.Size);
        var downloadedBytes = 0L;

        foreach (var (url, filePath, fileSize) in files)
        {
            _logger.Info("ModelManager", $"Downloading {Path.GetFileName(filePath)} from {url}...");
            var tempPath = filePath + ".tmp";

            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var contentLen = response.Content.Headers.ContentLength ?? fileSize;

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                int read;
                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, ct);
                    downloadedBytes += read;

                    DownloadProgress?.Invoke(this, new ModelDownloadProgress
                    {
                        BytesDownloaded = downloadedBytes,
                        TotalBytes = totalBytes,
                        StatusMessage = $"Downloading {Path.GetFileName(filePath)} ({downloadedBytes / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB)"
                    });
                }
            }

            if (File.Exists(filePath)) File.Delete(filePath);
            File.Move(tempPath, filePath);
        }

        _logger.Info("ModelManager", "Model and tokenizer downloaded successfully.");
    }
}
