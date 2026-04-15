using Microsoft.Extensions.Logging;
using SingBoxServer.Models;

namespace SingBoxServer.Logging;

// (Trace -> Debug -> Information -> Warning -> Error -> Critical)
public static partial class SharedLogMessages
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
}