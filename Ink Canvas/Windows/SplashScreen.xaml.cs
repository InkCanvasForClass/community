using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.IO;
using Newtonsoft.Json;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// SplashScreen.xaml 的交互逻辑
    /// </summary>
    public partial class SplashScreen : Window
    {
        private DispatcherTimer _timer;
        private int _loadingStep = 0;
        private int _actualSplashStyle = 1; 
        private readonly string[] _loadingMessages = {
            "正在启动 Ink Canvas...",
            "正在初始化组件...",
            "正在加载配置...",
            "正在准备界面...",
            "启动完成！"
        };

        public SplashScreen()
        {
            InitializeComponent();
            InitializeSplashScreen();
        }

        private void InitializeSplashScreen()
        {
            // 设置窗口居中
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // 设置版本号
            SetVersionText();
            
            // 加载启动图片并获取实际样式
            _actualSplashStyle = LoadSplashImageWithStyle();
            
            // 启动加载动画
            StartLoadingAnimation();
        }

        private void StartLoadingAnimation()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1200) 
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_loadingStep < _loadingMessages.Length)
            {
                LoadingText.Text = _loadingMessages[_loadingStep];
                _loadingStep++;
            }
            else
            {
                _timer.Stop();
                // 不要自动关闭启动画面，等待外部调用CloseSplashScreen
            }
        }

        public void CloseSplashScreen()
        {
            // 添加淡出动画
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOutAnimation.Completed += (s, e) => 
            {
                this.Close();
            };

            this.BeginAnimation(OpacityProperty, fadeOutAnimation);
        }

        /// <summary>
        /// 设置加载进度（0-100）
        /// </summary>
        /// <param name="progress">进度百分比</param>
        public void SetProgress(int progress)
        {
            Dispatcher.Invoke(() =>
            {
                // 设置进度条颜色
                SetProgressBarColor();
                
                // 获取进度条容器的实际宽度
                double containerWidth = ProgressBarBackground.ActualWidth;
                if (containerWidth <= 0)
                {
                    // 如果ActualWidth为0，使用设计时宽度
                    containerWidth = 530;
                }
                
                // 计算目标宽度
                double targetWidth = containerWidth * (progress / 100.0);
                
                // 创建Storyboard动画
                var storyboard = new Storyboard();
                
                // 创建宽度动画
                var widthAnimation = new DoubleAnimation
                {
                    From = ProgressBarFill.Width,
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                // 设置动画目标
                Storyboard.SetTarget(widthAnimation, ProgressBarFill);
                Storyboard.SetTargetProperty(widthAnimation, new PropertyPath(Border.WidthProperty));
                
                // 添加动画到Storyboard
                storyboard.Children.Add(widthAnimation);
                
                // 添加动画完成事件
                storyboard.Completed += (s, e) =>
                {
                    // 确保最终值正确设置
                    ProgressBarFill.Width = targetWidth;
                    
                    // 根据进度调整圆角
                    if (progress >= 100)
                    {
                        // 进度100%时，底部角都是圆角
                        ProgressBarFill.CornerRadius = new CornerRadius(0, 0, 7, 7);
                    }
                    else
                    {
                        // 进度未满时，只有左侧是圆角
                        ProgressBarFill.CornerRadius = new CornerRadius(0, 0, 0, 7);
                    }
                };
                
                // 开始动画
                storyboard.Begin();
            });
        }

        /// <summary>
        /// 设置加载消息
        /// </summary>
        /// <param name="message">加载消息</param>
        public void SetLoadingMessage(string message)
        {
            // 使用实际选择的样式
            SetLoadingMessage(message, _actualSplashStyle);
        }

        public void SetLoadingMessage(string message, int actualSplashStyle)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = message;
                
                // 根据实际启动动画样式调整加载文本样式
                if (actualSplashStyle == 6) // 马年限定
                {
                    // 马年限定样式
                    LoadingText.FontSize = 12;
                    LoadingText.FontWeight = FontWeights.SemiBold;
                    LoadingText.Foreground = Brushes.White;
                    LoadingText.HorizontalAlignment = HorizontalAlignment.Center;
                    LoadingText.Margin = new Thickness(0, -135, 145, 140);
                }
                else
                {
                    // 默认样式
                    LoadingText.FontSize = 18;
                    LoadingText.FontWeight = FontWeights.SemiBold;
                    LoadingText.Foreground = Brushes.White;
                    LoadingText.HorizontalAlignment = HorizontalAlignment.Center;
                    LoadingText.Margin = new Thickness(0, -150, 0, 140);
                }
            });
        }

        /// <summary>
        /// 获取当前启动动画样式
        /// </summary>
        /// <returns>启动动画样式索引</returns>
        private int GetCurrentSplashStyle()
        {
            try
            {
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    if (obj?["appearance"]?["splashScreenStyle"] != null)
                    {
                        return (int)obj["appearance"]["splashScreenStyle"];
                    }
                }
                return 1; // 默认跟随四季
            }
            catch
            {
                return 1; // 默认跟随四季
            }
        }

        /// <summary>
        /// 设置版本号文本
        /// </summary>
        private void SetVersionText()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    VersionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                }
                else
                {
                    VersionTextBlock.Text = "v5.0.4.0";
                }
            }
            catch
            {
                VersionTextBlock.Text = "v5.0.4.0";
            }
        }

        /// <summary>
        /// 加载启动图片
        /// </summary>
        private void LoadSplashImage()
        {
            try
            {
                string imagePath = GetSplashImagePath();
                if (!string.IsNullOrEmpty(imagePath))
                {
                    StartupImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认图片
                System.Diagnostics.Debug.WriteLine($"加载启动图片失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载启动图片并返回实际样式
        /// </summary>
        /// <returns>实际选择的样式</returns>
        public int LoadSplashImageWithStyle()
        {
            try
            {
                int actualStyle;
                string imagePath = GetSplashImagePath(out actualStyle);
                if (!string.IsNullOrEmpty(imagePath))
                {
                    StartupImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagePath));
                }
                return actualStyle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载启动图片失败: {ex.Message}");
                return GetActualStyle(1); 
            }
        }

        /// <summary>
        /// 根据设置获取启动图片路径
        /// </summary>
        private string GetSplashImagePath()
        {
            try
            {
                // 读取设置
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                int splashStyle = 1; // 默认跟随四季
                
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    if (obj?["appearance"]?["splashScreenStyle"] != null)
                    {
                        splashStyle = (int)obj["appearance"]["splashScreenStyle"];
                    }
                }

                // 根据样式选择图片
                string imageName = GetImageNameByStyle(splashStyle);
                return $"pack://application:,,,/Resources/Startup-animation/{imageName}";
            }
            catch
            {
                string imageName = GetImageNameByStyle(1); 
                return $"pack://application:,,,/Resources/Startup-animation/{imageName}";
            }
        }

        /// <summary>
        /// 根据设置获取启动图片路径和实际样式
        /// </summary>
        /// <param name="actualStyle">返回实际选择的样式</param>
        /// <returns>图片路径</returns>
        private string GetSplashImagePath(out int actualStyle)
        {
            try
            {
                // 读取设置
                var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "Settings.json");
                int splashStyle = 1; // 默认跟随四季
                
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    dynamic obj = JsonConvert.DeserializeObject(json);
                    if (obj?["appearance"]?["splashScreenStyle"] != null)
                    {
                        splashStyle = (int)obj["appearance"]["splashScreenStyle"];
                    }
                }

                // 根据样式选择图片，并获取实际样式
                actualStyle = GetActualStyle(splashStyle);
                string imageName = GetImageNameByStyle(splashStyle);
                return $"pack://application:,,,/Resources/Startup-animation/{imageName}";
            }
            catch
            {
                actualStyle = GetActualStyle(1);
                string imageName = GetImageNameByStyle(1); 
                return $"pack://application:,,,/Resources/Startup-animation/{imageName}";
            }
        }

        /// <summary>
        /// 获取实际样式
        /// </summary>
        /// <param name="style">设置中的样式</param>
        /// <returns>实际选择的样式</returns>
        private int GetActualStyle(int style)
        {
            switch (style)
            {
                case 0: // 随机
                    var random = new Random();
                    var randomStyles = new[] { 2, 3, 4, 5, 6 }; // 春季、夏季、秋季、冬季、马年限定
                    return randomStyles[random.Next(randomStyles.Length)];
                
                case 1: // 跟随四季
                    var month = DateTime.Now.Month;
                    if (month >= 3 && month <= 5) return 2; // 春季
                    if (month >= 6 && month <= 8) return 3; // 夏季
                    if (month >= 9 && month <= 11) return 4; // 秋季
                    return 5; // 冬季
                
                default:
                    return style; 
            }
        }

        /// <summary>
        /// 根据样式获取图片名称
        /// </summary>
        private string GetImageNameByStyle(int style)
        {
            switch (style)
            {
                case 0: // 随机
                    var random = new Random();
                    var randomStyles = new[] { 2, 3, 4, 5, 6 }; // 春季、夏季、秋季、冬季、马年限定
                    return GetImageNameByStyle(randomStyles[random.Next(randomStyles.Length)]);
                
                case 1: // 跟随四季
                    var month = DateTime.Now.Month;
                    if (month >= 3 && month <= 5) return GetImageNameByStyle(2); // 春季
                    if (month >= 6 && month <= 8) return GetImageNameByStyle(3); // 夏季
                    if (month >= 9 && month <= 11) return GetImageNameByStyle(4); // 秋季
                    return GetImageNameByStyle(5); // 冬季
                
                case 2: // 春季
                    return "ICC Spring.png";
                case 3: // 夏季
                    return "ICC Summer.png";
                case 4: // 秋季
                    return "ICC Autumn.png";
                case 5: // 冬季
                    return "ICC Winter.png";
                case 6: // 马年限定
                    return "ICC Horse.png";
                default:// 默认返回
                    return "ICC Horse.png"; 
            }
        }

        /// <summary>
        /// 根据实际样式设置进度条颜色
        /// </summary>
        private void SetProgressBarColor()
        {
            Color progressColor;
            
            switch (_actualSplashStyle)
            {
                case 2: // 春季 - H=136, S=15, L=22
                    progressColor = HslToRgb(136, 15, 22);
                    break;
                case 3: // 夏季 - H=6, S=15, L=22
                    progressColor = HslToRgb(6, 15, 22);
                    break;
                case 4: // 秋季 - H=39, S=15, L=22
                    progressColor = HslToRgb(39, 15, 22);
                    break;
                case 5: // 冬季 - H=204, S=15, L=22
                    progressColor = HslToRgb(204, 15, 22);
                    break;
                case 6: // 马年限定 - 白色
                    progressColor = Colors.White;
                    break;
                default: // 默认使用
                    progressColor = Colors.White;
                    break;
            }
            
            // 创建渐变画刷
            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 0)
            };
            
            // 根据颜色类型设置渐变
            if (_actualSplashStyle == 6) // 马年限定使用白色渐变
            {
                gradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0));
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, 255, 255, 255), 0.5));
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 255, 255), 1));
            }
            else // 其他样式使用HSL颜色的渐变
            {
                var lighterColor = Color.FromArgb(255, 
                    (byte)Math.Min(255, progressColor.R + 30),
                    (byte)Math.Min(255, progressColor.G + 30),
                    (byte)Math.Min(255, progressColor.B + 30));
                var darkerColor = Color.FromArgb(255,
                    (byte)Math.Max(0, progressColor.R - 30),
                    (byte)Math.Max(0, progressColor.G - 30),
                    (byte)Math.Max(0, progressColor.B - 30));
                
                gradientBrush.GradientStops.Add(new GradientStop(lighterColor, 0));
                gradientBrush.GradientStops.Add(new GradientStop(progressColor, 0.5));
                gradientBrush.GradientStops.Add(new GradientStop(darkerColor, 1));
            }
            
            ProgressBarFill.Background = gradientBrush;
        }

        /// <summary>
        /// 将HSL颜色转换为RGB颜色
        /// </summary>
        /// <param name="h">色相 (0-360)</param>
        /// <param name="s">饱和度 (0-100)</param>
        /// <param name="l">亮度 (0-100)</param>
        /// <returns>RGB颜色</returns>
        private Color HslToRgb(double h, double s, double l)
        {
            // 将HSL值转换为0-1范围
            h = h / 360.0;
            s = s / 100.0;
            l = l / 100.0;

            double r, g, b;

            if (s == 0)
            {
                // 无饱和度，为灰度
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;

                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromRgb(
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255)
            );
        }

        /// <summary>
        /// HSL颜色转换辅助方法
        /// </summary>
        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}