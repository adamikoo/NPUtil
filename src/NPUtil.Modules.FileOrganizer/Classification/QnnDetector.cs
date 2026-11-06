using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using NPUtil.Core.Logging;

namespace NPUtil.Modules.FileOrganizer.Classification;

public enum ExecutionBackend
{
    QualcommNpu,
    Cpu
}

public class ExecutionProviderResult
{
    public ExecutionBackend Backend { get; set; }
    public string DisplayName { get; set; } = "CPU";
    public bool IsNpu { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class QnnDetector
{
    private readonly ILogger _logger;

    public QnnDetector(ILogger logger)
    {
        _logger = logger;
    }

    public ExecutionProviderResult DetectAndConfigureSession(SessionOptions options, string? requestedBackend = null)
    {
        var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var allowQnn = isArm64 && (requestedBackend == null || requestedBackend.Equals("Auto", StringComparison.OrdinalIgnoreCase) || requestedBackend.Equals("QnnNpu", StringComparison.OrdinalIgnoreCase));

        if (allowQnn)
        {
            try
            {
                _logger.Info("QnnDetector", "Attempting Qualcomm QNN Execution Provider (Hexagon NPU HTP)...");

                var qnnOptions = new Dictionary<string, string>
                {
                    { "backend_path", "QnnHtp.dll" },
                    { "htp_performance_mode", "burst" }
                };

                options.AppendExecutionProvider("QNN", qnnOptions);
                _logger.Info("QnnDetector", "QNN HTP execution provider appended successfully.");

                return new ExecutionProviderResult
                {
                    Backend = ExecutionBackend.QualcommNpu,
                    DisplayName = "Qualcomm Hexagon NPU (QNN HTP)",
                    IsNpu = true,
                    Details = "Hardware-accelerated on Qualcomm Hexagon NPU via QNN Execution Provider."
                };
            }
            catch (Exception ex)
            {
                _logger.Warning("QnnDetector", "Qualcomm QNN HTP initialization failed, falling back to CPU.", ex);
            }
        }

        // CPU Fallback
        _logger.Info("QnnDetector", "Configuring CPU execution provider (ARM64 Vectorized / NEON)...");
        try
        {
            options.AppendExecutionProvider_CPU(useArena: 1);
        }
        catch { /* default fallback */ }

        return new ExecutionProviderResult
        {
            Backend = ExecutionBackend.Cpu,
            DisplayName = isArm64 ? "CPU (ARM64 Vectorized / NEON)" : "CPU (x64)",
            IsNpu = false,
            Details = "Running on CPU with native vector SIMD operations."
        };
    }
}
