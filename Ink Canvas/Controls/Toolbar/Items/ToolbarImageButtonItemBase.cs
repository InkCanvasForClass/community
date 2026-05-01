using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    /// <summary>
    /// 通用 ToolbarImageButton 工具栏条目基类——大幅减少每个按钮的样板代码。
    /// 派生类通常只需给 Id / 本地化键 / Slot / Order / 点击处理 / Attach 回填。
    /// </summary>
    internal abstract class ToolbarImageButtonItemBase : IToolbarItem
    {
        public abstract string Id { get; }
        public abstract string LocalizationKey { get; }
        public abstract ToolbarSlot DefaultSlot { get; }
        public abstract int DefaultOrder { get; }
        public virtual bool DefaultVisible => true;
        public virtual ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.Prepend;
        public virtual string DefaultAnchorName => null;

        /// <summary>DynamicResource 名称，用于 IconBrush。默认为 null（使用控件自带前景色）。</summary>
        protected virtual string IconBrushResourceKey => null;

        /// <summary>DynamicResource 名称，用于 LabelBrush（文字颜色）。默认为 null（使用控件自带前景色）。</summary>
        protected virtual string LabelBrushResourceKey => null;

        protected abstract void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e);

        /// <summary>构建后调用，用于回填 MainWindow 的原命名属性（partial 扩展里的 Attach*）。可选。</summary>
        protected virtual void AfterBuild(IToolbarHost host, ToolbarImageButton view) { }

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var btn = new ToolbarImageButton
            {
                Label = Strings.GetString(LocalizationKey) ?? LocalizationKey
            };
            if (!string.IsNullOrEmpty(IconBrushResourceKey))
            {
                if (btn.TryFindResource(IconBrushResourceKey) is Brush brush) btn.IconBrush = brush;
                else btn.SetResourceReference(ToolbarImageButton.IconBrushProperty, IconBrushResourceKey);
            }
            if (!string.IsNullOrEmpty(LabelBrushResourceKey))
            {
                if (btn.TryFindResource(LabelBrushResourceKey) is Brush brush) btn.LabelBrush = brush;
                else btn.SetResourceReference(ToolbarImageButton.LabelBrushProperty, LabelBrushResourceKey);
            }
            btn.ButtonMouseUp += (s, e) => OnClick(host, s, e);
            AfterBuild(host, btn);
            return btn;
        }
    }
}