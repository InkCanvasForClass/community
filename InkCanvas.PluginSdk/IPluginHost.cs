using System;

namespace Ink_Canvas.Plugins
{
    public interface IPluginHost
    {
        void Log(string message);
        void LogError(string message, Exception ex = null);
        T GetService<T>() where T : class;
        void RegisterService<T>(T service) where T : class;
    }
}
