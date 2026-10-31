using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Controls;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.Windows.SettingsViews.Pages;
using Newtonsoft.Json;

internal static class WordNavigationRegression
{
    internal static void Run(Action<bool, string> check)
    {
        // Use the same theme dictionaries as the application so SettingsCard's
        // responsive layout and ToggleSwitch bindings run in a real resource scope.
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(
            "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:ui=\"http://schemas.inkore.net/lib/ui/wpf/modern\"><ResourceDictionary.MergedDictionaries><ui:ThemeResources/><ui:XamlControlsResources/></ResourceDictionary.MergedDictionaries></ResourceDictionary>"));
        var old = JsonConvert.DeserializeObject<WordLinkSettings>("{\"enabled\":true,\"showNavigation\":false}");
        check(old.Enabled && !old.ShowNavigation && old.UsePptAppearance && old.ShowZoom && old.FollowWordWindow,
            "Existing Word preferences lost their defaults during migration");
        var invalid = JsonConvert.DeserializeObject<WordLinkSettings>("{\"navigationScale\":-3,\"navigationOpacity\":8,\"shadowSize\":99,\"horizontalInset\":-5,\"bottomInset\":999}");
        check(invalid.NavigationScale == .5 && invalid.NavigationOpacity == 1 && invalid.ShadowSize == 60
            && invalid.HorizontalInset == 0 && invalid.BottomInset == 200, "Invalid appearance settings were not bounded during reload");

        var settings = SettingsManager.Settings.WordLink;
        var ppt = SettingsManager.Settings.PowerPointSettings;
        ppt.PPTLBUseGlobalSettings = false; ppt.PPTLBBlackBackground = true;
        ppt.PPTLBButtonScale = 1.25; ppt.PPTLBButtonOpacity = .65; ppt.PPTLBShowPageNumber = false;
        ppt.PPTLBShadowEnabled = false;
        var word = new WordLectureBar();
        Invoke(word, "SetState", "Ready", "Fixture.docx", 4, 10, false, 125);
        Invoke(word, "ApplySettings", settings, ppt, false);
        var navigation = word.FindName("PageNavigation") as PPTNavBar;
        check(navigation != null && navigation.CurrentSlide == 4 && navigation.TotalSlides == 10,
            "Word did not reuse the production PPTNavBar for its page navigation");
        var reference = new PPTNavBar(); reference.ApplyTheme(true); reference.SetBarScale(1.25);
        reference.SetBarOpacity(.65); reference.SetShadow(false, 16); reference.SetPageButtonVisibility(Visibility.Collapsed);
        check(((SolidColorBrush)((Border)navigation.FindName("RootBorder")).Background).Color
            == ((SolidColorBrush)((Border)reference.FindName("RootBorder")).Background).Color
            && ((ScaleTransform)navigation.FindName("NavBarScaleTransform")).ScaleX == 1.25
            && ((Border)navigation.FindName("RootBorder")).Opacity == .65
            && ((Border)navigation.FindName("RootBorder")).Effect == null
            && ((Border)navigation.FindName("PageButtonBorder")).Visibility == Visibility.Collapsed,
            "Word's inherited appearance differed from the equivalent PPT control");
        check(!word.ShowZoom, "Left navigation displayed document zoom");
        ppt.PPTLBShadowEnabled = true; ppt.PPTLBShadowSize = 48;
        Invoke(word, "ApplySettings", settings, ppt, false);
        check(((System.Windows.Media.Effects.DropShadowEffect)((Border)navigation.FindName("RootBorder")).Effect).BlurRadius == 48,
            "Inherited PPT shadow was truncated to a different appearance");
        settings.UsePptAppearance = false; settings.NavigationScale = .75; settings.NavigationOpacity = .9;
        settings.NavigationShadow = true; settings.ShadowSize = 8;
        Invoke(word, "ApplySettings", settings, ppt, true);
        check(word.ShowZoom && ((ScaleTransform)navigation.FindName("NavBarScaleTransform")).ScaleX == .75
            && ((Border)navigation.FindName("RootBorder")).Opacity == .9
            && ((System.Windows.Media.Effects.DropShadowEffect)((Border)navigation.FindName("RootBorder")).Effect).BlurRadius == 8,
            "Independent Word appearance or right-side zoom was not applied");
        var zoomControls = (StackPanel)word.FindName("ZoomControls");
        zoomControls.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        check(zoomControls.DesiredSize.Width <= 112 && ((Button)word.FindName("ZoomInButton")).Width == 34,
            "Document zoom controls are no longer compact");
        check(word.FindName("HistoryButton") == null, "The removed history button is still present");

        var page = new WordPage(); page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        page.Measure(new Size(1100, 900)); page.Arrange(new Rect(0, 0, 1100, 900));
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var previewRight = (WordLectureBar)page.FindName("PreviewRight");
        DependencyObject ancestor = (DependencyObject)previewRight.FindName("ZoomControls");
        var previewNavigation = previewRight.FindName("PageNavigation");
        while (ancestor != null && ancestor != previewNavigation) ancestor = VisualTreeHelper.GetParent(ancestor);
        check(ancestor == previewNavigation, "Document zoom did not share the PPT navigation's scale, surface and shadow");
        var scale = (Slider)page.FindName("NavScale"); scale.Value = 1.1;
        ((Slider)page.FindName("HorizontalInset")).Value = 40;
        ((Slider)page.FindName("BottomInset")).Value = 30;
        ((Slider)page.FindName("FocusToolbarOpacity")).Value = .35;
        ((LabeledSettingsCard)page.FindName("AutoEnterFocusMode")).ToggleSwitchControl.IsOn = true;
        var zoomSwitch = (LabeledSettingsCard)page.FindName("ShowZoom");
        zoomSwitch.ToggleSwitchControl.IsOn = false;
        page.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        check(settings.NavigationScale == 1.1 && settings.HorizontalInset == 40 && settings.BottomInset == 30 && !settings.ShowZoom,
            "The settings page did not update the persisted navigation model");
        check(!((WordLectureBar)page.FindName("PreviewRight")).ShowZoom,
            "Settings preview did not reflect the right-side zoom switch");
        check(settings.AutoEnterFocusMode && settings.FocusToolbarOpacity == .35,
            "Focus options did not update from the settings page");
        var exit = new Border { Tag = "ToolbarRegistryInjected" };
        var other = new Border { Tag = "ToolbarRegistryInjected" };
        Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRegistry.SetHidingRuleset(exit, Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRuleset.PPTOnly());
        Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRegistry.SetHidingRuleset(other, Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRuleset.PPTOnly());
        var panel = new StackPanel(); panel.Children.Add(exit); panel.Children.Add(other);
        Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRegistry.UpdateVisibilityByMode(panel, false, false, true, exit);
        check(exit.Visibility == Visibility.Visible && other.Visibility == Visibility.Collapsed,
            "Word Focus failed to show Exit independently of unrelated PPT-only controls");
        Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRegistry.UpdateVisibilityByMode(panel, false, false);
        check(exit.Visibility == Visibility.Collapsed, "Exit remained visible after leaving presentation modes");
        Ink_Canvas.Controls.Toolbar.FloatingToolbar.ToolbarRegistry.UpdateVisibilityByMode(panel, false, true);
        check(exit.Visibility == Visibility.Visible && other.Visibility == Visibility.Visible, "Word changes broke PPT toolbar visibility");
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Console.WriteLine("Word navigation: shared PPT control, inherited/custom appearance, settings page and preview verified");
    }
    private static void Invoke(object instance, string method, params object[] args)
        => instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
}
