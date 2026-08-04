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
| InkCanvas.PptAgent.Contracts | `InkCanvas.PPTAgent.Contracts/InkCanvas.PptAgent.Contracts.csproj` | netstandard2.0 | 全部 Any CPU |
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

## 常见编译错误修复

| 错误 | 原因 | 修复方法 |
| --- | --- | --- |
| CS0246 找不到类型 | 缺少 using 指令 | 添加 `using Ink_Canvas.Properties;` 等 |
| CS0103 找不到名称 | 未引用正确命名空间 | 检查是否需要 `using iNKORE.UI.WPF.Modern.Controls;` |
| CS0102 重复定义 | resx Designer.cs 中重复添加属性 | 删除重复的属性声明 |
| XAML 解析错误 | XML 格式错误 | 检查标签闭合、属性引号等 |
| MSB3027 / MSB3021 无法复制，文件被占用 | 上次运行残留的 `InkCanvas.IACoreHelper.exe` / `InkCanvas.LiquidGlassMagHost.exe` 仍在跑，锁住了输出文件 | 先结束残留 helper 进程再重新编译 |
| 找不到 MagHost / IACoreHelper exe | Copy target 按 `$(Platform)` 找目录，平台传错 | 确认用的是 `-p:Platform=x64`，并检查 `bin\$(Platform)\$(Configuration)\$(TargetFramework)\` 是否有产物 |
