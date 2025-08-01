﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private bool isClipboardMonitoringEnabled = false;
        private BitmapSource lastClipboardImage = null;

        // 初始化剪贴板监控
        private void InitializeClipboardMonitoring()
        {
            try
            {
                // 监听剪贴板变化
                ClipboardNotification.ClipboardUpdate += OnClipboardUpdate;
                isClipboardMonitoringEnabled = true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化剪贴板监控失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 剪贴板内容变化事件处理
        private void OnClipboardUpdate()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var clipboardImage = Clipboard.GetImage();
                    if (clipboardImage != null && clipboardImage != lastClipboardImage)
                    {
                        lastClipboardImage = clipboardImage;
                        // 在白板模式下显示粘贴提示
                        if (currentMode == 1) // 白板模式
                        {
                            ShowPasteNotification();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理剪贴板更新失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 显示粘贴提示
        private void ShowPasteNotification()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    ShowNotification("检测到剪贴板中有图片，右键点击白板可粘贴");
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示粘贴提示失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 处理右键菜单显示
        private void ShowPasteContextMenu(Point position)
        {
            try
            {
                if (!Clipboard.ContainsImage()) return;

                // 创建右键菜单
                var contextMenu = new ContextMenu();
                
                var pasteMenuItem = new MenuItem
                {
                    Header = "粘贴图片"
                };
                
                pasteMenuItem.Click += async (s, e) => await PasteImageFromClipboard(position);
                contextMenu.Items.Add(pasteMenuItem);

                // 显示菜单
                contextMenu.IsOpen = true;
                contextMenu.PlacementTarget = inkCanvas;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示粘贴菜单失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 从剪贴板粘贴图片
        private async Task PasteImageFromClipboard(Point? position = null)
        {
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    ShowNotification("剪贴板中没有图片");
                    return;
                }

                var clipboardImage = Clipboard.GetImage();
                if (clipboardImage == null)
                {
                    ShowNotification("无法获取剪贴板图片");
                    return;
                }

                // 创建Image控件
                var image = new Image
                {
                    Source = clipboardImage,
                    Width = clipboardImage.PixelWidth,
                    Height = clipboardImage.PixelHeight,
                    Stretch = System.Windows.Media.Stretch.Fill
                };

                // 生成唯一名称
                string timestamp = "img_clipboard_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                image.Name = timestamp;

                // 设置位置
                if (position.HasValue)
                {
                    // 在指定位置居中显示
                    InkCanvas.SetLeft(image, position.Value.X - image.Width / 2);
                    InkCanvas.SetTop(image, position.Value.Y - image.Height / 2);
                }
                else
                {
                    // 使用与文件选择相同的居中和缩放逻辑
                    CenterAndScaleElement(image);
                }

                // 添加到画布
                inkCanvas.Children.Add(image);

                // 添加鼠标事件处理
                image.MouseDown += UIElement_MouseDown;
                image.IsManipulationEnabled = true;

                // 提交到历史记录
                timeMachine.CommitElementInsertHistory(image);

                ShowNotification("图片已从剪贴板粘贴");
            }
            catch (Exception ex)
            {
                ShowNotification($"粘贴图片失败: {ex.Message}");
                LogHelper.WriteLogToFile($"粘贴图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }



        // 处理白板右键事件
        private void InkCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 只在白板模式下处理
                if (currentMode != 1) return;

                // 检查是否有图片在剪贴板中
                if (Clipboard.ContainsImage())
                {
                    var position = e.GetPosition(inkCanvas);
                    ShowPasteContextMenu(position);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理右键事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 处理全局粘贴快捷键
        private async void HandleGlobalPaste(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                // 只在白板模式下处理
                if (currentMode != 1) return;

                if (Clipboard.ContainsImage())
                {
                    await PasteImageFromClipboard();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理全局粘贴失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 清理剪贴板监控
        private void CleanupClipboardMonitoring()
        {
            try
            {
                if (isClipboardMonitoringEnabled)
                {
                    ClipboardNotification.ClipboardUpdate -= OnClipboardUpdate;
                    isClipboardMonitoringEnabled = false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理剪贴板监控失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }

    // 剪贴板通知类
    public static class ClipboardNotification
    {
        public static event Action ClipboardUpdate;

        private static System.Windows.Forms.Timer clipboardTimer;
        private static string lastClipboardText = "";
        private static bool lastHadImage = false;

        static ClipboardNotification()
        {
            clipboardTimer = new System.Windows.Forms.Timer();
            clipboardTimer.Interval = 500; // 每500ms检查一次
            clipboardTimer.Tick += CheckClipboard;
            clipboardTimer.Start();
        }

        private static void CheckClipboard(object sender, EventArgs e)
        {
            try
            {
                bool currentHasImage = Clipboard.ContainsImage();
                string currentText = Clipboard.ContainsText() ? Clipboard.GetText() : "";

                if (currentHasImage != lastHadImage || currentText != lastClipboardText)
                {
                    lastHadImage = currentHasImage;
                    lastClipboardText = currentText;
                    ClipboardUpdate?.Invoke();
                }
            }
            catch
            {
                // 忽略剪贴板访问错误
            }
        }

        public static void Stop()
        {
            clipboardTimer?.Stop();
            clipboardTimer?.Dispose();
        }
    }
}
