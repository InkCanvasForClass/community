using Ink_Canvas.WorkflowAutomation.Abstractions;
using Ink_Canvas.WorkflowAutomation.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ActionModel = Ink_Canvas.WorkflowAutomation.Models.Action;

namespace Ink_Canvas.WorkflowAutomation.Services
{
    /// <summary>
    /// 行动服务，负责执行和恢复行动。
    /// </summary>
    public class ActionService : IActionService
    {
        /// <summary>
        /// 触发行动组
        /// </summary>
        public void Invoke(ActionSet actionSet)
        {
            if (!actionSet.IsEnabled) return;

            // 先清除所有行动的错误状态
            foreach (var action in actionSet.Actions)
                action.Exception = null;

            // 启用恢复时设置 IsOn 标志
            // 未启用恢复时，IsOn 不应阻止重复触发
            if (actionSet.IsRevertEnabled)
            {
                actionSet.IsOn = true;
            }

            // 异步执行行动，避免阻塞 UI 线程
            Task.Run(() =>
            {
                foreach (var action in actionSet.Actions)
                {
                    InvokeAction(action);
                }
            });
        }

        /// <summary>
        /// 恢复行动组
        /// </summary>
        public void Revert(ActionSet actionSet)
        {
            if (!actionSet.IsOn) return;

            // 先清除所有行动的错误状态
            foreach (var action in actionSet.Actions)
                action.Exception = null;

            actionSet.IsOn = false;

            // 异步执行恢复，避免阻塞 UI 线程
            Task.Run(() =>
            {
                foreach (var action in actionSet.Actions)
                {
                    RevertAction(action);
                }
            });
        }

        /// <summary>
        /// 注册行动处理程序。
        /// </summary>
        public void RegisterActionHandler(string id, ActionRegistryInfo.HandleDelegate handler)
        {
            if (!IActionService.Actions.TryGetValue(id, out var actionRegistryInfo))
                throw new KeyNotFoundException($"找不到行动 {id}。");

            // 幂等：同一 handler 注册多次直接返回，避免累加触发
            if (actionRegistryInfo.Handle == handler) return;
            actionRegistryInfo.Handle += handler;
        }

        public void UnregisterActionHandler(string id, ActionRegistryInfo.HandleDelegate handler)
        {
            if (!IActionService.Actions.TryGetValue(id, out var actionRegistryInfo)) return;
            if (actionRegistryInfo.Handle == null) return;
            actionRegistryInfo.Handle -= handler;
        }

        /// <summary>
        /// 注册行动恢复处理程序。
        /// </summary>
        public void RegisterRevertHandler(string id, ActionRegistryInfo.HandleDelegate handler)
        {
            if (!IActionService.Actions.TryGetValue(id, out var actionRegistryInfo))
                throw new KeyNotFoundException($"找不到行动 {id}。");

            // 幂等：同一 handler 注册多次直接返回
            if (actionRegistryInfo.RevertHandle == handler) return;
            actionRegistryInfo.RevertHandle += handler;
        }

        public void UnregisterRevertHandler(string id, ActionRegistryInfo.HandleDelegate handler)
        {
            if (!IActionService.Actions.TryGetValue(id, out var actionRegistryInfo)) return;
            if (actionRegistryInfo.RevertHandle == null) return;
            actionRegistryInfo.RevertHandle -= handler;
        }

        /// <summary>
        /// 执行单个行动
        /// </summary>
        private void InvokeAction(ActionModel action)
        {
            if (!IActionService.Actions.TryGetValue(action.Id, out var info)) return;

            // 反序列化 settings
            object settings = null;
            var settingsType = info.SettingsType;
            if (settingsType != null)
            {
                settings = action.Settings ?? Activator.CreateInstance(settingsType);
                if (settings is JToken jToken)
                {
                    try
                    {
                        settings = jToken.ToObject(settingsType);
                    }
                    catch
                    {
                        settings = Activator.CreateInstance(settingsType);
                    }
                }
            }

            action.IsWorking = true;
            action.Exception = null;
            try
            {
                info.Handle?.Invoke(settings, action.Id);
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
            if (action.Id == string.Empty) return;
            if (!IActionService.Actions.TryGetValue(action.Id, out var info)) return;
            if (info.RevertHandle == null) return;

            // 反序列化 settings
            object settings = null;
            var settingsType = info.SettingsType;
            if (settingsType != null)
            {
                settings = action.Settings ?? Activator.CreateInstance(settingsType);
                if (settings is JToken jToken)
                {
                    try
                    {
                        settings = jToken.ToObject(settingsType);
                    }
                    catch
                    {
                        settings = Activator.CreateInstance(settingsType);
                    }
                }
            }

            action.IsWorking = true;
            action.Exception = null;
            try
            {
                info.RevertHandle.Invoke(settings, action.Id);
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
            if (!IActionService.Actions.TryGetValue(action.Id, out var info)) return false;
            return info.RevertHandle != null;
        }
    }
}
