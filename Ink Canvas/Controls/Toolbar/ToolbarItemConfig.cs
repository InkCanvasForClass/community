using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ink_Canvas.Controls.Toolbar
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

        public static ToolbarRuleset PptOnly()
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
                            new ToolbarRule { ConditionId = "isPptMode", IsReversed = true }
                        }
                    }
                }
            };
        }

        public static ToolbarRuleset PptAnnotationOnly()
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
                            new ToolbarRule { ConditionId = "isPptMode", IsReversed = true },
                            new ToolbarRule { ConditionId = "isAnnotating", IsReversed = true }
                        }
                    }
                }
            };
        }

        public static ToolbarRuleset GestureRule()
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
                    },
                    new ToolbarRuleGroup
                    {
                        Mode = ToolbarLogicalMode.And,
                        Rules = new List<ToolbarRule>
                        {
                            new ToolbarRule { ConditionId = "isPptMode" },
                            new ToolbarRule { ConditionId = "isGestureEnabled", IsReversed = true }
                        }
                    }
                }
            };
        }
    }

    public class ToolbarComponentEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("hidingRule")]
        public ToolbarHidingRule HidingRule { get; set; } = ToolbarHidingRule.AlwaysShow;

        [JsonProperty("showSeparateBorder")]
        public bool ShowSeparateBorder { get; set; } = false;

        [JsonProperty("settings")]
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

        [JsonProperty("children")]
        public List<ToolbarComponentEntry> Children { get; set; } = new List<ToolbarComponentEntry>();

        [JsonProperty("hidingRuleset")]
        public ToolbarRuleset HidingRuleset { get; set; } = null;

        public bool IsGroup => Id == "builtin.group";
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
        PptOnly = 2,
        PptAnnotationOnly = 3,
        AnnotationOrPptGesture = 4
    }
}
