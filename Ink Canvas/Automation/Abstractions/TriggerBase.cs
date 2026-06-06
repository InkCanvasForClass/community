using Ink_Canvas.WorkflowAutomation.Models;
using System;

namespace Ink_Canvas.WorkflowAutomation.Abstractions
{
    /// <summary>
    /// 自动化触发器基类。
    /// </summary>
    public abstract class TriggerBase
    {
        internal object SettingsInternal { get; set; }

        /// <summary>
        /// 触发这个触发器。
        /// </summary>
        protected void Trigger()
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 触发恢复触发器。
        /// </summary>
        protected void TriggerRevert()
        {
            TriggeredRecover?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 当此触发器被加载到工作流上时，调用此方法。
        /// </summary>
        public abstract void Loaded();

        /// <summary>
        /// 当此触发器被从工作流上卸载时，调用此方法。
        /// </summary>
        public abstract void UnLoaded();

        internal event EventHandler Triggered;

        internal event EventHandler TriggeredRecover;

        /// <summary>
        /// 此触发器关联的工作流。
        /// </summary>
        public Workflow AssociatedWorkflow { get; internal set; }
    }

    /// <summary>
    /// 带强类型设置的触发器基类。
    /// </summary>
    public abstract class TriggerBase<T> : TriggerBase where T : class
    {
        /// <summary>
        /// 当前触发器的设置
        /// </summary>
        protected T Settings => (SettingsInternal as T) ?? Activator.CreateInstance<T>();
    }
}
