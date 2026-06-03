using System;
using System.Collections.Generic;
using System.Linq;
using Ink_Canvas.WorkflowAutomation.Enums;
using Ink_Canvas.WorkflowAutomation.Models;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 规则集服务，负责评估规则集是否满足。
    /// </summary>
    public class RulesetService
    {
        /// <summary>
        /// 判断指定的规则集是否成立。
        /// </summary>
        public bool IsRulesetSatisfied(Ruleset ruleset)
        {
            if (ruleset.Groups.Count == 0) return true;

            bool result;
            if (ruleset.Mode == RulesetLogicalMode.And)
            {
                result = ruleset.Groups.All(g => IsRuleGroupSatisfied(g));
            }
            else
            {
                result = ruleset.Groups.Any(g => IsRuleGroupSatisfied(g));
            }

            return ruleset.IsReversed ? !result : result;
        }

        /// <summary>
        /// 判断规则组是否成立。
        /// </summary>
        private bool IsRuleGroupSatisfied(RuleGroup group)
        {
            if (!group.IsEnabled) return true;
            if (group.Rules.Count == 0) return true;

            bool result;
            if (group.Mode == RulesetLogicalMode.And)
            {
                result = group.Rules.All(r => IsRuleSatisfied(r));
            }
            else
            {
                result = group.Rules.Any(r => IsRuleSatisfied(r));
            }

            return group.IsReversed ? !result : result;
        }

        /// <summary>
        /// 判断单条规则是否成立。
        /// </summary>
        private bool IsRuleSatisfied(Rule rule)
        {
            if (!AutomationRegistry.RegisteredRules.TryGetValue(rule.Id, out var info)) return false;
            if (info.Handle == null) return false;

            try
            {
                bool result = info.Handle(rule.Settings);
                return rule.IsReversed ? !result : result;
            }
            catch
            {
                return false;
            }
        }
    }
}
