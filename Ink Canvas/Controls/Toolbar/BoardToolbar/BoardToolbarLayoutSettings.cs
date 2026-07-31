using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public class BoardToolbarComponentEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; } = "Middle";

        [JsonProperty("settings")]
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

        public double? GetSettingDouble(string key)
        {
            if (Settings != null && Settings.TryGetValue(key, out var val))
            {
                if (val is double d) return d;
                if (val is long l) return l;
                if (val is int i) return i;
                if (val != null && double.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return null;
        }

        public string GetSettingString(string key)
        {
            if (Settings != null && Settings.TryGetValue(key, out var val))
                return val?.ToString();
            return null;
        }

        public bool GetSettingBool(string key)
        {
            if (Settings != null && Settings.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (val != null && bool.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return false;
        }

        public void SetSetting(string key, object value)
        {
            if (Settings == null) Settings = new Dictionary<string, object>();
            Settings[key] = value;
        }
    }

    public class BoardToolbarGroupEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("components")]
        public List<BoardToolbarComponentEntry> Components { get; set; } = new List<BoardToolbarComponentEntry>();

        [JsonProperty("settings")]
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }

    public class BoardToolbarAreaEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("groups")]
        public List<BoardToolbarGroupEntry> Groups { get; set; } = new List<BoardToolbarGroupEntry>();
    }

    public class BoardToolbarLayoutSettings
    {
        public const int CurrentSchemaVersion = 2;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonProperty("areas")]
        public List<BoardToolbarAreaEntry> Areas { get; set; } = new List<BoardToolbarAreaEntry>();

        public bool EnsureCurrentVersion()
        {
            if (SchemaVersion >= CurrentSchemaVersion) return false;

            for (var areaIndex = 0; areaIndex < Areas.Count; areaIndex++)
            {
                var groups = Areas[areaIndex].Groups;
                for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    var group = groups[groupIndex];
                    if (group.Id != "tools") continue;

                    var hasMath = false;
                    var insertIndex = group.Components.Count;
                    for (var componentIndex = 0; componentIndex < group.Components.Count; componentIndex++)
                    {
                        var id = group.Components[componentIndex].Id;
                        if (id == "board.math") hasMath = true;
                        if (id == "board.shape") insertIndex = componentIndex + 1;
                    }

                    if (!hasMath)
                        group.Components.Insert(insertIndex, new BoardToolbarComponentEntry { Id = "board.math" });
                }
            }

            SchemaVersion = CurrentSchemaVersion;
            return true;
        }

        public static BoardToolbarLayoutSettings CreateDefault()
        {
            return new BoardToolbarLayoutSettings
            {
                SchemaVersion = CurrentSchemaVersion,
                Areas = new List<BoardToolbarAreaEntry>
                {
                    new BoardToolbarAreaEntry
                    {
                        Id = "left",
                        Groups = new List<BoardToolbarGroupEntry>
                        {
                            new BoardToolbarGroupEntry
                            {
                                Id = "navigation",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.previousPage" },
                                    new BoardToolbarComponentEntry { Id = "board.pageInfo" },
                                    new BoardToolbarComponentEntry { Id = "board.nextPage" }
                                }
                            },
                            new BoardToolbarGroupEntry
                            {
                                Id = "videoBooth",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.videoBooth" }
                                }
                            }
                        }
                    },
                    new BoardToolbarAreaEntry
                    {
                        Id = "center",
                        Groups = new List<BoardToolbarGroupEntry>
                        {
                            new BoardToolbarGroupEntry
                            {
                                Id = "gesture",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.gesture" },
                                    new BoardToolbarComponentEntry { Id = "board.backgroundColor" }
                                }
                            },
                            new BoardToolbarGroupEntry
                            {
                                Id = "tools",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.select" },
                                    new BoardToolbarComponentEntry { Id = "board.pen" },
                                    new BoardToolbarComponentEntry { Id = "board.inkFreeze" },
                                    new BoardToolbarComponentEntry { Id = "board.roaming" },
                                    new BoardToolbarComponentEntry { Id = "board.eraser" },
                                    new BoardToolbarComponentEntry { Id = "board.strokeEraser" },
                                    new BoardToolbarComponentEntry { Id = "board.shape" },
                                    new BoardToolbarComponentEntry { Id = "board.math" },
                                    new BoardToolbarComponentEntry { Id = "board.insertImage" },
                                    new BoardToolbarComponentEntry { Id = "board.undo" },
                                    new BoardToolbarComponentEntry { Id = "board.redo" }
                                }
                            },
                            new BoardToolbarGroupEntry
                            {
                                Id = "system",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.tools" },
                                    new BoardToolbarComponentEntry { Id = "board.exit" }
                                }
                            }
                        }
                    },
                    new BoardToolbarAreaEntry
                    {
                        Id = "right",
                        Groups = new List<BoardToolbarGroupEntry>
                        {
                            new BoardToolbarGroupEntry
                            {
                                Id = "addPage",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.addNewPage" }
                                }
                            },
                            new BoardToolbarGroupEntry
                            {
                                Id = "navigation",
                                Components = new List<BoardToolbarComponentEntry>
                                {
                                    new BoardToolbarComponentEntry { Id = "board.previousPage" },
                                    new BoardToolbarComponentEntry { Id = "board.pageInfo" },
                                    new BoardToolbarComponentEntry { Id = "board.nextPage" }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
