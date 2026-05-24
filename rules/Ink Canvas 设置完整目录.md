# Ink Canvas CE - 设置完整目录

> 导出日期：2026-05-24
> 数据模型源码：`Ink Canvas\Resources\Settings.cs`
> 设置窗口源码：`Ink Canvas\Windows\SettingsViews\SettingsWindow.xaml`
> 设置持久化：`Configs\Settings.json`

---

## 导航结构总览

```
设置窗口 (SettingsWindow)
├── 🏠 主页 (HomePage)
├── 📋 ICC CE 设置
│   ├── 通用 (General)
│   │   ├── 基本 (StartupPage)
│   │   ├── 时钟 (ClockPage)
│   │   ├── 隐私 (PrivacyPage)
│   │   └── 高级 (AdvancedPage)
│   ├── 存储 (Storage)
│   │   ├── 存储管理 (StoragePage)
│   │   └── 备份与还原 (BackupPage)
│   └── 工具栏 (Toolbar)
│       ├── 组件 (ToolbarPage)
│       └── 外观 (ToolbarAppearancePage)
└── 🔌 插件设置
    └── 插件 (PluginPage)
```

---

## 一、主页 (HomePage)

主页为设置入口概览页，无独立设置项。

---

## 二、通用 - 基本 (StartupPage)

### 2.1 行为

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 开机自启 | — | 开关 | 关 | 在系统启动时自动运行本应用 |
| 注册 Url 协议 | `Advanced.IsEnableUriScheme` | 开关 | 关 | 允许第三方应用通过 URI 协议 icc:// 调用 |
| 托盘图标 | `Appearance.EnableTrayIcon` | SettingsExpander+开关 | 开 | 在托盘显示图标 |
| ├─ 鼠标左键/触屏单击时 | `Appearance.TrayLeftClickAction` | 下拉选择 | 显示菜单 | 托盘左键动作 |
| └─ 鼠标右键/触屏长按时 | `Appearance.TrayRightClickAction` | 下拉选择 | 显示菜单 | 托盘右键动作 |
| 教学安全模式 | `Startup.CrashAction` | 下拉选择 | 2(显示崩溃窗口) | 崩溃后操作：静默重启/不操作/显示崩溃窗口 |
| 显示启动加载界面 | `Appearance.EnableSplashScreen` | SettingsExpander(左combobox+右开关) | 关 | 启动时显示加载界面 |
| ├─ 启动画面风格 | `Appearance.SplashScreenStyle` | 下拉选择 | 1(跟随四季) | 随机/跟随四季/春/夏/秋/冬/马年限定/自定义 |
| ├─ 自定义启动画面图片 | `Appearance.CustomSplashImagePath` | 按钮 | 空 | 选择自定义图片(自定义风格时可见) |
| └─ 自定义启动画面文字位置 | `Appearance.CustomSplashTextPosition` | 位置选择 | 1(中下) | 左下/中下/右下(自定义风格时可见) |

---

## 三、通用 - 时钟 (ClockPage)

### 3.1 时间显示

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 当前时间 | 显示文本 | 实时显示当前时间 hh:mm:ss |
| 当前日期 | 显示文本 | 实时显示当前日期 yyyy年MM月dd日 星期X |

### 3.2 时钟设置

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 时间格式 | `Appearance.Use24HourTimeFormat` | 下拉选择 | 12小时制 | 12小时制 / 24小时制 |

---

## 四、通用 - 隐私 (PrivacyPage)

### 4.1 隐私与遥测

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 隐私协议同意 | `Startup.HasAcceptedTelemetryPrivacy` | 复选框 | 关 | 同意遥测隐私协议 |
| 遥测上传级别 | `Startup.TelemetryUploadLevel` | 下拉选择 | None(0) | 0=不上传, 1=仅基础数据, 2=基础+可选数据 |

---

## 五、通用 - 高级 (AdvancedPage)

