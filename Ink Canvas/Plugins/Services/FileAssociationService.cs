using Ink_Canvas.Helpers;

namespace Ink_Canvas.Plugins
{
    internal class FileAssociationService : IFileAssociationService
    {
        public bool Register(string extension, string progId, string description, string iconPath = null)
        {
            try
            {
                // 复用主程序的 FileAssociationManager 逻辑
                // 当前仅支持 .icstk 格式，插件可扩展其他格式
                return FileAssociationManager.RegisterFileAssociation();
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
                return FileAssociationManager.UnregisterFileAssociation();
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
                return FileAssociationManager.IsFileAssociationRegistered();
            }
            catch
            {
                return false;
            }
        }
    }
}
