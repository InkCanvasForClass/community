# Project Rules

## XAML Controls

### ComboBox 不设置宽度

所有 `<ComboBox>` 控件不得设置 `Width`、`MinWidth` 或 `MaxWidth` 属性，应让 ComboBox 根据内容自适应宽度。如果发现 ComboBox 上有这些宽度属性，应当删除。

### controls:LabeledSettingsCard — 带开关的设置卡片

所有需要展示 ToggleSwitch 开关的设置卡片，**必须**使用 `controls:LabeledSettingsCard` 控件，而不要手动用 `ui:SettingsCard` 内嵌 `ui:ToggleSwitch`。

**属性说明：**

| 属性 | 类型 | 说明 |
|------|------|------|
| `Header` | string | 设置项标题 |
| `Description` | string | 设置项描述（可选） |
| `Icon` | FontIconData? | 标题图标，使用 `SegoeFluentIcons` 枚举值（可选） |
| `IconSource` | ImageSource | 自定义图片图标（可选，优先级高于 Icon） |
| `HeaderIcon` | object | 自定义 HeaderIcon 内容（可选，优先级最高） |
| `IsOn` | bool | 开关状态，默认 false |
| `SwitchName` | string | 内部 ToggleSwitch 的 Name（可选） |
| `ShowWhen` | bool | 控制卡片可见性，为 false 时卡片折叠（可选，默认 true） |
| `Toggled` | RoutedEventHandler | 开关状态变更事件（可选） |

**用法示例：**

```xml
<!-- 最简用法 -->
<controls:LabeledSettingsCard x:Name="CardShowCursor"
    Header="显示画笔光标"
    Description="绘制时显示光标位置。"
    Icon="{x:Static ui:SegoeFluentIcons.TouchPointer}"
    SwitchName="ToggleSwitchShowCursor" />

<!-- 绑定开关状态 + 事件 -->
<controls:LabeledSettingsCard x:Name="CardAutoUpdate"
    Header="自动检查更新"
    Description="允许后台检查更新并下载新版本。"
    Icon="{x:Static ui:SegoeFluentIcons.Sync}"
    IsOn="True"
    SwitchName="ToggleSwitchAutoUpdate"
    Toggled="CardAutoUpdate_Toggled" />

<!-- 条件显示 -->
<controls:LabeledSettingsCard x:Name="CardSomeOption"
    Header="某选项"
    ShowWhen="{Binding IsOn, ElementName=CardParentOption}" />
```

### ui:SettingsCard — 通用设置卡片

用于非开关类型的设置项，右侧内容区域可放置 ComboBox、Slider、Button 等任意控件。

**常见用法：**

```xml
<!-- 右侧放 ComboBox -->
<ui:SettingsCard Header="{i18n:I18n Key=Theme_WindowBackdrop}"
                 Description="{i18n:I18n Key=Theme_WindowBackdrop_Description}">
    <ui:SettingsCard.HeaderIcon>
        <ui:FontIcon Icon="{x:Static ui:SegoeFluentIcons.FullScreen}" />
    </ui:SettingsCard.HeaderIcon>
    <ComboBox x:Name="ComboBoxWindowBackdrop"
              SelectionChanged="ComboBoxWindowBackdrop_SelectionChanged">
        <!-- ComboBoxItem ... -->
    </ComboBox>
</ui:SettingsCard>

<!-- 右侧放 Slider + TextBlock（显示当前值） -->
<ui:SettingsCard Header="{i18n:I18n Key=Advanced_NibModeBoundsWidthHeader}">
    <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock x:Name="SomeSliderText" VerticalAlignment="Center" FontFamily="Consolas" TextAlignment="Right"/>
        <Slider x:Name="SomeSlider" Width="200" Minimum="1" Maximum="50"
                IsSnapToTickEnabled="True" TickFrequency="1" Value="5"
                TickPlacement="None"
                ValueChanged="SomeSlider_ValueChanged" />
    </ikw:SimpleStackPanel>
</ui:SettingsCard>

<!-- 跳转式设置卡片（点击后导航到其他页面或打开窗口） -->
<ui:SettingsCard Header="工具栏按钮管理"
                 Description="请前往「工具栏」设置页面管理浮动工具栏的组件显示与排序。"
                 IsClickEnabled="True"
                 Click="CardFloatingBarButtons_Click">
    <ui:SettingsCard.HeaderIcon>
        <ui:FontIcon Icon="{x:Static ui:SegoeFluentIcons.ViewAll}" />
    </ui:SettingsCard.HeaderIcon>
</ui:SettingsCard>
```

