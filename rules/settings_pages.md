# 设置页面开发规范

## 导航结构

设置窗口使用 `NavigationView` 实现三级导航，结构如下：

```text
应用设置
├── 首页 (HomePage)
├── ── ICC CE 设置 ──（分隔符，Nav_ICCSettings）
├── 通用 (Nav_General)
│   ├── 基本 (StartupPage)
│   ├── 时钟 (ClockPage)
│   ├── 隐私 (PrivacyPage)
│   ├── 安全 (SecurityPage)
│   ├── 高级 (AdvancedPage)
│   └── 性能 (PerformancePage)
├── 主界面 (Nav_MainInterface)
│   ├── 窗口 (WindowPage)
│   ├── 个性化 (AppearancePage)
│   ├── 侧边栏 (SidebarPage)
│   └── 快捷键 (HotkeyPage)
├── 画板 (Canvas_GroupTitle)
│   ├── 画布 (CanvasPage)
│   └── 墨迹识别 (InkRecognitionPage)
├── PPT联动 (PowerPointPage)
├── 更新 (UpdatePage)
├── 通知 (NotificationStrings.DefaultTitle)
│   ├── 通知设置 (NotificationPage)
│   └── 公告中心 (AnnouncementCenterPage)
├── 实验性 (ExperimentalPage)
├── 存储 (Storage_GroupTitle)
│   ├── 存储管理 (StoragePage)
│   ├── 备份与还原 (BackupPage)
│   └── 云存储 (CloudStoragePage)
├── 工具栏 (Nav_Toolbar)
│   ├── 组件 (ToolbarPage)
│   ├── 外观 (ToolbarAppearancePage)
│   └── 菜单 (ToolbarMenuPage)
├── 白板 (Nav_Board)
│   ├── 组件 (BoardToolbarPage)
│   ├── 外观 (BoardAppearancePage)
│   └── 菜单 (BoardMenuPage)
├── 自动化 (AutomationWorkflowPage)
├── 点名与计时器 (RandomDrawPage)
├── Debug (DebugPage，硬编码)
├── ── 浮动栏主题 ──（分隔符，Theme_FloatingBarThemesTitle）
│   ├── 浮动栏主题 (FloatingBarThemePage)
│   └── 浮动栏主题市场 (FloatingBarThemeMarketPage)
├── ── 插件设置 ──（分隔符，Nav_PluginSettings）
│   ├── 插件 (PluginPage)
│   └── 插件市场 (PluginMarketplacePage)
├── ── 底部 ──（FooterMenuItems）
├── 友情链接 (FriendlyLinksPage)
└── 关于 Ink Canvas (AboutPage)
```

> 注意：`WhiteboardTipsPage`、`PPTPageFlipPreviewPage` 已注册在 `_pageTypes` 中但**没有导航项**，由其它页面内部跳转，勿在导航树里找它们。

## 导航栏文字

导航栏文字**必须**使用 `NavStrings` 资源文件中的字符串，不得自行编写。

