namespace Ink_Canvas.Plugins
{
    public interface IAppRestartService
    {
        bool IsRunningAsAdmin { get; }

        void RestartApp(bool asAdmin);

        void RestartWithCurrentPrivileges();

        void RestartAsAdmin();

        void RestartAsNormal();

        void SwitchToUIATopMostAndRestart();

        void SwitchToNormalTopMostAndRestart();
    }
}
