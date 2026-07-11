using System.Text;

namespace SingBoxServer.Core;

public static class FileHelper
{
    /// <summary>
    /// Асинхронно читает файл с поддержкой FileShare.ReadWrite и механизмом повторных попыток (Retry).
    /// Позволяет читать файлы, которые в данный момент записываются или временно заблокированы редактором.
    /// </summary>
    public static async Task<string> ReadAllTextSafeAsync(string path, CancellationToken ct = default)
    {
        const int maxRetries = 5;
        const int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return await reader.ReadToEndAsync(ct);
            }
            catch (IOException)
            {
                if (i == maxRetries - 1)
                    throw; // Выбрасываем исключение на последней попытке

                await Task.Delay(delayMs, ct);
            }
        }

        throw new IOException($"Не удалось прочитать файл {path} после {maxRetries} попыток.");
    }

    /// <summary>
    /// Синхронно читает файл с поддержкой FileShare.ReadWrite и механизмом повторных попыток (Retry).
    /// Позволяет читать файлы, которые в данный момент записываются или временно заблокированы редактором.
    /// </summary>
    public static string ReadAllTextSafe(string path)
    {
        const int maxRetries = 5;
        const int delayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                if (i == maxRetries - 1)
                    throw; // Выбрасываем исключение на последней попытке

                Thread.Sleep(delayMs);
            }
        }

        throw new IOException($"Не удалось прочитать файл {path} после {maxRetries} попыток.");
    }
}
