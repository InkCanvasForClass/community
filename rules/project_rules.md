# Project Rules Index

本目录包含了 Ink Canvas 项目的开发规范与规则，按功能模块分类。

## 项目结构

| 项目 | 说明 |
|------|------|
| `Ink Canvas/` | 主应用（WPF，net6.0-windows10.0.19041.0，Debug x64） |
| `InkCanvas.Controls/` | 自定义控件库（LabeledSettingsCard 等） |
| `InkCanvas.PluginSdk/` | 插件 SDK（已发布 nuget.org，对外契约） |
| `InkCanvas.SettingsTreeView/` | 设置目录树查看器（独立 WPF 应用） |
| `InkCanvas.PptAgent.Contracts/` | PPT agent 契约（netstandard2.0，注意目录名是 PPTAgent） |
| `InkCanvas.IACoreHelper/` | IACore 辅助（net472，x86） |
| `InkCanvas.LiquidGlassMagHost/` | 液态玻璃 MagHost（net6.0-windows10.0.19041.0，真·多平台） |
| `InkCanvas.PowerPointAddIn/` | PowerPoint VSTO 加载项（net472，**不在 sln 内**，单独 MSBuild） |
| `InkCanvas.NativeInk.Tests/` | 原生墨迹手动验证程序（**不在 sln 内**） |

## 规则目录

| 规则文件 | 说明 |
|---------|------|
| [xaml_controls.md](./xaml_controls.md) | XAML 控件使用规范 |
| [settings_pages.md](./settings_pages.md) | 设置页面开发规范（含 icc:// 深链接定位/高亮） |
| [plugin_sdk.md](./plugin_sdk.md) | 插件 SDK 开发规范（宿主 API、icc://plugin 路由、PluginLogs） |
| [popups_menus.md](./popups_menus.md) | 弹出菜单/工具栏规范 |
| [toolbar.md](./toolbar.md) | 浮动工具栏开发规范 |
| [chat_log_board_toolbar.md](./chat_log_board_toolbar.md) | 聊天记录/白板工具栏规范 |
| [build.md](./build.md) | 编译规范（Debug x64、PowerPointAddIn、Copy target） |
| [general.md](./general.md) | 通用开发规范 |
| [消息去重说明.md](./消息去重说明.md) | NotificationCenterService 滑动窗口去重策略 |
| [Ink Canvas 设置完整目录.md](./Ink%20Canvas%20设置完整目录.md) | 设置页面完整目录树 |

## 快速导航

- [设置页面开发规范](./settings_pages.md) - 添加/删除设置项、设置页面布局、深链接定位
- [插件 SDK 规范](./plugin_sdk.md) - 插件开发、宿主服务、icc://plugin 深链接
- [弹出菜单规范](./popups_menus.md) - 笔菜单、橡皮擦菜单等 Popup 开发
- [工具栏规范](./toolbar.md) - 浮动工具栏按钮管理
- [编译规范](./build.md) - 编译前清理流程、Debug x64 构建校验
- [消息去重说明](./消息去重说明.md) - 通知去重触发条件
- [设置完整目录](./Ink%20Canvas%20设置完整目录.md) - 所有设置页面的树状结构
