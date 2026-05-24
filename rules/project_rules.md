# Project Rules Index

本目录包含了 Ink Canvas 项目的开发规范与规则，按功能模块分类。

## 项目结构

| 项目 | 说明 |
|------|------|
| `Ink Canvas/` | 主应用（WPF，net6.0-windows10.0.19041.0） |
| `InkCanvas.Controls/` | 自定义控件库（LabeledSettingsCard 等） |
| `InkCanvas.PluginSdk/` | 插件 SDK |
| `InkCanvas.IACoreHelper/` | IACore 辅助（不引用输出） |
| `InkCanvas.SettingsTreeView/` | 设置目录树查看器（独立 WPF 应用） |

## 规则目录

| 规则文件 | 说明 |
|---------|------|
| [xaml_controls.md](./xaml_controls.md) | XAML 控件使用规范 |
| [settings_pages.md](./settings_pages.md) | 设置页面开发规范 |
| [popups_menus.md](./popups_menus.md) | 弹出菜单/工具栏规范 |
| [toolbar.md](./toolbar.md) | 浮动工具栏开发规范 |
| [build.md](./build.md) | 编译规范 |
| [general.md](./general.md) | 通用开发规范 |
| [Ink Canvas 设置完整目录.md](./Ink%20Canvas%20设置完整目录.md) | 设置页面完整目录树 |

## 快速导航

- [设置页面开发规范](./settings_pages.md) - 添加/删除设置项、设置页面布局
- [弹出菜单规范](./popups_menus.md) - 笔菜单、橡皮擦菜单等 Popup 开发
- [工具栏规范](./toolbar.md) - 浮动工具栏按钮管理
- [编译规范](./build.md) - 编译前清理流程
- [设置完整目录](./Ink%20Canvas%20设置完整目录.md) - 所有设置页面的树状结构
