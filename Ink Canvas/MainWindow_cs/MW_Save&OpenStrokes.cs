using Ink_Canvas.Helpers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using File = System.IO.File;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void SymbolIconSaveStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender || inkCanvas.Visibility != Visibility.Visible) return;

            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            GridNotifications.Visibility = Visibility.Collapsed;

            SaveInkCanvasStrokes(true, true);
        }

        private void SaveInkCanvasStrokes(bool newNotice = true, bool saveByUser = false) {
            try {
                var savePath = Settings.Automation.AutoSavedStrokesLocation
                               + (saveByUser ? @"\User Saved - " : @"\Auto Saved - ")
                               + (currentMode == 0 ? "Annotation Strokes" : "BlackBoard Strokes");
                if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
                string savePathWithName;
                if (currentMode != 0) // 黑板模式下
                    savePathWithName = savePath + @"\" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + " Page-" +
                                       CurrentWhiteboardIndex + " StrokesCount-" + inkCanvas.Strokes.Count + ".icstk";
                else
                    //savePathWithName = savePath + @"\" + DateTime.Now.ToString("u").Replace(':', '-') + ".icstk";
                    savePathWithName = savePath + @"\" + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".icstk";
                
                var fs = new FileStream(savePathWithName, FileMode.Create);
                
                if (Settings.Automation.IsSaveFullPageStrokes)
                {
                    // 全页面保存模式 - 保存整个墨迹页面的图像
                    var bitmap = new System.Drawing.Bitmap(
                        (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, 
                        (int)System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height);
                    
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        // 创建黑色或透明背景
                        System.Drawing.Color bgColor = Settings.Canvas.UsingWhiteboard 
                            ? System.Drawing.Color.White 
                            : System.Drawing.Color.FromArgb(22, 41, 36); // 黑板背景色
                        g.Clear(bgColor);
                        
                        // 将InkCanvas墨迹渲染到Visual
                        var visual = new DrawingVisual();
                        using (var dc = visual.RenderOpen())
                        {
                            // 创建一个VisualBrush，使用inkCanvas作为源
                            var visualBrush = new VisualBrush(inkCanvas);
                            // 绘制矩形并填充为inkCanvas的内容
                            dc.DrawRectangle(visualBrush, null, new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight));
                        }
                        
                        // 创建适合墨迹画布尺寸的渲染位图
                        var rtb = new RenderTargetBitmap(
                            (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 
                            96, 96, 
                            PixelFormats.Pbgra32);
                        rtb.Render(visual);
                        
                        // 转换为GDI+ Bitmap并保存
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rtb));
                        
                        using (var ms = new MemoryStream())
                        {
                            encoder.Save(ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            var imgBitmap = new System.Drawing.Bitmap(ms);
                            
                            // 将生成的墨迹图像绘制到屏幕截图上
                            // 居中绘制，确保墨迹位于屏幕中央
                            int x = (bitmap.Width - imgBitmap.Width) / 2;
                            int y = (bitmap.Height - imgBitmap.Height) / 2;
                            g.DrawImage(imgBitmap, x, y);
                            
                            // 保存为PNG
                            string imagePathWithName = Path.ChangeExtension(savePathWithName, "png");
                            bitmap.Save(imagePathWithName, System.Drawing.Imaging.ImageFormat.Png);
                            
                            // 仍然保存墨迹文件以兼容旧版本
                            inkCanvas.Strokes.Save(fs);
                        }
                    }
                    
                    // 显示提示
                    if (newNotice) ShowNotification("墨迹成功全页面保存至 " + Path.ChangeExtension(savePathWithName, "png"));
                }
                else
                {
                    // 常规保存模式 - 仅保存墨迹对象
                    inkCanvas.Strokes.Save(fs);
                    if (newNotice) ShowNotification("墨迹成功保存至 " + savePathWithName);
                }
                
                fs.Close();
            }
            catch (Exception ex) {
                ShowNotification("墨迹保存失败");
                LogHelper.WriteLogToFile("墨迹保存失败 | " + ex.ToString(), LogHelper.LogType.Error);
            }
        }

        private void SymbolIconOpenStrokes_MouseUp(object sender, MouseButtonEventArgs e) {
            if (lastBorderMouseDownObject != sender) return;
            AnimationsHelper.HideWithSlideAndFade(BorderTools);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderTools);

            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Settings.Automation.AutoSavedStrokesLocation;
            openFileDialog.Title = "打开墨迹文件";
            openFileDialog.Filter = "Ink Canvas Strokes File (*.icstk)|*.icstk";
            if (openFileDialog.ShowDialog() != true) return;
            LogHelper.WriteLogToFile($"Strokes Insert: Name: {openFileDialog.FileName}",
                LogHelper.LogType.Event);
            try {
                var fileStreamHasNoStroke = false;
                using (var fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read)) {
                    var strokes = new StrokeCollection(fs);
                    fileStreamHasNoStroke = strokes.Count == 0;
                    if (!fileStreamHasNoStroke) {
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert: Strokes Count: {inkCanvas.Strokes.Count.ToString()}");
                    }
                }

                if (fileStreamHasNoStroke)
                    using (var ms = new MemoryStream(File.ReadAllBytes(openFileDialog.FileName))) {
                        ms.Seek(0, SeekOrigin.Begin);
                        var strokes = new StrokeCollection(ms);
                        ClearStrokes(true);
                        timeMachine.ClearStrokeHistory();
                        inkCanvas.Strokes.Add(strokes);
                        LogHelper.NewLog($"Strokes Insert (2): Strokes Count: {strokes.Count.ToString()}");
                    }

                if (inkCanvas.Visibility != Visibility.Visible) SymbolIconCursor_Click(sender, null);
            }
            catch {
                ShowNotification("墨迹打开失败");
            }
        }
    }
}
