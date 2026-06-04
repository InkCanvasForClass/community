using System;
using System.Collections.Generic;
using Ink_Canvas.WorkflowAutomation.Models;
using ActionModel = Ink_Canvas.WorkflowAutomation.Models.Action;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 行动服务，负责执行和恢复行动。
    /// </summary>
    public class ActionService
    {
        /// <summary>
        /// 触发行动组
        /// </summary>
        public void Invoke(ActionSet actionSet)
        {
            if (!actionSet.IsEnabled) return;

            foreach (var action in actionSet.Actions)
            {
                InvokeAction(action);
            }
            actionSet.IsOn = true;
        }

        /// <summary>
        /// 恢复行动组
        /// </summary>
        public void Revert(ActionSet actionSet)
        {
            if (!actionSet.IsOn) return;

            foreach (var action in actionSet.Actions)
            {
                RevertAction(action);
            }
            actionSet.IsOn = false;
        }

        /// <summary>
        /// 执行单个行动
        /// </summary>
        private void InvokeAction(ActionModel action)
        {
            if (!AutomationRegistry.RegisteredActions.TryGetValue(action.Id, out var info)) return;

            action.IsWorking = true;
            action.Exception = null;
            try
            {
                info.Handle?.Invoke(action.Settings, action.Id);
            }
            catch (Exception ex)
            {
                action.Exception = ex;
            }
            finally
            {
                action.IsWorking = false;
            }
        }

        /// <summary>
        /// 恢复单个行动
        /// </summary>
        private void RevertAction(ActionModel action)
        {
            if (!AutomationRegistry.RegisteredActions.TryGetValue(action.Id, out var info)) return;
            if (info.RevertHandle == null) return;

            action.IsWorking = true;
            action.Exception = null;
            try
            {
                info.RevertHandle.Invoke(action.Settings, action.Id);
            }
            catch (Exception ex)
            {
                action.Exception = ex;
            }
            finally
            {
                action.IsWorking = false;
            }
        }

        /// <summary>
        /// 行动是否有内建的恢复
        /// </summary>
        public bool ExistRevertHandler(ActionModel action)
        {
            if (!AutomationRegistry.RegisteredActions.TryGetValue(action.Id, out var info)) return false;
            return info.RevertHandle != null;
        }
    }
}
