using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件模板，用于开发者参考
    /// 注意：实际开发时，请将此类移到单独的程序集中
    /// </summary>
    public class PluginTemplate : PluginBase
    {
        #region 插件基本信息

        /// <summary>
        /// 插件名称
        /// </summary>
        public override string Name => "插件模板";

        /// <summary>
        /// 插件描述
        /// </summary>
        public override string Description => "这是一个插件开发模板，用于开发者参考。";

        /// <summary>
        /// 插件版本
        /// </summary>
        public override Version Version => new Version(1, 0, 0);

        /// <summary>
        /// 插件作者
        /// </summary>
        public override string Author => "Your Name";

        /// <summary>
        /// 是否为内置插件（外部插件请返回false）
        /// </summary>
        public override bool IsBuiltIn => false;

        #endregion

        #region 插件生命周期

        /// <summary>
        /// 插件初始化
        /// 在这里进行插件的初始化工作，如加载配置、注册事件等
        /// </summary>
        public override void Initialize()
        {
            // 先调用基类方法，这样会设置插件ID和记录日志
            base.Initialize();
            
            // TODO: 在这里进行插件初始化工作
            
            // 示例：记录初始化信息
            LogHelper.WriteLogToFile($"插件 {Name} 开始初始化", LogHelper.LogType.Info);
            
            // 示例：加载配置
            LoadConfig();
            
            // 示例：注册自定义事件
            // MainWindow.Instance.SomeEvent += OnSomeEvent;
            
            LogHelper.WriteLogToFile($"插件 {Name} 初始化完成", LogHelper.LogType.Info);
        }

        /// <summary>
        /// 启用插件
        /// 在这里激活插件功能
        /// </summary>
        public override void Enable()
        {
            // 先调用基类方法，这样会设置插件状态和记录日志
            base.Enable();
            
            // TODO: 在这里启用插件功能
            
            LogHelper.WriteLogToFile($"插件 {Name} 已启用", LogHelper.LogType.Info);
        }

        /// <summary>
        /// 禁用插件
        /// 在这里停用插件功能
        /// </summary>
        public override void Disable()
        {
            // 先调用基类方法，这样会设置插件状态和记录日志
            base.Disable();
            
            // TODO: 在这里禁用插件功能
            
            LogHelper.WriteLogToFile($"插件 {Name} 已禁用", LogHelper.LogType.Info);
        }

        /// <summary>
        /// 清理资源
        /// 在插件卸载时调用，清理资源
        /// </summary>
        public override void Cleanup()
        {
            // TODO: 在这里清理插件资源
            
            // 示例：取消注册事件
            // MainWindow.Instance.SomeEvent -= OnSomeEvent;
            
            // 示例：保存配置
            SaveConfig();
            
            // 最后调用基类方法
            base.Cleanup();
        }

        #endregion

        #region 插件配置

        /// <summary>
        /// 加载插件配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                // TODO: 从文件或其他位置加载配置
                // 示例：
                // string configPath = Path.Combine(App.RootPath, "PluginConfigs", "YourPluginName.json");
                // if (File.Exists(configPath))
                // {
                //     string json = File.ReadAllText(configPath);
                //     YourConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<YourConfigClass>(json);
                // }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存插件配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                // TODO: 保存配置到文件或其他位置
                // 示例：
                // string configDir = Path.Combine(App.RootPath, "PluginConfigs");
                // if (!Directory.Exists(configDir))
                // {
                //     Directory.CreateDirectory(configDir);
                // }
                // string configPath = Path.Combine(configDir, "YourPluginName.json");
                // string json = Newtonsoft.Json.JsonConvert.SerializeObject(YourConfig, Newtonsoft.Json.Formatting.Indented);
                // File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存插件配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 插件设置界面

        /// <summary>
        /// 获取插件设置界面
        /// </summary>
        /// <returns>插件设置界面</returns>
        public override UserControl GetSettingsView()
        {
            // 创建插件设置界面
            return new PluginTemplateSettingsControl();
        }

        #endregion

        #region 插件功能方法

        // TODO: 在这里添加插件的具体功能方法

        /// <summary>
        /// 示例方法：执行一些功能
        /// </summary>
        public void DoSomething()
        {
            if (!IsEnabled) return;
            
            try
            {
                // TODO: 实现你的功能
                MessageBox.Show("插件功能执行示例", "插件模板", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"执行插件功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion
    }

    /// <summary>
    /// 插件设置控件
    /// </summary>
    public class PluginTemplateSettingsControl : UserControl
    {
        public PluginTemplateSettingsControl()
        {
            // 创建设置界面布局
            var panel = new StackPanel
            {
                Margin = new Thickness(10)
            };

            // 添加标题
            panel.Children.Add(new TextBlock
            {
                Text = "插件模板设置",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // 添加说明文字
            panel.Children.Add(new TextBlock
            {
                Text = "这是一个示例设置界面，你可以在这里添加自己的设置控件。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // 添加示例设置选项
            var checkBox = new CheckBox
            {
                Content = "启用某项功能",
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(checkBox);

            // 添加文本输入框
            panel.Children.Add(new TextBlock
            {
                Text = "设置项:",
                Margin = new Thickness(0, 5, 0, 5)
            });
            
            panel.Children.Add(new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            });

            // 添加按钮
            var button = new Button
            {
                Content = "保存设置",
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            
            button.Click += (sender, e) => 
            {
                MessageBox.Show("设置已保存！", "插件模板", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            
            panel.Children.Add(button);

            // 设置控件内容
            this.Content = panel;
        }
    }
} 