using Ink_Canvas.Helpers;

namespace Ink_Canvas.Plugins
{
    public class AppRestartService : IAppRestartService
    {
        public bool IsRunningAsAdmin => AppRestartHelper.IsRunningAsAdmin();

        public void RestartApp(bool asAdmin)
        {
            AppRestartHelper.RestartApp(asAdmin);
        }

        public void RestartWithCurrentPrivileges()
        {
            AppRestartHelper.RestartWithCurrentPrivileges();
        }

        public void RestartAsAdmin()
        {
            AppRestartHelper.RestartAsAdmin();
        }

        public void RestartAsNormal()
        {
            AppRestartHelper.RestartAsNormal();
        }

        public void SwitchToUIATopMostAndRestart()
        {
            AppRestartHelper.SwitchToUIATopMostAndRestart();
        }

        public void SwitchToNormalTopMostAndRestart()
        {
            AppRestartHelper.SwitchToNormalTopMostAndRestart();
        }
    }
}
