using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
            LoadSplashImage();
        }

        private void InitializeSplashScreen()
        {
            // 设置窗口居中
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // 设置版本号
            SetVersionText();
            
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
            Dispatcher.Invoke(() =>
            {
                LoadingText.Text = message;
            });
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
                default:
                    return "ICC Autumn.png"; // 默认秋季
            }
        }
    }
}