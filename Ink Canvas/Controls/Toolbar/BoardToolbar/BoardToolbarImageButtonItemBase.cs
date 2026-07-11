using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    internal abstract class BoardToolbarImageButtonItemBase : IBoardToolbarItem
    {
        public abstract string Id { get; }
        public abstract string LocalizationKey { get; }
        public virtual ButtonPosition DefaultPosition => ButtonPosition.Middle;
        public virtual string Description => "";

        public string DisplayName => Strings.GetString(LocalizationKey) ?? LocalizationKey;

        public virtual string IconGeometry => null;
        public virtual FontIconData? IconKey => null;

        protected abstract void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e);

        protected virtual void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view) { }

        public FrameworkElement BuildView(IBoardToolbarHost host)
        {
            var btn = new BoardToolbarButton
            {
                Label = Strings.GetString(LocalizationKey) ?? LocalizationKey,
                Position = DefaultPosition
            };

            if (!string.IsNullOrEmpty(IconGeometry))
                btn.IconGeometry = IconGeometry;

            btn.ButtonMouseUp += (s, e) => OnClick(host, s, e);
            AfterBuild(host, btn);
            return btn;
        }

        public void ApplyPosition(FrameworkElement view, ButtonPosition position)
        {
            if (view is BoardToolbarButton btn)
            {
                btn.Position = position;
            }
        }
    }
}
