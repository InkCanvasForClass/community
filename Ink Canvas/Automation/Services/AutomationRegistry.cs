using Ink_Canvas.WorkflowAutomation.Models;
using System;
using System.Collections.Generic;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 自动化注册中心，管理所有已注册的触发器、行动和规则。
    /// </summary>
    public static class AutomationRegistry
    {
        /// <summary>
        /// 已注册的触发器列表
        /// </summary>
        public static List<TriggerInfo> RegisteredTriggers { get; } = new();

        /// <summary>
        /// 已注册的行动字典
        /// </summary>
        public static Dictionary<string, ActionRegistryInfo> RegisteredActions { get; } = new();

        /// <summary>
        /// 已注册的规则字典
        /// </summary>
        public static Dictionary<string, RuleRegistryInfo> RegisteredRules { get; } = new();

        /// <summary>
        /// 触发器工厂字典（替代 Keyed Service）
        /// </summary>
        private static readonly Dictionary<string, Func<Abstractions.TriggerBase>> _triggerFactories = new();

        /// <summary>
        /// 注册触发器
        /// </summary>
        public static void RegisterTrigger(TriggerInfo info, Func<Abstractions.TriggerBase> factory)
        {
            RegisteredTriggers.Add(info);
            _triggerFactories[info.Id] = factory;
        }

        /// <summary>
        /// 注册行动
        /// </summary>
        public static void RegisterAction(ActionRegistryInfo info)
        {
            RegisteredActions[info.Id] = info;
        }

        /// <summary>
        /// 注册规则
        /// </summary>
        public static void RegisterRule(RuleRegistryInfo info)
        {
            RegisteredRules[info.Id] = info;
        }

        /// <summary>
        /// 解析触发器实例
        /// </summary>
        public static Abstractions.TriggerBase? ResolveTrigger(string id)
        {
            return _triggerFactories.TryGetValue(id, out var factory) ? factory() : null;
        }
    }
}
