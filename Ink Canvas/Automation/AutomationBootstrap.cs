using Ink_Canvas.WorkflowAutomation.Actions;
using Ink_Canvas.WorkflowAutomation.Models;
using Ink_Canvas.WorkflowAutomation.Rules;
using Ink_Canvas.WorkflowAutomation.Services;
using Ink_Canvas.WorkflowAutomation.Triggers;
using System;
using System.IO;
using System.Linq;

namespace Ink_Canvas.WorkflowAutomation
{
    /// <summary>
    /// 自动化系统启动引导，负责注册所有内置触发器、行动和规则，并启动服务。
    /// </summary>
    public static class AutomationBootstrap
    {
        private static AutomationService? _service;

        /// <summary>
        /// 获取自动化服务实例
        /// </summary>
        public static AutomationService Service => _service ??= new AutomationService(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations"));

        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化自动化系统，注册所有内置组件。
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            // 注册触发器
            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.processdetected", "进程检测", "ApplicationOutline")
                {
                    TriggerType = typeof(ProcessDetectedTrigger),
                    SettingsType = typeof(ProcessDetectedSettings)
                },
                () => new ProcessDetectedTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.pptslideshow", "PPT放映检测", "Presentation")
                {
                    TriggerType = typeof(PptSlideShowTrigger),
                    SettingsType = typeof(PptSlideShowSettings)
                },
                () => new PptSlideShowTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.timer", "定时触发", "ClockOutline")
                {
                    TriggerType = typeof(TimerTrigger),
                    SettingsType = typeof(TimerTriggerSettings)
                },
                () => new TimerTrigger());

            // 新增触发器
            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.windowfocuschanged", "前台窗口变化", "Window")
                {
                    TriggerType = typeof(WindowFocusChangedTrigger),
                    SettingsType = typeof(WindowFocusChangedSettings)
                },
                () => new WindowFocusChangedTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.pptslideshowenter", "进入PPT放映", "Presentation")
                {
                    TriggerType = typeof(PptSlideShowEnterTrigger),
                    SettingsType = typeof(PptSlideShowEnterSettings)
                },
                () => new PptSlideShowEnterTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.pptslideshowexit", "退出PPT放映", "Presentation")
                {
                    TriggerType = typeof(PptSlideShowExitTrigger),
                    SettingsType = typeof(PptSlideShowExitSettings)
                },
                () => new PptSlideShowExitTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.annotationenter", "进入批注模式", "PenTool")
                {
                    TriggerType = typeof(AnnotationModeEnterTrigger),
                    SettingsType = typeof(AnnotationModeEnterSettings)
                },
                () => new AnnotationModeEnterTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.annotationexit", "退出批注模式", "PenTool")
                {
                    TriggerType = typeof(AnnotationModeExitTrigger),
                    SettingsType = typeof(AnnotationModeExitSettings)
                },
                () => new AnnotationModeExitTrigger());

            AutomationRegistry.RegisterTrigger(
                new TriggerInfo("inkcanvas.rulesetchanged", "规则集更新", "Refresh")
                {
                    TriggerType = typeof(RulesetChangedTrigger),
                    SettingsType = typeof(RulesetChangedSettings)
                },
                () => new RulesetChangedTrigger());

            // 注册行动
            AutomationRegistry.RegisterAction(FoldAction.Register());
            AutomationRegistry.RegisterAction(KillProcessAction.Register());
            AutomationRegistry.RegisterAction(SaveStrokesAction.Register());
            AutomationRegistry.RegisterAction(ToggleAnnotationModeAction.Register());
            AutomationRegistry.RegisterAction(ClearStrokesAction.Register());
            AutomationRegistry.RegisterAction(ShowNotificationAction.Register());
            AutomationRegistry.RegisterAction(ToggleTopmostAction.Register());

            // 注册规则
            AutomationRegistry.RegisterRule(ProcessRunningRule.Register());
            AutomationRegistry.RegisterRule(WindowTitleContainsRule.Register());
            AutomationRegistry.RegisterRule(IsAnnotationModeRule.Register());
            AutomationRegistry.RegisterRule(IsPptSlideshowRule.Register());
            AutomationRegistry.RegisterRule(ForegroundWindowProcessRule.Register());
            AutomationRegistry.RegisterRule(IsFloatingBarFoldedRule.Register());

            // 加载配置
            Service.RefreshConfigs();
            Service.LoadConfig();
        }

        /// <summary>
        /// 关闭自动化系统
        /// </summary>
        public static void Shutdown()
        {
            // 卸载所有工作流
            foreach (var workflow in Service.Workflows.ToList())
            {
                Service.UnloadWorkflow(workflow);
            }
        }
    }
}
