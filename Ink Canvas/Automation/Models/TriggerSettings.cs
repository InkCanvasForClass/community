using CommunityToolkit.Mvvm.ComponentModel;
using Ink_Canvas.WorkflowAutomation.Services;
using System;
using System.Linq;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个触发器的设置。
    /// </summary>
    public class TriggerSettings : ObservableObject
    {
        private string _id = "";
        private object _settings;

        /// <summary>
        /// 触发器 ID
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (value == _id) return;
                _id = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AssociatedTriggerInfo));
            }
        }

        /// <summary>
        /// 触发器设置
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
        /// 关联的触发器信息
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public TriggerInfo AssociatedTriggerInfo => AutomationRegistry.RegisteredTriggers.FirstOrDefault(x => x.Id == Id);

        [Newtonsoft.Json.JsonIgnore]
        internal Abstractions.TriggerBase TriggerInstance { get; set; }

        internal void Unload()
        {
            Unloading?.Invoke(this, EventArgs.Empty);
        }

        internal event EventHandler Unloading;
    }
}