### 5.1 高级设置

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 特殊屏幕模式 | `Advanced.IsSpecialScreen` | 开关 | 关 | 适配特殊屏幕 |
| 禁用硬件加速 | `Canvas.UseHardwareAcceleration` | 开关 | 开(反向) | 关闭硬件加速 |
| 触控倍率 | `Advanced.TouchMultiplier` | SettingsExpander+滑块 | 0.25 | 0-2 触控输入缩放倍率 |
| ├─ 触控倍率校准 | — | 触控区域 | — | 通过触摸校准触控倍率 |
| 橡皮擦绑定触控倍率 | `Advanced.EraserBindTouchMultiplier` | 开关 | 关 | 橡皮擦大小跟随触控倍率 |
| 笔尖模式边界宽度 | `Advanced.NibModeBoundsWidth` | 滑块 | 10 | 1-50 笔尖模式触控边界 |
| 手指模式边界宽度 | `Advanced.FingerModeBoundsWidth` | 滑块 | 30 | 1-50 手指模式触控边界 |
| 四红外模式 | `Advanced.IsQuadIR` | 开关 | 关 | 四红外触摸屏适配 |

### 5.2 日志

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 启用日志 | `Advanced.IsLogEnabled` | 开关 | 开 | 记录运行日志 |
| 按日期保存日志 | `Advanced.IsSaveLogByDate` | 开关 | 开 | 日志文件按日期分文件 |
| 退出确认 | `Advanced.IsSecondConfirmWhenShutdownApp` | 开关 | 关 | 退出时二次确认 |

### 5.3 配置方案

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 配置方案选择 | 下拉选择 | 选择配置方案 |
| 删除方案 | 按钮 | 删除当前方案 |
| 另存为 | 按钮 | 将当前设置保存为新方案 |

---

## 六、存储 - 存储管理 (StoragePage)

存储管理页面，用于查看和管理本地存储空间、缓存清理等。

---

## 七、存储 - 备份与还原 (BackupPage)

### 7.1 备份设置

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 自动更新前自动备份 | `Advanced.IsAutoBackupBeforeUpdate` | 开关 | 开 | 更新前自动备份配置 |
| 定期自动备份 | `Advanced.IsAutoBackupEnabled` | SettingsExpander+开关 | 开 | 定期自动备份 |
| └─ 备份间隔 | `Advanced.AutoBackupIntervalDays` | 下拉选择 | 7天 | 1/3/7/14/30天 |
| 手动备份 | — | SettingsExpander | — | 手动备份操作 |
| ├─ 立即备份 | — | 可点击卡片 | — | 立即执行备份 |
| └─ 恢复备份 | — | 可点击卡片 | — | 从备份恢复 |

---

## 八、工具栏 - 组件 (ToolbarPage)

### 8.1 配置方案

| 设置项 | 数据字段 | 类型 | 默认值 | 说明 |
|--------|----------|------|--------|------|
| 配置方案选择 | `Settings.ToolbarConfigName` | 下拉选择 | "default" | 选择工具栏布局方案 |
| 新建方案 | — | 按钮 | — | 创建新的工具栏布局 |
| 复制方案 | — | 按钮 | — | 复制当前方案 |
| 删除方案 | — | 按钮 | — | 删除当前方案 |

### 8.2 已添加组件

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 已添加组件列表 | 拖拽列表 | 当前方案中的工具栏组件，支持拖拽排序 |
| 分组内组件 | 拖拽列表 | 选中分组后可管理分组内组件 |

### 8.3 组件库 (Tab 1)

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 可用组件列表 | 列表 | 可添加到工具栏的所有组件 |

### 8.4 组件设置 (Tab 2)

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 分隔边框 | 复选框 | 组件是否显示分隔边框 |
| 红色样式 | 复选框 | 组件是否使用红色样式 |
| 显示模式(快速调色板) | 下拉选择 | 双行/单行(仅快速调色板组件) |
| 固定宽度/高度 | 文本框 | 组件固定尺寸 |
| 最小/最大宽度 | 文本框 | 组件尺寸范围 |
| 最小/最大高度 | 文本框 | 组件尺寸范围 |
| 水平/垂直对齐 | 下拉选择 | 默认/左/中/右/拉伸 |
| 字体大小 | 文本框 | 组件文字大小 |
| 图标大小 | 文本框 | 组件图标大小 |
| 透明度 | 文本框 | 组件透明度 |
| 外边距(左/上/右/下) | 文本框 | 组件外边距 |

