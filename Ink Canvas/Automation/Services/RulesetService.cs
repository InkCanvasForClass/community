using Ink_Canvas.WorkflowAutomation.Enums;
using Ink_Canvas.WorkflowAutomation.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Timers;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 规则集服务，负责评估规则集是否满足。
    /// 对齐 ClassIsland 的 RulesetService 实现，在评估时更新所有层级的 State。
    /// </summary>
    public class RulesetService : IDisposable
    {
        /// <summary>
        /// 规则状态更新事件，当规则条件可能发生变化时触发。
        /// </summary>
        public event EventHandler? StatusUpdated;

        private Timer? _pollingTimer;

        private int BoolToRuleObjectState(bool? v) => v switch
        {
            true => 2,
            false => 1,
            null => 0
        };

        public RulesetService()
        {
            // 启动定期轮询以检测状态变化
            _pollingTimer = new Timer(500);
            _pollingTimer.Elapsed += OnPollingTimerElapsed;
            _pollingTimer.AutoReset = true;
            _pollingTimer.Start();
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

            if (!AutomationRegistry.RegisteredRules.TryGetValue(rule.Id, out var info))
                return false;

            if (info.Handle == null)
                return false;

            // 对齐 ClassIsland：反序列化 settings
            object? settings = null;
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
        /// 手动通知规则状态已更新，触发所有订阅者重新评估规则。
        /// </summary>
        public void NotifyStatusChanged()
        {
            StatusUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnPollingTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // 定期通知状态已更改，让所有订阅者重新评估规则
            // 这样即使没有显式调用 NotifyStatusChanged 也能检测到变化
            NotifyStatusChanged();
        }

        public void Dispose()
        {
            _pollingTimer?.Stop();
            _pollingTimer?.Dispose();
            _pollingTimer = null;
        }
    }
}
