using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    public enum ToolbarLogicalMode
    {
        Or = 0,
        And = 1
    }

    public class ToolbarRule
    {
        [JsonProperty("isReversed")]
        public bool IsReversed { get; set; } = false;

        [JsonProperty("conditionId")]
        public string ConditionId { get; set; } = "";

        [JsonProperty("state")]
        internal int _state = 0;

        [JsonIgnore]
        public int State
        {
            get => _state;
            set => _state = value;
        }

        public ToolbarRule Clone()
        {
            return new ToolbarRule
            {
                IsReversed = IsReversed,
                ConditionId = ConditionId
            };
        }
    }

    public class ToolbarRuleGroup
    {
        [JsonProperty("mode")]
        public ToolbarLogicalMode Mode { get; set; } = ToolbarLogicalMode.And;

        [JsonProperty("isReversed")]
        public bool IsReversed { get; set; } = false;

        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;

        [JsonProperty("rules")]
        public List<ToolbarRule> Rules { get; set; } = new List<ToolbarRule>();

        [JsonProperty("state")]
        internal int _state = 0;

        [JsonIgnore]
        public int State
        {
            get => _state;
            set => _state = value;
        }

        public ToolbarRuleGroup Clone()
        {
            return new ToolbarRuleGroup
            {
                Mode = Mode,
                IsReversed = IsReversed,
                IsEnabled = IsEnabled,
                Rules = new List<ToolbarRule>(Rules.ConvertAll(r => r.Clone()))
            };
        }
    }

    public class ToolbarRuleset
    {
        [JsonProperty("mode")]
        public ToolbarLogicalMode Mode { get; set; } = ToolbarLogicalMode.Or;

        [JsonProperty("isReversed")]
        public bool IsReversed { get; set; } = false;

        [JsonProperty("groups")]
        public List<ToolbarRuleGroup> Groups { get; set; } = new List<ToolbarRuleGroup>();

        [JsonProperty("state")]
        internal int _state = 0;

        [JsonIgnore]
        public int State
        {
            get => _state;
            set => _state = value;
        }

        public ToolbarRuleset Clone()
        {
            return new ToolbarRuleset
            {
                Mode = Mode,
                IsReversed = IsReversed,
                Groups = new List<ToolbarRuleGroup>(Groups.ConvertAll(g => g.Clone()))
            };
        }

        public static ToolbarRuleset AlwaysShow()
        {
            return new ToolbarRuleset
            {
                Mode = ToolbarLogicalMode.Or,
                IsReversed = false,
                Groups = new List<ToolbarRuleGroup>()
            };
        }

        public static ToolbarRuleset AnnotationOnly()
        {
            return new ToolbarRuleset
            {
                Mode = ToolbarLogicalMode.Or,
                IsReversed = false,
                Groups = new List<ToolbarRuleGroup>
                {
                    new ToolbarRuleGroup
                    {
                        Mode = ToolbarLogicalMode.And,
                        Rules = new List<ToolbarRule>
                        {
                            new ToolbarRule { ConditionId = "isAnnotating", IsReversed = true }
                        }
                    }
                }
            };
        }

        public static ToolbarRuleset PPTOnly()
        {
            return new ToolbarRuleset
            {
                Mode = ToolbarLogicalMode.Or,
                IsReversed = false,
                Groups = new List<ToolbarRuleGroup>
                {
                    new ToolbarRuleGroup
                    {
                        Mode = ToolbarLogicalMode.And,
                        Rules = new List<ToolbarRule>
                        {
                            new ToolbarRule { ConditionId = "isPPTMode", IsReversed = true }
                        }
                    }
                }
            };
        }

        public static ToolbarRuleset PPTAnnotationOnly()
        {
            return new ToolbarRuleset
            {
                Mode = ToolbarLogicalMode.Or,
                IsReversed = false,
                Groups = new List<ToolbarRuleGroup>
                {
                    new ToolbarRuleGroup
                    {
                        Mode = ToolbarLogicalMode.Or,
                        Rules = new List<ToolbarRule>
                        {
                            new ToolbarRule { ConditionId = "isPPTMode", IsReversed = true },
                            new ToolbarRule { ConditionId = "isAnnotating", IsReversed = true }
                        }
                    }
                }
            };
        }

        public ToolbarRuleset WithHideOnCollapsed()
        {
            var result = Clone();
            result.Groups.Add(new ToolbarRuleGroup
            {
                Mode = ToolbarLogicalMode.And,
                Rules = new List<ToolbarRule>
                {
                    new ToolbarRule { ConditionId = "isContentCollapsedByUser", IsReversed = false }
                }
            });
            return result;
        }

        public ToolbarRuleset WithPreventHideOnCollapsed()
        {
            var result = Clone();
            if (result.Groups.Count == 0)
            {
                return result;
            }
            foreach (var group in result.Groups)
            {
                group.Rules.Add(new ToolbarRule { ConditionId = "isContentCollapsedByUser", IsReversed = true });
            }
            return result;
        }
    }

    public class ToolbarComponentEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        // 唯一实例 ID，用于区分相同 Id 的不同实例
        [JsonProperty("instanceId")]
        public string InstanceId { get; set; }

        [JsonProperty("hidingRule")]
        public ToolbarHidingRule HidingRule { get; set; } = ToolbarHidingRule.AlwaysShow;

        [JsonProperty("showSeparateBorder")]
        public bool ShowSeparateBorder { get; set; } = false;

        [JsonProperty("preventHideOnDragClick")]
        public bool PreventHideOnDragClick { get; set; } = false;

        [JsonProperty("settings")]
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

        [JsonProperty("children")]
        public List<ToolbarComponentEntry> Children { get; set; } = new List<ToolbarComponentEntry>();

        [JsonProperty("hidingRuleset")]
        public ToolbarRuleset HidingRuleset { get; set; } = null;

        public bool IsGroup => Id == "builtin.group";

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

    public static class ComponentSettingKeys
    {
        public const string MinWidth = "minWidth";
        public const string MaxWidth = "maxWidth";
        public const string FixedWidth = "fixedWidth";
        public const string MinHeight = "minHeight";
        public const string MaxHeight = "maxHeight";
        public const string FixedHeight = "fixedHeight";
        public const string FontSize = "fontSize";
        public const string IconSize = "iconSize";
        public const string HorizontalAlignment = "horizontalAlignment";
        public const string VerticalAlignment = "verticalAlignment";
        public const string MarginLeft = "marginLeft";
        public const string MarginTop = "marginTop";
        public const string MarginRight = "marginRight";
        public const string MarginBottom = "marginBottom";
        public const string PaddingLeft = "paddingLeft";
        public const string PaddingTop = "paddingTop";
        public const string PaddingRight = "paddingRight";
        public const string PaddingBottom = "paddingBottom";
        public const string Opacity = "opacity";
        public const string UseRedStyle = "useRedStyle";
        public const string DisplayMode = "displayMode";
    }

    public class ToolbarLayoutSettings
    {
        [JsonProperty("components")]
        public List<ToolbarComponentEntry> Components { get; set; } = new List<ToolbarComponentEntry>();
    }

    public enum ToolbarHidingRule
    {
        AlwaysShow = 0,
        AnnotationOnly = 1,
        PPTOnly = 2,
        PPTAnnotationOnly = 3,
        AnnotationOrPPTGesture = 4
    }
}
