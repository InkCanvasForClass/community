using System;
using System.IO;
using System.Text;
using Ink_Canvas.Mathematics.Models;

namespace Ink_Canvas.Mathematics.Persistence
{
    public static class MathSceneFileStore
    {
        public static void Save(string path, MathScene scene)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, MathSceneSerializer.Serialize(scene), new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                }

                throw;
            }
        }

        public static MathSceneLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
            return MathSceneSerializer.Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }
    }
}