### 8.5 高级设置 - 隐藏规则 (Tab 3)

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 规则集模式 | 下拉选择 | 任一条件组满足/所有条件组满足 |
| 反转 | 复选框 | 反转规则逻辑 |
| 添加条件组 | 按钮 | 添加新的条件组 |
| 条件组模式 | 下拉选择 | 任一条件满足/所有条件满足 |
| 条件组反转 | 复选框 | 反转条件组逻辑 |
| 条件组启用 | 开关 | 启用/禁用条件组 |
| 条件组复制/删除 | 按钮 | 复制或删除条件组 |
| 规则条件 | 下拉选择 | 选择具体条件 |
| 规则反转 | 复选框 | 反转规则条件 |

### 8.6 重置布局

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 重置布局 | 按钮 | 恢复默认工具栏布局 |

---

## 九、工具栏 - 外观 (ToolbarAppearancePage)

### 9.1 基本

| 设置项 | 数据字段 | 类型 | 默认值 | 范围 | 说明 |
|--------|----------|------|--------|------|------|
| 工具栏缩放 | `Appearance.ViewboxFloatingBarScaleTransformValue` | 滑块 | 1.0 | 0.5-1.25 | 浮动栏大小缩放 |
| 工具栏不透明度 | — | SettingsExpander | — | — | 浮动栏透明度设置 |
| ├─ 悬浮工具栏不透明的 | `Appearance.ViewboxFloatingBarOpacityValue` | 滑块 | 1.0 | 0.3-1.0 | 浮动栏透明度 |
| └─ PPT工具栏不透明的 | `Appearance.ViewboxFloatingBarOpacityInPPTValue` | 滑块 | 0.5 | 0.3-1.0 | PPT放映时浮动栏透明度 |

---

## 十、插件 (PluginPage)

### 10.1 插件管理

| 设置项 | 类型 | 说明 |
|--------|------|------|
| 插件列表 | 动态列表 | 显示已安装插件 |
| 启用/禁用插件 | 开关 | 启用或禁用单个插件 |
| 插件设置 | 按钮 | 打开插件设置页面 |

---

## 附录：数据模型完整字段一览

### Settings (根对象)

| 字段 | 类型 | JSON键 | 说明 |
|------|------|--------|------|
| Advanced | Advanced | "advanced" | 高级设置 |
| Appearance | Appearance | "appearance" | 外观设置 |
| Automation | Automation | "automation" | 自动化设置 |
| PowerPointSettings | PowerPointSettings | "behavior" | PPT设置 |
| Canvas | Canvas | "canvas" | 画布设置 |
| Gesture | Gesture | "gesture" | 手势设置 |
| InkToShape | InkToShape | "inkToShape" | 墨迹识别设置 |
| Startup | Startup | "startup" | 启动设置 |
| RandSettings | RandSettings | "randSettings" | 随机点名设置 |
| ModeSettings | ModeSettings | "modeSettings" | 模式设置 |
| Camera | CameraSettings | "camera" | 摄像头设置 |
| Dlass | DlassSettings | "dlass" | Dlass平台设置 |
| Upload | UploadSettings | "upload" | 上传设置 |
| Security | Security | "security" | 安全设置 |
| Notification | NotificationSettings | "notification" | 通知设置 |
| Toolbar | ToolbarLayoutSettings | "toolbar" | 工具栏布局设置 |
| ToolbarConfigName | string | "toolbarConfigName" | 工具栏配置方案名 |

