using System.Windows.Controls;

namespace Ink_Canvas.WorkflowAutomation.Abstractions
{
    /// <summary>
    /// 触发器设置控件的基类。
    /// </summary>
    public abstract class TriggerSettingsControlBase : UserControl
    {
        /// <summary>
        /// 触发器设置数据
        /// </summary>
        public abstract object Settings { get; set; }
    }

    /// <summary>
    /// 带强类型设置的触发器设置控件基类。
    /// </summary>
    public abstract class TriggerSettingsControlBase<T> : TriggerSettingsControlBase where T : class, new()
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
