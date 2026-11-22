using Ink_Canvas.Controls;
using System;
using System.Windows;

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
            StopAutoCollapseQuickPanelTimer();
            LeftSidePanel.BeginAnimation(MarginProperty, null);
            RightSidePanel.BeginAnimation(MarginProperty, null);
            LeftUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, null);
            RightUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, null);
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            bool useBall = Settings.Appearance.UseFloatingFoldBall;
            LeftUnFoldButtonQuickPanel.VerticalAlignment = useBall ? VerticalAlignment.Top : VerticalAlignment.Center;
            LeftUnFoldButtonQuickPanel.Margin = new Thickness(QuickPanelFoldedMargin, 0, 0,
                Settings.Appearance.QuickPanelBottomOffset);
            LeftSidePanel.Visibility = isFloatingBarFolded && !useBall ? Visibility.Visible : Visibility.Collapsed;
            LeftSidePanel.Margin = new Thickness(-10, 0, 0, Settings.Appearance.QuickPanelBottomOffset);
            RightSidePanel.Margin = new Thickness(0, 0, -10, Settings.Appearance.QuickPanelBottomOffset);
            FoldedBall.Visibility = isFloatingBarFolded && useBall ? Visibility.Visible : Visibility.Collapsed;
            FoldedBall.SetPosition(new Point(Settings.Appearance.FloatingFoldBallX, Settings.Appearance.FloatingFoldBallY));
        }

        private void FoldedBall_Click(object sender, RoutedEventArgs e)
        {
            if (!isFloatingBarFolded || isFloatingBarChangingHideMode) return;
            if (!Settings.Appearance.IsShowQuickPanel)
            {
                UnFoldFloatingBar_MouseUp(sender, null);
                return;
            }
            if (LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible)
            {
                HideQuickPanelAndResetMargin();
                return;
            }
            LeftUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, null);
            LeftUnFoldButtonQuickPanel.VerticalAlignment = VerticalAlignment.Top;
            LeftUnFoldButtonQuickPanel.Visibility = Visibility.Visible;
            UpdateFloatingFoldBallLayout();
            StartAutoCollapseQuickPanelTimer();
        }

        private void FoldedBall_PositionChanged(object sender, EventArgs e)
        {
            Settings.Appearance.FloatingFoldBallX = FoldedBall.Margin.Left;
            Settings.Appearance.FloatingFoldBallY = FoldedBall.Margin.Top;
            SaveSettingsToFile();
            UpdateFloatingFoldBallLayout();
        }

        private void FoldedBallHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFloatingFoldBallLayout();

        private void UpdateFloatingFoldBallLayout()
        {
            if (FoldedBall == null || !Settings.Appearance.UseFloatingFoldBall ||
                Main_Grid.ActualWidth <= 0 || Main_Grid.ActualHeight <= 0) return;
            FoldedBall.SetPosition(new Point(FoldedBall.Margin.Left, FoldedBall.Margin.Top));
            if (LeftUnFoldButtonQuickPanel.Visibility != Visibility.Visible) return;
            // 复用原快捷面板；优先放在球右侧，空间不足时放在左侧。
            LeftUnFoldButtonQuickPanel.Margin = new Thickness(0);
            LeftUnFoldButtonQuickPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = LeftUnFoldButtonQuickPanel.DesiredSize;
            double x = FoldedBall.Margin.Left + FoldedBall.Width + 8;
            if (x + size.Width > Main_Grid.ActualWidth) x = FoldedBall.Margin.Left - size.Width - 8;
            var point = FloatingFoldBall.ClampPosition(new Point(x, FoldedBall.Margin.Top), Main_Grid.RenderSize, size);
            LeftUnFoldButtonQuickPanel.Margin = new Thickness(point.X, point.Y, 0, 0);
        }
    }
}
