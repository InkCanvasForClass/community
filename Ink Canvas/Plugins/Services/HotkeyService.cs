using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    internal class HotkeyService : IHotkeyService
    {
        /// <summary>
        /// 注意：不能直接注入 GlobalHotkeyManager。RegisterPluginServices 在 MainWindow
        /// 构造后立即执行，而 GlobalHotkeyManager 原本在 RunDeferredStartupPhaseBAsync
        /// （Loaded 后数百毫秒）才创建——注入会拿到 null，导致插件热键全部静默失败。
        /// 因此这里持 MainWindow 引用，访问时按需 EnsureGlobalHotkeyManagerCreated()。
        /// </summary>
        private readonly MainWindow _mainWindow;
        private readonly Dictionary<string, (uint Modifiers, uint Key, Action Callback)> _pluginHotkeys
            = new Dictionary<string, (uint, uint, Action)>();

        public HotkeyService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        private GlobalHotkeyManager Manager => _mainWindow?.EnsureGlobalHotkeyManagerCreated();

        public bool Register(string id, uint modifiers, uint key, Action callback)
        {
            var manager = Manager;
            if (manager == null || string.IsNullOrEmpty(id) || callback == null) return false;
            if (_pluginHotkeys.ContainsKey(id)) return false;

            try
            {
                var modKeys = (ModifierKeys)modifiers;
                var wpfKey = KeyInterop.KeyFromVirtualKey((int)key);
                var result = manager.RegisterPluginHotkey(id, wpfKey, modKeys, callback);
                if (result)
                {
                    _pluginHotkeys[id] = (modifiers, key, callback);
                }
                return result;
            }
            catch
            {
                return false;
            }
        }

        public bool Unregister(string id)
        {
            var manager = Manager;
            if (manager == null || string.IsNullOrEmpty(id)) return false;
            if (!_pluginHotkeys.ContainsKey(id)) return false;

            try
            {
                manager.UnregisterHotkey(id);
                _pluginHotkeys.Remove(id);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsRegistered(string id)
        {
            return _pluginHotkeys.ContainsKey(id);
        }

        public System.Collections.Generic.IReadOnlyList<PluginHotkeyInfo> GetRegisteredHotkeys()
        {
            try
            {
                var list = Manager?.GetRegisteredHotkeys();
                if (list == null || list.Count == 0) return System.Array.Empty<PluginHotkeyInfo>();
                return list.Select(h => new PluginHotkeyInfo
                {
                    Name = h.Name ?? "",
                    Key = h.Key,
                    Modifiers = h.Modifiers,
                }).ToList();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.GetRegisteredHotkeys failed: {ex.Message}", LogHelper.LogType.Warning);
                return System.Array.Empty<PluginHotkeyInfo>();
            }
        }

        public bool UpdateHotkey(string hotkeyName, Key key, ModifierKeys modifiers)
        {
            try
            {
                return Manager?.UpdateHotkey(hotkeyName, key, modifiers) ?? false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.UpdateHotkey failed: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public void EnableRegistration()
        {
            try { Manager?.EnableHotkeyRegistration(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.EnableRegistration failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public void DisableRegistration()
        {
            try { Manager?.DisableHotkeyRegistration(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.DisableRegistration failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }
    }
}
