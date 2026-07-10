using Microsoft.Extensions.Options;

namespace SingBoxServer.Core;

public class PlatformPath
{
    public string SettingsPath { get; set; } = string.Empty;
    public class Setup(IConfiguration config) : IConfigureOptions<PlatformPath>
    {
        public void Configure(PlatformPath options)
        {
            var userSettingsPath = config["SettingsPath"];
            string defaultDir = GetDefaultConfigDirectory();
            string fullPath = MakeAbsolute(userSettingsPath, defaultDir, Constants.ConfigName);

            // Создаем директорию
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Записываем результат в объект настроек
            options.SettingsPath = fullPath;
        }

        private static string GetDefaultConfigDirectory()
        {
            return Environment.OSVersion.Platform switch
            {
                _ when OperatingSystem.IsLinux() => GetLinuxConfigDirectory(),
                _ when OperatingSystem.IsWindows() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.AppName),
                _ when OperatingSystem.IsMacOS() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", Constants.AppName),
                _ => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.AppName)
            };
        }

        private static string GetLinuxConfigDirectory()
        {
            if (IsOpenWrt())
            {
                return $"/etc/{Constants.AppName}";
            }
            // Если XDG_CONFIG_HOME задан, .NET сам его подхватит через SpecialFolder.UserProfile + .config
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", Constants.AppName);
        }
        private static bool IsOpenWrt()
        {
            if (File.Exists("/etc/openwrt_release")) return true;
            const string osReleasePath = "/etc/os-release";
            if (File.Exists(osReleasePath))
                try
                {
                    var tags = File.ReadLines(osReleasePath)
                        .Select(line => line.Split('=', 2))
                        .Where(p => p.Length == 2)
                        .Where(p => p[0] == "ID" || p[0] == "ID_LIKE")
                        .SelectMany(p => p[1].Trim('"', '\'').Split(' ')) // Разделяем ID_LIKE по пробелам
                        .Select(t => t.ToLowerInvariant());

                    if (tags.Contains("openwrt")) return true;
                }
                catch { } //log
            return false;
        }

        private static string MakeAbsolute(string? userPath, string defaultDir, string defaultFileName)
        {
            string path = string.IsNullOrWhiteSpace(userPath)
                ? Path.Combine(defaultDir, defaultFileName)
                : userPath;

            return Path.GetFullPath(path);
        }
    }
}
