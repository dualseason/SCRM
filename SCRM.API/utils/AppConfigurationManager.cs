using SCRM.Models.Configurations;
using Serilog;
using Serilog.Core;
using System;
using System.IO;
using System.Text.Json;
using SCRM.Shared.Core; // For Utility class

namespace SCRM.Shared.Core
{
    public static class AppConfigurationManager
    {
        public static string configPath;

        public static Settings settings;
        
        // Temporary logger for configuration loading phase
        public static readonly Logger logger = new LoggerConfiguration()
            .WriteTo.Debug(outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss.fff} 【{Level:u3}】 {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        public static string GetAppDataPath()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamePlatform");
            }
            else if (OperatingSystem.IsAndroid())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamePlatform");
            }
            else if (OperatingSystem.IsIOS())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "..", "Library", "GamePlatform");
            }
            else if (OperatingSystem.IsLinux())
            {
                return "/opt/GamePlatform";
            }
            else
            {
                string configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(configDir, "GamePlatform");
            }
        }

        public static string GetConfigPath(string appName)
        {
            var basePath = GetAppDataPath();
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var fileName = $"config.{environment}.json";
            var fullPath = Path.Combine(basePath, appName, fileName);

            return File.Exists(fullPath) ? fullPath : Path.Combine(basePath, appName, "config.json");
        }

        private static bool ValidateSettings(Settings settings)
        {
            if (string.IsNullOrEmpty(settings.DbPath))
            {
                logger.Error("数据库路径不能为空");
                return false;
            }

            if (settings.Port <= 0 || settings.Port > 65535)
            {
                logger.Error("端口号必须在 1-65535 范围内");
                return false;
            }

            return true;
        }

        public static bool InitSettings(string appName)
        {
            configPath = Path.Combine(GetAppDataPath(), appName, "config.json");

            EnsureDirectoryExists(Path.GetDirectoryName(configPath));

            logger.Information("-----------{0}", configPath);

            if (!File.Exists(configPath))
            {
                CreateDefaultConfigFile(configPath);
            }

            try
            {
                string json = File.ReadAllText(configPath);
                settings = JsonSerializer.Deserialize<Settings>(json);
                
                // Assign to Utility.settings
                Utility.settings = settings;

                logger.Information($"------------ {configPath},{settings.ToJsonString()}");
                return true;
            }
            catch (FileNotFoundException ex)
            {
                logger.Error($"配置文件未找到，请检查路径：{configPath}，{ex.Message}");
                Environment.Exit(1);
            }
            catch (JsonException ex)
            {
                logger.Error($"配置文件格式错误，请检查文件内容：{configPath}，{ex.Message}");
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                logger.Error($"Settings配置文件读取错误，请检查{configPath}权限，{ex.Message}");
                Environment.Exit(1);
            }
            return false;
        }

        public static void SetSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                logger.Error($"Settings配置文件写入错误，请检查{configPath}权限，{ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void CreateDefaultConfigFile(string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            };
            var defaultConfig = new Settings
            {
                DbPath = Path.Combine(Path.GetDirectoryName(configPath), "GamePlatform.db")
            };

            var json = JsonSerializer.Serialize(defaultConfig, options);
            File.WriteAllText(filePath, json);
        }
        
        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
