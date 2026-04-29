using Ink_Canvas.Plugins;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PluginSettingsPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private PluginInfo _currentPlugin;

        public PluginInfo CurrentPlugin
        {
            get { return _currentPlugin; }
            set
            {
                _currentPlugin = value;
                if (IsLoaded && _currentPlugin != null)
                {
                    LoadPluginSettings();
                }
            }
        }

        public PluginSettingsPage()
        {
            InitializeComponent();
            Loaded += PluginSettingsPage_Loaded;
        }

        private void PluginSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPluginSettings();
        }

        public void LoadPluginSettings()
        {
            if (_currentPlugin == null)
            {
                return;
            }

            try
            {
                var settingsView = _currentPlugin.Instance.GetSettingsView();
                if (settingsView != null && settingsView is UIElement uiElement)
                {
                    var parent = VisualTreeHelper.GetParent(uiElement);
                    if (parent != null)
                    {
                        if (parent is Decorator decorator)
                        {
                            decorator.Child = null;
                        }
                        else if (parent is ContentControl contentControl)
                        {
                            contentControl.Content = null;
                        }
                        else if (parent is Panel panel)
                        {
                            panel.Children.Remove(uiElement);
                        }
                    }

                    PluginSettingsContent.Content = uiElement;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("加载插件设置时出错: {0}", ex.Message));
            }
        }
    }
}
