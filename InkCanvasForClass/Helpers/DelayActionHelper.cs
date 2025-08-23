using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Ink_Canvas.Helpers
{
    public class DelayAction
    {
        Timer _timerDebounce;

        /// <summary>
        /// 防抖函式
        /// </summary>
        /// <param name="inv">同步的對象，一般傳入控件，不需要可null</param>
        public void DebounceAction(int timeMs, ISynchronizeInvoke inv, Action action)
        {
            lock (this) {
                if (_timerDebounce == null) {
                    _timerDebounce = new Timer(timeMs) { AutoReset = false };
                    _timerDebounce.Elapsed += (o, e) => {
                        _timerDebounce.Stop(); _timerDebounce.Close(); _timerDebounce = null;
                        InvokeAction(action, inv);
                    };
                }
                _timerDebounce.Stop();
                _timerDebounce.Start();
            }
        }

        private static void InvokeAction(Action action, ISynchronizeInvoke inv)
        {
            if (inv == null)
            {
                action();
            }
            else
            {
                if (inv.InvokeRequired)
                {
                    inv.Invoke(action, null);
                }
                else
                {
                    action();
                }
            }
        }
    }
}
