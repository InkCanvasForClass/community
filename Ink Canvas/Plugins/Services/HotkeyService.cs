using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    internal class HotkeyService : IHotkeyService
    {
        private readonly GlobalHotkeyManager _manager;
        private readonly Dictionary<string, (uint Modifiers, uint Key, Action Callback)> _pluginHotkeys
            = new Dictionary<string, (uint, uint, Action)>();

        public HotkeyService(GlobalHotkeyManager manager)
        {
            _manager = manager;
        }

        public bool Register(string id, uint modifiers, uint key, Action callback)
        {
            if (_manager == null || string.IsNullOrEmpty(id) || callback == null) return false;
            if (_pluginHotkeys.ContainsKey(id)) return false;

            try
            {
                var modKeys = (ModifierKeys)modifiers;
                var wpfKey = KeyInterop.KeyFromVirtualKey((int)key);
                var result = _manager.RegisterHotkey(id, wpfKey, modKeys, callback);
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
            if (_manager == null || string.IsNullOrEmpty(id)) return false;
            if (!_pluginHotkeys.ContainsKey(id)) return false;

            try
            {
                _manager.UnregisterHotkey(id);
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
                var list = _manager?.GetRegisteredHotkeys();
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
                return _manager?.UpdateHotkey(hotkeyName, key, modifiers) ?? false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.UpdateHotkey failed: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        public void EnableRegistration()
        {
            try { _manager?.EnableHotkeyRegistration(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.EnableRegistration failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        public void DisableRegistration()
        {
            try { _manager?.DisableHotkeyRegistration(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"HotkeyService.DisableRegistration failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }
    }
}
