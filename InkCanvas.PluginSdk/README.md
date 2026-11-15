# InkCanvas.PluginSdk

InkCanvas 墨迹白板（ICC-CE）插件开发 SDK。

提供插件接口、宿主服务抽象、API 兼容性声明以及 `.icpx` 打包支持。插件项目通过 `PackageReference` 引用本包后自动获得 `CreateIcpx` 打包能力，将插件项目直接编译为可被 ICC-CE 加载的 `.icpx` 扩展包。

## 安装

```bash
dotnet add package InkCanvas.PluginSdk
```

或在 `csproj`：

```xml
<PackageReference Include="InkCanvas.PluginSdk" Version="1.7.19.9" />
```

> 最低运行时要求：.NET 6 + Windows 10 1903+（`net6.0-windows10.0.19041.0`）。

## 目标用户

- 想要扩展 ICC-CE 功能（白板、墨迹、PPT 联动、UI 元素）的开发者。
- 想要为智教联盟生态贡献插件的开发者。

## 相关包

- [`InkCanvas.Controls`](https://www.nuget.org/packages/InkCanvas.Controls) — 插件共用的 WPF 控件库（工具栏、颜色选择、设置卡片等）。

## 摄像头提供者（camera provider）

从 API 1.14.0 起，插件可以把自行采集的画面（RTSP/网络摄像头/视频展台等非 DirectShow 来源）
注册进宿主内置「视频展台」的摄像头下拉框，由宿主负责预览、拍照与分辨率切换，
无需编写原生 COM 或注册系统级虚拟摄像头：

```csharp
// Initialize 阶段
var cameraProvider = host.GetService<ICameraProviderService>();
cameraProvider?.RegisterProvider(new MyCameraProvider());   // 实现 IPluginCameraProvider
```

实现 `IPluginCameraProvider` 后，在 `StartCamera(cameraId, sink)` 中保存 `sink`，采集线程调用
`sink.Submit(bgr24Buffer, width, height, stride)` 即可推帧（BGR24 / top-down）。
接口与类型：`ICameraProviderService`、`IPluginCameraProvider`、`IPluginCameraFrameSink`、
`PluginCameraDescriptor`、`PluginCameraCapability`。
章节说明见仓库 `docs/camera-provider.md`，接口清单见 `rules/plugin_sdk.md`。

## 资源

- 项目主页：<https://github.com/InkCanvasForClass/community>
- 许可证：GPL-3.0-only
- 反馈与贡献：仓库 Issues / Pull Requests
