using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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
                if (LoadingProgress.IsIndeterminate)
                {
                    LoadingProgress.IsIndeterminate = false;
                    LoadingProgress.Value = 0; 
                }
                
                // 创建平滑过渡动画
                var animation = new DoubleAnimation
                {
                    From = LoadingProgress.Value,
                    To = progress,
                    Duration = TimeSpan.FromMilliseconds(300), 
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                // 添加动画完成事件
                animation.Completed += (s, e) =>
                {
                    // 确保最终值正确设置
                    LoadingProgress.Value = progress;
                };
                
                LoadingProgress.BeginAnimation(ProgressBar.ValueProperty, animation);
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
    }
}