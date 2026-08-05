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

## 资源

- 项目主页：<https://github.com/InkCanvasForClass/community>
- 许可证：GPL-3.0-only
- 反馈与贡献：仓库 Issues / Pull Requests
