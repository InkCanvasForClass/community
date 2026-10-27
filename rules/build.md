# 编译规范

## 约定

- 下文所有路径均**相对于仓库根目录**（即 `Ink Canvas.sln` 所在目录），命令请在仓库根目录下执行。
- 当前开发分支：**net6**（使用 Augment MCP 检索代码时务必指定该分支，否则调用失败）。
- `dotnet` 已在 PATH 中，直接用 `dotnet` 即可，不需要写全路径。

## 编译命令

### 1. 主解决方案（默认，Debug x64）

```powershell
dotnet build "Ink Canvas.sln" -c Debug -p:Platform=x64
```

**默认配置为 `Debug` + `x64`，任何代码修改完成后都要跑这条命令做构建校验。**

### 2. PowerPoint 加载项（VSTO，不在 sln 内）

`InkCanvas.PowerPointAddIn` 是 net472 VSTO 项目，**不在 `Ink Canvas.sln` 里**，`dotnet build` 也无法编译 VSTO，必须单独用 VS 2022 自带的 MSBuild：

```powershell
& "<VS2022 安装目录>\MSBuild\Current\Bin\MSBuild.exe" "InkCanvas.PowerPointAddIn\InkCanvas.PowerPointAddIn.csproj" -p:Configuration=Debug
```

`<VS2022 安装目录>` 形如 `C:\Program Files\Microsoft Visual Studio\2022\<Edition>`，`<Edition>` 取决于本机装的是 Community / Professional / Enterprise；不确定时用 `vswhere` 查：

```powershell
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
```

> 改动涉及 PPT 联动 / ppt-agent / `InkCanvas.PptAgent.Contracts` 时，**两条命令都要跑**。

### 3. 编译单个子项目

```powershell
dotnet build "InkCanvas.Controls\InkCanvas.Controls.csproj"
dotnet build "InkCanvas.PluginSdk\InkCanvas.PluginSdk.csproj"
dotnet build "InkCanvas.SettingsTreeView\InkCanvas.SettingsTreeView.csproj"
```

## 项目列表

### 解决方案内（7 个）

| 项目 | csproj 路径 | 目标框架 | sln 平台映射 |
| --- | --- | --- | --- |
| InkCanvasForClass（主应用） | `Ink Canvas/InkCanvasForClass.csproj` | net6.0-windows10.0.19041.0 | `Debug\|x64` → `Debug\|x64`；`Release\|x64` → `Release\|Any CPU` |
| InkCanvas.Controls | `InkCanvas.Controls/InkCanvas.Controls.csproj` | net6.0-windows10.0.19041.0 | 全部 Any CPU |
| InkCanvas.PluginSdk | `InkCanvas.PluginSdk/InkCanvas.PluginSdk.csproj` | net6.0-windows10.0.19041.0 | 全部 Any CPU |
| InkCanvas.SettingsTreeView | `InkCanvas.SettingsTreeView/InkCanvas.SettingsTreeView.csproj` | net6.0-windows10.0.19041.0 | 全部 Any CPU |
| InkCanvas.PptAgent.Contracts | `InkCanvas.PPTAgent.Contracts/InkCanvas.PPTAgent.Contracts.csproj` | netstandard2.0 | 全部 Any CPU |
| InkCanvas.IACoreHelper | `InkCanvas.IACoreHelper/InkCanvas.IACoreHelper.csproj` | net472 | **所有配置一律映射到 x86** |
| InkCanvas.LiquidGlassMagHost | `InkCanvas.LiquidGlassMagHost/InkCanvas.LiquidGlassMagHost.csproj` | net6.0-windows10.0.19041.0 | 真·多平台：AnyCPU/ARM→x64，ARM64→ARM64，x86→x86 |

> 注意目录名与项目名不一致：`InkCanvas.PptAgent.Contracts` 位于 `InkCanvas.PPTAgent.Contracts/`。
> `Ink Canvas/InkCanvasForClass_*_wpftmp.csproj`、`InkCanvas.Controls/*_wpftmp.csproj` 是 WPF 编译中间产物，**不是真实项目，不要改**。

### 解决方案外（2 个）