**跳转式设置卡片要点：**
- 设置 `IsClickEnabled="True"` 使卡片可点击（显示右箭头指示）
- 通过 `Click` 事件处理导航逻辑
- 不要在右侧内容区域放置控件

**Slider + TextBlock 后端实现：**

Slider 旁的 TextBlock 用于实时显示当前值，需要在后端实现 `UpdateSliderText` 辅助方法和 `ValueChanged` 事件处理。

1. 在页面代码中添加辅助方法（每个设置页面都需要此方法）：

```csharp
private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
{
    if (slider == null || textBlock == null) return;
    textBlock.Text = string.Format(format, slider.Value);
}
```

2. 实现 Slider 的 `ValueChanged` 事件：

```csharp
private void SomeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    UpdateSliderText(SomeSlider, SomeSliderText, "{0:0}");
    if (!_isLoaded) return;
    SettingsManager.Settings.SomeSection.SomeProperty = (int)e.NewValue;
    SettingsManager.SaveSettingsToFile();
}
```

**要点：**
- `UpdateSliderText` 必须在 `if (!_isLoaded) return;` 之前调用，确保页面加载时 TextBlock 就显示初始值
- `_isLoaded` 守卫防止页面初始化期间重复保存设置
- 格式字符串常用值：`"{0:0}"` 整数、`"{0:F2}"` 两位小数、`"{0:0} ms"` 带单位
- 对于浮点数 Slider，需要额外用 `Math.Round` 处理精度：

```csharp
private void SomeFloatSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    UpdateSliderText(SomeFloatSlider, SomeFloatSliderText, "{0:F2}");
    if (!_isLoaded) return;
    var val = Math.Round(SomeFloatSlider.Value, 2);
    SomeFloatSlider.Value = val;
    SettingsManager.Settings.SomeSection.SomeProperty = val;
    SettingsManager.SaveSettingsToFile();
}
```

3. 在 `LoadSettings()` 中设置 Slider 初始值时，`UpdateSliderText` 会自动通过 `ValueChanged` 被调用，无需手动设置 TextBlock 文本。

### ui:SettingsExpander — 可展开设置组

用于将多个相关设置项折叠为一组，点击可展开/收起。

**结构说明：**

```xml
<ui:SettingsExpander Header="组标题"
                     Description="组描述（可选）"
                     IsExpanded="True">
    <ui:SettingsExpander.HeaderIcon>
        <ui:FontIcon Icon="{x:Static ui:SegoeFluentIcons.SomeIcon}" />
    </ui:SettingsExpander.HeaderIcon>

    <!-- 右侧内容区域（展开前可见），可放 ToggleSwitch 等 -->
    <ui:ToggleSwitch x:Name="ToggleSwitchSomeOption"
                     OnContent="{DynamicResource Common_On}"
                     OffContent="{DynamicResource Common_Off}"
                     Toggled="ToggleSwitchSomeOption_Toggled" />

    <!-- 展开后的子项列表 -->
    <ui:SettingsExpander.Items>
        <ui:SettingsCard Header="子项1">
            <!-- 子项内容 -->
        </ui:SettingsCard>
        <ui:SettingsCard Header="子项2">
            <!-- 子项内容 -->
        </ui:SettingsCard>
    </ui:SettingsExpander.Items>
</ui:SettingsExpander>
```

**关键规则：**

1. **`ui:SettingsExpander.Items` 内的子卡片必须使用 `ui:SettingsCard`，不得使用 `controls:LabeledSettingsCard`。** 因为 `LabeledSettingsCard` 是 `UserControl`，无法作为 `SettingsExpander` 的子项正确渲染。
2. 如果子项需要开关功能，应在 `ui:SettingsCard` 内手动放置 `CheckBox` 或 `ui:ToggleSwitch`。
3. `SettingsExpander` 的直接内容区域（非 Items）可放置 `ui:ToggleSwitch` 等控件，作为该组的总开关。

