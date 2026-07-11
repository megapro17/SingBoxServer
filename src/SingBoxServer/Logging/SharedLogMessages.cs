using SingBoxServer.Core.Models.Enums;

namespace SingBoxServer.Logging;

// (Trace -> Debug -> Information -> Warning -> Error -> Critical)
internal static partial class SharedLogMessages
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "В настройках не найдено ни одного пользователя.")]
    public static partial void LogSettingsHasNoUsers(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Начата обработка пользователя")]
    public static partial void LogProcessingUser(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ошибка при генерации: {Error}")]
    public static partial void LogGenerationError(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Конфиг успешно сгенерирован")]
    public static partial void LogGenerationSuccess(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "У пользователя нет серверов (или профиль пуст)")]
    public static partial void LogUserHasNoServers(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Сервер: {ServerName} ({Type} {Format}) - {ServerUrl}")]
    public static partial void LogServerInfo(this ILogger logger, string ServerName, ServerType Type, ServerFormat Format, string serverUrl);

    // 1. Для удаленных подписок
    [LoggerMessage(Level = LogLevel.Information, Message = "Скачивание подписки: {ServerName} (URL: {Url})")]
    public static partial void LogDownloadingSubscription(this ILogger logger, string serverName, string url);

    // 2. Для локальных файлов (то, что ты просил вынести)
    [LoggerMessage(Level = LogLevel.Information, Message = "Чтение локального конфига: {Url}")]
    public static partial void LogLoadingLocalConfig(this ILogger logger, string url);

    // 3. Общая ошибка скачивания (с передачей Exception)
    [LoggerMessage(Level = LogLevel.Error, Message = "Ошибка при загрузке данных из {Url}")]
    public static partial void LogDownloadError(this ILogger logger, string url, Exception ex);

    // --- Автоматически сгенерированные при переносе ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Попытка несанкционированного доступа к конфигу: {Username}")]
    public static partial void LogUnauthorizedConfigAccess(this ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Генерируем конфиг на лету для юзера {Username}")]
    public static partial void LogGeneratingConfigForUser(this ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Error, Message = "Критический сбой при генерации для {Username}")]
    public static partial void LogCriticalGenerationFailure(this ILogger logger, Exception ex, string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Кэш удаленных подписок очищен по запросу.")]
    public static partial void LogRemoteCacheClearedByRequest(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Конфигурации успешно загружены.")]
    public static partial void LogConfigurationsLoadedSuccessfully(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ошибка загрузки конфигураций.")]
    public static partial void LogConfigurationLoadError(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Пропущен дублирующий reload — предыдущий ещё выполняется.")]
    public static partial void LogDuplicateReloadSkipped(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Начинаем генерацию конфига")]
    public static partial void LogStartingConfigGeneration(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbound: {Tag}")]
    public static partial void LogOutboundTag(this ILogger logger, string tag);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Локальный файл загружен из диска: {Path}")]
    public static partial void LogLocalFileLoadedFromDisk(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Запущено слежение за директорией: {Directory}")]
    public static partial void LogStartedWatchingDirectory(this ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Обновлен локальный файл в кэше: {Path}")]
    public static partial void LogLocalFileUpdatedInCache(this ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Не удалось обновить локальный файл {Path} после нескольких попыток. Оставляем старую версию в кэше.")]
    public static partial void LogFailedToUpdateLocalFileInCache(this ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Кэш пропущен, загружаем: {Key}")]
    public static partial void LogCacheSkippedLoading(this ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "Кэш удалённых подписок очищен.")]
    public static partial void LogRemoteCacheCleared(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Не удалось прочитать версию ОС из /etc/os-release")]
    public static partial void LogFailedToReadOsRelease(this ILogger logger, Exception ex);
}