| 项目 | csproj 路径 | 目标框架 | 说明 |
| --- | --- | --- | --- |
| InkCanvas.PowerPointAddIn | `InkCanvas.PowerPointAddIn/InkCanvas.PowerPointAddIn.csproj` | net472（VSTO） | 单独用 MSBuild 编译，见上文命令 2 |
| InkCanvas.NativeInk.Tests | `InkCanvas.NativeInk.Tests/InkCanvas.NativeInk.Tests.csproj` | net6.0-windows10.0.19041.0 | 原生墨迹手动验证程序，按需单独编译 |

## 主项目 MSBuild 目标（改动构建流程前必读）

`Ink Canvas/InkCanvasForClass.csproj` 里有几个自定义 Target，改路径/平台时容易踩：

| Target | 时机 | 作用 |
| --- | --- | --- |
| `CopyVstoAgent` | AfterTargets=Build | 把 `InkCanvas.PowerPointAddIn.dll/.vsto/.dll.manifest`、`Microsoft.Office.Tools.*`、`Microsoft.VisualStudio.Tools.Applications.Runtime.dll`、`InkCanvas.PptAgent.Contracts.dll`、`Newtonsoft.Json.dll` 复制到 `$(OutputPath)ppt-agent` |
| `CopyIACoreHelper` | AfterTargets=Build，`PublishSingleFile != true` | 复制 IACore helper exe 到主输出目录 |
| `CopyLiquidGlassMagHost` | AfterTargets=Build | 复制 MagHost exe 到主输出目录 |
| `CopyIACoreHelperToPublishDirectory` / `CopyLiquidGlassMagHostToPublishDirectory` | AfterTargets=Publish，`PublishSingleFile == true` | 单文件发布时的对应复制 |
| `GenerateTelemetryToken` / `CleanTelemetryToken` | BeforeTargets=PrepareResources | 仅当环境变量 `DLASS_TELEMETRY_TOKEN` 非空时注入 |
| `SetAssemblyInformationalVersion` | AfterTargets=GetBuildVersion | 配合 Nerdbank.GitVersioning 写版本号 |

**Copy target 的平台陷阱**（csproj 内原注释）：

