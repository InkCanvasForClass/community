using Ink_Canvas.Helpers;

namespace Ink_Canvas.Plugins
{
    internal class FileAssociationService : IFileAssociationService
    {
        public bool Register(string extension, string progId, string description, string iconPath = null)
        {
            try
            {
                // 插件自定义扩展名关联：写入 HKCU\Software\Classes，打开命令指向宿主 exe
                return FileAssociationManager.RegisterFileAssociation(extension, progId, description, iconPath);
            }
            catch
            {
                return false;
            }
        }

        public bool Unregister(string extension)
        {
            try
            {
                // 注销插件自定义扩展名关联（宿主自动读取并清理其 ProgId）
                return FileAssociationManager.UnregisterFileAssociation(extension);
            }
            catch
            {
                return false;
            }
        }

        public bool IsRegistered(string extension)
        {
            try
            {
                return FileAssociationManager.IsFileAssociationRegistered(extension);
            }
            catch
            {
                return false;
            }
        }
    }
}
