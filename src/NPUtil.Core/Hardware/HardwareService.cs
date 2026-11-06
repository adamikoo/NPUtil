using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NPUtil.Core.Hardware;

public record HardwareDiagnostics(
    string Architecture,
    string OperatingSystem,
    string ProcessorName,
    int LogicalCores,
    bool IsArm64,
    bool IsQualcommPlatform,
    bool IsQnnLibraryAvailable,
    string ActiveAiBackend,
    bool IsOnBattery,
    int BatteryPercent
);

public interface IHardwareService
{
    HardwareDiagnostics GetDiagnostics();
    bool IsRunningOnBattery();
    void SetActiveAiBackend(string backendName);
}

public class HardwareService : IHardwareService
{
    private string _activeAiBackend = "Auto-Detecting...";

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public void SetActiveAiBackend(string backendName)
    {
        _activeAiBackend = backendName;
    }

    public bool IsRunningOnBattery()
    {
        if (GetSystemPowerStatus(out var status))
        {
            // ACLineStatus: 0 = Offline (on battery), 1 = Online (AC), 255 = Unknown
            return status.ACLineStatus == 0;
        }
        return false;
    }

    public HardwareDiagnostics GetDiagnostics()
    {
        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        var os = RuntimeInformation.OSDescription;
        var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var cores = Environment.ProcessorCount;

        var procName = GetProcessorName();
        var isQualcomm = procName.Contains("Snapdragon", StringComparison.OrdinalIgnoreCase) ||
                         procName.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase) ||
                         isArm64;

        var isQnnAvailable = CheckQnnAvailability();

        var onBattery = false;
        var batteryPercent = 100;
        if (GetSystemPowerStatus(out var status))
        {
            onBattery = (status.ACLineStatus == 0);
            if (status.BatteryLifePercent <= 100)
            {
                batteryPercent = status.BatteryLifePercent;
            }
        }

        return new HardwareDiagnostics(
            Architecture: arch,
            OperatingSystem: os,
            ProcessorName: procName,
            LogicalCores: cores,
            IsArm64: isArm64,
            IsQualcommPlatform: isQualcomm,
            IsQnnLibraryAvailable: isQnnAvailable,
            ActiveAiBackend: _activeAiBackend,
            IsOnBattery: onBattery,
            BatteryPercent: batteryPercent
        );
    }

    private static string GetProcessorName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key?.GetValue("ProcessorNameString") is string name)
            {
                return name.Trim();
            }
        }
        catch { /* ignore */ }

        return "Qualcomm Snapdragon X Elite / ARM64 Processor";
    }

    private static bool CheckQnnAvailability()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var nativeArm64 = Path.Combine(baseDir, "runtimes", "win-arm64", "native");

            string[] qnnDlls = ["QnnHtp.dll", "onnxruntime_providers_qnn.dll"];
            foreach (var dll in qnnDlls)
            {
                if (File.Exists(Path.Combine(baseDir, dll)) ||
                    File.Exists(Path.Combine(nativeArm64, dll)))
                {
                    return true;
                }
            }
        }
        catch { /* ignore */ }

        return false;
    }
}
