using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Text.Json;

namespace Ink_Canvas.Plugins
{
    internal class SettingsService : ISettingsService
    {
        public event Action<string, object> SettingChanged;

        public T Get<T>(string key)
        {
            try
            {
                var settings = SettingsManager.Settings;
                if (settings == null) return default;
                return GetByReflection<T>(settings, key);
            }
            catch
            {
                return default;
            }
        }

        public void Set<T>(string key, T value)
        {
            try
            {
                var settings = SettingsManager.Settings;
                if (settings == null) return;
                SetByReflection(settings, key, value);
                SettingsManager.SaveSettingsToFile();
                SettingChanged?.Invoke(key, value);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"SettingsService.Set({key}) failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public bool Has(string key)
        {
            try
            {
                var settings = SettingsManager.Settings;
                if (settings == null) return false;
                return GetByReflection<object>(settings, key) != null;
            }
            catch
            {
                return false;
            }
        }

        private static T GetByReflection<T>(object obj, string key)
        {
            var parts = key.Split('.');
            object current = obj;
            foreach (var part in parts)
            {
                if (current == null) return default;
                var prop = current.GetType().GetProperty(part,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop == null) return default;
                current = prop.GetValue(current);
            }
            if (current is T typed) return typed;
            return default;
        }

        private static void SetByReflection(object obj, string key, object value)
        {
            var parts = key.Split('.');
            object current = obj;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current == null) return;
                var prop = current.GetType().GetProperty(parts[i],
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (prop == null) return;
                current = prop.GetValue(current);
            }
            if (current == null) return;
            var targetProp = current.GetType().GetProperty(parts[parts.Length - 1],
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            targetProp?.SetValue(current, value);
        }
    }
}
