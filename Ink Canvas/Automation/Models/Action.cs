using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个行动。
    /// </summary>
    public class Action : ObservableObject
    {
        private string _id = "";
        private object _settings;
        private Exception _exception;
        private bool _isWorking = false;

        /// <summary>
        /// 行动 ID。
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (value == _id) return;
                _id = value;
                Exception = null;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动设置。
        /// </summary>
        public object Settings
        {
            get => _settings;
            set
            {
                if (Equals(value, _settings)) return;
                _settings = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动错误。
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Exception Exception
        {
            get => _exception;
            set
            {
                if (Equals(value, _exception)) return;
                _exception = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 行动正在运行。
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool IsWorking
        {
            get => _isWorking;
            set
            {
                if (Equals(value, _isWorking)) return;
                _isWorking = value;
                OnPropertyChanged();
            }
        }
    }
}