| 资源键 | 中文值 |
|--------|--------|
| Nav_Home | 首页 |
| Nav_ICCSettings | ICC CE 设置 |
| Nav_General | 通用 |
| Nav_Startup | 基本 |
| Nav_Clock | 时钟 |
| Nav_Privacy | 隐私 |
| Settings_Nav_Security | 安全 |
| Nav_Advanced | 高级 |
| Nav_Performance | 性能 |
| Nav_MainInterface | 主界面 |
| Nav_Window | 窗口 |
| Theme_GroupTitle | 个性化 |
| Nav_Sidebar | 侧边栏 |
| Nav_Shortcuts | 快捷键 |
| Canvas_GroupTitle | 画板设置 / 画布 |
| InkRecog_Title | 墨迹识别 |
| PPTStrings.GroupTitle | PPT联动 |
| NotificationStrings.Type_Update | 更新 |
| NotificationStrings.DefaultTitle | 通知 |
| NotificationStrings.SettingsTitle | 通知设置 |
| AnnouncementStrings.CenterTitle | 公告中心 |
| AdvancedStrings.Experimental | 实验性 |
| StorageStrings.Storage_NavTitle | 存储 |
| StorageStrings.Storage_Title | 存储管理 |
| StorageStrings.Backup_Title | 备份与还原 |
| CloudStorageStrings.CloudStorage_Manage | 云存储 |
| Nav_Toolbar | 工具栏 |
| Nav_ToolbarComponents | 组件 |
| Nav_ToolbarAppearance | 外观 |
| Nav_ToolbarMenu | 菜单 |
| Nav_Board | 白板 |
| Nav_BoardComponents | 组件(白板) |
| Nav_BoardAppearance | 外观(白板) |
| Nav_BoardMenu | 菜单(白板) |
| AutomationStrings.Automation_Title | 自动化 |
| RandomStrings.Random_Title | 点名与计时器 |
| (硬编码) "Debug" | Debug |
| Theme_FloatingBarThemesTitle | 浮动栏主题（分隔符标题） |
| Theme_FloatingBarThemeMarketTitle | 浮动栏主题市场 |
| Nav_PluginSettings | 插件设置(分隔符) |
| Nav_Plugins | 插件 |
| PluginStrings.Market_TabInstalled | 已安装 |
| PluginStrings.Market_Title | 插件市场 |
| Nav_FriendlyLinks | 友情链接 |
| Nav_AboutInkCanvas | 关于 Ink Canvas |

## 页面类型映射

在 `SettingsWindow.xaml.cs` 中，导航 Tag 到页面类型的映射。

### ⚠️ 页面注册（重要）

只有**一个**字典：`_pageTypes`，在 `SettingsWindow` **构造函数**中初始化（`private readonly Dictionary<string, Type>`，含全部内置页面 + 插件页面）。

```csharp
// SettingsWindow.xaml.cs 构造函数内
_pageTypes = new Dictionary<string, Type>
{
    { "CanvasPage", typeof(CanvasPage) },
    { "PluginPage", typeof(PluginPage) },
    // ... 见下方完整映射
};
```

添加新页面：**在 `SettingsWindow.xaml` 加导航项（Tag = 页面名）+ 在 `_pageTypes` 注册**，缺一不可。只注册不加载项则无入口；只加导航项不注册则点击无反应且无报错（`NavigateToPage` 找不到类型只写 Warning 日志）。

> 旧文档提到的 `_staticPageTypes` 静态字典**已删除**，只有 `_pageTypes` 一个字典，勿再按双字典写。

导航相关字典：

- `_pageTypes` — Tag → 页面 Type（构造函数初始化，**唯一注册点**）
- `_pages` — Tag → 页面实例缓存（`NavigateToPage` 时 `Activator.CreateInstance` 创建并缓存，重复导航复用实例）
- `_pluginPages` — Tag → `PluginInfo`（插件设置页用；`NavigateToPage(tag, pluginInfo)` 会把 `CurrentPlugin` 塞给 `PluginSettingsPage`）

### 完整映射（40 项）

