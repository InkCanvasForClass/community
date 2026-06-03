using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ink_Canvas.WorkflowAutomation;
using Ink_Canvas.WorkflowAutomation.Models;
using Ink_Canvas.WorkflowAutomation.Services;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AutomationWorkflowPage : Page
    {
        private AutomationService Service => AutomationBootstrap.Service;

        public AutomationWorkflowPage()
        {
            InitializeComponent();
            Loaded += AutomationWorkflowPage_Loaded;
        }

        private void AutomationWorkflowPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshWorkflowList();
            PopulateComboBoxes();

            ToggleIsConditionEnabled.Toggled += ToggleIsConditionEnabled_Toggled;
            ToggleIsRevertEnabled.Toggled += ToggleIsRevertEnabled_Toggled;
        }

        private void RefreshWorkflowList()
        {
            WorkflowListBox.ItemsSource = Service.Workflows;
        }

        private void PopulateComboBoxes()
        {
            ComboBoxTriggerType.ItemsSource = AutomationRegistry.RegisteredTriggers;
            ComboBoxTriggerType.DisplayMemberPath = "Name";
            ComboBoxTriggerType.SelectedValuePath = "Id";

            ComboBoxActionType.ItemsSource = AutomationRegistry.RegisteredActions.Values.ToList();
            ComboBoxActionType.DisplayMemberPath = "Name";
            ComboBoxActionType.SelectedValuePath = "Id";

            ComboBoxRuleType.ItemsSource = AutomationRegistry.RegisteredRules.Values.ToList();
            ComboBoxRuleType.DisplayMemberPath = "Name";
            ComboBoxRuleType.SelectedValuePath = "Id";
        }

        private void BtnAddWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var workflow = new Workflow();
            workflow.ActionSet.Name = $"自动化 {Service.Workflows.Count + 1}";
            Service.Workflows.Add(workflow);
            Service.SaveConfig("AddWorkflow");
            WorkflowListBox.SelectedItem = workflow;
        }

        private void BtnDeleteWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is Workflow workflow)
            {
                Service.Workflows.Remove(workflow);
                Service.SaveConfig("DeleteWorkflow");
                UpdateEditorVisibility();
            }
        }

        private void BtnDuplicateWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is Workflow source)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
                var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<Workflow>(json);
                if (copy != null)
                {
                    copy.ActionSet.Name += " (副本)";
                    Service.Workflows.Add(copy);
                    Service.SaveConfig("DuplicateWorkflow");
                    WorkflowListBox.SelectedItem = copy;
                }
            }
        }

        private void WorkflowListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEditorVisibility();
            UpdateEditorBindings();
        }

        private void UpdateEditorVisibility()
        {
            bool hasSelection = WorkflowListBox.SelectedItem != null;
            PlaceholderPanel.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
            EditorPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateEditorBindings()
        {
            if (WorkflowListBox.SelectedItem is Workflow workflow)
            {
                TextBoxWorkflowName.Text = workflow.ActionSet.Name;
                TextBoxWorkflowName.TextChanged -= TextBoxWorkflowName_TextChanged;
                TextBoxWorkflowName.TextChanged += TextBoxWorkflowName_TextChanged;

                ToggleIsEnabled.IsOn = workflow.ActionSet.IsEnabled;
                ToggleIsRevertEnabled.IsOn = workflow.ActionSet.IsRevertEnabled;
                ToggleIsConditionEnabled.IsOn = workflow.IsConditionEnabled;

                TriggersItemsControl.ItemsSource = workflow.Triggers;
                ActionsItemsControl.ItemsSource = workflow.ActionSet.Actions;

                if (workflow.Ruleset.Groups.Count > 0)
                {
                    RulesItemsControl.ItemsSource = workflow.Ruleset.Groups[0].Rules;
                }

                UpdateConditionVisibility(workflow.IsConditionEnabled);
                UpdateRevertHintVisibility(workflow.ActionSet.IsRevertEnabled);
            }
        }

        private void TextBoxWorkflowName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is Workflow workflow)
            {
                workflow.ActionSet.Name = TextBoxWorkflowName.Text;
                Service.SaveConfig("NameChanged");
                // 刷新列表显示
                WorkflowListBox.Items.Refresh();
            }
        }

        private void ToggleIsConditionEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is Workflow workflow)
            {
                workflow.IsConditionEnabled = ToggleIsConditionEnabled.IsOn;
                UpdateConditionVisibility(workflow.IsConditionEnabled);
                Service.SaveConfig("ConditionEnabledChanged");
            }
        }

        private void ToggleIsRevertEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is Workflow workflow)
            {
                workflow.ActionSet.IsRevertEnabled = ToggleIsRevertEnabled.IsOn;
                UpdateRevertHintVisibility(workflow.ActionSet.IsRevertEnabled);
                Service.SaveConfig("RevertEnabledChanged");
            }
        }

        private void UpdateConditionVisibility(bool enabled)
        {
            ConditionDisabledHint.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            ConditionEditorPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRevertHintVisibility(bool enabled)
        {
            RevertHintPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAddTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            var triggerId = ComboBoxTriggerType.SelectedValue as string;
            if (string.IsNullOrEmpty(triggerId)) return;

            var triggerSettings = new TriggerSettings { Id = triggerId };
            workflow.Triggers.Add(triggerSettings);
            Service.SaveConfig("AddTrigger");
        }

        private void BtnRemoveTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not TriggerSettings trigger) return;

            workflow.Triggers.Remove(trigger);
            Service.SaveConfig("RemoveTrigger");
        }

        private void BtnAddAction_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            var actionId = ComboBoxActionType.SelectedValue as string;
            if (string.IsNullOrEmpty(actionId)) return;

            var action = new Action { Id = actionId };
            workflow.ActionSet.Actions.Add(action);
            Service.SaveConfig("AddAction");
        }

        private void BtnRemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not Action action) return;

            workflow.ActionSet.Actions.Remove(action);
            Service.SaveConfig("RemoveAction");
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            var ruleId = ComboBoxRuleType.SelectedValue as string;
            if (string.IsNullOrEmpty(ruleId)) return;

            if (workflow.Ruleset.Groups.Count == 0)
            {
                workflow.Ruleset.Groups.Add(new RuleGroup());
            }

            var rule = new Rule { Id = ruleId };
            workflow.Ruleset.Groups[0].Rules.Add(rule);
            Service.SaveConfig("AddRule");
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (WorkflowListBox.SelectedItem is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not Rule rule) return;

            if (workflow.Ruleset.Groups.Count > 0)
            {
                workflow.Ruleset.Groups[0].Rules.Remove(rule);
                Service.SaveConfig("RemoveRule");
            }
        }
    }
}
