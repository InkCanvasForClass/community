using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace InkCanvas.DevTools
{
    public partial class MainWindow : Window
    {
        private readonly BuildRunner _runner;
        private bool _isBuilding;

        public MainWindow()
        {
            InitializeComponent();

            string repoRoot = FindRepositoryRoot();
            _runner = new BuildRunner(repoRoot);

            foreach (var project in BuildRunner.Projects)
                ProjectComboBox.Items.Add(project);
            ProjectComboBox.SelectedIndex = 0;

            AppendOutput($"仓库根目录：{repoRoot}\n");
            AppendOutput($"当前项目：{GetSelectedProject()?.DisplayName}\n");
            AppendOutput($"默认模式：{GetSelectedMode()}\n\n");
        }

        /// <summary>
        /// 从当前程序目录向上查找包含 "Ink Canvas.sln" 的目录。
        /// </summary>
        private static string FindRepositoryRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "Ink Canvas.sln")))
                    return dir;
                dir = Directory.GetParent(dir)?.FullName;
            }
            // 兜底：使用工作目录
            return Directory.GetCurrentDirectory();
        }

        private ProjectDefinition GetSelectedProject()
        {
            return ProjectComboBox.SelectedItem as ProjectDefinition;
        }

        private BuildMode GetSelectedMode()
        {
            var item = ModeComboBox.SelectedItem as ComboBoxItem;
            string text = item?.Content?.ToString();
            return text switch
            {
                "Release" => BuildMode.Release,
                "Publish" => BuildMode.Publish,
                _ => BuildMode.Debug
            };
        }

        private void AppendOutput(string text)
        {
            OutputTextBox.AppendText(text);
            OutputTextBox.ScrollToEnd();
        }

        /// <summary>
        /// 根据当前项目是否为可执行项目刷新按钮可用状态。
        /// 只输出 DLL 的项目仅保留“编译”按钮。
        /// </summary>
        private void UpdateButtonStates()
        {
            bool canRun = !_isBuilding && (GetSelectedProject()?.IsExecutable ?? false);

            BuildButton.IsEnabled = !_isBuilding;
            RunButton.IsEnabled = canRun;
            BuildAndRunButton.IsEnabled = canRun;
            KillButton.IsEnabled = canRun;
            ModeComboBox.IsEnabled = !_isBuilding;
            ProjectComboBox.IsEnabled = !_isBuilding;
        }

        private void SetBusy(bool busy)
        {
            _isBuilding = busy;
            UpdateButtonStates();
        }

        private void ProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();

            var project = GetSelectedProject();
            if (project != null)
            {
                AppendOutput($"---- 已选择项目：{project.DisplayName}");
                AppendOutput(project.IsExecutable ? "（可编译/运行）\n\n" : "（仅输出 DLL，只可编译）\n\n");
            }
        }

        /// <summary>
        /// 杀掉目标项目对应的进程并在日志中反馈。
        /// </summary>
        private void KillAndReport(ProjectDefinition project)
        {
            if (project == null || !project.IsExecutable)
            {
                AppendOutput("[杀进程] 当前项目只输出 DLL，无需杀进程。\n");
                return;
            }

            int killed = _runner.KillProjectProcesses(project);
            if (killed > 0)
                AppendOutput($"[杀进程] 已结束 {killed} 个 {project.ProcessName} 进程。\n");
            else
                AppendOutput($"[杀进程] 未发现正在运行的 {project.ProcessName} 进程。\n");
        }

        private async void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuilding) return;

            ProjectDefinition project = GetSelectedProject();
            if (project == null) return;

            SetBusy(true);
            try
            {
                BuildMode mode = GetSelectedMode();
                AppendOutput($"========== 开始编译 {project.DisplayName}（{mode}）==========\n");

                if (project.IsExecutable)
                    KillAndReport(project);

                var progress = new Progress<string>(line => AppendOutput(line));
                int exitCode = await _runner.RunDotnetAsync(project, mode, progress);

                if (exitCode == 0)
                    AppendOutput($"\n[成功] {project.DisplayName} 编译完成。\n");
                else
                    AppendOutput($"\n[失败] {project.DisplayName} 编译返回码 {exitCode}。\n");
            }
            catch (Exception ex)
            {
                AppendOutput($"\n[异常] {ex.Message}\n");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            ProjectDefinition project = GetSelectedProject();
            if (project == null) return;

            BuildMode mode = GetSelectedMode();
            AppendOutput($"========== 运行 {project.DisplayName}（{mode}）==========\n");

            if (_runner.TryLaunch(project, mode, out string message))
                AppendOutput($"[成功] {message}\n\n");
            else
                AppendOutput($"[失败] {message}\n\n");
        }

        private async void BuildAndRunButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuilding) return;

            ProjectDefinition project = GetSelectedProject();
            if (project == null) return;

            SetBusy(true);
            try
            {
                BuildMode mode = GetSelectedMode();
                AppendOutput($"========== 编译并运行 {project.DisplayName}（{mode}）==========\n");

                if (project.IsExecutable)
                    KillAndReport(project);

                var progress = new Progress<string>(line => AppendOutput(line));
                int exitCode = await _runner.RunDotnetAsync(project, mode, progress);

                if (exitCode != 0)
                {
                    AppendOutput($"\n[失败] 编译返回码 {exitCode}，跳过运行。\n");
                    return;
                }

                AppendOutput("\n[成功] 编译完成，正在启动...\n");

                if (_runner.TryLaunch(project, mode, out string message))
                    AppendOutput($"[成功] {message}\n\n");
                else
                    AppendOutput($"[失败] {message}\n\n");
            }
            catch (Exception ex)
            {
                AppendOutput($"\n[异常] {ex.Message}\n");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void KillButton_Click(object sender, RoutedEventArgs e)
        {
            ProjectDefinition project = GetSelectedProject();
            if (project == null) return;

            AppendOutput("========== 杀进程 ==========\n");
            KillAndReport(project);
            AppendOutput("\n");
        }

        /// <summary>
        /// 切换日志区域的自动换行。
        /// </summary>
        private void AutoWrapCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 初始化阶段日志控件可能尚未创建
            if (OutputTextBox == null) return;

            bool wrap = AutoWrapCheckBox.IsChecked == true;
            OutputTextBox.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            OutputTextBox.HorizontalScrollBarVisibility = wrap
                ? ScrollBarVisibility.Disabled
                : ScrollBarVisibility.Auto;
        }
    }
}
