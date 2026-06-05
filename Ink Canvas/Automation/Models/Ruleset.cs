using CommunityToolkit.Mvvm.ComponentModel;
using Ink_Canvas.WorkflowAutomation.Enums;
using System.Collections.ObjectModel;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个包含若干个规则的规则集。
    /// </summary>
    public class Ruleset : ObservableObject
    {
        private RulesetLogicalMode _mode = RulesetLogicalMode.Or;
        private bool _isReversed = false;
        private ObservableCollection<RuleGroup> _groups = new();
        private int _state = 0;

        /// <summary>
        /// 逻辑模式。
        /// </summary>
        public RulesetLogicalMode Mode
        {
            get => _mode;
            set
            {
                if (value == _mode) return;
                _mode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否反转判断。
        /// </summary>
        public bool IsReversed
        {
            get => _isReversed;
            set
            {
                if (value == _isReversed) return;
                _isReversed = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 规则分组
        /// </summary>
        public ObservableCollection<RuleGroup> Groups
        {
            get => _groups;
            set
            {
                if (Equals(value, _groups)) return;
                _groups = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 满足状态
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int State
        {
            get => _state;
            set
            {
                if (value == _state) return;
                _state = value;
                OnPropertyChanged();
            }
        }
    }
}
