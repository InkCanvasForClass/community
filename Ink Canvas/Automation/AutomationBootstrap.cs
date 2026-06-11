using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.ActionHandlers;
using Ink_Canvas.WorkflowAutomation.Actions;
using Ink_Canvas.WorkflowAutomation.Extensions;
using Ink_Canvas.WorkflowAutomation.Rules;
using Ink_Canvas.WorkflowAutomation.Services;
using Ink_Canvas.WorkflowAutomation.Triggers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;

namespace Ink_Canvas.WorkflowAutomation
{
    /// <summary>
    /// 自动化系统启动引导。
    /// 对齐 ClassIsland 的 App.xaml.cs 注册模式，使用 DI 容器注册所有组件。
    /// </summary>
    public static class AutomationBootstrap
    {
        private static AutomationService _service;
        private static SystemEventMonitor _monitor;
        private static IServiceProvider _serviceProvider;
        private static ActionService _actionService;
        private static RulesetService _rulesetService;

        /// <summary>
        /// 获取自动化服务实例
        /// </summary>
        public static AutomationService Service => _service ??= new AutomationService(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations"),
            _serviceProvider);

        /// <summary>
        /// 获取系统事件监控器实例
        /// </summary>
        public static SystemEventMonitor Monitor => _monitor;

        /// <summary>
        /// 获取 DI 服务提供者
        /// </summary>
        public static IServiceProvider ServiceProvider => _serviceProvider;

        /// <summary>
        /// 获取行动服务实例
        /// </summary>
        public static IActionService ActionService => _actionService;

        /// <summary>
        /// 获取规则集服务实例
        /// </summary>
        public static IRulesetService RulesetService => _rulesetService;

        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化自动化系统。
        /// 对齐 ClassIsland：通过 IServiceCollection 注册所有触发器、行动和规则，
        /// 然后通过 DI 容器解析。
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // 1. 创建 DI 容器
            var services = new ServiceCollection();

            // 2. 注册核心服务
            services.AddSingleton<SystemEventMonitor>();
            services.AddSingleton<IActionService, ActionService>();
            services.AddSingleton<IRulesetService, RulesetService>();

            // 3. 注册触发器（对齐 ClassIsland 的 AddTrigger<T>()）
            services.AddTrigger<ProcessDetectedTrigger>();
            services.AddTrigger<PptSlideShowTrigger>();
            services.AddTrigger<TimerTrigger>();
            services.AddTrigger<WindowFocusChangedTrigger>();
            services.AddTrigger<PptSlideShowEnterTrigger>();
            services.AddTrigger<PptSlideShowExitTrigger>();
            services.AddTrigger<AnnotationModeEnterTrigger>();
            services.AddTrigger<AnnotationModeExitTrigger>();
            services.AddTrigger<RulesetChangedTrigger>();

            // 4. 注册行动（对齐 ClassIsland 的 AddAction<TSettings>()）
            services.AddAction<FoldActionSettings>("inkcanvas.fold", "折叠/展开工具栏", "DockBottom");
            services.AddAction<KillProcessActionSettings>("inkcanvas.killprocess", "结束进程", "CloseOutline");
            services.AddAction<SaveStrokesActionSettings>("inkcanvas.savestrokes", "保存笔画", "ContentSaveOutline");
            services.AddAction<ToggleAnnotationModeActionSettings>("inkcanvas.toggleannotationmode", "切换批注模式", "PenTool");
            services.AddAction<ClearStrokesActionSettings>("inkcanvas.clearstrokes", "清空笔画", "Eraser");
            services.AddAction<ShowNotificationActionSettings>("inkcanvas.shownotification", "显示通知", "BellOutline");
            services.AddAction<ToggleTopmostActionSettings>("inkcanvas.toggletopmost", "切换窗口置顶", "PinOutline");
            services.AddAction<ResetDesktopPositionActionSettings>("inkcanvas.resetdesktopposition", "重置桌面模式位置", "DockBottom");
            services.AddAction<ResetPptPositionActionSettings>("inkcanvas.resetpptposition", "重置PPT模式位置", "Presentation");

