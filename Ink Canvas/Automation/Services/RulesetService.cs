using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Enums;
using Ink_Canvas.WorkflowAutomation.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 规则集服务，负责评估规则集是否满足。
    /// 对齐 ClassIsland 的 RulesetService，实现 IRulesetService 接口。
    /// 事件驱动模式：订阅 SystemEventMonitor 的系统事件，仅在状态可能变化时重新评估。
    /// 保留 5s 兜底轮询防止遗漏。
    /// </summary>
    public class RulesetService : IRulesetService, IDisposable
    {
        /// <summary>
        /// 规则状态更新事件，当规则条件可能发生变化时触发。
        /// </summary>
        public event EventHandler StatusUpdated;

        private Timer _fallbackTimer;
        private SystemEventMonitor _monitor;

        private int BoolToRuleObjectState(bool? v) => v switch
        {
            true => 2,
            false => 1,
            null => 0
        };

        public RulesetService()
        {
            _monitor = AutomationBootstrap.Monitor;

            // 订阅系统事件监控器
            if (_monitor != null)
            {
                _monitor.ForegroundWindowChanged += OnStatusMayHaveChanged;
                _monitor.ProcessChanged += OnStatusMayHaveChanged;
                _monitor.InternalStateChanged += OnStatusMayHaveChanged;
            }

            // 兜底轮询（5s），防止事件遗漏
            _fallbackTimer = new Timer(5000);
            _fallbackTimer.Elapsed += OnFallbackTimerElapsed;
            _fallbackTimer.AutoReset = true;
            _fallbackTimer.Start();
        }

        private void OnStatusMayHaveChanged(object sender, EventArgs e)
        {
            NotifyStatusChanged();
        }

        private void OnFallbackTimerElapsed(object sender, ElapsedEventArgs e)
        {
            NotifyStatusChanged();
        }

        /// <summary>
        /// 判断指定的规则集是否成立，同时更新所有层级的 State。
        /// </summary>
        public bool IsRulesetSatisfied(Ruleset ruleset)
        {
            if (ruleset.Groups.Count <= 0)
            {
                ruleset.State = BoolToRuleObjectState(false);
                return false;
            }

            // 先重置所有状态
            foreach (var group in ruleset.Groups)
            {
                group.State = 0;
                foreach (var rule in group.Rules)
                {
                    rule.State = 0;
                }
            }

            var isSatisfied = ruleset.Mode == RulesetLogicalMode.And;

            foreach (var group in ruleset.Groups.Where(x => x.IsEnabled))
            {
                bool? res = IsRuleGroupSatisfied(group);
                group.State = BoolToRuleObjectState(res);
                if (res == null)
                    continue;

                bool result = res.Value;
                if (!result && ruleset.Mode == RulesetLogicalMode.And)
                {
                    isSatisfied = false;
                    break;
                }
                if (result && ruleset.Mode == RulesetLogicalMode.Or)
                {
                    isSatisfied = true;
                    break;
                }
            }

            isSatisfied ^= ruleset.IsReversed;
            ruleset.State = BoolToRuleObjectState(isSatisfied);
            return isSatisfied;
        }

        /// <summary>
        /// 判断规则组是否成立，同时更新规则组内所有规则的 State。
        /// </summary>
        private bool? IsRuleGroupSatisfied(RuleGroup group)
        {
            // 没有有效规则时返回 null（未知状态）
            if (group.Rules.Where(r => r.Id != "").ToList().Count <= 0)
            {
                return null;
            }

            var groupSatisfied = group.Mode == RulesetLogicalMode.And;

            foreach (var rule in group.Rules)
            {
                bool? res = IsRuleSatisfied(rule);
                if (res == null)
                {
                    rule.State = BoolToRuleObjectState(res);
                    continue;
                }

                bool result = res.Value;
                result ^= rule.IsReversed;
                rule.State = BoolToRuleObjectState(result);
                if (!result && group.Mode == RulesetLogicalMode.And)
                {
                    groupSatisfied = false;
                    break;
                }
                if (result && group.Mode == RulesetLogicalMode.Or)
                {
                    groupSatisfied = true;
                    break;
                }
            }

            groupSatisfied ^= group.IsReversed;
            return groupSatisfied;
        }

        /// <summary>
        /// 判断单条规则是否成立。
        /// </summary>
        private bool? IsRuleSatisfied(Rule rule)
        {
            if (rule.Id == string.Empty)
                return null;

            if (!IRulesetService.Rules.TryGetValue(rule.Id, out var info))
                return false;

            if (info.Handle == null)
                return false;

            // 对齐 ClassIsland：反序列化 settings
            object settings = null;
            var settingsType = info.SettingsType;
            if (settingsType != null)
            {
                settings = rule.Settings ?? Activator.CreateInstance(settingsType);
                if (settings is JToken jToken)
                {
                    try
                    {
                        settings = jToken.ToObject(settingsType);
                    }
                    catch
                    {
                        settings = Activator.CreateInstance(settingsType);
                    }
                }
            }

            try
            {
                return info.Handle(settings);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 注册规则处理程序。
        /// 对齐 ClassIsland 的 RegisterRuleHandler。
        /// </summary>
        public void RegisterRuleHandler(string id, RuleRegistryInfo.HandleDelegate handler)
        {
            if (!IRulesetService.Rules.TryGetValue(id, out var ruleRegistryInfo))
                throw new KeyNotFoundException($"找不到规则 {id}。");

            ruleRegistryInfo.Handle += handler;
        }

        /// <summary>
        /// 手动通知规则状态已更新，触发所有订阅者重新评估规则。
        /// </summary>
        public void NotifyStatusChanged()
        {
            StatusUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_monitor != null)
            {
                _monitor.ForegroundWindowChanged -= OnStatusMayHaveChanged;
                _monitor.ProcessChanged -= OnStatusMayHaveChanged;
                _monitor.InternalStateChanged -= OnStatusMayHaveChanged;
            }

            _fallbackTimer?.Stop();
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
        }
    }
}
