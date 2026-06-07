using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ink_Canvas.Controls.Toolbar
{
    public class ToolsMenuItemInfo
    {
        public string Id { get; set; }
        public string LocalizationKey { get; set; }
        public string Description { get; set; }
        public string IconGeometry { get; set; }

        public string DisplayName => Strings.GetString(LocalizationKey) ?? LocalizationKey;
    }

    public class ToolsMenuLayoutSettings
    {
        [JsonProperty("floatingBarItems")]
        public List<string> FloatingBarItems { get; set; } = new List<string>();

        [JsonProperty("boardItems")]
        public List<string> BoardItems { get; set; } = new List<string>();
    }

    public static class ToolsMenuRegistry
    {
        private static readonly string ConfigSubDir = Path.Combine("Configs", "ToolsMenuConfigs");

        public static readonly List<ToolsMenuItemInfo> AllItems = new List<ToolsMenuItemInfo>
        {
            new ToolsMenuItemInfo { Id = "timer", LocalizationKey = "QuickPanel_Timer", Description = "计时器" },
            new ToolsMenuItemInfo { Id = "randomDraw", LocalizationKey = "Tools_RandomDraw", Description = "随机抽签" },
            new ToolsMenuItemInfo { Id = "singleDraw", LocalizationKey = "QuickPanel_SingleDraw", Description = "单人抽签" },
            new ToolsMenuItemInfo { Id = "save", LocalizationKey = "Tools_Save", Description = "保存" },
            new ToolsMenuItemInfo { Id = "open", LocalizationKey = "Tools_Open", Description = "打开" },
            new ToolsMenuItemInfo { Id = "replay", LocalizationKey = "Tools_Replay", Description = "回放" },
            new ToolsMenuItemInfo { Id = "screenshot", LocalizationKey = "Tools_Screenshot", Description = "截图" },
            new ToolsMenuItemInfo { Id = "shapeDraw", LocalizationKey = "FloatingBar_Geometry", Description = "几何图形" },
            new ToolsMenuItemInfo { Id = "redo", LocalizationKey = "Board_Redo", Description = "重做" },
            new ToolsMenuItemInfo { Id = "manual", LocalizationKey = "Tools_Manual", Description = "使用指南" },
            new ToolsMenuItemInfo { Id = "settings", LocalizationKey = "Settings_Title", Description = "设置" },
        };

        public static ToolsMenuItemInfo FindItem(string id)
            => AllItems.FirstOrDefault(i => i.Id == id);

        public static List<ToolsMenuItemInfo> FloatingBarAvailableItems => AllItems;

        public static List<ToolsMenuItemInfo> BoardAvailableItems => AllItems;

        public static ToolsMenuLayoutSettings CreateDefaultFloatingBarLayout()
        {
            return new ToolsMenuLayoutSettings
            {
                FloatingBarItems = new List<string>
                {
                    "timer", "randomDraw", "singleDraw",
                    "save", "open", "replay",
                    "screenshot", "manual", "settings"
                }
            };
        }

        public static ToolsMenuLayoutSettings CreateDefaultBoardLayout()
        {
            return new ToolsMenuLayoutSettings
            {
                BoardItems = new List<string>
                {
                    "timer", "randomDraw", "singleDraw",
                    "save", "open", "replay",
                    "screenshot", "manual", "settings"
                }
            };
        }

        #region Config file system

        public static string GetConfigDirectory()
            => Path.Combine(App.RootPath, ConfigSubDir);

        public static string GetFloatingBarConfigPath()
            => Path.Combine(GetConfigDirectory(), "floatingbar.json");

        public static string GetBoardConfigPath()
            => Path.Combine(GetConfigDirectory(), "board.json");

        public static ToolsMenuLayoutSettings LoadFloatingBarConfig()
        {
            var path = GetFloatingBarConfigPath();
            if (!File.Exists(path))
            {
                var layout = CreateDefaultFloatingBarLayout();
                SaveFloatingBarConfig(layout);
                return layout;
            }
            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonConvert.DeserializeObject<ToolsMenuLayoutSettings>(json);
                if (layout?.FloatingBarItems != null && layout.FloatingBarItems.Count > 0)
                    return layout;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolsMenuRegistry: 加载浮动栏菜单配置失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return CreateDefaultFloatingBarLayout();
        }

        public static ToolsMenuLayoutSettings LoadBoardConfig()
        {
            var path = GetBoardConfigPath();
            if (!File.Exists(path))
            {
                var layout = CreateDefaultBoardLayout();
                SaveBoardConfig(layout);
                return layout;
            }
            try
            {
                var json = File.ReadAllText(path);
                var layout = JsonConvert.DeserializeObject<ToolsMenuLayoutSettings>(json);
                if (layout?.BoardItems != null && layout.BoardItems.Count > 0)
                    return layout;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolsMenuRegistry: 加载白板菜单配置失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            return CreateDefaultBoardLayout();
        }

        public static void SaveFloatingBarConfig(ToolsMenuLayoutSettings layout)
        {
            try
            {
                var dir = GetConfigDirectory();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                File.WriteAllText(GetFloatingBarConfigPath(), json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolsMenuRegistry: 保存浮动栏菜单配置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public static void SaveBoardConfig(ToolsMenuLayoutSettings layout)
        {
            try
            {
                var dir = GetConfigDirectory();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(layout, Formatting.Indented);
                File.WriteAllText(GetBoardConfigPath(), json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ToolsMenuRegistry: 保存白板菜单配置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion
    }
}
