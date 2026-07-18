using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

            var panel = new StackPanel();

            // 启用开关
            var enableCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = FloatingBarStrings.MiniWhiteboard_Settings_Enable,
                Icon = SegoeFluentIcons.Edit,
                IsOn = settings.IsEnabled
            };
            enableCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.MiniWhiteboard.IsEnabled = enableCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };
            panel.Children.Add(enableCard);

            // 同步 PPT 开关
            var syncPptCard = new Ink_Canvas.Controls.LabeledSettingsCard
            {
                Header = FloatingBarStrings.MiniWhiteboard_Settings_SyncPPT,
                Icon = SegoeFluentIcons.Slideshow,
                IsOn = settings.SyncWithPPTPages
            };
            syncPptCard.Toggled += (s, e) =>
            {
                SettingsManager.Settings.MiniWhiteboard.SyncWithPPTPages = syncPptCard.IsOn;
                SettingsManager.SaveSettingsToFile();
            };
            panel.Children.Add(syncPptCard);

            // 默认尺寸
            var sizeText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
            var widthSlider = new Slider { Minimum = 200, Maximum = 1200, Width = 200, IsSnapToTickEnabled = true, TickFrequency = 10, TickPlacement = TickPlacement.None, Value = settings.DefaultWidth };
            var heightSlider = new Slider { Minimum = 150, Maximum = 900, Width = 200, IsSnapToTickEnabled = true, TickFrequency = 10, TickPlacement = TickPlacement.None, Value = settings.DefaultHeight };

            UpdateSizeText();

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

            var sizeContent = new StackPanel();
            sizeContent.Children.Add(sizeText);
            var widthRow = new StackPanel { Orientation = Orientation.Horizontal };
            widthRow.Children.Add(new TextBlock { Text = "W", Width = 16, VerticalAlignment = VerticalAlignment.Center });
            widthRow.Children.Add(widthSlider);
            sizeContent.Children.Add(widthRow);
            var heightRow = new StackPanel { Orientation = Orientation.Horizontal };
            heightRow.Children.Add(new TextBlock { Text = "H", Width = 16, VerticalAlignment = VerticalAlignment.Center });
            heightRow.Children.Add(heightSlider);
            sizeContent.Children.Add(heightRow);

            var sizeCard = new iNKORE.UI.WPF.Modern.Controls.SettingsCard
            {
                Header = FloatingBarStrings.MiniWhiteboard_Settings_DefaultSize,
                Content = sizeContent
            };
            panel.Children.Add(sizeCard);

            // 透明度
            var opacityText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextAlignment = TextAlignment.Right };
            var opacitySlider = new Slider { Minimum = 0.3, Maximum = 1, Width = 200, IsSnapToTickEnabled = true, TickFrequency = 0.05, TickPlacement = TickPlacement.None, Value = settings.DefaultOpacity };

            UpdateOpacityText();

            opacitySlider.ValueChanged += (s, e) =>
            {
                UpdateOpacityText();
                SettingsManager.Settings.MiniWhiteboard.DefaultOpacity = opacitySlider.Value;
                SettingsManager.SaveSettingsToFile();
            };

            var opacityRow = new StackPanel { Orientation = Orientation.Horizontal };
            opacityRow.Children.Add(opacityText);
            opacityRow.Children.Add(opacitySlider);

            var opacityCard = new iNKORE.UI.WPF.Modern.Controls.SettingsCard
            {
                Header = FloatingBarStrings.MiniWhiteboard_Settings_Opacity,
                Content = opacityRow
            };
            panel.Children.Add(opacityCard);

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
