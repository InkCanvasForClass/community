namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 应用重启服务：供插件以指定权限/置顶模式重启宿主应用。
    /// </summary>
    public interface IAppRestartService
    {
        /// <summary>当前宿主进程是否以管理员身份运行。</summary>
        bool IsRunningAsAdmin { get; }

        /// <summary>重启宿主应用。</summary>
        /// <param name="asAdmin">true 时以管理员权限重启；false 时以普通权限重启。</param>
        void RestartApp(bool asAdmin);

        /// <summary>以当前权限重启宿主应用。</summary>
        void RestartWithCurrentPrivileges();

        /// <summary>以管理员权限重启宿主应用。</summary>
        void RestartAsAdmin();

        /// <summary>以普通权限重启宿主应用。</summary>
        void RestartAsNormal();

        /// <summary>开启 UIA 置顶模式并重启宿主应用。</summary>
        void SwitchToUIATopMostAndRestart();

        /// <summary>切换到普通置顶模式并重启宿主应用。</summary>
        void SwitchToNormalTopMostAndRestart();
    }
}
