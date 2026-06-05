using CommunityToolkit.Mvvm.ComponentModel;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 自动化服务，负责管理工作流的生命周期、触发和恢复。
    /// </summary>
    public class AutomationService : ObservableObject
    {
        private readonly string _configsFolderPath;

        public AutomationService(string configsFolderPath)
        {
            _configsFolderPath = configsFolderPath;
            if (!Directory.Exists(_configsFolderPath))
            {
                Directory.CreateDirectory(_configsFolderPath);
            }

            // 监听规则状态变化，自动恢复不再满足条件的工作流
            RulesetService.StatusUpdated += RulesetServiceOnStatusUpdated;
        }

        private void RulesetServiceOnStatusUpdated(object? sender, EventArgs e)
        {
            if (!IsAutomationEnabled) return;

            foreach (var workflow in Workflows.Where(x => x is { ActionSet: { IsOn: true, IsRevertEnabled: true }, IsConditionEnabled: true }))
            {
                if (RulesetService.IsRulesetSatisfied(workflow.Ruleset))
                    continue;
                ActionService.Revert(workflow.ActionSet);
            }
        }

        private string _currentConfig = "default";
        public string CurrentConfig
        {
            get => _currentConfig;
            set
            {
                if (value == _currentConfig) return;
                _currentConfig = value;
                OnPropertyChanged();
            }
        }

        public string CurrentConfigPath => Path.GetFullPath(Path.Combine(_configsFolderPath, CurrentConfig + ".json"));

        private ObservableCollection<Workflow> _workflows = new();
        public ObservableCollection<Workflow> Workflows
        {
            get => _workflows;
            set
            {
                if (Equals(value, _workflows)) return;
                _workflows = value;
                OnPropertyChanged();
            }
        }

        private List<string> _configs = new();
        public List<string> Configs
        {
            get => _configs;
            set
            {
                if (Equals(value, _configs)) return;
                _configs = value;
                OnPropertyChanged();
            }
        }

        private bool _isAutomationEnabled = true;
        public bool IsAutomationEnabled
        {
            get => _isAutomationEnabled;
            set
            {
                if (value == _isAutomationEnabled) return;
                _isAutomationEnabled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 刷新配置文件列表
        /// </summary>
        public void RefreshConfigs()
        {
            Configs = Directory.GetFiles(_configsFolderPath, "*.json")
                               .Select(Path.GetFileNameWithoutExtension)
                               .Where(x => x != null)
                               .Select(x => x!)
                               .ToList();
        }

        /// <summary>
        /// 加载当前配置
        /// </summary>
        public void LoadConfig()
        {
            // 卸载当前工作流
            foreach (var workflow in Workflows)
            {
                UnloadWorkflow(workflow);
            }
            Workflows.CollectionChanged -= WorkflowsOnCollectionChanged;

            if (File.Exists(CurrentConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(CurrentConfigPath);
                    Workflows = JsonConvert.DeserializeObject<ObservableCollection<Workflow>>(json) ?? new ObservableCollection<Workflow>();
                }
                catch
                {
                    Workflows = new ObservableCollection<Workflow>();
                }
            }
            else
            {
                Workflows = new ObservableCollection<Workflow>();
                SaveConfig();
            }

            foreach (var workflow in Workflows)
            {
                LoadWorkflow(workflow);
            }
            Workflows.CollectionChanged += WorkflowsOnCollectionChanged;
        }

        /// <summary>
        /// 保存当前配置
        /// </summary>
        public void SaveConfig(string note = "")
        {
            try
            {
                var json = JsonConvert.SerializeObject(Workflows, Formatting.Indented);
                File.WriteAllText(CurrentConfigPath, json);
            }
            catch
            {
                // 忽略保存失败
            }
        }

        private void WorkflowsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (Workflow workflow in e.NewItems!)
                        LoadWorkflow(workflow);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (Workflow workflow in e.OldItems!)
                        UnloadWorkflow(workflow);
                    break;
            }
            SaveConfig("CollectionChanged");
        }

        private void LoadWorkflow(Workflow workflow)
        {
            // 如果规则组为空，初始化一个默认的
            if (workflow.Ruleset.Groups.Count == 0)
            {
                workflow.Ruleset.Groups.Add(new RuleGroup
                {
                    Rules = new ObservableCollection<Rule> { new Rule() }
                });
            }

            void TriggersOnCollectionChanged(object? s, NotifyCollectionChangedEventArgs e)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (TriggerSettings trigger in e.NewItems!)
                            LoadTrigger(workflow, trigger);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        foreach (TriggerSettings trigger in e.OldItems!)
                            UnloadTrigger(workflow, trigger);
                        break;
                }
            }

            workflow.Triggers.CollectionChanged += TriggersOnCollectionChanged;

            // 通过 Unloading 事件取消订阅，避免内存泄漏
            workflow.Unloading += OnWorkflowUnloading;

            foreach (var trigger in workflow.Triggers)
            {
                LoadTrigger(workflow, trigger);
            }

            return;

            void OnWorkflowUnloading(object? sender, EventArgs e)
            {
                workflow.Unloading -= OnWorkflowUnloading;
                workflow.Triggers.CollectionChanged -= TriggersOnCollectionChanged;
            }
        }

        public void UnloadWorkflow(Workflow workflow)
        {
            workflow.Unload();
            foreach (var trigger in workflow.Triggers)
            {
                UnloadTrigger(workflow, trigger);
            }
        }

        private RulesetService? _rulesetService;
        public RulesetService RulesetService => _rulesetService ??= new RulesetService();

        private ActionService? _actionService;
        public ActionService ActionService => _actionService ??= new ActionService();

        private void LoadTrigger(Workflow workflow, TriggerSettings trigger)
        {
            if (trigger.TriggerInstance != null) return;

            var triggerInstance = AutomationRegistry.ResolveTrigger(trigger.Id);
            if (triggerInstance == null) return;

            // 处理设置反序列化（对齐 ClassIsland ActivateTrigger 逻辑）
            var settings = trigger.Settings;
            var triggerInfo = trigger.AssociatedTriggerInfo;
            if (triggerInfo?.SettingsType != null)
            {
                // settings 为 null 时创建默认实例
                var settingsReal = settings ?? Activator.CreateInstance(triggerInfo.SettingsType);
                try
                {
                    if (settingsReal is JToken jToken)
                    {
                        settingsReal = jToken.ToObject(triggerInfo.SettingsType);
                    }
                }
                catch
                {
                    settingsReal = Activator.CreateInstance(triggerInfo.SettingsType);
                }

                if (settingsReal?.GetType() != triggerInfo.SettingsType)
                {
                    settingsReal = Activator.CreateInstance(triggerInfo.SettingsType);
                }

                settings = settingsReal;
                trigger.Settings = settings;
            }

            triggerInstance.SettingsInternal = settings;
            triggerInstance.AssociatedWorkflow = workflow;
            triggerInstance.Triggered += TriggerTriggered;
            triggerInstance.TriggeredRecover += TriggerTriggeredRecover;
            trigger.TriggerInstance = triggerInstance;

            // 对齐 ClassIsland：监听 trigger.Id 变化，自动重新加载触发器
            trigger.PropertyChanged += TriggerOnPropertyChanged;
            trigger.Unloading += TriggerOnUnloading;

            try
            {
                triggerInstance.Loaded();
            }
            catch
            {
                // 触发器加载失败不影响其他
            }

            return;

            void TriggerOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName != nameof(trigger.Id)) return;
                UnloadTrigger(workflow, trigger);
                LoadTrigger(workflow, trigger);
            }

            void TriggerOnUnloading(object? sender, EventArgs e)
            {
                trigger.Unloading -= TriggerOnUnloading;
                trigger.PropertyChanged -= TriggerOnPropertyChanged;
            }
        }

        private void UnloadTrigger(Workflow workflow, TriggerSettings trigger)
        {
            if (trigger.TriggerInstance == null) return;

            // 对齐 ClassIsland：先触发 Unloading 事件取消订阅，再卸载触发器实例
            trigger.Unload();

            try
            {
                trigger.TriggerInstance.UnLoaded();
            }
            catch { }

            trigger.TriggerInstance.Triggered -= TriggerTriggered;
            trigger.TriggerInstance.TriggeredRecover -= TriggerTriggeredRecover;
            trigger.TriggerInstance = null;
        }

        private void TriggerTriggered(object? sender, EventArgs e)
        {
            if (!IsAutomationEnabled) return;
            if (sender is not TriggerBase trigger) return;

            var workflow = trigger.AssociatedWorkflow;
            if (workflow == null) return;
            if (!workflow.ActionSet.IsEnabled) return;

            // 如果已触发且启用了恢复，则跳过（等待恢复触发器）
            if (workflow.ActionSet.IsRevertEnabled && workflow.ActionSet.IsOn) return;

            // 检查条件
            if (workflow.IsConditionEnabled)
            {
                if (!RulesetService.IsRulesetSatisfied(workflow.Ruleset)) return;
            }

            // 执行行动
            ActionService.Invoke(workflow.ActionSet);
            SaveConfig("TriggerTriggered");
        }

        private void TriggerTriggeredRecover(object? sender, EventArgs e)
        {
            if (!IsAutomationEnabled) return;
            if (sender is not TriggerBase trigger) return;

            var workflow = trigger.AssociatedWorkflow;
            if (workflow == null) return;
            if (!workflow.ActionSet.IsOn) return;

            ActionService.Revert(workflow.ActionSet);
            SaveConfig("TriggerTriggeredRecover");
        }
    }
}
