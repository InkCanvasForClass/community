using System.Windows.Controls;

namespace Ink_Canvas.WorkflowAutomation.Abstractions
{
    /// <summary>
    /// 规则设置控件的基类。
    /// 对齐 ClassIsland 的规则设置控件模式。
    /// </summary>
    public abstract class RuleSettingsControlBase : UserControl
    {
        /// <summary>
        /// 规则设置数据
        /// </summary>
        public abstract object Settings { get; set; }
    }

    /// <summary>
    /// 带强类型设置的规则设置控件基类。
    /// </summary>
    public abstract class RuleSettingsControlBase<T> : RuleSettingsControlBase where T : class, new()
    {
        private T _settings;

        public override object Settings
        {
            get => _settings;
            set
            {
                _settings = value as T ?? new T();
                OnSettingsChanged(_settings);
            }
        }

        protected T TypedSettings => _settings ?? new T();

        protected virtual void OnSettingsChanged(T settings) { }
    }
}
