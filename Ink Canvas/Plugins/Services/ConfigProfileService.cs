using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IConfigProfileService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.ConfigProfileManager"/>。
    /// </summary>
    internal sealed class ConfigProfileService : IConfigProfileService
    {
        public IReadOnlyList<string> ListProfiles()
        {
            try { return Ink_Canvas.Helpers.ConfigProfileManager.ListProfileNames(); }
            catch (System.Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ConfigProfileService.ListProfiles failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return new List<string>();
            }
        }

        public string GetProfilePath(string profileName)
        {
            try { return Ink_Canvas.Helpers.ConfigProfileManager.GetProfilePath(profileName); }
            catch (System.Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ConfigProfileService.GetProfilePath failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public bool SaveAsProfile(string profileName, string settingsJson)
        {
            try { return Ink_Canvas.Helpers.ConfigProfileManager.SaveAsProfile(profileName, settingsJson); }
            catch (System.Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ConfigProfileService.SaveAsProfile failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool ApplyProfile(string profileName)
        {
            try { return Ink_Canvas.Helpers.ConfigProfileManager.ApplyProfile(profileName); }
            catch (System.Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ConfigProfileService.ApplyProfile failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool DeleteProfile(string profileName)
        {
            try { return Ink_Canvas.Helpers.ConfigProfileManager.DeleteProfile(profileName); }
            catch (System.Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ConfigProfileService.DeleteProfile failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }
    }
}
