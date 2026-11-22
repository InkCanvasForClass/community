using Ink_Canvas.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private int _foldedModeVersion;

        internal void ApplyFoldedMode()
        {
            // 使旧布局失效，但保留折叠/展开互斥状态，直到进行中的操作结束。
            ++_foldedModeVersion;
            ++_sidePanelAnimVersion;
            CloseFloatingFoldMenu(animate: false);
            LeftSidePanel.BeginAnimation(MarginProperty, null);
            RightSidePanel.BeginAnimation(MarginProperty, null);
            LeftUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, null);
            RightUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, null);
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            bool useBall = Settings.Appearance.UseFloatingFoldBall;
            LeftUnFoldButtonQuickPanel.Margin = new Thickness(QuickPanelFoldedMargin, 0, 0,
                Settings.Appearance.QuickPanelBottomOffset);
            LeftSidePanel.Visibility = isFloatingBarFolded && !useBall ? Visibility.Visible : Visibility.Collapsed;
            LeftSidePanel.Margin = new Thickness(-10, 0, 0, Settings.Appearance.QuickPanelBottomOffset);
            RightSidePanel.Margin = new Thickness(0, 0, -10, Settings.Appearance.QuickPanelBottomOffset);
            FoldedBall.Visibility = isFloatingBarFolded && useBall ? Visibility.Visible : Visibility.Collapsed;
            FoldedBall.SetPosition(new Point(Settings.Appearance.FloatingFoldBallX, Settings.Appearance.FloatingFoldBallY));
            ApplyFloatingFoldBallOpacity();
        }

        internal void ApplyFloatingFoldBallOpacity()
        {
            FoldedBall.Opacity = Settings.Appearance.FloatingFoldBallOpacity;
            FoldedBallMenu.Opacity = FoldedBall.Opacity;
        }

        private bool AnimateFloatingFoldMenu =>
            SystemParameters.ClientAreaAnimation && !Settings.Appearance.DisableToolbarAnimation;

        private void FoldedBall_Click(object sender, RoutedEventArgs e)
        {
            if (!isFloatingBarFolded || isFloatingBarChangingHideMode) return;
            if (!Settings.Appearance.IsShowQuickPanel)
            {
                UnFoldFloatingBar_MouseUp(sender, null);
                return;
            }
            if (FoldedBallMenu.IsOpen)
            {
                CloseFloatingFoldMenu();
                return;
            }
            FoldedBallMenu.PopOpen(AnimateFloatingFoldMenu);
            UpdateFloatingFoldBallLayout();
            StartAutoCollapseQuickPanelTimer();
        }

        private void CloseFloatingFoldMenu(bool animate = true)
        {
            StopAutoCollapseQuickPanelTimer();
            if (FoldedBallMenu.Visibility != Visibility.Visible) return;
            if (animate && !FoldedBallMenu.IsOpen) return;
            bool menuHadFocus = FoldedBallMenu.IsKeyboardFocusWithin;
            FoldedBallMenu.Close(animate && AnimateFloatingFoldMenu,
                () => FoldedBall.ShowMenuState(false, default));
            if (menuHadFocus) FoldedBall.Focus();
        }

        private void FoldedBallMenu_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || !FoldedBallMenu.IsOpen) return;
            CloseFloatingFoldMenu();
            e.Handled = true;
        }

        private void FoldedBallMenu_ToolClick(object sender, RoutedEventArgs e)
        {
            if (!FoldedBallMenu.IsOpen || e.OriginalSource is not Button tool) return;

            CloseFloatingFoldMenu();
            e.Handled = true;
            switch (tool.Name)
            {
                case "RestoreToolbar": UnFoldFloatingBar_MouseUp(tool, null); break;
                case "SingleDraw": SymbolIconRandOne_MouseUp(tool, null); break;
                case "RandomDraw": SymbolIconRand_MouseUp(tool, null); break;
                case "Timer": ImageCountdownTimer_MouseUp(tool, null); break;
                case "Whiteboard": ImageBlackboard_MouseUp(tool, null); break;
                case "ExitShow": ExitPPTSlideShow_MouseUp(tool, null); break;
            }
        }

        private void FoldedBall_DragMoved(object sender, EventArgs e)
        {
            if (FoldedBallMenu.Visibility == Visibility.Visible) UpdateFloatingFoldBallLayout(moving: true);
        }

        private void FoldedBall_PositionChanged(object sender, EventArgs e)
        {
            Settings.Appearance.FloatingFoldBallX = FoldedBall.Margin.Left;
            Settings.Appearance.FloatingFoldBallY = FoldedBall.Margin.Top;
            SaveSettingsToFile();
            if (FoldedBallMenu.IsOpen) StartAutoCollapseQuickPanelTimer();
        }

        private void FoldedBallHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFloatingFoldBallLayout();

        private void UpdateFloatingFoldBallLayout(bool moving = false)
        {
            if (FoldedBall == null || !Settings.Appearance.UseFloatingFoldBall ||
                Main_Grid.ActualWidth <= 0 || Main_Grid.ActualHeight <= 0) return;
            FoldedBall.SetPosition(new Point(FoldedBall.Margin.Left, FoldedBall.Margin.Top));
            if (FoldedBallMenu.Visibility != Visibility.Visible) return;

            var anchor = new Point(FoldedBall.Margin.Left + FoldedBall.Width / 2,
                FoldedBall.Margin.Top + FoldedBall.Height / 2);
            var offset = FoldedBallMenu.Place(anchor, Main_Grid.RenderSize);
            if (moving)
            {
                // 保存圆心实际到达的位置，避免收起时小球跳到被圆环边界挡住的位置。
                FoldedBall.SetPosition(new Point(FoldedBall.Margin.Left + offset.X, FoldedBall.Margin.Top + offset.Y));
                offset = default;
            }
            FoldedBall.ShowMenuState(true, offset);
        }

        private bool IsPointInFloatingFoldMenu(Point windowPoint)
        {
            if (FoldedBallMenu?.IsVisible != true || !FoldedBallMenu.IsOpen) return false;
            var point = TranslatePoint(windowPoint, FoldedBallMenu);
            return FoldedBallMenu.InputHitTest(point) != null;
        }
    }
}
