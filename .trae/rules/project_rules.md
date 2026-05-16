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

<!-- 右侧放 Slider -->
<ui:SettingsCard Header="{i18n:I18n Key=Advanced_NibModeBoundsWidthHeader}">
    <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock x:Name="SomeText" VerticalAlignment="Center" />
        <Slider x:Name="SomeSlider" Width="200" Minimum="1" Maximum="50"
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
<!-- ✅ 正确：子项中使用 ui:SettingsCard + CheckBox -->
<ui:SettingsExpander.Items>
    <ui:SettingsCard ContentAlignment="Left">
        <CheckBox x:Name="CheckboxOption1" IsChecked="True"
                  Content="选项1"
                  Checked="CheckboxOption1_Changed"
                  Unchecked="CheckboxOption1_Changed" />
    </ui:SettingsCard>
</ui:SettingsExpander.Items>

<!-- ❌ 错误：子项中不得使用 controls:LabeledSettingsCard -->
<ui:SettingsExpander.Items>
    <controls:LabeledSettingsCard Header="选项1" />
</ui:SettingsExpander.Items>
```

### 控件选择速查

| 场景 | 使用控件 |
|------|---------|
| 带开关的设置项 | `controls:LabeledSettingsCard` |
| 右侧放 ComboBox/Slider/Button 等 | `ui:SettingsCard` |
| 点击后导航/跳转 | `ui:SettingsCard` + `IsClickEnabled="True"` |
| 多个相关设置折叠为一组 | `ui:SettingsExpander` |
| Expander 子项带开关 | `ui:SettingsCard` + `CheckBox` 或 `ui:ToggleSwitch` |
| Expander 子项放其他控件 | `ui:SettingsCard` |
