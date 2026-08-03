using System.Text.Json;
using Microsoft.Extensions.Options;
using SingBoxServer.Core;
using SingBoxServer.Core.Models;
using SingBoxServer.Logging;
using SingBoxServer.Services.Generators.SingBox;
using SingBoxServer.Services.Generators.SingBox.Patchers;

namespace SingBoxServer.Services;

internal interface IConfigurationService : IDisposable
{
    UserSettings Settings { get; }
    SingBoxTemplate GetTemplate(string? templateName, string? device);
}

internal sealed class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IEnumerable<IConfigPatcher> _patchers;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly PlatformPath _paths;
    private readonly PlatformPath.Setup _pathSetup;
    private UserSettings _settings = null!;
    private FileSystemWatcher? _settingsWatcher;
    private FileSystemWatcher? _templateWatcher;
    private Timer? _debounceTimer;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SingBoxTemplate> _templateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Path, string Device), SingBoxTemplate> _deviceTemplates = new();

    public UserSettings Settings => _settings;

    public ConfigurationService(IOptions<PlatformPath> config, PlatformPath.Setup pathSetup, IEnumerable<IConfigPatcher> patchers, ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _patchers = patchers;
        _paths = config.Value;
        _pathSetup = pathSetup;
        LoadAll();
        SetupWatchers();
    }
    public SingBoxTemplate GetTemplate(string? templateName, string? device)
    {
        string key = string.IsNullOrEmpty(templateName) ? "default" : templateName;
        
        if (!_settings.BaseConfig.Path.TryGetValue(key, out var targetFileName))
        {
            if (key == "default")
            {
                targetFileName = "template.json";
            }
            else
            {
                throw new FileNotFoundException($"Шаблон '{key}' не разрешён. Добавьте его в словарь base_config.path.");
            }
        }

        string configDir = Path.GetDirectoryName(_paths.SettingsPath) ?? string.Empty;
        string fullPath = PlatformPath.Setup.MakeAbsolute(targetFileName, configDir, targetFileName);

        var template = _templateCache.GetOrAdd(fullPath, path => 
        {
            var input = FileHelper.ReadAllTextSafe(path);
            return JsonSerializer.Deserialize(input, AppJsonContext.Default.SingBoxTemplate)
                   ?? throw new InvalidOperationException($"Не удалось десериализовать {path}");
        });

        if (string.IsNullOrEmpty(device))
        {
            return CloneTemplate(template);
        }

        var deviceTemplate = _deviceTemplates.GetOrAdd((fullPath, device), key =>
        {
            var t = template;
            foreach (var patcher in _patchers.Where(p => p.CanPatch(key.Device)))
            {
                t = patcher.ApplyPatch(t);
            }
            return t;
        });

        return CloneTemplate(deviceTemplate);
    }

    private static SingBoxTemplate CloneTemplate(SingBoxTemplate template)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(template, AppJsonContext.Default.SingBoxTemplate);
        return JsonSerializer.Deserialize(bytes, AppJsonContext.Default.SingBoxTemplate)!;
    }
    private void LoadAll()
    {
        try
        {
            var settingsInput = FileHelper.ReadAllTextSafe(_paths.SettingsPath);
            var newSettings = JsonSerializer.Deserialize(settingsInput, AppJsonContext.Default.UserSettings)
                ?? throw new InvalidOperationException($"Ошибка десериализации {_paths.SettingsPath}");

            _settings = newSettings;
            _templateCache.Clear();
            _deviceTemplates.Clear();

            string displayPath = string.Join(", ", _settings.BaseConfig.Path.Values);
            _logger.LogConfigurationsLoadedSuccessfully(_paths.SettingsPath, displayPath);
            UpdateTemplateWatcher();
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
    private void UpdateTemplateWatcher()
    {
        if (_templateWatcher != null) return;
        
        var dir = Path.GetDirectoryName(_paths.SettingsPath);
        if (dir != null && Directory.Exists(dir))
        {
            _templateWatcher = new FileSystemWatcher(dir)
            {
                Filter = "*.json",
                EnableRaisingEvents = true
            };
            _templateWatcher.Changed += (s, e) => { if (e.FullPath != _paths.SettingsPath) _debounceTimer?.Change(500, Timeout.Infinite); };
            _templateWatcher.Created += (s, e) => { if (e.FullPath != _paths.SettingsPath) _debounceTimer?.Change(500, Timeout.Infinite); };
            _templateWatcher.Renamed += (s, e) => { if (e.FullPath != _paths.SettingsPath) _debounceTimer?.Change(500, Timeout.Infinite); };
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
