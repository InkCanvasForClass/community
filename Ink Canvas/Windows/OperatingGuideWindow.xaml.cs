﻿using System.Windows;
using System.Windows.Input;
using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for StopwatchWindow.xaml
    /// </summary>
    public partial class OperatingGuideWindow : Window
    {
        public OperatingGuideWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void WindowDragMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnFullscreen_MouseUp(object sender, MouseButtonEventArgs e) {
            if (WindowState == WindowState.Normal) {
                WindowState = WindowState.Maximized;
                SymbolIconFullscreen.Symbol = Symbol.BackToWindow;
            } else {
                WindowState = WindowState.Normal;
                SymbolIconFullscreen.Symbol = Symbol.FullScreen;
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e) {
            e.Handled = true;
        }
    }
}