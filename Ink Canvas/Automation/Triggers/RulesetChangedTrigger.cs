using System;
using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Services;

namespace Ink_Canvas.WorkflowAutomation.Triggers
{
    /// <summary>
    /// 规则集更新触发器设置
    /// </summary>
    public class RulesetChangedSettings
    {
    }

    /// <summary>
    /// 规则集状态更新时触发的触发器。
    /// 当任何规则条件可能发生变化时触发。
    /// </summary>
    public class RulesetChangedTrigger : TriggerBase<RulesetChangedSettings>
    {
        public override void Loaded()
        {
            AutomationBootstrap.Service.GetRulesetService().StatusUpdated += StatusUpdatedHandler;
        }

        public override void UnLoaded()
        {
            AutomationBootstrap.Service.GetRulesetService().StatusUpdated -= StatusUpdatedHandler;
        }

        private void StatusUpdatedHandler(object? sender, EventArgs e)
        {
            Trigger();
        }
    }
}
