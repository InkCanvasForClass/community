# 编译规范

## 编译流程

每次编译前必须按以下顺序执行：

1. **杀掉所有 inkcanvasforclass 进程**
2. **删除项目的 4 个 bin 和 4 个 obj 目录**
3. **再执行编译**

## 使用 PowerShell 完整脚本

```powershell
# 1. 杀掉所有 inkcanvasforclass 进程
Get-Process -Name "*inkcanvas*" -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. 删除所有 bin 和 obj 目录
$projectRoot = "c:\Users\PrefacedCorg\Documents\GitHub\community"
Get-ChildItem -Path $projectRoot -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -Path $projectRoot -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

# 3. 执行编译（使用 dotnet build）
& "c:\Program Files\dotnet\dotnet.exe" build "$projectRoot\Ink Canvas.sln"
```

## 手动操作步骤

如果不使用脚本，可以手动执行：

1. 打开任务管理器（Ctrl+Shift+Esc）
2. 找到所有名称包含 "inkcanvas" 的进程，右键 → 结束任务
3. 在项目根目录下删除所有 `bin` 和 `obj` 文件夹
4. 在命令行中运行：
   ```powershell
   & "c:\Program Files\dotnet\dotnet.exe" build "c:\Users\PrefacedCorg\Documents\GitHub\community\Ink Canvas.sln"
   ```
5. 或者在 Visual Studio 中点击生成 → 重新生成解决方案
