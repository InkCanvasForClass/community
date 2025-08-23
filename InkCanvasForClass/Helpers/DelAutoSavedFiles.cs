using System;
using System.IO;
using System.Windows;

namespace Ink_Canvas.Helpers {
    internal class DelAutoSavedFiles {
        public static void DeleteFilesOlder(string directoryPath, int daysThreshold) {
            string[] extensionsToDel = { ".icstk", ".png" };
            if (Directory.Exists(directoryPath)) {
                // 获取目录中的所有子目录
                string[] subDirectories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);
                foreach (string subDirectory in subDirectories) {
                    try {
                        // 获取子目录下的所有文件
                        string[] files = Directory.GetFiles(subDirectory);
                        foreach (string filePath in files) {
                            // 获取文件的创建日期
                            DateTime creationDate = File.GetCreationTime(filePath);
                            // 获取文件的扩展名
                            string fileExtension = Path.GetExtension(filePath);
                            // 如果文件的创建日期早于指定天数且是要删除的扩展名，则删除文件
                            if (creationDate < DateTime.Now.AddDays(-daysThreshold)) {
                                if (Array.Exists(extensionsToDel, ext => ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase))
                                    || Path.GetFileName(filePath).Equals("Position", StringComparison.OrdinalIgnoreCase)) {
                                    File.Delete(filePath);
                                }
                            }
                        }
                    } catch (Exception ex) {
                        LogHelper.WriteLogToFile("DelAutoSavedFiles | 处理文件时出错: " + ex.ToString(), LogHelper.LogType.Error);
                    }
                }

                try { // 递归删除空文件夹
                    DeleteEmptyFolders(directoryPath);
                } catch (Exception ex) {
                    LogHelper.WriteLogToFile("DelAutoSavedFiles | 处理文件时出错: " + ex.ToString(), LogHelper.LogType.Error);
                }
            }
        }

        private static void DeleteEmptyFolders(string directoryPath) {
            foreach (string dir in Directory.GetDirectories(directoryPath)) {
                DeleteEmptyFolders(dir);
                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0) {
                    Directory.Delete(dir, false);
                }
            }
        }
    }
}