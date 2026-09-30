using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Input;

namespace Ink_Canvas.Services.Shell
{
    /// <summary>
    /// 全局热键配置服务（M16 寄生提取）。承载从 <c>Helpers/GlobalHotkeyManager</c> 搬出的
    /// 配置层：HotkeyConfig.json 的读写解析、默认配置定义、从配置/默认集合到注册引擎的
    /// 加载编排。热键触发回调本体留在主窗口壳（MW_Hotkeys.cs），以「热键名 → <see cref="Action"/>」
    /// 字典在构造时注入，替代原 <c>GetActionByName</c> switch 的硬编码回调。
    /// NHotkey 注册引擎与多屏/焦点上下文门控仍在 <see cref="GlobalHotkeyManager"/> 中，
    /// 本服务不引用主窗口类型、不做任何 UI 线程调度（M30 门禁）。
    /// 注意与插件服务 <c>Ink_Canvas.Plugins.HotkeyService</c> 同名不同命名空间，勿混淆。
    /// </summary>
    internal sealed class HotkeyService
    {
        private readonly GlobalHotkeyManager _manager;
        private readonly IReadOnlyDictionary<string, Action> _builtinActions;

        // 配置文件路径
        private static readonly string HotkeyConfigFile = Path.Combine(App.RootPath, "Configs", "HotkeyConfig.json");

        /// <summary>
        /// 创建热键配置服务并接线到注册引擎。
        /// </summary>
        /// <param name="manager">NHotkey 注册引擎（GlobalHotkeyManager，由主窗口创建）。</param>
        /// <param name="builtinActions">内置热键名 → 触发回调字典（回调本体留在主窗口壳）。</param>
        public HotkeyService(GlobalHotkeyManager manager, IReadOnlyDictionary<string, Action> builtinActions)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _builtinActions = builtinActions ?? throw new ArgumentNullException(nameof(builtinActions));

            // 接线：管理器原位的 public 配置方法为转发壳，经这些委托回到本服务
            _manager.LoadHotkeysHandler = LoadHotkeysFromSettings;
            _manager.SaveHotkeysHandler = SaveHotkeysToSettings;
            _manager.ConfigHotkeysProvider = GetHotkeysFromConfigFile;
            _manager.RegisterDefaultsHandler = RegisterDefaultHotkeys;

            // 启动时确保配置文件存在（原在 GlobalHotkeyManager 构造函数中，时序不变：
            // 主窗口先创建管理器、紧接着创建本服务，同一调用栈）
            EnsureConfigFileExists();
        }