```csharp
// SettingsWindow.xaml.cs 构造函数内，_pageTypes = new Dictionary<string, Type> { ... }
{ "HomePage", typeof(HomePage) },
{ "StartupPage", typeof(StartupPage) },
{ "ClockPage", typeof(ClockPage) },
{ "PrivacyPage", typeof(PrivacyPage) },
{ "SecurityPage", typeof(SecurityPage) },
{ "WindowPage", typeof(WindowPage) },
{ "AppearancePage", typeof(AppearancePage) },
{ "SidebarPage", typeof(SidebarPage) },
{ "HotkeyPage", typeof(HotkeyPage) },
{ "ToolbarPage", typeof(ToolbarPage) },
{ "ToolbarAppearancePage", typeof(ToolbarAppearancePage) },
{ "FloatingBarThemePage", typeof(FloatingBarThemePage) },
{ "FloatingBarThemeMarketPage", typeof(FloatingBarThemeMarketPage) },
{ "ToolbarMenuPage", typeof(ToolbarMenuPage) },
{ "BoardToolbarPage", typeof(BoardToolbarPage) },
{ "BoardAppearancePage", typeof(BoardAppearancePage) },
{ "BoardMenuPage", typeof(BoardMenuPage) },
{ "WhiteboardTipsPage", typeof(WhiteboardTipsPage) },
{ "UpdatePage", typeof(UpdatePage) },
{ "NotificationPage", typeof(NotificationPage) },
{ "AnnouncementCenterPage", typeof(AnnouncementCenterPage) },
{ "ExperimentalPage", typeof(ExperimentalPage) },
{ "AdvancedPage", typeof(AdvancedPage) },
{ "StoragePage", typeof(StoragePage) },
{ "BackupPage", typeof(BackupPage) },
{ "CloudStoragePage", typeof(CloudStoragePage) },
{ "AutomationWorkflowPage", typeof(AutomationWorkflowPage) },
{ "PowerPointPage", typeof(PowerPointPage) },
{ "RandomDrawPage", typeof(RandomDrawPage) },
{ "CanvasPage", typeof(CanvasPage) },
{ "InkRecognitionPage", typeof(InkRecognitionPage) },
{ "PerformancePage", typeof(PerformancePage) },
{ "DebugPage", typeof(DebugPage) },
{ "FriendlyLinksPage", typeof(FriendlyLinksPage) },
{ "AboutPage", typeof(AboutPage) },
{ "Settings", typeof(SettingsPage) },
{ "PluginPage", typeof(PluginPage) },
{ "PluginSettingsPage", typeof(PluginSettingsPage) },
{ "PluginMarketplacePage", typeof(PluginMarketplacePage) },
{ "PPTPageFlipPreviewPage", typeof(PPTPageFlipPreviewPage) },
```

## 深链接：定位并高亮设置项

设置窗口支持 `icc://settings/<PageTag>?key=<SettingsJsonKey>` 深链接：打开（或复用）设置窗口、导航到指定页面、高亮对应设置项。

### 开关

由 `Settings.Advanced.IsEnableUriScheme`（默认 `false`）控制，UI 开关在 基本(StartupPage) 的「启用外部协议」。设置关闭时 `MW_UriHandler` 直接拒绝处理并写 Warning 日志。

### 路由（`MainWindow_cs/MW_UriHandler.cs`）

- `ParseUriCommand`：解析 `icc:` 前缀，host + path 转小写后作为命令
- `icc://settings[ /<PageTag>][?key=<JsonKey>]` → `HandleUriSettingsNavigation`，例如 `icc://settings/CanvasPage?key=inkFadeSpeedMultiplier`
- `icc://plugin/<pluginId>/<subPath>?<query>` → `HandlePluginUriNavigation`（见 plugin_sdk.md）

### 打开设置窗口流程

1. 优先复用已打开的 `SettingsWindow`（`Application.Current.Windows` 里找），复用不触发 `SuppressInitialNavigation`
2. 没有则 `new` 一个并设 `window.SuppressInitialNavigation = true` 后 `Show()` —— 跳过 Loaded 中默认跳 HomePage，由深链接指定目标页
3. `window.NavigateToPage(pageTag)` 导航（Tag 不在 `_pageTypes` 时只写日志、无页面）
4. 同步 `NavigationView.SelectedItem` 选中态（子菜单项需先把父项 `IsExpanded = true`）
5. 有 `key` 参数则 `window.SetPendingHighlightKey(key)`

### 高亮机制

- `SettingsNavigator.SettingsKey` 附加属性（`Windows/SettingsViews/Helpers/SettingsNavigator.cs`）标记控件对应的 Settings.json 键名：

  ```xml
  <ui:ToggleSwitch controls:SettingsNavigator.SettingsKey="enableInkFade"
                   Header="{i18n:I18n Key=Canvas_EnableInkFade}" ... />
  ```

- `SettingsWindow.SetPendingHighlightKey(key)`：存 `_pendingHighlightKey`；若当前页面已 Loaded 立即触发，否则推迟
- `TryApplyPendingHighlight`：等窗口与页面都 Loaded 后，用三段 `Dispatcher.BeginInvoke`（ContextIdle → ContextIdle → Background）让出两帧，保证模板生成、渲染、滚动条 `BringIntoView` 都稳定后再调 `HighlightSetting(key)`
- key 匹配不到任何控件的 `SettingsKey` 时无高亮，仅日志，不弹错

