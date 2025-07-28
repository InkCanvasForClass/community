using System;
using System.Windows.Controls;

namespace Ink_Canvas.Helpers.Plugins
{
    /// <summary>
    /// 插件基类，提供基本实现
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        /// <summary>
        /// 插件状态（私有字段）
        /// </summary>
        private bool _isEnabled;
        
        /// <summary>
        /// 插件状态（公共属性）
        /// </summary>
        public bool IsEnabled 
        {
            get => _isEnabled;
            protected set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnEnabledStateChanged(value);
                }
            }
        }
        
        /// <summary>
        /// 插件ID
        /// </summary>
        public string Id { get; protected set; }
        
        /// <summary>
        /// 插件路径
        /// </summary>
        public string PluginPath { get; set; }

        /// <summary>
        /// 插件名称
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 插件描述
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// 插件版本
        /// </summary>
        public abstract Version Version { get; }

        /// <summary>
        /// 插件作者
        /// </summary>
        public abstract string Author { get; }

        /// <summary>
        /// 是否为内置插件
        /// </summary>
        public virtual bool IsBuiltIn => false;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event EventHandler<bool> EnabledStateChanged;

        /// <summary>
        /// 初始化插件
        /// </summary>
        public virtual void Initialize()
        {
            Id = GetType().FullName;
            
            // 添加日志，记录插件名称
            try
            {
                string name = Name;
                LogHelper.WriteLogToFile($"初始化插件: ID={Id}, 名称={name ?? "未命名"}");
                
                if (string.IsNullOrEmpty(name))
                {
                    LogHelper.WriteLogToFile($"警告: 插件 {Id} 的名称为空", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取插件名称时出错: {ex.Message}", LogHelper.LogType.Error);
            }
            
            LogHelper.WriteLogToFile($"插件 {Name} 已初始化");
        }

        /// <summary>
        /// 启用插件
        /// </summary>
        public virtual void Enable()
        {
            if (!IsEnabled)
            {
                IsEnabled = true;
                LogHelper.WriteLogToFile($"插件 {Name} 已启用");
            }
        }

        /// <summary>
        /// 禁用插件
        /// </summary>
        public virtual void Disable()
        {
            if (IsEnabled)
            {
                IsEnabled = false;
                LogHelper.WriteLogToFile($"插件 {Name} 已禁用");
            }
        }

        /// <summary>
        /// 获取插件设置界面
        /// </summary>
        /// <returns>插件设置界面</returns>
        public virtual UserControl GetSettingsView()
        {
            // 默认返回空设置页面
            return new UserControl();
        }

        /// <summary>
        /// 插件卸载时的清理工作
        /// </summary>
        public virtual void Cleanup()
        {
            LogHelper.WriteLogToFile($"插件 {Name} 已卸载");
        }
        
        /// <summary>
        /// 保存插件自身的设置
        /// 注意：此方法仅用于保存插件的特定设置，不应影响插件启用/禁用状态
        /// 插件启用状态由PluginManager统一管理
        /// </summary>
        public virtual void SavePluginSettings()
        {
            // 默认实现不做任何事情
            // 子类可以重写此方法，将自身设置保存到配置文件中
            LogHelper.WriteLogToFile($"插件 {Name} 设置已保存", LogHelper.LogType.Event);
        }
        
        /// <summary>
        /// 触发状态变更事件
        /// </summary>
        /// <param name="isEnabled">是否启用</param>
        protected virtual void OnEnabledStateChanged(bool isEnabled)
        {
            EnabledStateChanged?.Invoke(this, isEnabled);
        }
    }
} 