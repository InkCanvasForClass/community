using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace InkCanvas.DevTools
{
    public partial class MainWindow : Window
    {
        private readonly BuildRunner _runner;
        private readonly CleanRunner _cleaner;
        private bool _isBuilding;

        public MainWindow()
        {
            InitializeComponent();

            string repoRoot = FindRepositoryRoot();
            _runner = new BuildRunner(repoRoot);
            _cleaner = new CleanRunner(repoRoot);

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
            CleanButton.IsEnabled = !_isBuilding;
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
        /// 扫描并删除所有子项目的 bin / obj 目录，删除前弹窗确认。
        /// </summary>
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuilding) return;

            SetBusy(true);
            try
            {
                AppendOutput("========== 清理所有 bin/obj ==========\n");

                var progress = new Progress<string>(line => AppendOutput(line));
                CleanScanResult scan = await Task.Run(() => _cleaner.Scan());

                foreach (string skipped in scan.SkippedPaths)
                    AppendOutput($"[跳过] {skipped}（本工具正在运行，产物被占用）\n");

                if (scan.Targets.Count == 0)
                {
                    AppendOutput("[完成] 未找到需要清理的 bin/obj 目录。\n\n");
                    return;
                }

                AppendOutput($"[扫描] 找到 {scan.Targets.Count} 个目录，共 {CleanRunner.FormatBytes(scan.TotalBytes)}。\n");

                const int previewLimit = 10;
                string preview = string.Join("\n", scan.Targets.Take(previewLimit).Select(t => "  " + t.RelativePath));
                if (scan.Targets.Count > previewLimit)
                    preview += $"\n  ……（其余 {scan.Targets.Count - previewLimit} 个）";

                MessageBoxResult result = MessageBox.Show(
                    this,
                    $"将删除以下 {scan.Targets.Count} 个构建产物目录（共 {CleanRunner.FormatBytes(scan.TotalBytes)}）：\n\n{preview}\n\n"
                        + "提示：正在运行的程序（如主程序、MagHost）会锁住 bin 目录，请先结束相关进程。\n"
                        + "此操作不可撤销，是否继续？",
                    "删除所有 bin/obj",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (result != MessageBoxResult.Yes)
                {
                    AppendOutput("[取消] 已取消清理。\n\n");
                    return;
                }

                CleanSummary summary = await Task.Run(() => _cleaner.Delete(scan.Targets, progress));

                AppendOutput($"\n[完成] 删除 {summary.DeletedCount} 个目录，释放 {CleanRunner.FormatBytes(summary.DeletedBytes)}，失败 {summary.FailedCount} 个，用时 {summary.Elapsed.TotalSeconds:0.0} 秒。\n");

                if (summary.FailedCount > 0)
                {
                    AppendOutput("[提示] 失败目录通常被正在运行的程序占用，请先结束相关进程后重试。\n");
                    foreach (string failure in summary.Failures.Take(10))
                        AppendOutput($"  - {failure}\n");
                }

                AppendOutput("\n");
            }
            catch (Exception ex)
            {
                AppendOutput($"\n[异常] {ex.Message}\n\n");
            }
            finally
            {
                SetBusy(false);
            }
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
