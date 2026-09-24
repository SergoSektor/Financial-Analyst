using System;
using System.IO;
using System.Text.Json;

namespace FinancialAnalyst.Models
{
    public enum AiMode
    {
        Ollama,
        Api
    }

    public class AppSettings
    {
        public AiMode AiMode { get; set; } = AiMode.Ollama;
        public string OllamaUrl { get; set; } = "http://localhost:11434";
        public string OllamaModel { get; set; } = "llama3";
        public string ApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiModel { get; set; } = "gpt-3.5-turbo";
        public bool DebugMode { get; set; } = false;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinancialAnalyst", "settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            return new AppSettings();
        }

        public void Reload()
        {
            var fresh = Load();
            AiMode = fresh.AiMode;
            OllamaUrl = fresh.OllamaUrl;
            OllamaModel = fresh.OllamaModel;
            ApiUrl = fresh.ApiUrl;
            ApiKey = fresh.ApiKey;
            ApiModel = fresh.ApiModel;
            DebugMode = fresh.DebugMode;
        }

        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}
