using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个行动组。
    /// </summary>
    public class ActionSet : ObservableObject
    {
        private bool _isEnabled = true;
        private string _name = "新行动";
        private string _guid = System.Guid.NewGuid().ToString();
        private bool _isOn = false;
        private ObservableCollection<Action> _actions = new();
        private bool _isRevertEnabled = true;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value == _isEnabled) return;
                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动组Guid，仅用于标识设置叠层。
        /// </summary>
        public string Guid
        {
            get => _guid;
            set
            {
                if (value == _guid) return;
                _guid = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动组被触发后还未恢复。
        /// </summary>
        public bool IsOn
        {
            get => _isOn;
            set
            {
                if (value == _isOn) return;
                _isOn = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动组中的所有行动。
        /// </summary>
        public ObservableCollection<Action> Actions
        {
            get => _actions;
            set
            {
                if (Equals(value, _actions)) return;
                _actions = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否启用恢复
        /// </summary>
        public bool IsRevertEnabled
        {
            get => _isRevertEnabled;
            set
            {
                if (value == _isRevertEnabled) return;
                _isRevertEnabled = value;
                OnPropertyChanged();
            }
        }
    }
}
