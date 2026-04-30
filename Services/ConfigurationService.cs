using System.Text.Json;
using SingBoxServer.Models;
using SingBoxServer.Services.Generators.SingBox;

namespace SingBoxServer.Services;

public interface IConfigurationService : IDisposable
{
    UserSettings Settings { get; }
    SingBoxTemplate Template { get; }
}

public class ConfigurationService : IConfigurationService
{
    private readonly JsonSerializerOptions _options;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly string _settingsPath;
    private readonly string _templatePath;
    private UserSettings _settings = null!;
    private SingBoxTemplate _template = null!;
    private FileSystemWatcher? _settingsWatcher;
    private FileSystemWatcher? _templateWatcher;
    private Timer? _debounceTimer;
    private bool _disposed;

    public UserSettings Settings => _settings;
    public SingBoxTemplate Template => _template;

    public ConfigurationService(IConfiguration config, JsonSerializerOptions options, ILogger<ConfigurationService> logger)
    {
        _options = options;
        _logger = logger;

        _settingsPath = config["SettingsPath"]!;
        _templatePath = config["TemplatePath"]!;
        LoadAll();
        SetupWatchers();
    }

    private void LoadAll()
    {
        try
        {
            var settingsInput = File.ReadAllText(_settingsPath);
            var templateInput = File.ReadAllText(_templatePath);
            _settings = JsonSerializer.Deserialize<UserSettings>(settingsInput, _options)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {_settingsPath} — результат null.");
            _template = JsonSerializer.Deserialize<SingBoxTemplate>(templateInput, _options)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {_templatePath} — результат null.");
            _logger.LogInformation("Конфигурации успешно загружены.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки конфигураций.");
            if (_settings == null) throw;
        }
    }

    private void SetupWatchers()
    {
        // Debounce: ждём 500мс после последнего события, потом один раз грузим
        var debounceCallback = new Timer(_ =>
        {
            if (_disposed) return;
            _ = TryReloadAsync();
        }, null, Timeout.Infinite, Timeout.Infinite);
        _debounceTimer = debounceCallback;

        _settingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(_settingsPath)!) { Filter = "settings.json" };
        _settingsWatcher.Changed += (s, e) => debounceCallback.Change(500, Timeout.Infinite);
        _settingsWatcher.EnableRaisingEvents = true;

        _templateWatcher = new FileSystemWatcher(Path.GetDirectoryName(_templatePath)!) { Filter = "latest_whitelist.json" };
        _templateWatcher.Changed += (s, e) => debounceCallback.Change(500, Timeout.Infinite);
        _templateWatcher.EnableRaisingEvents = true;
    }

    private async Task TryReloadAsync()
    {
        if (!await _reloadLock.WaitAsync(TimeSpan.Zero))
        {
            _logger.LogDebug("Пропущен дублирующий reload — предыдущий ещё выполняется.");
            return;
        }

        try
        {
            LoadAll();
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;

        // Отключаем события до dispose — иначе колбэк может сработать на умирающем объекте
        _settingsWatcher?.EnableRaisingEvents = false;
        _templateWatcher?.EnableRaisingEvents = false;

        // Останавливаем и удаляем таймер
        _debounceTimer?.Dispose();

        _settingsWatcher?.Dispose();
        _templateWatcher?.Dispose();
        _reloadLock.Dispose();
    }
}