### Appearance 完整字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| IsColorfulViewboxFloatingBar | bool | false | 彩色浮动栏 |
| ViewboxFloatingBarScaleTransformValue | double | 1.0 | 浮动栏缩放 |
| FloatingBarImg | int | 0 | 浮动栏图标 |
| CustomFloatingBarImgs | List | 空 | 自定义浮动栏图标 |
| ViewboxFloatingBarOpacityValue | double | 1.0 | 浮动栏透明度 |
| EnableTrayIcon | bool | true | 托盘图标 |
| TrayLeftClickAction | TrayClickAction | ShowMenu | 左键点击动作 |
| TrayRightClickAction | TrayClickAction | ShowMenu | 右键点击动作 |
| ViewboxFloatingBarOpacityInPPTValue | double | 0.5 | PPT中浮动栏透明度 |
| ViewboxBlackBoardScaleTransformValue | double | 1 | 黑板缩放 |
| IsTransparentButtonBackground | bool | true | 透明按钮背景 |
| IsShowExitButton | bool | true | 显示退出按钮 |
| IsShowEraserButton | bool | true | 显示橡皮擦按钮 |
| EnableTimeDisplayInWhiteboardMode | bool | true | 白板显示时间 |
| EnableChickenSoupInWhiteboardMode | bool | true | 白板显示语录 |
| IsShowHideControlButton | bool | false | 显示隐藏控制按钮 |
| UnFoldButtonImageType | int | 0 | 展开按钮图标 |
| IsShowLRSwitchButton | bool | false | 显示左右切换按钮 |
| EnableSplashScreen | bool | false | 启动画面 |
| SplashScreenStyle | int | 1 | 启动画面风格 |
| CustomSplashImagePath | string | "" | 自定义启动画面路径 |
| CustomSplashTextPosition | int | 1 | 自定义启动画面文字位置 |
| IsShowQuickPanel | bool | true | 快捷面板 |
| ChickenSoupSource | int | 1 | 语录来源 |
| HitokotoCategories | List | null | 一言分类 |
| IsShowModeFingerToggleSwitch | bool | true | 显示模式切换开关 |
| Theme | int | 2 | 主题 |
| WindowBackdrop | string | "Mica" | 窗口背景材质 |
| UseLegacyFloatingBarUI | bool | false | 旧版浮动栏UI |
| IsShowShapeButton | bool | true | 显示形状按钮 |
| IsShowUndoButton | bool | true | 显示撤销按钮 |
| IsShowRedoButton | bool | true | 显示重做按钮 |
| IsShowClearButton | bool | true | 显示清除按钮 |
| IsShowWhiteboardButton | bool | true | 显示白板按钮 |
| IsShowHideButton | bool | true | 显示隐藏按钮 |
| IsShowLassoSelectButton | bool | true | 显示套索选择按钮 |
| IsShowClearAndMouseButton | bool | true | 显示清除+鼠标按钮 |
| EraserDisplayOption | int | 0 | 橡皮擦显示选项 |
| IsShowQuickColorPalette | bool | false | 显示快速调色板 |
| QuickColorPaletteDisplayMode | int | 1 | 快速调色板显示模式 |
| EnableHotkeysInMouseMode | bool | false | 鼠标模式快捷键 |
| Language | string | "" | 语言 |
| Use24HourTimeFormat | bool | false | 24小时制 |
| QuickPanelBottomOffset | double | -150 | 快捷面板底部偏移 |

### Startup 完整字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| IsAutoUpdate | bool | true | 自动更新 |
| IsAutoUpdateWithSilence | bool | false | 静默更新 |
| AutoUpdateWithSilenceStartTime | string | "06:00" | 静默更新开始时间 |
| AutoUpdateWithSilenceEndTime | string | "22:00" | 静默更新结束时间 |
| UpdateChannel | UpdateChannel | Release | 更新通道 |
| UpdatePackageArchitecture | UpdatePackageArchitecture | 跟随进程 | 安装包架构 |
| IsSmartUpdate | bool | true | 智能更新 |
| SkippedVersion | string | "" | 已跳过版本 |
| AutoUpdatePauseUntilDate | string | "" | 暂停更新截止日期 |
| IsEnableNibMode | bool | false | 笔尖模式 |
| IsFoldAtStartup | bool | false | 启动时折叠 |
| CrashAction | int | 2 | 崩溃后操作 |
| TelemetryUploadLevel | TelemetryUploadLevel | None | 遥测上传级别 |
| HasAcceptedTelemetryPrivacy | bool | false | 已接受遥测隐私 |
| HasShownOobe | bool | false | 已显示OOBE |
| EnableWindowChromeRendering | bool | false | WindowChrome渲染 |

