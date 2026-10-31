using System;
using System.ComponentModel;
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
            lock (this)
            {
                if (_timerDebounce == null)
                {
                    _timerDebounce = new Timer(timeMs) { AutoReset = false };
                    ElapsedEventHandler elapsedHandler = null;
                    elapsedHandler = (o, e) =>
                    {
                        // 解除订阅，打破 timer.Elapsed → lambda → timer 循环引用
                        _timerDebounce.Elapsed -= elapsedHandler;
                        _timerDebounce.Stop(); _timerDebounce.Close(); _timerDebounce = null;
                        InvokeAction(action, inv);
                    };
                    _timerDebounce.Elapsed += elapsedHandler;
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
