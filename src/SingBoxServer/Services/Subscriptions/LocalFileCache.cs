using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using SingBoxServer.Core;
using SingBoxServer.Logging;

namespace SingBoxServer.Services.Subscriptions;

/// <summary>
/// Кэш для локальных файлов. Читает файл один раз и следит за изменениями через FileSystemWatcher.
/// </summary>
internal interface ILocalFileCache
{
    Task<string> GetContentAsync(string path, CancellationToken ct = default);
}

internal sealed class LocalFileCache(ILogger<LocalFileCache> logger, IOptions<PlatformPath> platformPathOptions) : ILocalFileCache, IDisposable
{
    private readonly ILogger<LocalFileCache> _logger = logger;
    private readonly string _configDirectory = Path.GetDirectoryName(platformPathOptions.Value.SettingsPath) ?? string.Empty;
    // Кэш содержимого файлов (Путь -> Текст)
    private readonly ConcurrentDictionary<string, string> _contentCache = new();

    // Watcher'ы для директорий (Директория -> Watcher)
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _directoryWatchers = new();

    public async Task<string> GetContentAsync(string path, CancellationToken ct = default)
    {
        string absolutePath = Path.GetFullPath(Path.Combine(_configDirectory, path));
        // Если уже в кэше — возвращаем мгновенно
        if (_contentCache.TryGetValue(absolutePath, out var cached))
        {
            return cached;
        }

        // Иначе читаем с диска и ставим слежение
        _logger.LogLocalFileLoadedFromDisk(absolutePath);
        var content = await FileHelper.ReadAllTextSafeAsync(absolutePath, ct).ConfigureAwait(false);

        // Сохраняем в кэш
        _contentCache[absolutePath] = content;

        // Убеждаемся, что для директории этого файла есть Watcher
        EnsureWatcherForDirectory(absolutePath);

        return content;
    }

    private void EnsureWatcherForDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory)) return;

        // Если для этой директории уже есть Watcher, выходим
        if (_directoryWatchers.ContainsKey(directory)) return;

        var watcher = new FileSystemWatcher(directory)
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        watcher.Changed += (s, e) => OnFileChanged(e.FullPath);
        watcher.Created += (s, e) => OnFileChanged(e.FullPath);
        watcher.Renamed += (s, e) => OnFileChanged(e.FullPath);
        watcher.EnableRaisingEvents = true;

        // Пытаемся добавить в словарь. Если уже добавили другой поток — просто удаляем этот дубликат
        if (!_directoryWatchers.TryAdd(directory, watcher))
        {
            watcher.Dispose();
        }
        else
        {
            _logger.LogStartedWatchingDirectory(directory);
        }
    }

    private void OnFileChanged(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return;

        // Нормализуем путь, чтобы он совпадал с ключом в кэше
        if (_contentCache.ContainsKey(fullPath))
        {
            _logger.LogLocalFileUpdatedInCache(fullPath);
            // Читаем синхронно, так как это событие файловой системы
            try
            {
                _contentCache[fullPath] = FileHelper.ReadAllTextSafe(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogFailedToUpdateLocalFileInCache(ex, fullPath);
            }
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _directoryWatchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _directoryWatchers.Clear();

        GC.SuppressFinalize(this);
    }
}
