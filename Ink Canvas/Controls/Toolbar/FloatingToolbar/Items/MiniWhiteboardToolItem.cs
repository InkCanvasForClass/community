using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    /// <summary>
    /// 小白板浮动工具栏组件
    /// 提供一个按钮用于打开/关闭浮窗小白板
    /// 用户可通过工具栏配置添加或移除此组件
    /// </summary>
    internal sealed class MiniWhiteboardToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.miniWhiteboard";
        public override string LocalizationKey => "FloatingBar_MiniWhiteboard";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.FloatingBar_MiniWhiteboard;

        // 使用与浮动栏白板按钮相同的图标几何
        public override string IconGeometry => XamlGraphicsIconGeometries.WhiteboardFloatingBarBtnIcon;

        // 小白板设置是全局设置（SettingsManager.Settings.MiniWhiteboard），非 per-component 设置，
        // 因此使用 CustomSettingsPanelFactory 提供完全自定义的设置面板，而非通过 CustomSettings 声明式生成。
        public override Func<FrameworkElement> CustomSettingsPanelFactory => BuildMiniWhiteboardSettingsPanel;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ToggleMiniWhiteboard();

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachMiniWhiteboardBtn(view);

        private FrameworkElement BuildMiniWhiteboardSettingsPanel()
        {
            var settings = SettingsManager.Settings.MiniWhiteboard ??= new MiniWhiteboardSettings();

            // 通过 Pack URI 直接加载资源词典（自包含，不依赖 Page.Resources 合并）
            var dict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/InkCanvasForClass;component/Controls/Toolbar/FloatingToolbar/Items/MiniWhiteboardSettingsPanel.xaml", UriKind.Absolute)
            };
            var template = dict["MiniWhiteboardSettingsPanelTemplate"] as DataTemplate;
            if (template == null) return new StackPanel();
            var panel = (StackPanel)template.LoadContent();

            // 通过名称查找各控件
            var enableCard = (Ink_Canvas.Controls.LabeledSettingsCard)panel.FindName("EnableCard");
            var syncPptCard = (Ink_Canvas.Controls.LabeledSettingsCard)panel.FindName("SyncPptCard");
            var sizeText = (TextBlock)panel.FindName("SizeText");
            var widthSlider = (Slider)panel.FindName("WidthSlider");
            var heightSlider = (Slider)panel.FindName("HeightSlider");
            var opacityText = (TextBlock)panel.FindName("OpacityText");
            var opacitySlider = (Slider)panel.FindName("OpacitySlider");

            // 初始化控件状态
            enableCard.IsOn = settings.IsEnabled;
            syncPptCard.IsOn = settings.SyncWithPPTPages;
            widthSlider.Value = settings.DefaultWidth;
            heightSlider.Value = settings.DefaultHeight;
            opacitySlider.Value = settings.DefaultOpacity;

            UpdateSizeText();
            UpdateOpacityText();

            // 绑定事件
            enableCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.MiniWhiteboard.IsEnabled = enableCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };
            syncPptCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.MiniWhiteboard.SyncWithPPTPages = syncPptCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };
            widthSlider.ValueChanged += (s, e) =>
            {
                UpdateSizeText();
                SettingsManager.Settings.MiniWhiteboard.DefaultWidth = widthSlider.Value;
                SettingsManager.SaveSettingsToFile();
            };
            heightSlider.ValueChanged += (s, e) =>
            {
                UpdateSizeText();
                SettingsManager.Settings.MiniWhiteboard.DefaultHeight = heightSlider.Value;
                SettingsManager.SaveSettingsToFile();
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                UpdateOpacityText();
                SettingsManager.Settings.MiniWhiteboard.DefaultOpacity = opacitySlider.Value;
                SettingsManager.SaveSettingsToFile();
            };

            return panel;

            // 局部函数：更新尺寸文本
            void UpdateSizeText()
            {
                sizeText.Text = $"{(int)widthSlider.Value} × {(int)heightSlider.Value}";
            }

            // 局部函数：更新透明度文本
            void UpdateOpacityText()
            {
                opacityText.Text = $"{Math.Round(opacitySlider.Value * 100):0}%";
            }
        }
    }
}
