namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 应用信息服务：供插件读取宿主应用的基本信息。
    /// </summary>
    public interface IAppInfoService
    {
        /// <summary>宿主版本号，如 "1.7.18.0 (sha)"。</summary>
        string Version { get; }

        /// <summary>宿主安装目录路径（插件/设置/日志相对此路径）。</summary>
        string RootPath { get; }

        /// <summary>是否正在安装新版本（更新流程中）。</summary>
        bool IsUpdateInstalling { get; }

        /// <summary>是否已启用 UIAccess 置顶模式。</summary>
        bool IsUIAccessTopMostEnabled { get; }

        /// <summary>本次启动是否带 --board 参数（直接进入白板模式）。</summary>
        bool StartWithBoardMode { get; }
    }
}