### Advanced 完整字段

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| IsSpecialScreen | bool | false | 特殊屏幕模式 |
| TouchMultiplier | double | 0.25 | 触控倍率 |
| EraserBindTouchMultiplier | bool | false | 橡皮擦绑定触控倍率 |
| NibModeBoundsWidth | int | 10 | 笔尖模式边界宽度 |
| FingerModeBoundsWidth | int | 30 | 手指模式边界宽度 |
| IsQuadIR | bool | false | 四红外模式 |
| IsLogEnabled | bool | true | 启用日志 |
| IsSaveLogByDate | bool | true | 按日期保存日志 |
| IsSecondConfirmWhenShutdownApp | bool | false | 退出确认 |
| IsAutoBackupBeforeUpdate | bool | true | 更新前自动备份 |
| IsAutoBackupEnabled | bool | true | 定期自动备份 |
| AutoBackupIntervalDays | int | 7 | 备份间隔天数 |
| IsEnableUriScheme | bool | false | 启用URI协议 |
| IsNoFocusMode | bool | true | 无焦点模式 |
| WindowMode | bool | true | 无边框模式 |
| IsEnableAvoidFullScreenHelper | bool | Win11默认开 | 避免全屏助手 |
| EnableMultiScreenSupport | bool | true | 多屏支持 |
| FollowMouseForScreenSelection | bool | true | 跟随鼠标屏幕 |
| IsAlwaysOnTop | bool | true | 窗口置顶 |
| EnableUIAccessTopMost | bool | false | UIAccess置顶 |
| IsEnableFullScreenHelper | bool | false | 全屏助手 |
| IsEnableEdgeGestureUtil | bool | false | 边缘手势工具 |
| IsEnableForceFullScreen | bool | false | 强制全屏 |
| IsEnableDPIChangeDetection | bool | false | DPI变更检测 |
| IsEnableResolutionChangeDetection | bool | false | 分辨率变更检测 |

### 枚举类型

| 枚举 | 值 | 说明 |
|------|-----|------|
| OptionalOperation | Yes/No/Ask | 可选操作 |
| UpdateChannel | Release/Preview/Beta | 更新通道 |
| UpdatePackageArchitecture | X86/X64 | 安装包架构 |
| TelemetryUploadLevel | None/Basic/Extended | 遥测上传级别 |
| TrayClickAction | ShowMenu/HideShowMainWindow/TempShowMainWindow/OpenSettings/DisableAllHotkeys/ForceFullScreen/ToggleFoldFloatingBar/ResetFloatingBarPosition/RestartApp/CloseApp | 托盘点击动作 |

---

## 设置项迁移记录

以下设置项从原位置迁移到了新位置：

| 设置项 | 原位置 | 新位置 |
|--------|--------|--------|
| 托盘图标 + 左右键动作 | 外观 (AppearancePage) | 通用 > 基本 (StartupPage) |
| 启用启动画面 + 风格选择 | 外观 (AppearancePage) | 通用 > 基本 (StartupPage) |
| 时间格式(24小时制) | 外观 (AppearancePage) | 通用 > 时钟 (ClockPage) |
| 自动更新前备份 | 高级 (AdvancedPage) | 存储 > 备份与还原 (BackupPage) |
| 定期自动备份 + 间隔 | 高级 (AdvancedPage) | 存储 > 备份与还原 (BackupPage) |
| 手动备份/恢复备份 | 高级 (AdvancedPage) | 存储 > 备份与还原 (BackupPage) |
| 浮动栏缩放 | 外观 (AppearancePage) | 工具栏 > 外观 (ToolbarAppearancePage) |
| 浮动栏透明度 | 外观 (AppearancePage) | 工具栏 > 外观 (ToolbarAppearancePage) |
| PPT浮动栏透明度 | 外观 (AppearancePage) | 工具栏 > 外观 (ToolbarAppearancePage) |