> MagHost 是 SDK 项目，SDK 会按平台给输出目录加前缀：x86/x64 平台输出到 `bin\$(Platform)\$(Configuration)\$(TargetFramework)\`，AnyCPU 输出到 `bin\$(Configuration)\$(TargetFramework)\`。这里在 Build / Publish 后把 exe 复制到主程序输出目录，与 IACoreHelper 的 Copy target 模式一致。

所以：**不要用主项目的 `$(TargetDir)` 去拼 helper 输出路径**，必须按 `$(Platform)` 走带前缀的路径并保留无前缀 fallback。

## 编译前检查

1. 确保没有 CS0246（缺少 using）错误
2. 确保没有 CS0103（找不到名称）错误
3. 确保没有 CS0102（重复定义）错误
4. 确保所有 resx 资源键在默认 resx、en-US、zh-ME 三个文件中完全一致
5. 确保没有未使用的 resx 资源键
6. 新增/修改 UI 文案一律走 i18n，不允许硬编码中文字符串

## 卡顿与触摸回归检查

当前 `net10` 分支可在 Windows 上执行以下自动检查（独立于主程序启动）：

```powershell
dotnet run --project build/ReliabilityTests/ReliabilityTests.csproj -c Release
dotnet run --project build/ReliabilityTests/ReliabilityTests.csproj -c Release -- "Ink Canvas/bin/Debug/x64/net10.0-windows10.0.19041.0/InkCanvasForClass.dll"
dotnet build build/VstoTransportCompile/VstoTransportCompile.csproj -c Release
```

`ReliabilityTests` 直接编译生产代码中的输入门控、原生窗口区域、坐标转发、渲染等待、管道、文件写入、日志、平滑和 Office STA 调度器；测试替身仅覆盖应用配置及不可直接构造的 WinRT 事件边界。它会创建隐藏窗口及临时命名管道，不启动主程序或 Office。文件测试涵盖并发合并、写入失败保留原文件及恢复；STA 测试涵盖线程复用、排队取消及消息循环。手掌擦测试涵盖启动实际擦除回调、移动中升级识别、接触缩小保持橡皮、双指竞争、丢失/抬起/取消恢复、拒绝误写笔迹及 UI 队列溢出恢复。

WinRT 运行状态应与设置开关区分：普通画笔下，`InkCanvasForClass.WinRTInkOverlay.v1` 原生窗口存在说明宿主窗口已创建；可见且未被 DWM cloak 才可能承接当前书写。新构建在 Trace 等级记录“新墨迹管线已挂载”、`Native pointer input confirmed` 及卸载/失败信息；输入确认才说明实际收到原生指针。仅加载 `Windows.UI.Input.Inking.dll` 或配置 `UseWinRTInk=true` 不能证明运行中正在使用 WinRT。

设置与工具栏稳定性检查还涵盖：一万次 UI 更新合并后保留最终值、退出前强制提交、跨线程请求共用调度器、持续输入下定期提交、回调异常后恢复、释放后拒绝旧回调，以及自动字体适配控件未显示/卸载后的可回收性。字体监听滞留测试在修复前失败、修复后通过。可将回归进程限制到单个逻辑处理器运行，检查资源受限时的调度行为；这不能代替真实低性能设备上的帧率和交互延迟测量。

手工复测应包含：反复打开/关闭设置、首次搜索过程中继续输入与清空/关闭、持续拖动工具栏缩放滑块后立即关闭设置、工具栏快速展开/折叠与拖动、启动后立刻书写并等待超过 15 秒、存储统计时离开页面后重新进入。慢操作诊断使用 `[UI slow operation]`，记录超过 250ms 的设置导航、搜索页构造和工具栏重建；正常输入不新增同步磁盘写入。

`VstoTransportCompile` 仅校验实际加载项 IPC 源码在 net472 下的兼容性，**不能代替完整 VSTO 构建、清单签名或 Office 加载验证**。完整 VSTO 验证仍按上文命令执行，需要安装 Office 开发工具。

向 `ReliabilityTests` 传入主程序集路径时，还会扫描实际编译产物中 PowerPoint 增强守护的异步体和委托体，禁止调用 Office 的 `Quit`、演示文稿 `Close`、`Visible`/`WindowState` 写入。PowerPoint 的 COM 创建可能复用用户已有实例；增强功能的启停、失败和取消清理只能释放自身引用，不能据此关闭用户的应用或演示文稿。该回归在旧产物上失败，在修复后通过。

必须另做的实机验证：两块触摸屏在复制显示下持续书写后点击工具栏；100%/150%/200% 缩放与负坐标副屏；弹出菜单、冻结、切页、失焦、最小化及 WinRT 开关；摄像头拔插与驱动无响应；Office 忙碌、翻页和退出；XML/ISF/UInk/多页 ZIP 的保存及导入。

边界：同进程旧插件仍可在 UI 回调内死循环，任意 COM/摄像头驱动调用也无法被托管取消令牌强制终止。后台有界队列保护主界面，但不保证挂起的第三方组件自行恢复；完全隔离需独立进程。测试通过不等于这些硬件及第三方路径已经验证。

## 常见编译错误修复

| 错误 | 原因 | 修复方法 |
| --- | --- | --- |
| CS0246 找不到类型 | 缺少 using 指令 | 添加 `using Ink_Canvas.Properties;` 等 |
| CS0103 找不到名称 | 未引用正确命名空间 | 检查是否需要 `using iNKORE.UI.WPF.Modern.Controls;` |
| CS0102 重复定义 | resx Designer.cs 中重复添加属性 | 删除重复的属性声明 |
| XAML 解析错误 | XML 格式错误 | 检查标签闭合、属性引号等 |
| MSB3027 / MSB3021 无法复制，文件被占用 | 上次运行残留的 `InkCanvas.IACoreHelper.exe` / `InkCanvas.LiquidGlassMagHost.exe` 仍在跑，锁住了输出文件 | 先结束残留 helper 进程再重新编译 |
| 找不到 MagHost / IACoreHelper exe | Copy target 按 `$(Platform)` 找目录，平台传错 | 确认用的是 `-p:Platform=x64`，并检查 `bin\$(Platform)\$(Configuration)\$(TargetFramework)\` 是否有产物 |
