# ICC 文档

## 开发

本项目目前开发状态：

- 正在 [`master`](https://github.com/InkCanvasForClass/InkCanvasForClass/tree/master) 分支上开发第一个正式版本 **v6.0.0**。

要在本地编译应用，您需要安装以下负载和工具：
1. **[.NET Framework 4.7.2](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)**
2. [Visual Studio](https://visualstudio.microsoft.com/)

对于 Visual Studio，您需要在安装时勾选以下工作负载：

- .NET 桌面开发

## 子项目

**`InkCanvasForClass.IACoreHelper`** <br/>
该项目实现了基于 .NET Framework 4.7.2 和 x86 运行环境的墨迹识别库 IACore 的封装。

**`InkCanvasForClass.PowerPoint.InteropHelper`** <br/>
该项目将 ICC 的 PPT 适配功能给单独提取了出来，并下放 ZPH 的部分功能到本项目上面。

**`InkCanvasForClass.PowerPoint.Vsto`** <br/>
该项目实现了 ICC 对 PPT 的 VSTO 插件支持，可以缓解 Office 和 WPS 共存导致的 COM 接口被占用的问题（或者其他任何有关 COM 接口的疑难杂症，只要软件正常，文档不是被保护文档或兼容模式或只读文档，VSTO都能行）。

**`InkCanvasForClassX`** <br/>
该项目已废弃，皆在重写 ICC（💀bro以为自己能够摆脱 IC 的💩山）。

**`InkCanvasForClass.IccInkCanvas`** <br/>
该项目将 ICC 魔改的 InkCanvas 控件给提取了出来，方便控件重用，减少 ICC 主程序代码量。封装选择V2、橡皮V2、实时笔锋、白板多页面管理、漫游管理、快捷键重写以及其他魔改。

**`InkCanvasForClass.IccInkCanvas.Demo`** <br/>
IccInkCanvas 的 Demo 测试。

## 组件库

ICC 自己造了一套风格类似于 Gnome Gtk4 的 WPF 组件库，下面有具体每个控件的文档：

1. [`ToggleSwitch`](./components/ToggleSwitch.md) Gtk.Switch 青春版，切换开关状态的按钮控件
2. [`SegmentedButtons`]() 类似 Gtk.StackSwitcher 的分段单选按钮