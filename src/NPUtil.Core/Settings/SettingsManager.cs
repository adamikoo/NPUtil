using System.Text.Json;
using System.Text.Json.Serialization;

namespace NPUtil.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public class GlobalSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string Language { get; set; } = "en";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = false;
    public bool ThrottleOnBattery { get; set; } = true;
    public string LensHotkey { get; set; } = "Ctrl+Alt+L";
    public string AssistantHotkey { get; set; } = "Ctrl+Alt+A";
    public string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NPUtil");
}

public class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private GlobalSettings _settings = new();
    private readonly object _lock = new();

    public event EventHandler? SettingsChanged;

    public SettingsManager(string? customPath = null)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NPUtil", "Config");
        Directory.CreateDirectory(dir);
        _filePath = customPath ?? Path.Combine(dir, "settings.json");
        Load();
    }

    public GlobalSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _settings = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
                }
                catch
                {
                    _settings = new GlobalSettings();
                }
            }
            else
            {
                _settings = new GlobalSettings();
                Save();
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, JsonOptions);
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(_filePath, json);
            }
            catch { /* ignore */ }
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<GlobalSettings> updateAction)
    {
        lock (_lock)
        {
            updateAction(_settings);
            Save();
        }
    }
}
