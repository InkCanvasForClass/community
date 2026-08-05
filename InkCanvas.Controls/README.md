# InkCanvas.Controls

InkCanvas 墨迹白板（ICC-CE）插件共用 WPF 控件库。

为插件 UI 与宿主视觉风格保持一致而设计：工具栏按钮、颜色选择、设置卡片等控件，宿主应用与插件共用同一视觉语言。

## 安装

```bash
dotnet add package InkCanvas.Controls
```

或在 `csproj`：

```xml
<PackageReference Include="InkCanvas.Controls" Version="1.7.19.9" />
```

> 最低运行时要求：.NET 6 + Windows 10 1903+（`net6.0-windows10.0.19041.0`）。

## 依赖

- [`iNKORE.UI.WPF.Modern`](https://github.com/iNKORE-NET/UI.WPF.Modern) 0.10.2.1
- [`iNKORE.UI.WPF`](https://github.com/iNKORE-NET/UI.WPF) 1.2.8

## 相关包

- [`InkCanvas.PluginSdk`](https://www.nuget.org/packages/InkCanvas.PluginSdk) — 插件开发 SDK（含本控件库所需接口）。

## 资源

- 项目主页：<https://github.com/InkCanvasForClass/community>
- 许可证：GPL-3.0-only
