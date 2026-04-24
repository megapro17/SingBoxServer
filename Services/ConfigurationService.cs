using System.Text.Json;
using Microsoft.Extensions.Logging;
using SingBoxServer.Models;

namespace SingBoxServer.Services;

public interface IConfigurationService
{
    UserSettings Settings { get; }
    SingBoxTemplate Template { get; }
}

public class ConfigurationService : IConfigurationService, IDisposable
{
    private readonly string _settingsPath = @"C:\Users\megapro17\SourceCode\SingBoxServer\settings.json";
    private readonly string _templatePath = @"C:\Users\megapro17\SourceCode\sing-box\latest_whitelist.json";
    private readonly JsonSerializerOptions _options;
    private readonly ILogger<ConfigurationService> _logger;
    
    private UserSettings _settings;
    private SingBoxTemplate _template;
    private FileSystemWatcher? _settingsWatcher;
    private FileSystemWatcher? _templateWatcher;

    public UserSettings Settings => _settings;
    public SingBoxTemplate Template => _template;

    public ConfigurationService(JsonSerializerOptions options, ILogger<ConfigurationService> logger)
    {
        _options = options;
        _logger = logger;
        LoadAll();
        SetupWatchers();
    }

    private void LoadAll()
    {
        try
        {
            var settingsInput = File.ReadAllText(_settingsPath);
            var templateInput = File.ReadAllText(_templatePath);
            _settings = JsonSerializer.Deserialize<UserSettings>(settingsInput, _options) ?? throw new Exception();
            _template = JsonSerializer.Deserialize<SingBoxTemplate>(templateInput, _options) ?? throw new Exception();
            _logger.LogInformation("Конфигурации успешно загружены.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Ошибка загрузки конфигураций.");
            if (_settings == null) throw;
        }
    }

    private void SetupWatchers()
    {
        _settingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(_settingsPath)!) { Filter = "settings.json" };
        _settingsWatcher.Changed += (s, e) => LoadAll();
        _settingsWatcher.EnableRaisingEvents = true;

        _templateWatcher = new FileSystemWatcher(Path.GetDirectoryName(_templatePath)!) { Filter = "latest_whitelist.json" };
        _templateWatcher.Changed += (s, e) => LoadAll();
        _templateWatcher.EnableRaisingEvents = true;
    }

    public void Dispose() {
        _settingsWatcher?.Dispose();
        _templateWatcher?.Dispose();
    }
}