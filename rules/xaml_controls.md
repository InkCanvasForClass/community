# XAML 控件使用规范

## ComboBox 不设置宽度

所有 `<ComboBox>` 控件不得设置 `Width`、`MinWidth` 或 `MaxWidth` 属性，应让 ComboBox 根据内容自适应宽度。如果发现 ComboBox 上有这些宽度属性，应当删除。

## controls:LabeledSettingsCard — 带开关的设置卡片

所有需要展示 ToggleSwitch 开关的设置项，**必须**使用 `controls:LabeledSettingsCard` 控件，而不要手动用 `ui:SettingsCard` 内嵌 `ui:ToggleSwitch`。

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

## ui:SettingsCard — 通用设置卡片

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

## ui:SettingsExpander — 可展开设置组

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

## 互斥选项使用 ComboBox

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

## 控件选择速查

| 场景 | 使用控件 |
|------|---------|
| 带开关的设置项（独立） | `controls:LabeledSettingsCard` |
| 互斥选项（二选一或多选一） | `ui:SettingsCard` + `ComboBox` |
| 右侧放 Slider/Button 等 | `ui:SettingsCard` |
| 点击后导航/跳转 | `ui:SettingsCard` + `IsClickEnabled="True"` |
| 多个相关设置折叠为一组 | `ui:SettingsExpander` |
| Expander 子项带开关 | `ui:SettingsCard` + `CheckBox` 或 `ui:ToggleSwitch` |
| Expander 子项放其他控件 | `ui:SettingsCard` |
