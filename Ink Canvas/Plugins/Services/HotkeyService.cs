using System;
using System.Collections.Generic;
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
    }
}
