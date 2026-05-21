# 设置页面开发规范

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
2. 参考现有页面（如 `AppearancePage.xaml`）的结构
3. 在 `SettingsWindow.xaml` 中添加导航入口
4. 在主窗口代码中添加必要的访问器属性

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