### 其它 `icc://` 命令

- `icc://restart` / `icc://restart/admin` / `icc://restart/normal` / `icc://exit` / `icc://quit`（3 秒防抖，`_uriNonRepeatableCommands`）
- `icc://config-profile/list` → 输出 `%TEMP%\InkCanvasConfigProfileList.json`
- `icc://config-profile/switch?name=<方案名>` → 输出 `%TEMP%\InkCanvasConfigProfileSwitchResult.txt`

## 设置添加与删除

### 添加新设置完整流程

以添加"墨迹渐隐"功能的 `InkFadeSpeedMultiplier` 设置为例：

#### 1. 在 `Resources/Settings.cs` 中添加属性

```csharp
public class Canvas
{
    [JsonProperty("inkFadeSpeedMultiplier")]
    public double InkFadeSpeedMultiplier { get; set; } = 1.0;
}
```

#### 2. 在对应页面的 XAML 中添加设置控件

使用 `controls:LabeledSettingsCard` 或 `ui:SettingsCard`：

```xml
<controls:LabeledSettingsCard x:Name="CardEnableInkFade"
    Header="{i18n:I18n Key=Canvas_EnableInkFade}"
    Icon="{x:Static ui:SegoeFluentIcons.Delay}"
    SwitchName="ToggleSwitchEnableInkFade"
    Toggled="CardEnableInkFade_Toggled" />
```

#### 3. 在页面代码中添加事件处理

```csharp
private void CardEnableInkFade_Toggled(object sender, RoutedEventArgs e)
{
    if (!_isLoaded) return;
    SettingsManager.Settings.Canvas.EnableInkFade = CardEnableInkFade.IsOn;
    SettingsManager.SaveSettingsToFile();
}
```

#### 4. 在设置加载方法中读取并应用

在 `LoadSettings()` 方法中添加：

```csharp
CardEnableInkFade.IsOn = settings.Canvas.EnableInkFade;
```

#### 5. 在主窗口中使用设置

通过 `MainWindow` 的属性访问器获取控件或直接操作设置：

```csharp
var enabled = Settings.Canvas.EnableInkFade;
Settings.Canvas.EnableInkFade = newValue;
```

### 添加不需要 UI 的纯数据设置

如果设置项不需要 UI 控件（仅通过代码访问），只需在 `Settings.cs` 中添加属性即可。

### 删除设置完整流程

以删除 `IsEnableDisPlayNibModeToggler` 设置为例：

#### 1. 删除 `Settings.cs` 中的属性定义

```csharp
// 删除前
[JsonProperty("isEnableDisPlayNibModeToggler")]
public bool IsEnableDisPlayNibModeToggler { get; set; } = true;
```

#### 2. 删除设置页面 XAML 中的控件

从 `.xaml` 文件中移除对应的 `LabeledSettingsCard` 或其他控件。

#### 3. 删除页面代码中的事件处理方法

从 `.xaml.cs` 中删除：

- 事件处理方法（如 `ToggleSwitchXXX_Toggled`）
- `LoadSettings()` 中的状态加载代码
- `_isLoaded` 守卫块中的保存逻辑

#### 4. 删除 `MW_SettingsToLoad.cs` 中的相关逻辑

如果存在初始化或条件显示逻辑，删除相关代码：

```csharp
// 删除前
if (!Settings.Appearance.IsEnableDisPlayNibModeToggler)
{
    NibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
}
```

#### 5. 删除 `MW_Settings.cs` 中的默认值设置

在 `ResetSettings()` 方法中删除对应的默认值赋值：

```csharp
// 删除前
Settings.Appearance.IsEnableDisPlayNibModeToggler = false;
```

### 添加新设置页面

