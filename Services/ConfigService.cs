using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WpfApp.Models;

namespace WpfApp.Services
{
    public class ConfigService
    {
        private readonly LogManager _logger = LogManager.Instance;
        private readonly string _configPath = "AppConfig.json";
        private Dictionary<string, object> _settings;
        private readonly JsonSerializerSettings _jsonOptions = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        public ConfigService()
        {
            _settings = LoadSettings();
        }

        private Dictionary<string, object> LoadSettings()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) 
                        ?? new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("ConfigService", "加载设置失败", ex);
            }
            return new Dictionary<string, object>();
        }

        public T GetSetting<T>(string key, T defaultValue)
        {
            if (_settings.TryGetValue(key, out object value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SaveSetting(string key, object value)
        {
            _settings[key] = value;
            try
            {
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                LogManager.Instance.LogError("ConfigService", "保存设置失败", ex);
            }
        }
    }
} 