**子项中使用开关的正确写法：**

```xml
<!-- ✅ 正确：CheckBox 使用 Content 属性显示文本，不要额外加 TextBlock -->
<ui:SettingsExpander.Items>
    <ui:SettingsCard ContentAlignment="Left">
        <CheckBox x:Name="CheckboxOption1" IsChecked="True"
                  Content="选项1"
                  Checked="CheckboxOption1_Changed"
                  Unchecked="CheckboxOption1_Changed" />
    </ui:SettingsCard>
</ui:SettingsExpander.Items>

<!-- ❌ 错误：不要在 CheckBox 外额外添加 TextBlock 显示标签 -->
<ui:SettingsExpander.Items>
    <ui:SettingsCard ContentAlignment="Left">
        <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
            <TextBlock Text="选项1" VerticalAlignment="Center" />
            <CheckBox x:Name="CheckboxOption1" IsChecked="True" />
        </ikw:SimpleStackPanel>
    </ui:SettingsCard>
</ui:SettingsExpander.Items>

<!-- ❌ 错误：子项中不得使用 controls:LabeledSettingsCard -->
<ui:SettingsExpander.Items>
    <controls:LabeledSettingsCard Header="选项1" />
</ui:SettingsExpander.Items>
```

### 互斥选项使用 ComboBox

当设置项存在两个或多个互斥选项时，**必须**使用 `ui:SettingsCard` + `ComboBox`，而不要使用多个 `controls:LabeledSettingsCard` 或多个 `CheckBox`。

**互斥选项**是指同一时间只能选择一个的选项，例如"模式A / 模式B"、"启用 / 禁用 / 跟随系统"等。

```xml
<!-- ✅ 正确：互斥选项使用 ComboBox -->
<ui:SettingsCard Header="应用主题">
    <ui:SettingsCard.HeaderIcon>
        <ui:FontIcon Icon="{x:Static ui:SegoeFluentIcons.Personalize}" />
    </ui:SettingsCard.HeaderIcon>
    <ComboBox x:Name="ComboBoxTheme"
              SelectionChanged="ComboBoxTheme_SelectionChanged">
        <ComboBoxItem Content="浅色" />
        <ComboBoxItem Content="深色" />
        <ComboBoxItem Content="跟随系统" />
    </ComboBox>
</ui:SettingsCard>

<!-- ❌ 错误：不要用两个 ToggleSwitch 表示互斥选项 -->
<controls:LabeledSettingsCard Header="浅色模式" ... />
<controls:LabeledSettingsCard Header="深色模式" ... />

<!-- ❌ 错误：不要用两个 CheckBox 表示互斥选项 -->
<ui:SettingsCard ContentAlignment="Left">
    <CheckBox Content="选项A" />
</ui:SettingsCard>
<ui:SettingsCard ContentAlignment="Left">
    <CheckBox Content="选项B" />
</ui:SettingsCard>
```

**判断标准：**
- 选项之间互斥（选了A就不能选B）→ 用 `ComboBox`
- 选项之间独立（A和B可以同时开/关）→ 用 `controls:LabeledSettingsCard` 或 `CheckBox`

### 控件选择速查

| 场景 | 使用控件 |
|------|---------|
| 带开关的设置项（独立） | `controls:LabeledSettingsCard` |
| 互斥选项（二选一或多选一） | `ui:SettingsCard` + `ComboBox` |
| 右侧放 Slider/Button 等 | `ui:SettingsCard` |
| 点击后导航/跳转 | `ui:SettingsCard` + `IsClickEnabled="True"` |
| 多个相关设置折叠为一组 | `ui:SettingsExpander` |
| Expander 子项带开关 | `ui:SettingsCard` + `CheckBox` 或 `ui:ToggleSwitch` |
| Expander 子项放其他控件 | `ui:SettingsCard` |

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
