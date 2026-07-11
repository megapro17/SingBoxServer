using System.Collections.Concurrent;
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

internal sealed class LocalFileCache : ILocalFileCache, IDisposable
{
    private readonly ILogger<LocalFileCache> _logger;

    // Кэш содержимого файлов (Путь -> Текст)
    private readonly ConcurrentDictionary<string, string> _contentCache = new();

    // Watcher'ы для директорий (Директория -> Watcher)
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _directoryWatchers = new();

    public LocalFileCache(ILogger<LocalFileCache> logger)
    {
        _logger = logger;
    }

    public async Task<string> GetContentAsync(string path, CancellationToken ct = default)
    {
        // Если уже в кэше — возвращаем мгновенно
        if (_contentCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        // Иначе читаем с диска и ставим слежение
        _logger.LogLocalFileLoadedFromDisk(path);
        var content = await FileHelper.ReadAllTextSafeAsync(path, ct).ConfigureAwait(false);
        
        // Сохраняем в кэш
        _contentCache[path] = content;
        
        // Убеждаемся, что для директории этого файла есть Watcher
        EnsureWatcherForDirectory(path);
        
        return content;
    }

    private void EnsureWatcherForDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
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
