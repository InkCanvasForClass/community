using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件配置管理器，允许插件管理自己的配置
    /// </summary>
    public class PluginConfigurationManager
    {
        private static readonly string PluginConfigDirectory = Path.Combine(App.RootPath, "PluginConfigs");
        private static readonly Dictionary<string, Dictionary<string, object>> _pluginConfigs = new Dictionary<string, Dictionary<string, object>>();
        private static readonly object _lockObject = new object();

        static PluginConfigurationManager()
        {
            // 确保配置目录存在
            if (!Directory.Exists(PluginConfigDirectory))
            {
                Directory.CreateDirectory(PluginConfigDirectory);
            }
        }

        /// <summary>
        /// 获取插件配置值
        /// </summary>
        /// <typeparam name="T">配置值类型</typeparam>
        /// <param name="pluginName">插件名称</param>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        public static T GetConfiguration<T>(string pluginName, string key, T defaultValue = default(T))
        {
            lock (_lockObject)
            {
                try
                {
                    if (_pluginConfigs.TryGetValue(pluginName, out var pluginConfig))
                    {
                        if (pluginConfig.TryGetValue(key, out var value))
                        {
                            if (value is T typedValue)
                            {
                                return typedValue;
                            }
                            else
                            {
                                // 尝试类型转换
                                try
                                {
                                    return (T)Convert.ChangeType(value, typeof(T));
                                }
                                catch
                                {
                                    return defaultValue;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"获取插件 {pluginName} 配置 {key} 时出错: {ex.Message}", LogHelper.LogType.Error);
                }

                return defaultValue;
            }
        }

        /// <summary>
        /// 设置插件配置值
        /// </summary>
        /// <typeparam name="T">配置值类型</typeparam>
        /// <param name="pluginName">插件名称</param>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        public static void SetConfiguration<T>(string pluginName, string key, T value)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_pluginConfigs.ContainsKey(pluginName))
                    {
                        _pluginConfigs[pluginName] = new Dictionary<string, object>();
                    }

                    _pluginConfigs[pluginName][key] = value;

                    // 异步保存配置
                    Task.Run(() => SavePluginConfiguration(pluginName));
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"设置插件 {pluginName} 配置 {key} 时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 删除插件配置
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        /// <param name="key">配置键</param>
        public static void RemoveConfiguration(string pluginName, string key)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_pluginConfigs.TryGetValue(pluginName, out var pluginConfig))
                    {
                        if (pluginConfig.Remove(key))
                        {
                            // 异步保存配置
                            Task.Run(() => SavePluginConfiguration(pluginName));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"删除插件 {pluginName} 配置 {key} 时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 获取插件的所有配置
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        /// <returns>配置字典</returns>
        public static Dictionary<string, object> GetAllConfigurations(string pluginName)
        {
            lock (_lockObject)
            {
                if (_pluginConfigs.TryGetValue(pluginName, out var pluginConfig))
                {
                    return new Dictionary<string, object>(pluginConfig);
                }
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 清除插件的所有配置
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        public static void ClearAllConfigurations(string pluginName)
        {
            lock (_lockObject)
            {
                try
                {
                    if (_pluginConfigs.Remove(pluginName))
                    {
                        // 删除配置文件
                        string configFile = Path.Combine(PluginConfigDirectory, $"{pluginName}.json");
                        if (File.Exists(configFile))
                        {
                            File.Delete(configFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"清除插件 {pluginName} 所有配置时出错: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 加载插件配置
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        public static void LoadPluginConfiguration(string pluginName)
        {
            try
            {
                string configFile = Path.Combine(PluginConfigDirectory, $"{pluginName}.json");
                if (File.Exists(configFile))
                {
                    string json = File.ReadAllText(configFile);
                    var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    lock (_lockObject)
                    {
                        _pluginConfigs[pluginName] = config ?? new Dictionary<string, object>();
                    }

                    LogHelper.WriteLogToFile($"已加载插件 {pluginName} 的配置");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载插件 {pluginName} 配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存插件配置
        /// </summary>
        /// <param name="pluginName">插件名称</param>
        private static void SavePluginConfiguration(string pluginName)
        {
            try
            {
                Dictionary<string, object> pluginConfig;
                lock (_lockObject)
                {
                    if (!_pluginConfigs.TryGetValue(pluginName, out pluginConfig))
                    {
                        return;
                    }
                }

                string configFile = Path.Combine(PluginConfigDirectory, $"{pluginName}.json");
                string json = JsonConvert.SerializeObject(pluginConfig, Formatting.Indented);
                File.WriteAllText(configFile, json);

                LogHelper.WriteLogToFile($"已保存插件 {pluginName} 的配置");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存插件 {pluginName} 配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载所有插件的配置
        /// </summary>
        public static void LoadAllPluginConfigurations()
        {
            try
            {
                if (Directory.Exists(PluginConfigDirectory))
                {
                    string[] configFiles = Directory.GetFiles(PluginConfigDirectory, "*.json");
                    foreach (string configFile in configFiles)
                    {
                        string pluginName = Path.GetFileNameWithoutExtension(configFile);
                        LoadPluginConfiguration(pluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载所有插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存所有插件的配置
        /// </summary>
        public static void SaveAllPluginConfigurations()
        {
            try
            {
                lock (_lockObject)
                {
                    foreach (string pluginName in _pluginConfigs.Keys)
                    {
                        SavePluginConfiguration(pluginName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存所有插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
} 