1. 在 `Windows/SettingsViews/Pages/` 下创建新的 `.xaml` 和 `.xaml.cs` 文件
2. 参考现有页面的结构
3. 在 `SettingsWindow.xaml` 中添加导航入口（使用 `NavStrings` 资源）
4. 在 `SettingsWindow.xaml.cs` 的 `_pageTypes` 中添加 Tag→Type 映射
5. 更新 `rules/Ink Canvas 设置完整目录.md`

### 添加笔工具栏滑块

笔工具栏的滑块（如粗细、透明度）需要特殊的交叉同步处理：

#### 1. 在 `PenPalettePopupContent.xaml` 中定义控件

```xml
<StackPanel Orientation="Horizontal" Margin="0,0,0,8">
    <Label Content="粗细" FontWeight="Bold" FontSize="17" />
    <Slider x:Name="_PenWidthSlider" Minimum="1" Maximum="45" Width="200"
            IsSnapToTickEnabled="True" TickFrequency="0.1" />
    <TextBlock x:Name="_PenWidthText" Width="45" FontFamily="Consolas" />
</StackPanel>
```

#### 2. 在 `PenPalettePopupContent.xaml.cs` 中暴露属性

```csharp
public Slider PenWidthSlider { get; }
public TextBlock PenWidthText { get; }

public PenPalettePopupContent()
{
    // ...
    PenWidthSlider = (Slider)FindName("_PenWidthSlider");
    PenWidthText = (TextBlock)FindName("_PenWidthText");
}
```

#### 3. 在 `MW_Toolbar.cs` 中添加访问器

```csharp
internal Slider PenWidthSlider => PenPalettePopupContent?.PenWidthSlider ?? BoardPenPalettePopupContent?.PenWidthSlider;
internal Slider BoardPenWidthSlider => BoardPenPalettePopupContent?.PenWidthSlider;
internal TextBlock PenWidthText => PenPalettePopupContent?.PenWidthText ?? BoardPenPalettePopupContent?.PenWidthText;
internal TextBlock BoardPenWidthText => BoardPenPalettePopupContent?.PenWidthText;
```

#### 4. 在 `MainWindow.xaml.cs` 的 `WireUp()` 中绑定事件

```csharp
PenWidthSlider.ValueChanged += PenWidthSlider_ValueChanged;
BoardPenWidthSlider.ValueChanged += PenWidthSlider_ValueChanged;
```

#### 5. 在 `MW_Settings.cs` 中实现事件处理

```csharp
private void PenWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    UpdateSliderText(PenWidthSlider, PenWidthText, "{0:0.0}");
    UpdateSliderText(BoardPenWidthSlider, BoardPenWidthText, "{0:0.0}");
    if (!isLoaded) return;
    if (_isUpdatingSliders) return;

    _isUpdatingSliders = true;
    var val = Math.Round(((Slider)sender).Value, 1);
    Settings.Canvas.InkWidth = val / 2;
    if (sender == PenWidthSlider && BoardPenWidthSlider != null)
        BoardPenWidthSlider.Value = val;
    if (sender == BoardPenWidthSlider && PenWidthSlider != null)
        PenWidthSlider.Value = val;
    _isUpdatingSliders = false;

    SaveSettingsToFile();
}
```

**关键点：**

- 使用 `_isUpdatingSliders` 标志防止交叉同步时的死循环
- `UpdateSliderText` 必须在 `_isLoaded` 检查之前调用，确保初始值显示
- 使用 `Math.Round` 处理浮点数精度

## 资源文件规范

### 资源文件体系

每个功能模块有独立的 resx 文件，支持三语：

- `XxxStrings.resx` — 默认（中文）
- `XxxStrings.en-US.resx` — 英文
- `XxxStrings.zh-ME.resx` — 简繁混合

### 资源键命名

- 导航栏文字：`Nav_XXX`，放在 `NavStrings.resx`
- 页面内容文字：按模块分文件（如 `StartupStrings.resx`、`CanvasStrings.resx`）
- 通用文字：`CommonStrings.resx`

### 资源完整性检查

修改 resx 后必须确保：

1. `Designer.cs` 中有对应的属性声明
2. 默认 resx、en-US、zh-ME 三个文件的键完全一致
3. 不存在未使用的资源键
