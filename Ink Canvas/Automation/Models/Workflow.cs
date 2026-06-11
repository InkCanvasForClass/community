using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个自动化工作流。自动化工作流会被自动触发和恢复。
    /// </summary>
    public class Workflow : ObservableObject
    {
        private Ruleset _ruleset = new();
        private ActionSet _actionSet = new();
        private ObservableCollection<TriggerSettings> _triggers = new();
        private bool _isConditionEnabled = false;

        /// <summary>
        /// 规则集
        /// </summary>
        public Ruleset Ruleset
        {
            get => _ruleset;
            set
            {
                if (value == _ruleset) return;
                _ruleset = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动组
        /// </summary>
        public ActionSet ActionSet
        {
            get => _actionSet;
            set
            {
                if (value == _actionSet) return;
                _actionSet = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 触发此工作流的触发器
        /// </summary>
        public ObservableCollection<TriggerSettings> Triggers
        {
            get => _triggers;
            set
            {
                if (Equals(value, _triggers)) return;
                _triggers = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否启用条件判定
        /// </summary>
        public bool IsConditionEnabled
        {
            get => _isConditionEnabled;
            set
            {
                if (value == _isConditionEnabled) return;
                _isConditionEnabled = value;
                OnPropertyChanged();
            }
        }

        internal void Unload()
        {
            Unloading?.Invoke(this, EventArgs.Empty);
        }

        internal event EventHandler Unloading;
    }
}
