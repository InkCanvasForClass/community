using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardTimerToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.timer";
        public override string LocalizationKey => "QuickPanel_Timer";
        public override string Description => "计时器";
        public override string IconGeometry => XamlGraphicsIconGeometries.TimerIconGeometry;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImageCountdownTimer_MouseUp(sender, e);
    }
}
