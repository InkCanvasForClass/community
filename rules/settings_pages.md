# 设置页面开发规范

## 导航结构

设置窗口使用 `NavigationView` 实现三级导航，结构如下：

```
应用设置
├── 首页
├── ── ICC CE 设置 ──（分隔符）
├── 通用
│   ├── 基本 (StartupPage)
│   ├── 时钟 (ClockPage)
│   ├── 隐私 (PrivacyPage)
│   ├── 安全 (SecurityPage)
│   └── 高级 (AdvancedPage)
├── 主界面
│   ├── 窗口 (WindowPage)
│   ├── 个性化 (AppearancePage)
│   └── 快捷键 (HotkeyPage)
├── 画板设置
│   ├── 画布 (CanvasPage)
│   └── 墨迹识别 (InkRecognitionPage)
├── PPT联动 (PowerPointPage)
├── 更新 (UpdatePage)
├── 通知
│   ├── 通知设置 (NotificationPage)
│   └── 公告中心 (AnnouncementCenterPage)
├── 实验性 (ExperimentalPage)
├── 存储
│   ├── 存储管理 (StoragePage)
│   └── 备份与还原 (BackupPage)
├── 云存储 (CloudStoragePage)
├── 工具栏
│   ├── 组件 (ToolbarPage)
│   ├── 外观 (ToolbarAppearancePage)
│   └── 白板工具栏 (BoardToolbarPage)
├── 自动化 (AutomationPage)
├── 随机点名 (RandomDrawPage)
├── Debug (DebugPage)
├── ── 插件设置 ──（分隔符）
├── 插件 (PluginPage)
├── ── 底部 ──（分隔符）
├── 友情链接 (FriendlyLinksPage)
└── 关于 Ink Canvas (AboutPage)
```

## 导航栏文字

导航栏文字**必须**使用 `NavStrings` 资源文件中的字符串，不得自行编写。

| 资源键 | 中文值 |
|--------|--------|
| Nav_General | 通用 |
| Nav_Startup | 基本 |
| Nav_Clock | 时钟 |
| Nav_Privacy | 隐私 |
| Nav_Security | 安全 |
| Nav_Advanced | 高级 |
| Nav_MainInterface | 主界面 |
| Nav_Window | 窗口 |
| Nav_Appearance | 个性化 |
| Nav_Hotkey | 快捷键 |
| Nav_CanvasSettings | 画板设置 |
| Nav_Canvas | 画布 |
| Nav_InkRecognition | 墨迹识别 |
| Nav_PPT | PPT联动 |
| Nav_Update | 更新 |
| Nav_Notification | 通知 |
| Nav_NotificationSettings | 通知设置 |
| Nav_AnnouncementCenter | 公告中心 |
| Nav_Experimental | 实验性 |
| Nav_Storage | 存储 |
| Nav_StorageManagement | 存储管理 |
| Nav_Backup | 备份与还原 |
| Nav_CloudStorage | 云存储 |
| Nav_Toolbar | 工具栏 |
| Nav_ToolbarComponents | 组件 |
| Nav_ToolbarAppearance | 外观 |
| Nav_BoardToolbar | 白板工具栏 |
| Nav_Automation | 自动化 |
| Nav_RandomDraw | 随机点名 |
| Nav_Debug | Debug |

## 页面类型映射

在 `SettingsWindow.xaml.cs` 中，导航 Tag 到页面类型的映射：

### ⚠️ 双字典注册（重要）

`SettingsWindow.xaml.cs` 中存在**两个**页面类型字典，添加新页面时**必须同时注册到两个字典**：

1. `_staticPageTypes` — 静态字典
2. `_pageTypes` — 实例字典（构造函数中初始化）

导航逻辑使用的是**实例字典 `_pageTypes`**，如果只在 `_staticPageTypes` 中注册而忘记在 `_pageTypes` 中注册，页面将无法打开（点击导航项无反应，无报错）。

```csharp
// 静态字典
private static readonly Dictionary<string, Type> _staticPageTypes = new Dictionary<string, Type>
{
    // ...
    { "BoardToolbarPage", typeof(BoardToolbarPage) },  // ✅ 必须添加
    // ...
};

// 构造函数中的实例字典
_pageTypes = new Dictionary<string, Type>
{
    // ...
    { "BoardToolbarPage", typeof(BoardToolbarPage) },  // ✅ 必须添加
    // ...
};
```

### 完整映射

```csharp
private static readonly Dictionary<string, Type> _pageDict = new()
{
    { "Startup", typeof(StartupPage) },
    { "Clock", typeof(ClockPage) },
    { "Privacy", typeof(PrivacyPage) },
    { "Security", typeof(SecurityPage) },
    { "Advanced", typeof(AdvancedPage) },
    { "Window", typeof(WindowPage) },
    { "Appearance", typeof(AppearancePage) },
    { "Hotkey", typeof(HotkeyPage) },
    { "Canvas", typeof(CanvasPage) },
    { "InkRecognition", typeof(InkRecognitionPage) },
    { "PowerPoint", typeof(PowerPointPage) },
    { "Update", typeof(UpdatePage) },
    { "Notification", typeof(NotificationPage) },
    { "AnnouncementCenter", typeof(AnnouncementCenterPage) },
    { "Experimental", typeof(ExperimentalPage) },
    { "Storage", typeof(StoragePage) },
    { "Backup", typeof(BackupPage) },
    { "CloudStorage", typeof(CloudStoragePage) },
    { "Toolbar", typeof(ToolbarPage) },
    { "ToolbarAppearance", typeof(ToolbarAppearancePage) },
    { "Automation", typeof(AutomationPage) },
    { "RandomDraw", typeof(RandomDrawPage) },
    { "Debug", typeof(DebugPage) },
    { "Plugin", typeof(PluginPage) },
    { "FriendlyLinks", typeof(FriendlyLinksPage) },
    { "About", typeof(AboutPage) },
};
```

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
4. 在 `SettingsWindow.xaml.cs` 的 `_pageDict` 中添加 Tag→Type 映射
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
