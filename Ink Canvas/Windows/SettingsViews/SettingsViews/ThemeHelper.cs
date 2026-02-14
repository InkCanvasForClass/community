using System.Windows.Media;
using Ink_Canvas;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// 主题辅助类：提供统一的主题颜色资源
    /// </summary>
    public static class ThemeHelper
    {
        private static bool IsToggleSwitchThumb(System.Windows.Controls.Border border)
        {
            try
            {
                if (border == null) return false;

                // ToggleSwitch thumb 常见尺寸：19x19，圆角 10，父级为 48x25 的开关背景
                if (border.Width < 16 || border.Width > 24 || border.Height < 16 || border.Height > 24)
                    return false;

                if (border.CornerRadius.TopLeft < 8) return false;

                if (border.Parent is System.Windows.Controls.Border parent)
                {
                    if (parent.Width >= 40 && parent.Width <= 60 &&
                        parent.Height >= 20 && parent.Height <= 35 &&
                        parent.CornerRadius.TopLeft >= 10)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 检查当前是否为深色主题
        /// </summary>
        public static bool IsDarkTheme
        {
            get
            {
                if (MainWindow.Settings?.Appearance == null) return false;
                return MainWindow.Settings.Appearance.Theme == 1 ||
                       (MainWindow.Settings.Appearance.Theme == 2 && !IsSystemThemeLight());
            }
        }

        /// <summary>
        /// 检查系统主题是否为浅色
        /// </summary>
        private static bool IsSystemThemeLight()
        {
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey("software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                var keyValue = 0;
                if (themeKey != null) keyValue = (int)themeKey.GetValue("SystemUsesLightTheme");
                return keyValue == 1;
            }
            catch
            {
                return true; // 默认返回浅色主题
            }
        }

        // 文字颜色 - 参考 Windows 系统设置
        public static Color TextPrimary => IsDarkTheme ? Color.FromRgb(243, 243, 243) : Color.FromRgb(0, 0, 0); // Windows 系统主文字颜色
        public static Color TextSecondary => IsDarkTheme ? Color.FromRgb(200, 200, 200) : Color.FromRgb(96, 96, 96); // Windows 系统次要文字颜色
        public static Color TextTertiary => IsDarkTheme ? Color.FromRgb(161, 161, 161) : Color.FromRgb(120, 120, 120); // Windows 系统三级文字颜色

        // 背景颜色 - 参考 Windows 系统设置
        public static Color BackgroundPrimary => IsDarkTheme ? Color.FromRgb(32, 32, 32) : Color.FromRgb(255, 255, 255); // Windows 系统主背景
        public static Color BackgroundSecondary => IsDarkTheme ? Color.FromRgb(25, 25, 25) : Color.FromRgb(243, 243, 243); // Windows 系统次要背景（侧边栏等）
        public static Color BackgroundTertiary => IsDarkTheme ? Color.FromRgb(43, 43, 43) : Color.FromRgb(237, 237, 237); // Windows 系统三级背景（按钮等）

        // 边框颜色 - 参考 Windows 系统设置
        public static Color BorderPrimary => IsDarkTheme ? Color.FromRgb(62, 62, 62) : Color.FromRgb(229, 229, 229); // Windows 系统主边框
        public static Color BorderSecondary => IsDarkTheme ? Color.FromRgb(55, 55, 55) : Color.FromRgb(220, 220, 220); // Windows 系统次要边框
        public static Color BorderTertiary => IsDarkTheme ? Color.FromRgb(70, 70, 70) : Color.FromRgb(211, 211, 211); // Windows 系统三级边框

        // 分隔线颜色 - 参考 Windows 系统设置
        public static Color Separator => IsDarkTheme ? Color.FromRgb(62, 62, 62) : Color.FromRgb(237, 237, 237); // Windows 系统分隔线

        // 选中/高亮颜色 - 参考 Windows 系统设置
        public static Color SelectedBackground => IsDarkTheme ? Color.FromRgb(62, 62, 62) : Color.FromRgb(237, 237, 237); // Windows 系统选中背景
        public static Color HoverBackground => IsDarkTheme ? Color.FromRgb(43, 43, 43) : Color.FromRgb(243, 243, 243); // Windows 系统悬停背景

        // 按钮颜色 - 参考 Windows 系统设置
        public static Color ButtonBackground => IsDarkTheme ? Color.FromRgb(43, 43, 43) : Color.FromRgb(237, 237, 237); // Windows 系统按钮背景
        public static Color ButtonHoverBackground => IsDarkTheme ? Color.FromRgb(55, 55, 55) : Color.FromRgb(220, 220, 220); // Windows 系统按钮悬停背景

        // 文本框颜色 - 参考 Windows 系统设置
        public static Color TextBoxBackground => IsDarkTheme ? Color.FromRgb(43, 43, 43) : Color.FromRgb(255, 255, 255); // Windows 系统文本框背景
        public static Color TextBoxBorder => IsDarkTheme ? Color.FromRgb(62, 62, 62) : Color.FromRgb(229, 229, 229); // Windows 系统文本框边框

        // 滚动条颜色 - 参考 Windows 系统设置
        public static Color ScrollBarTrack => IsDarkTheme ? Color.FromRgb(25, 25, 25) : Color.FromRgb(243, 243, 243); // Windows 系统滚动条轨道
        public static Color ScrollBarThumb => IsDarkTheme ? Color.FromRgb(122, 122, 122) : Color.FromRgb(191, 191, 191); // Windows 系统滚动条滑块
        public static Color ScrollBarThumbHover => IsDarkTheme ? Color.FromRgb(150, 150, 150) : Color.FromRgb(138, 138, 138); // Windows 系统滚动条滑块悬停

        // 转换为 SolidColorBrush
        public static SolidColorBrush GetTextPrimaryBrush() => new SolidColorBrush(TextPrimary);
        public static SolidColorBrush GetTextSecondaryBrush() => new SolidColorBrush(TextSecondary);
        public static SolidColorBrush GetTextTertiaryBrush() => new SolidColorBrush(TextTertiary);
        public static SolidColorBrush GetBackgroundPrimaryBrush() => new SolidColorBrush(BackgroundPrimary);
        public static SolidColorBrush GetBackgroundSecondaryBrush() => new SolidColorBrush(BackgroundSecondary);
        public static SolidColorBrush GetBackgroundTertiaryBrush() => new SolidColorBrush(BackgroundTertiary);
        public static SolidColorBrush GetBorderPrimaryBrush() => new SolidColorBrush(BorderPrimary);
        public static SolidColorBrush GetBorderSecondaryBrush() => new SolidColorBrush(BorderSecondary);
        public static SolidColorBrush GetBorderTertiaryBrush() => new SolidColorBrush(BorderTertiary);
        public static SolidColorBrush GetSeparatorBrush() => new SolidColorBrush(Separator);
        public static SolidColorBrush GetSelectedBackgroundBrush() => new SolidColorBrush(SelectedBackground);
        public static SolidColorBrush GetHoverBackgroundBrush() => new SolidColorBrush(HoverBackground);
        public static SolidColorBrush GetButtonBackgroundBrush() => new SolidColorBrush(ButtonBackground);
        public static SolidColorBrush GetButtonHoverBackgroundBrush() => new SolidColorBrush(ButtonHoverBackground);
        public static SolidColorBrush GetTextBoxBackgroundBrush() => new SolidColorBrush(TextBoxBackground);
        public static SolidColorBrush GetTextBoxBorderBrush() => new SolidColorBrush(TextBoxBorder);
        public static SolidColorBrush GetScrollBarTrackBrush() => new SolidColorBrush(ScrollBarTrack);
        public static SolidColorBrush GetScrollBarThumbBrush() => new SolidColorBrush(ScrollBarThumb);
        public static SolidColorBrush GetScrollBarThumbHoverBrush() => new SolidColorBrush(ScrollBarThumbHover);

        /// <summary>
        /// 更新控件的文字颜色
        /// </summary>
        public static void UpdateTextBlockColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.TextBlock textBlock)
                {
                    var foreground = textBlock.Foreground as SolidColorBrush;
                    if (foreground != null)
                    {
                        var color = foreground.Color;
                        // 检查是否是硬编码的浅色主题颜色
                        if (color.R == 46 && color.G == 52 && color.B == 54) // #2e3436 - 主文字
                        {
                            textBlock.Foreground = GetTextPrimaryBrush();
                        }
                        else if (color.R == 154 && color.G == 153 && color.B == 150) // #9a9996 - 次要文字
                        {
                            textBlock.Foreground = GetTextSecondaryBrush();
                        }
                        else if (color.R == 34 && color.G == 34 && color.B == 34) // #222222 - 深色文字
                        {
                            textBlock.Foreground = GetTextPrimaryBrush();
                        }
                    }
                }
                UpdateTextBlockColors(child);
            }
        }

        /// <summary>
        /// 更新控件的边框和背景颜色
        /// </summary>
        public static void UpdateBorderColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Border border)
                {
                    var background = border.Background as SolidColorBrush;
                    if (background != null)
                    {
                        var color = background.Color;
                        // 检查是否是硬编码的浅色主题颜色
                        if (color.R == 235 && color.G == 235 && color.B == 235) // #ebebeb - 分隔线
                        {
                            border.Background = GetSeparatorBrush();
                        }
                        else if (color.R == 217 && color.G == 217 && color.B == 217) // #d9d9d9 - 按钮背景
                        {
                            border.Background = GetButtonBackgroundBrush();
                        }
                        else if (color.R == 225 && color.G == 225 && color.B == 225) // #e1e1e1 - 按钮背景/分隔线
                        {
                            // 检查是否是按钮（有内边距或特定尺寸）
                            if (border.Padding.Left > 0 || border.Padding.Top > 0 || 
                                (border.Width > 0 && border.Height > 0 && border.Width < 200 && border.Height < 100))
                            {
                                border.Background = GetButtonBackgroundBrush();
                            }
                            else
                            {
                                // 可能是分隔线
                                border.Background = GetSeparatorBrush();
                            }
                        }
                        else if (color.R == 255 && color.G == 255 && color.B == 255) // White - 白色背景
                        {
                            // 检查是否是搜索结果项（有圆角和内边距）
                            if (border.CornerRadius.TopLeft == 6 && border.CornerRadius.TopRight == 6 && 
                                border.CornerRadius.BottomLeft == 6 && border.CornerRadius.BottomRight == 6 &&
                                border.Padding.Left > 0 && border.Padding.Top > 0)
                            {
                                // 搜索结果项背景
                                border.Background = IsDarkTheme 
                                    ? new SolidColorBrush(Color.FromRgb(43, 43, 43)) // 深色主题搜索结果项背景
                                    : new SolidColorBrush(Colors.White);
                            }
                            else
                            {
                                // ToggleSwitch thumb 应保持白色，不参与主题背景替换
                                if (!IsToggleSwitchThumb(border))
                                {
                                    // 其他白色背景（如搜索框）
                                    border.Background = GetTextBoxBackgroundBrush();
                                }
                            }
                        }
                        else if (color.R == 250 && color.G == 250 && color.B == 250) // #fafafa - 主背景
                        {
                            border.Background = GetBackgroundPrimaryBrush();
                        }
                    }
                    
                    var borderBrush = border.BorderBrush as SolidColorBrush;
                    if (borderBrush != null)
                    {
                        var color = borderBrush.Color;
                        if (color.R == 230 && color.G == 230 && color.B == 230) // #e6e6e6 - 边框
                        {
                            border.BorderBrush = GetBorderPrimaryBrush();
                        }
                        else if (color.R == 225 && color.G == 225 && color.B == 225) // #e1e1e1 - 边框
                        {
                            border.BorderBrush = GetBorderPrimaryBrush();
                        }
                        else if (color.R == 211 && color.G == 211 && color.B == 211) // #d3d3d3 - 边框
                        {
                            border.BorderBrush = GetBorderTertiaryBrush();
                        }
                    }
                }
                UpdateBorderColors(child);
            }
        }

        /// <summary>
        /// 更新控件的线条颜色
        /// </summary>
        public static void UpdateLineColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Shapes.Line line)
                {
                    var stroke = line.Stroke as SolidColorBrush;
                    if (stroke != null)
                    {
                        var color = stroke.Color;
                        if (color.R == 211 && color.G == 211 && color.B == 211) // #d3d3d3 - 分隔线
                        {
                            line.Stroke = GetSeparatorBrush();
                        }
                    }
                }
                UpdateLineColors(child);
            }
        }

        /// <summary>
        /// 更新控件的文本框和组合框颜色
        /// </summary>
        public static void UpdateInputControlsColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.TextBox textBox)
                {
                    var foreground = textBox.Foreground as SolidColorBrush;
                    if (foreground != null)
                    {
                        var color = foreground.Color;
                        if (color.R == 46 && color.G == 52 && color.B == 54) // #2e3436
                        {
                            textBox.Foreground = GetTextPrimaryBrush();
                        }
                    }
                    var background = textBox.Background as SolidColorBrush;
                    if (background != null)
                    {
                        var color = background.Color;
                        if (color.R == 255 && color.G == 255 && color.B == 255) // 白色背景
                        {
                            textBox.Background = IsDarkTheme ? GetBackgroundSecondaryBrush() : new SolidColorBrush(System.Windows.Media.Colors.White);
                        }
                    }
                }
                else if (child is System.Windows.Controls.ComboBox comboBox)
                {
                    var foreground = comboBox.Foreground as SolidColorBrush;
                    if (foreground != null)
                    {
                        var color = foreground.Color;
                        if (color.R == 46 && color.G == 52 && color.B == 54) // #2e3436
                        {
                            comboBox.Foreground = GetTextPrimaryBrush();
                        }
                    }
                }
                UpdateInputControlsColors(child);
            }
        }

        /// <summary>
        /// 更新控件的按钮颜色
        /// </summary>
        public static void UpdateButtonColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Button button)
                {
                    var background = button.Background as SolidColorBrush;
                    if (background != null)
                    {
                        var color = background.Color;
                        // 检查是否是默认按钮（没有特定背景色）或特定颜色的按钮
                        if (color.R == 37 && color.G == 99 && color.B == 235) // #2563eb - 蓝色按钮（保持原色）
                        {
                            // 蓝色按钮保持原色，但更新文字颜色
                            if (button.Foreground is SolidColorBrush fgBrush && 
                                fgBrush.Color.R == 255 && fgBrush.Color.G == 255 && fgBrush.Color.B == 255)
                            {
                                button.Foreground = new SolidColorBrush(Colors.White); // 保持白色文字
                            }
                        }
                        else if (color.R == 255 && color.G == 255 && color.B == 255) // 白色背景按钮
                        {
                            button.Background = GetButtonBackgroundBrush();
                            button.Foreground = GetTextPrimaryBrush();
                        }
                        else if (color.A == 0 || color == System.Windows.Media.Colors.Transparent) // 透明背景
                        {
                            // 透明背景按钮，只更新文字颜色
                            button.Foreground = GetTextPrimaryBrush();
                        }
                    }
                    else
                    {
                        // 没有背景色的按钮，更新文字颜色
                        button.Foreground = GetTextPrimaryBrush();
                    }
                }
                UpdateButtonColors(child);
            }
        }

        /// <summary>
        /// 更新控件中的图标颜色
        /// </summary>
        public static void UpdateImageIconColors(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.Image image && image.Source is DrawingImage drawingImage)
                {
                    if (drawingImage.Drawing is DrawingGroup drawingGroup)
                    {
                        Color iconColor = IsDarkTheme 
                            ? Color.FromRgb(243, 243, 243) // 深色主题使用浅色图标 #F3F3F3
                            : Color.FromRgb(34, 34, 34);   // 浅色主题使用深色图标 #222222
                        
                        // 检查图标是否使用了深色（需要更新的颜色）
                        bool needsUpdate = false;
                        foreach (var drawing in drawingGroup.Children)
                        {
                            if (drawing is GeometryDrawing geometryDrawing)
                            {
                                if (geometryDrawing.Brush is SolidColorBrush brush)
                                {
                                    var color = brush.Color;
                                    if (color.R == 34 && color.G == 34 && color.B == 34) // #222222
                                    {
                                        needsUpdate = true;
                                        break;
                                    }
                                }
                                if (geometryDrawing.Pen?.Brush is SolidColorBrush penBrush)
                                {
                                    var color = penBrush.Color;
                                    if (color.R == 34 && color.G == 34 && color.B == 34) // #222222
                                    {
                                        needsUpdate = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (needsUpdate)
                        {
                            // 克隆并更新图标颜色
                            var clonedDrawing = CloneDrawingGroupForTheme(drawingGroup, iconColor);
                            image.Source = new DrawingImage { Drawing = clonedDrawing };
                        }
                    }
                }
                UpdateImageIconColors(child);
            }
        }

        /// <summary>
        /// 克隆 DrawingGroup 并更新颜色（用于主题适配）
        /// </summary>
        private static DrawingGroup CloneDrawingGroupForTheme(DrawingGroup source, Color newColor)
        {
            var cloned = new DrawingGroup();
            cloned.ClipGeometry = source.ClipGeometry?.Clone();
            cloned.Opacity = source.Opacity;
            cloned.Transform = source.Transform?.Clone();

            foreach (var drawing in source.Children)
            {
                if (drawing is GeometryDrawing geometryDrawing)
                {
                    var clonedGeometry = geometryDrawing.Geometry?.Clone();
                    var clonedBrush = CloneBrushForTheme(geometryDrawing.Brush, newColor);
                    var clonedPen = geometryDrawing.Pen != null 
                        ? ClonePenForTheme(geometryDrawing.Pen, newColor) 
                        : null;

                    cloned.Children.Add(new GeometryDrawing(clonedBrush, clonedPen, clonedGeometry));
                }
                else if (drawing is DrawingGroup subGroup)
                {
                    cloned.Children.Add(CloneDrawingGroupForTheme(subGroup, newColor));
                }
                else
                {
                    cloned.Children.Add(drawing);
                }
            }

            return cloned;
        }

        /// <summary>
        /// 克隆 Brush 并更新颜色（用于主题适配）
        /// </summary>
        private static Brush CloneBrushForTheme(Brush source, Color newColor)
        {
            if (source is SolidColorBrush solidBrush)
            {
                var originalColor = solidBrush.Color;
                if (originalColor.R == 34 && originalColor.G == 34 && originalColor.B == 34) // #222222
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                else if (originalColor.A > 0 && originalColor != Colors.Transparent && 
                         originalColor.R < 50 && originalColor.G < 50 && originalColor.B < 50) // 深色
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                return new SolidColorBrush(originalColor) { Opacity = solidBrush.Opacity };
            }
            return source?.Clone();
        }

        /// <summary>
        /// 克隆 Pen 并更新颜色（用于主题适配）
        /// </summary>
        private static Pen ClonePenForTheme(Pen source, Color newColor)
        {
            var clonedBrush = CloneBrushForTheme(source.Brush, newColor);
            return new Pen(clonedBrush, source.Thickness)
            {
                StartLineCap = source.StartLineCap,
                EndLineCap = source.EndLineCap,
                LineJoin = source.LineJoin,
                MiterLimit = source.MiterLimit
            };
        }

        /// <summary>
        /// 应用主题到整个控件树
        /// </summary>
        public static void ApplyThemeToControl(System.Windows.DependencyObject control)
        {
            UpdateTextBlockColors(control);
            UpdateBorderColors(control);
            UpdateLineColors(control);
            UpdateInputControlsColors(control);
            UpdateButtonColors(control);
            UpdateImageIconColors(control);
        }
    }
}