        /// <summary>
        /// 获取配置文件中的快捷键信息（不注册，仅用于显示）
        /// </summary>
        /// <returns>配置文件中的快捷键列表</returns>
        internal List<GlobalHotkeyManager.HotkeyInfo> GetHotkeysFromConfigFile()
        {
            try
            {
                if (!File.Exists(HotkeyConfigFile))
                {
                    return new List<GlobalHotkeyManager.HotkeyInfo>();
                }

                // 读取配置文件内容
                string jsonContent = File.ReadAllText(HotkeyConfigFile, Encoding.UTF8);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    LogHelper.WriteLogToFile("快捷键配置文件为空", LogHelper.LogType.Warning);
                    return new List<GlobalHotkeyManager.HotkeyInfo>();
                }

                // 反序列化配置
                var config = JsonConvert.DeserializeObject<HotkeyConfig>(jsonContent);
                if (config?.Hotkeys == null || config.Hotkeys.Count == 0)
                {
                    LogHelper.WriteLogToFile("快捷键配置为空或格式错误", LogHelper.LogType.Warning);
                    return new List<GlobalHotkeyManager.HotkeyInfo>();
                }

                // 转换为HotkeyInfo列表（不注册，仅用于显示）
                var hotkeyList = new List<GlobalHotkeyManager.HotkeyInfo>();
                foreach (var hotkeyConfig in config.Hotkeys)
                {
                    hotkeyList.Add(new GlobalHotkeyManager.HotkeyInfo
                    {
                        Name = hotkeyConfig.Name,
                        Key = hotkeyConfig.Key,
                        Modifiers = hotkeyConfig.Modifiers,
                        Action = null // 不设置动作，仅用于显示
                    });
                }

                return hotkeyList;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置文件读取快捷键信息时出错: {ex.Message}", LogHelper.LogType.Error);
                return new List<GlobalHotkeyManager.HotkeyInfo>();
            }
        }

        /// <summary>
        /// 注册默认快捷键集合
        /// </summary>
        internal void RegisterDefaultHotkeys()
        {
            try
            {
                // 开始注册默认快捷键集合

                // 基本操作快捷键
                _manager.RegisterHotkey("Undo", Key.Z, ModifierKeys.Control, GetActionByName("Undo"));
                _manager.RegisterHotkey("Redo", Key.Y, ModifierKeys.Control, GetActionByName("Redo"));
                _manager.RegisterHotkey("Clear", Key.E, ModifierKeys.Control, GetActionByName("Clear"));
                _manager.RegisterHotkey("Paste", Key.V, ModifierKeys.Control, GetActionByName("Paste"));

                // 工具切换快捷键
                _manager.RegisterHotkey("SelectTool", Key.S, ModifierKeys.Alt, GetActionByName("SelectTool"));
                _manager.RegisterHotkey("DrawTool", Key.D, ModifierKeys.Alt, GetActionByName("DrawTool"));
                _manager.RegisterHotkey("EraserTool", Key.E, ModifierKeys.Alt, GetActionByName("EraserTool"));
                _manager.RegisterHotkey("BlackboardTool", Key.B, ModifierKeys.Alt, GetActionByName("BlackboardTool"));
                _manager.RegisterHotkey("QuitDrawTool", Key.Q, ModifierKeys.Alt, GetActionByName("QuitDrawTool"));

                // 画笔快捷键 - 使用反射访问penType字段
                _manager.RegisterHotkey("Pen1", Key.D1, ModifierKeys.Alt, GetActionByName("Pen1"));
                _manager.RegisterHotkey("Pen2", Key.D2, ModifierKeys.Alt, GetActionByName("Pen2"));
                _manager.RegisterHotkey("Pen3", Key.D3, ModifierKeys.Alt, GetActionByName("Pen3"));
                _manager.RegisterHotkey("Pen4", Key.D4, ModifierKeys.Alt, GetActionByName("Pen4"));
                _manager.RegisterHotkey("Pen5", Key.D5, ModifierKeys.Alt, GetActionByName("Pen5"));

                // 功能快捷键
                _manager.RegisterHotkey("DrawLine", Key.L, ModifierKeys.Alt, GetActionByName("DrawLine"));
                _manager.RegisterHotkey("Screenshot", Key.C, ModifierKeys.Alt, GetActionByName("Screenshot"));
                _manager.RegisterHotkey("QuickDraw", Key.K, ModifierKeys.Alt, GetActionByName("QuickDraw"));
                _manager.RegisterHotkey("Hide", Key.V, ModifierKeys.Alt, GetActionByName("Hide"));

                // 退出快捷键
                _manager.RegisterHotkey("Exit", Key.Escape, ModifierKeys.None, GetActionByName("Exit"));

                // 已注册默认全局快捷键集合
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册默认快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从配置文件加载快捷键
        /// </summary>
        internal void LoadHotkeysFromSettings()
        {
            try
            {
                // 开始从配置文件加载快捷键设置

                // 检查是否应该注册快捷键
                if (!_manager.HotkeysShouldBeRegistered)
                {
                    // 当前状态不允许注册快捷键，跳过加载
                    return;
                }

                // 如果配置文件不存在，先创建默认配置文件
                if (!File.Exists(HotkeyConfigFile))
                {
                    LogHelper.WriteLogToFile($"快捷键配置文件不存在: {HotkeyConfigFile}", LogHelper.LogType.Warning);
                    CreateDefaultConfigFile();
                    RegisterDefaultHotkeys();
                    _manager.HotkeysShouldBeRegistered = true;
                    return;
                }

                // 尝试从配置文件加载
                if (LoadHotkeysFromConfigFile())
                {
                    // 成功从配置文件加载快捷键设置
                    _manager.HotkeysShouldBeRegistered = true;
                }
                else
                {
                    LogHelper.WriteLogToFile("配置文件存在但加载失败，回退到默认快捷键", LogHelper.LogType.Warning);
                    RegisterDefaultHotkeys();
                    _manager.HotkeysShouldBeRegistered = true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从设置加载快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                // 出错时不自动使用默认快捷键，保持当前状态
            }
        }

        /// <summary>
        /// 保存快捷键配置到设置
        /// </summary>
        internal void SaveHotkeysToSettings()
        {
            try
            {

                if (SaveHotkeysToConfigFile())
                {
                }
                else
                {
                    LogHelper.WriteLogToFile("保存快捷键配置失败", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 确保配置文件存在，如果不存在则创建
        /// </summary>
        private void EnsureConfigFileExists()
        {
            try
            {
                // 如果配置文件不存在，创建默认配置文件
                if (!File.Exists(HotkeyConfigFile))
                {
                    CreateDefaultConfigFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"确保快捷键配置文件存在时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 创建默认的快捷键配置文件
        /// </summary>
        private void CreateDefaultConfigFile()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(HotkeyConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 创建默认配置对象
                var config = new HotkeyConfig
                {
                    Version = "1.0",
                    LastModified = DateTime.Now,
                    Hotkeys = new List<HotkeyConfigItem>()
                };

                // 添加默认快捷键配置
                config.Hotkeys.AddRange(new[]
                {
                    new HotkeyConfigItem { Name = "Undo", Key = Key.Z, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Redo", Key = Key.Y, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Clear", Key = Key.E, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "Paste", Key = Key.V, Modifiers = ModifierKeys.Control },
                    new HotkeyConfigItem { Name = "SelectTool", Key = Key.S, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "DrawTool", Key = Key.D, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "EraserTool", Key = Key.E, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "BlackboardTool", Key = Key.B, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "QuitDrawTool", Key = Key.Q, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen1", Key = Key.D1, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen2", Key = Key.D2, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen3", Key = Key.D3, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen4", Key = Key.D4, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Pen5", Key = Key.D5, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "DrawLine", Key = Key.L, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Screenshot", Key = Key.C, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "QuickDraw", Key = Key.K, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Hide", Key = Key.V, Modifiers = ModifierKeys.Alt },
                    new HotkeyConfigItem { Name = "Exit", Key = Key.Escape, Modifiers = ModifierKeys.None }
                });

                // 序列化为JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                string jsonContent = JsonConvert.SerializeObject(config, settings);

                // 写入配置文件
                File.WriteAllText(HotkeyConfigFile, jsonContent, Encoding.UTF8);

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建默认快捷键配置文件时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从配置文件加载快捷键设置
        /// </summary>
        /// <returns>是否加载成功</returns>
        private bool LoadHotkeysFromConfigFile()
        {
            try
            {
                if (!File.Exists(HotkeyConfigFile))
                {
                    LogHelper.WriteLogToFile($"快捷键配置文件不存在: {HotkeyConfigFile}", LogHelper.LogType.Warning);
                    return false;
                }

                // 读取配置文件内容
                string jsonContent = File.ReadAllText(HotkeyConfigFile, Encoding.UTF8);
                if (string.IsNullOrEmpty(jsonContent))
                {
                    LogHelper.WriteLogToFile("快捷键配置文件为空", LogHelper.LogType.Warning);
                    return false;
                }

                // 反序列化配置
                var config = JsonConvert.DeserializeObject<HotkeyConfig>(jsonContent);
                if (config?.Hotkeys == null || config.Hotkeys.Count == 0)
                {
                    LogHelper.WriteLogToFile("快捷键配置为空或格式错误", LogHelper.LogType.Warning);
                    return false;
                }

                // 注册配置中的快捷键
                int successCount = 0;
                foreach (var hotkeyConfig in config.Hotkeys)
                {
                    try
                    {
                        // 根据快捷键名称获取对应的动作
                        var action = GetActionByName(hotkeyConfig.Name);
                        if (action != null)
                        {
                            if (_manager.RegisterHotkey(hotkeyConfig.Name, hotkeyConfig.Key, hotkeyConfig.Modifiers, action))
                            {
                                successCount++;
                            }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile($"未找到快捷键 {hotkeyConfig.Name} 对应的动作", LogHelper.LogType.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"注册快捷键 {hotkeyConfig.Name} 时出错: {ex.Message}", LogHelper.LogType.Error);
                    }
                }

                // 旧版 HotkeyConfig.json 无「快抽」项时补注册默认组合，避免升级后无快捷键
                if (successCount > 0 && !_manager.IsHotkeyRegistered("QuickDraw"))
                {
                    var quickDrawAction = GetActionByName("QuickDraw");
                    if (quickDrawAction != null && _manager.RegisterHotkey("QuickDraw", Key.K, ModifierKeys.Alt, quickDrawAction))
                        successCount++;
                }

                if (successCount > 0)
                {
                    _manager.HotkeysShouldBeRegistered = true;
                }
                return successCount > 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置文件加载快捷键时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 保存快捷键配置到配置文件
        /// </summary>
        /// <returns>是否保存成功</returns>
        private bool SaveHotkeysToConfigFile()
        {
            try
            {
                // 确保配置目录存在
                string configDir = Path.GetDirectoryName(HotkeyConfigFile);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // 创建配置对象
                var config = new HotkeyConfig
                {
                    Version = "1.0",
                    LastModified = DateTime.Now,
                    Hotkeys = new List<HotkeyConfigItem>()
                };

                // 添加所有已注册的快捷键
                foreach (var hotkey in _manager.GetRegisteredHotkeys())
                {
                    config.Hotkeys.Add(new HotkeyConfigItem
                    {
                        Name = hotkey.Name,
                        Key = hotkey.Key,
                        Modifiers = hotkey.Modifiers
                    });
                }

                // 序列化为JSON
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                string jsonContent = JsonConvert.SerializeObject(config, settings);

                // 直接写入原文件，覆盖原有内容
                File.WriteAllText(HotkeyConfigFile, jsonContent, Encoding.UTF8);

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存快捷键配置到配置文件时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 根据快捷键名称获取对应的动作（M16：原 switch 硬编码改为构造注入的动作字典查表）
        /// </summary>
        /// <param name="hotkeyName">快捷键名称</param>
        /// <returns>对应的动作，如果不存在则返回null</returns>
        private Action GetActionByName(string hotkeyName)
        {
            try
            {
                if (hotkeyName != null && _builtinActions.TryGetValue(hotkeyName, out var action))
                {
                    return action;
                }

                LogHelper.WriteLogToFile($"未知的快捷键名称: {hotkeyName}", LogHelper.LogType.Warning);
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取快捷键 {hotkeyName} 对应动作时出错: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        #region Nested Classes
        /// <summary>
        /// 快捷键配置类
        /// </summary>
        private class HotkeyConfig
        {
            public string Version { get; set; }
            public DateTime LastModified { get; set; }
            public List<HotkeyConfigItem> Hotkeys { get; set; }
        }

        /// <summary>
        /// 快捷键配置项类
        /// </summary>
        private class HotkeyConfigItem
        {
            public string Name { get; set; }
            public Key Key { get; set; }
            public ModifierKeys Modifiers { get; set; }
        }
        #endregion
    }
}
