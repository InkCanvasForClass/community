using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;
using Ink_Canvas.WorkflowAutomation.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Ink_Canvas.WorkflowAutomation.Extensions
{
    /// <summary>
    /// 注册触发器的 IServiceCollection 扩展。
    /// 对齐 ClassIsland 的 ActionRegistryExtensions / TriggerRegistryExtensions。
    /// </summary>
    public static class AutomationRegistryExtensions
    {
        /// <summary>
        /// 注册触发器（从 [TriggerInfo] 特性自动读取元数据）
        /// </summary>
        public static IServiceCollection AddTrigger<TTrigger>(this IServiceCollection services)
            where TTrigger : TriggerBase
        {
            var triggerType = typeof(TTrigger);
            var attr = (TriggerInfoAttribute)triggerType
                .GetCustomAttributes(typeof(TriggerInfoAttribute), false)
                .FirstOrDefault();

            if (attr == null)
                throw new InvalidOperationException($"触发器类型 {triggerType.Name} 未标注 [TriggerInfo] 特性。");

            // 自动推断 SettingsType
            var baseType = triggerType.BaseType;
            Type settingsType = null;
            if (baseType?.IsGenericType == true && baseType.GetGenericTypeDefinition() == typeof(TriggerBase<>))
            {
                settingsType = baseType.GetGenericArguments().First();
            }

            var info = new TriggerInfo(attr.Id, attr.Name, attr.IconKind)
            {
                TriggerType = triggerType,
                SettingsType = settingsType,
                SettingsControlType = attr.SettingsControlType
            };

            AutomationRegistry.RegisterTrigger(info);

            // 注册触发器类型到 DI（Transient，每次 Resolve 创建新实例）
            services.AddTransient(triggerType);

            return services;
        }

        /// <summary>
        /// 注册带设置控件的触发器
        /// </summary>
        public static IServiceCollection AddTrigger<TTrigger, TSettingsControl>(this IServiceCollection services)
            where TTrigger : TriggerBase
            where TSettingsControl : TriggerSettingsControlBase
        {
            var triggerType = typeof(TTrigger);
            var attr = (TriggerInfoAttribute)triggerType
                .GetCustomAttributes(typeof(TriggerInfoAttribute), false)
                .FirstOrDefault();

            if (attr == null)
                throw new InvalidOperationException($"触发器类型 {triggerType.Name} 未标注 [TriggerInfo] 特性。");

            attr.SettingsControlType = typeof(TSettingsControl);

            var baseType = triggerType.BaseType;
            Type settingsType = null;
            if (baseType?.IsGenericType == true && baseType.GetGenericTypeDefinition() == typeof(TriggerBase<>))
            {
                settingsType = baseType.GetGenericArguments().First();
            }

            var info = new TriggerInfo(attr.Id, attr.Name, attr.IconKind)
            {
                TriggerType = triggerType,
                SettingsType = settingsType,
                SettingsControlType = typeof(TSettingsControl)
            };

            AutomationRegistry.RegisterTrigger(info);
            services.AddTransient(triggerType);
            services.AddTransient(typeof(TriggerSettingsControlBase), typeof(TSettingsControl));

            return services;
        }

        /// <summary>
        /// 注册无设置行动
        /// </summary>
        public static IServiceCollection AddAction(this IServiceCollection services,
            string id, string name = "", string iconKind = "BacteriaOutline",
            ActionRegistryInfo.HandleDelegate onHandle = null)
        {
            var info = new ActionRegistryInfo(id, name, iconKind);
            info.Handle += onHandle;
            IActionService.Actions[id] = info;
            return services;
        }

        /// <summary>
        /// 注册带设置的行动
        /// </summary>
        public static IServiceCollection AddAction<TSettings>(this IServiceCollection services,
            string id, string name = "", string iconKind = "BacteriaOutline",
            ActionRegistryInfo.HandleDelegate onHandle = null)
        {
            var info = new ActionRegistryInfo(id, name, iconKind);
            info.SettingsType = typeof(TSettings);
            info.Handle += onHandle;
            IActionService.Actions[id] = info;
            return services;
        }

        /// <summary>
        /// 注册带设置和设置控件的行动
        /// </summary>
        public static IServiceCollection AddAction<TSettings, TSettingsControl>(this IServiceCollection services,
            string id, string name = "", string iconKind = "BacteriaOutline",
            ActionRegistryInfo.HandleDelegate onHandle = null)
            where TSettingsControl : ActionSettingsControlBase
        {
            var info = new ActionRegistryInfo(id, name, iconKind);
            info.SettingsType = typeof(TSettings);
            info.SettingsControlType = typeof(TSettingsControl);
            info.Handle += onHandle;
            IActionService.Actions[id] = info;
            services.AddTransient(typeof(ActionSettingsControlBase), typeof(TSettingsControl));
            return services;
        }

        /// <summary>
        /// 注册无设置规则
        /// </summary>
        public static IServiceCollection AddRule(this IServiceCollection services,
            string id, string name = "", string iconKind = "CogOutline",
            RuleRegistryInfo.HandleDelegate onHandle = null)
        {
            var info = new RuleRegistryInfo(id, name, iconKind);
            info.Handle += onHandle;
            IRulesetService.Rules[id] = info;
            return services;
        }

        /// <summary>
        /// 注册带设置的规则
        /// </summary>
        public static IServiceCollection AddRule<TSettings>(this IServiceCollection services,
            string id, string name = "", string iconKind = "CogOutline",
            RuleRegistryInfo.HandleDelegate onHandle = null)
        {
            var info = new RuleRegistryInfo(id, name, iconKind);
            info.SettingsType = typeof(TSettings);
            info.Handle += onHandle;
            IRulesetService.Rules[id] = info;
            return services;
        }

        /// <summary>
        /// 注册带设置和设置控件的规则
        /// </summary>
        public static IServiceCollection AddRule<TSettings, TSettingsControl>(this IServiceCollection services,
            string id, string name = "", string iconKind = "CogOutline",
            RuleRegistryInfo.HandleDelegate onHandle = null)
            where TSettingsControl : RuleSettingsControlBase
        {
            var info = new RuleRegistryInfo(id, name, iconKind);
            info.SettingsType = typeof(TSettings);
            info.SettingsControlType = typeof(TSettingsControl);
            info.Handle += onHandle;
            IRulesetService.Rules[id] = info;
            services.AddTransient(typeof(RuleSettingsControlBase), typeof(TSettingsControl));
            return services;
        }
    }
}
