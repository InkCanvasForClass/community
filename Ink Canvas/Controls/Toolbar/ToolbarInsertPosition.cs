namespace Ink_Canvas.Controls.Toolbar
{
    public enum ToolbarInsertPosition
    {
        /// <summary>从容器头部依次插入；Order 小的在前。</summary>
        Prepend,
        /// <summary>追加到容器末尾。</summary>
        Append,
        /// <summary>插入到由 AnchorName 指定的已有元素之前。</summary>
        BeforeAnchor,
        /// <summary>插入到由 AnchorName 指定的已有元素之后（同一锚点多项按 Order 依次排列）。</summary>
        AfterAnchor
    }
}