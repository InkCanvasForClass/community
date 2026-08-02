namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IAppInfoService"/> 的宿主实现：读取 App 静态字段。
    /// </summary>
    internal sealed class AppInfoService : IAppInfoService
    {
        public string Version => App.AppVersion;
        public string RootPath => App.RootPath;
        public bool IsUpdateInstalling => App.IsUpdateInstalling;
        public bool IsUIAccessTopMostEnabled => App.IsUIAccessTopMostEnabled;
        public bool StartWithBoardMode => App.StartWithBoardMode;
    }
}
