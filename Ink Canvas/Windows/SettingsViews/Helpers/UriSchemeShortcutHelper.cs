using iNKORE.UI.WPF.Modern.Common.IconKeys;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using IWshRuntimeLibrary;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FontIcon = iNKORE.UI.WPF.Modern.Controls.FontIcon;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    /// <summary>
    /// 创建指向 icc:// 外部协议命令的桌面快捷方式。
    /// 快捷方式以主程序为目标并携带 icc:// 参数：已有实例运行时由 App 通过 IPC 转发命令，
    /// 无实例时启动应用后按启动 URI 参数执行命令。
    /// 快捷方式图标为按需生成的 .ico：半透明圆角矩形底 + 程序内图标 + 右下角半透明 ICC 文字，
    /// 与设置页徽章按钮使用同一套绘制参数。
    /// </summary>
    public static class UriSchemeShortcutHelper
    {
        public const string FeatureBoard = "board";       // 白板
        public const string FeatureBooth = "booth";       // 展台
        public const string FeatureRandom = "rand";       // 抽选
        public const string FeatureSettings = "settings"; // 设置
        public const string FeatureAnnotate = "annotate"; // 批注

        private const string BadgeText = "ICC";

        // 徽章固定配色：近乎不透明的白色圆角矩形（95%）+ 深色图标文字，在任意桌面壁纸上均可辨识
        private static readonly Color BadgeFillColor = Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF);
        private static readonly Color BadgeBorderColor = Color.FromArgb(0x59, 0x00, 0x00, 0x00);
        private static readonly Color BadgeIconColor = Color.FromArgb(0xD9, 0x00, 0x00, 0x00);
        private static readonly Color BadgeTextColor = Color.FromArgb(0x80, 0x00, 0x00, 0x00);
        // 设置页徽章底色：半透明中性灰，跟随明暗主题依然可辨识
        private static readonly Color BadgeFillColorOnCard = Color.FromArgb(0x2E, 0x80, 0x80, 0x80);
        private static readonly Color BadgeBorderColorOnCard = Color.FromArgb(0x47, 0x80, 0x80, 0x80);

        private const string ThemeForegroundBrushKey = "SystemControlForegroundBaseHighBrush";

        /// <summary>获取功能对应的 icc:// 命令路径（不含协议头）。</summary>
        public static string GetFeatureUri(string feature)
        {
            switch (feature)
            {
                case FeatureBoard: return "board";
                case FeatureBooth: return "booth";
                case FeatureRandom: return "rand";
                case FeatureSettings: return "settings";
                case FeatureAnnotate: return "tool/pen";
                default: return null;
            }
        }

        /// <summary>获取功能的本地化名称（同时用于快捷方式文件名）。</summary>
        public static string GetFeatureLabel(string feature)
        {
            switch (feature)
            {
                case FeatureBoard: return StartupStrings.ExternalProtocol_Shortcut_Board;
                case FeatureBooth: return StartupStrings.ExternalProtocol_Shortcut_Booth;
                case FeatureRandom: return StartupStrings.ExternalProtocol_Shortcut_Random;
                case FeatureSettings: return StartupStrings.ExternalProtocol_Shortcut_Settings;
                case FeatureAnnotate: return StartupStrings.ExternalProtocol_Shortcut_Annotate;
                default: return feature;
            }
        }

        /// <summary>获取功能在程序内使用的图标：几何路径字符串或 FontIconData 字形。</summary>
        private static object GetFeatureIcon(string feature)
        {
            switch (feature)
            {
                case FeatureBoard: return XamlGraphicsIconGeometries.WhiteboardFloatingBarBtnIcon;
                case FeatureBooth: return FluentSystemIcons.Video_24_Regular;
                case FeatureRandom: return XamlGraphicsIconGeometries.RandomDrawIconGeometry;
                case FeatureSettings: return SegoeFluentIcons.Settings;
                case FeatureAnnotate: return XamlGraphicsIconGeometries.SolidPenIcon;
                default: return null;
            }
        }

        /// <summary>
        /// 构建徽章视觉元素（半透明圆角矩形底 + 程序内图标 + 右下角半透明 ICC/CE 文字）。
        /// 用于设置页快捷方式按钮内容。
        /// </summary>
        public static FrameworkElement CreateBadgeElement(string feature, double size)
        {
            var grid = new Grid { Width = size, Height = size };

            var border = new Border
            {
                CornerRadius = new CornerRadius(size * 0.19),
                Background = new SolidColorBrush(BadgeFillColorOnCard),
                BorderThickness = new Thickness(Math.Max(1, size * 0.022)),
                BorderBrush = new SolidColorBrush(BadgeBorderColorOnCard),
            };
            grid.Children.Add(border);

            var icon = CreateIconElement(feature, size * 0.55);
            if (icon != null) grid.Children.Add(icon);

            var badge = new TextBlock
            {
                Text = BadgeText,
                FontSize = Math.Max(8, size * 0.18),
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, size * 0.08, size * 0.05),
                Opacity = 0.55,
            };
            badge.SetResourceReference(TextBlock.ForegroundProperty, ThemeForegroundBrushKey);
            grid.Children.Add(badge);

            return grid;
        }

        /// <summary>构建图标元素：字形类使用程序内 FontIcon，几何类使用 Stretch=Uniform 的 Path。</summary>
        private static FrameworkElement CreateIconElement(string feature, double iconSize)
        {
            object icon = GetFeatureIcon(feature);
            if (icon == null) return null;

            if (icon is FontIconData fontIconData)
            {
                var fontIcon = new FontIcon
                {
                    Icon = fontIconData,
                    FontSize = iconSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                fontIcon.SetResourceReference(FontIcon.ForegroundProperty, ThemeForegroundBrushKey);
                return fontIcon;
            }

            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse((string)icon),
                Stretch = Stretch.Uniform,
                Width = iconSize,
                Height = iconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            path.SetResourceReference(System.Windows.Shapes.Path.FillProperty, ThemeForegroundBrushKey);
            return path;
        }

        /// <summary>创建桌面快捷方式（已存在则覆盖），返回是否成功。</summary>
        public static bool CreateDesktopShortcut(string feature)
        {
            try
            {
                string uri = GetFeatureUri(feature);
                if (string.IsNullOrEmpty(uri)) return false;

                string iconPath = EnsureIconFile(feature);
                if (string.IsNullOrEmpty(iconPath)) return false;

                string label = GetFeatureLabel(feature);
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnkPath = Path.Combine(desktop, "Ink Canvas " + label + ".lnk");

                var shell = new WshShell();
                var shortcut = (IWshShortcut)shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = System.Windows.Forms.Application.ExecutablePath;
                shortcut.Arguments = "icc://" + uri;
                shortcut.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
                shortcut.IconLocation = iconPath + ",0";
                shortcut.Description = "Ink Canvas " + label + " (icc://" + uri + ")";
                shortcut.WindowStyle = 1;
                shortcut.Save();

                LogHelper.WriteLogToFile($"已创建桌面快捷方式: {lnkPath} (icc://{uri})", LogHelper.LogType.Event);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建快捷方式失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>生成（或覆盖）功能徽章 .ico 文件并返回路径，失败返回 null。
        /// 每次都重新生成：避免代码更新徽章样式后，旧的缓存 ico 一直被快捷方式复用。</summary>
        private static string EnsureIconFile(string feature)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "ShortcutIcons");
                string path = Path.Combine(dir, feature + ".ico");
                Directory.CreateDirectory(dir);
                WriteIco(path, RenderBadgeToPng(feature, 256), 256);
                return path;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"生成快捷方式图标失败: {ex.Message}", LogHelper.LogType.Error);
                return null;
            }
        }

        /// <summary>把徽章渲染为 PNG 字节（DrawingVisual 方式，不依赖控件模板）。</summary>
        private static byte[] RenderBadgeToPng(string feature, int size)
        {
            double fillInset = size * 0.01;
            double cornerRadius = size * 0.19;
            var rect = new Rect(fillInset, fillInset, size - fillInset * 2, size - fillInset * 2);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRoundedRectangle(
                    new SolidColorBrush(BadgeFillColor),
                    new Pen(new SolidColorBrush(BadgeBorderColor), Math.Max(2, size * 0.022)),
                    rect, cornerRadius, cornerRadius);

                DrawIcon(dc, feature, size, size * 0.55);
                DrawBadgeText(dc, size, size * 0.18);
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            return ms.ToArray();
        }

        /// <summary>在 DrawingVisual 上绘制居中的程序内图标（字形或几何路径）。</summary>
        private static void DrawIcon(DrawingContext dc, string feature, double size, double fitBox)
        {
            object icon = GetFeatureIcon(feature);
            if (icon == null) return;
            var brush = new SolidColorBrush(BadgeIconColor);
            var center = new Point(size / 2, size / 2);

            if (icon is FontIconData fontIconData)
            {
                var typeface = new Typeface(fontIconData.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var text = new FormattedText(fontIconData.Glyph, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, fitBox, brush, 1.0);
                dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
                return;
            }

            var geometry = Geometry.Parse((string)icon);
            Rect bounds = geometry.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            double scale = fitBox / Math.Max(bounds.Width, bounds.Height);
            dc.PushTransform(new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(scale, scale),
                    new TranslateTransform(
                        center.X - (bounds.X + bounds.Width / 2) * scale,
                        center.Y - (bounds.Y + bounds.Height / 2) * scale)
                }
            });
            dc.DrawGeometry(brush, null, geometry);
            dc.Pop();
        }

        /// <summary>在 DrawingVisual 右下角绘制右对齐的半透明 ICC 文字。</summary>
        private static void DrawBadgeText(DrawingContext dc, double size, double fontSize)
        {
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var brush = new SolidColorBrush(BadgeTextColor);
            double right = size - size * 0.08;
            double bottom = size - size * 0.05;

            var line = new FormattedText(BadgeText, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);
            dc.DrawText(line, new Point(right - line.Width, bottom - line.Height));
        }

        /// <summary>把 PNG 字节包装为单帧 ICO 文件（256px PNG 压缩条目）。</summary>
        private static void WriteIco(string path, byte[] png, int size)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            bw.Write((short)0);  // 保留字段
            bw.Write((short)1);  // 类型：图标
            bw.Write((short)1);  // 图像数量

            bw.Write((byte)(size >= 256 ? 0 : size)); // 宽（0 表示 256）
            bw.Write((byte)(size >= 256 ? 0 : size)); // 高
            bw.Write((byte)0);   // 调色板色数
            bw.Write((byte)0);   // 保留
            bw.Write((short)1);  // 颜色平面数
            bw.Write((short)32); // 位深
            bw.Write(png.Length);
            bw.Write(6 + 16);    // 图像数据偏移（ICONDIR + ICONDIRENTRY）

            bw.Write(png);
        }
    }
}
