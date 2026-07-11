using System.Text.Json;
using Microsoft.Extensions.Options;
using SingBoxServer.Core;
using SingBoxServer.Core.Models;
using SingBoxServer.Logging;
using SingBoxServer.Services.Generators.SingBox;

namespace SingBoxServer.Services;

internal interface IConfigurationService : IDisposable
{
    UserSettings Settings { get; }
    SingBoxTemplate Template { get; }
}

internal sealed class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly PlatformPath _paths;
    private UserSettings _settings = null!;
    private SingBoxTemplate _template = null!;
    private FileSystemWatcher? _settingsWatcher;
    private FileSystemWatcher? _templateWatcher;
    private string? _currentTemplatePath;
    private Timer? _debounceTimer;
    public UserSettings Settings => _settings;
    public SingBoxTemplate Template => _template;

    public ConfigurationService(IOptions<PlatformPath> config, ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _paths = config.Value;
        LoadAll();
        SetupWatchers();
    }

    private void LoadAll()
    {
        try
        {
            var settingsInput = FileHelper.ReadAllTextSafe(_paths.SettingsPath);
            var newSettings = JsonSerializer.Deserialize(settingsInput, AppJsonContext.Default.UserSettings)
                ?? throw new InvalidOperationException($"Ошибка десериализации {_paths.SettingsPath}");

            string templatePath = newSettings.BaseConfig.Path ?? "template.json"; // Проверить на существование файла
            if (!Path.IsPathRooted(templatePath))
            {
                templatePath = Path.Combine(Path.GetDirectoryName(_paths.SettingsPath)!, templatePath);
            }
            
            var templateInput = FileHelper.ReadAllTextSafe(templatePath);
            var newTemplate = JsonSerializer.Deserialize(templateInput, AppJsonContext.Default.SingBoxTemplate)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {templateInput} — результат null.");
            
            _settings = newSettings;
            _template = newTemplate;
            _logger.LogConfigurationsLoadedSuccessfully(_paths.SettingsPath, templatePath);
            UpdateTemplateWatcher(templatePath);
        }
        catch (Exception ex)
        {
            _logger.LogConfigurationLoadError(ex);
            if (_settings == null) throw;
        }
    }

    private void SetupWatchers()
    {
        _debounceTimer = new Timer(_ => _ = TryReloadAsync(), null, Timeout.Infinite, Timeout.Infinite);

        // Следим за основным файлом настроек
        var dir = Path.GetDirectoryName(_paths.SettingsPath);
        if (dir != null && Directory.Exists(dir))
        {
            _settingsWatcher = new FileSystemWatcher(dir)
            {
                Filter = Path.GetFileName(_paths.SettingsPath),
                EnableRaisingEvents = true
            };
            _settingsWatcher.Changed += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
            _settingsWatcher.Created += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
            _settingsWatcher.Renamed += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
        }
    }
    private void UpdateTemplateWatcher(string currentTemplatePath)
    {
        if (_currentTemplatePath == currentTemplatePath && _templateWatcher != null)
            return; // Путь не изменился, ничего не делаем

        _currentTemplatePath = currentTemplatePath;

        // Если путь к шаблону изменился, старый вочер удаляем, новый создаем
        _templateWatcher?.Dispose();

        var dir = Path.GetDirectoryName(currentTemplatePath);
        if (dir != null && Directory.Exists(dir))
        {
            _templateWatcher = new FileSystemWatcher(dir)
            {
                Filter = Path.GetFileName(currentTemplatePath),
                EnableRaisingEvents = true
            };
            _templateWatcher.Changed += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
            _templateWatcher.Created += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
            _templateWatcher.Renamed += (s, e) => _debounceTimer?.Change(500, Timeout.Infinite);
        }
    }

    private async Task TryReloadAsync()
    {
        var locked = await _reloadLock.WaitAsync(TimeSpan.Zero).ConfigureAwait(false);
        if (!locked)
        {
            _logger.LogDuplicateReloadSkipped();
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
        // Отключаем события до dispose — иначе колбэк может сработать на умирающем объекте
        _settingsWatcher?.EnableRaisingEvents = false;
        _templateWatcher?.EnableRaisingEvents = false;

        // Останавливаем и удаляем таймер
        _debounceTimer?.Dispose();

        _settingsWatcher?.Dispose();
        _templateWatcher?.Dispose();
        _reloadLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
