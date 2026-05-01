using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ink_Canvas.Controls.Toolbar
{
    /// <summary>
    /// 单个工具栏按钮的用户配置（可见性、顺序、所属 slot、插入位置）。
    /// 由 Settings.Toolbar 持久化。
    /// </summary>
    public class ToolbarItemConfig
    {
        [JsonProperty("visible")]
        public bool Visible { get; set; } = true;

        [JsonProperty("order")]
        public int Order { get; set; }

        [JsonProperty("slot")]
        public ToolbarSlot Slot { get; set; } = ToolbarSlot.FloatingBarMain;

        [JsonProperty("position")]
        public ToolbarInsertPosition Position { get; set; } = ToolbarInsertPosition.Prepend;

        [JsonProperty("anchorName")]
        public string AnchorName { get; set; }
    }

    public class ToolbarLayoutSettings
    {
        [JsonProperty("items")]
        public Dictionary<string, ToolbarItemConfig> Items { get; set; } = new Dictionary<string, ToolbarItemConfig>();
    }
}