            // 5. 注册规则（对齐 ClassIsland 的 AddRule<TSettings>()）
            services.AddRule<ProcessRunningRuleSettings>("inkcanvas.processrunning", "进程正在运行", "ApplicationCogOutline");
            services.AddRule<WindowTitleContainsRuleSettings>("inkcanvas.windowtitlecontains", "窗口标题包含", "FormatTitle");
            services.AddRule<IsAnnotationModeRuleSettings>("inkcanvas.isannotationmode", "批注模式", "PenTool");
            services.AddRule<IsPptSlideshowRuleSettings>("inkcanvas.ispptslideshow", "PPT放映中", "Presentation");
            services.AddRule<ForegroundWindowProcessRuleSettings>("inkcanvas.foregroundwindowprocess", "前台窗口进程名", "Window");
            services.AddRule<IsFloatingBarFoldedRuleSettings>("inkcanvas.isfloatingbarfolded", "工具栏已折叠", "DockBottom");

            // 6. 注册行动处理器（对齐 ClassIsland 的 IHostedService 模式）
            services.AddTransient<FoldActionHandler>();
            services.AddTransient<KillProcessActionHandler>();
            services.AddTransient<SaveStrokesActionHandler>();
            services.AddTransient<ToggleAnnotationModeActionHandler>();
            services.AddTransient<ClearStrokesActionHandler>();
            services.AddTransient<ShowNotificationActionHandler>();
            services.AddTransient<ToggleTopmostActionHandler>();
            services.AddTransient<ResetDesktopPositionActionHandler>();
            services.AddTransient<ResetPptPositionActionHandler>();

            // 7. 构建容器
            _serviceProvider = services.BuildServiceProvider();

            // 8. 初始化核心服务
            _monitor = _serviceProvider.GetRequiredService<SystemEventMonitor>();
            _monitor.Start();

            _actionService = (ActionService)_serviceProvider.GetRequiredService<IActionService>();
            _rulesetService = (RulesetService)_serviceProvider.GetRequiredService<IRulesetService>();

            // 9. 初始化行动处理器（注册 Handle/RevertHandle 委托）
            // 对齐 ClassIsland：ActionHandler 在构造时通过 IActionService 注册处理程序
            _serviceProvider.GetRequiredService<FoldActionHandler>();
            _serviceProvider.GetRequiredService<KillProcessActionHandler>();
            _serviceProvider.GetRequiredService<SaveStrokesActionHandler>();
            _serviceProvider.GetRequiredService<ToggleAnnotationModeActionHandler>();
            _serviceProvider.GetRequiredService<ClearStrokesActionHandler>();
            _serviceProvider.GetRequiredService<ShowNotificationActionHandler>();
            _serviceProvider.GetRequiredService<ToggleTopmostActionHandler>();
            _serviceProvider.GetRequiredService<ResetDesktopPositionActionHandler>();
            _serviceProvider.GetRequiredService<ResetPptPositionActionHandler>();

            // 10. 注册规则处理程序（对齐 ClassIsland 的 RegisterRuleHandler）
            RegisterRuleHandlers();

            // 11. 加载配置
            Service.RefreshConfigs();
            Service.LoadConfig();
        }

        /// <summary>
        /// 注册规则处理程序。
        /// 对齐 ClassIsland：规则处理程序通过 IRulesetService.RegisterRuleHandler 注册。
        /// </summary>
        private static void RegisterRuleHandlers()
        {
            _rulesetService.RegisterRuleHandler("inkcanvas.processrunning", ProcessRunningRule.Evaluate);
            _rulesetService.RegisterRuleHandler("inkcanvas.windowtitlecontains", WindowTitleContainsRule.Evaluate);
            _rulesetService.RegisterRuleHandler("inkcanvas.isannotationmode", IsAnnotationModeRule.Evaluate);
            _rulesetService.RegisterRuleHandler("inkcanvas.ispptslideshow", IsPptSlideshowRule.Evaluate);
            _rulesetService.RegisterRuleHandler("inkcanvas.foregroundwindowprocess", ForegroundWindowProcessRule.Evaluate);
            _rulesetService.RegisterRuleHandler("inkcanvas.isfloatingbarfolded", IsFloatingBarFoldedRule.Evaluate);
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

            // 释放规则集服务
            (_rulesetService as IDisposable)?.Dispose();

            // 释放系统事件监控器
            _monitor?.Dispose();
            _monitor = null;

            // 释放 DI 容器
            (_serviceProvider as IDisposable)?.Dispose();
            _serviceProvider = null;
        }
    }
}
