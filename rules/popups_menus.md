# 弹出菜单/工具栏规范

## PenPalettePopupContent — 笔菜单

笔菜单使用 `PopupTabShellContent` 和 `PopupTabTitleBar` 来实现标签页切换功能。

### 结构模式

```xml
<Grid>
    <controls:PopupTabShellContent x:Name="Shell" d:Visibility="Collapsed" />
    <ContentControl x:Name="InnerContentHost" Visibility="Collapsed" d:Visibility="Visible">
        <!-- 菜单内容 -->
    </ContentControl>
</Grid>
```

**后端代码：**
```csharp
public PenPalettePopupContent()
{
    InitializeComponent();
    Shell.InnerContent = InnerContentHost.Content;
}
```

### 重叠加深开关可见性规则

- **普通笔**：隐藏
- **荧光笔**：显示
- **激光笔**：隐藏

### 渐隐功能实现

见 [InkFadeManager 使用规范](./general.md)

## EraserPopupContent — 橡皮擦菜单

橡皮擦菜单使用 `TabControl` + `Pivot` 样式来实现圆形擦/黑板擦切换。

### Pivot TabControl 样式

```xml
<TabControl x:Name="EraserTypeTabControl"
            Style="{StaticResource {x:Static ui:ThemeKeys.TabControlPivotStyleKey}}"
            SelectedIndex="0">
    <TabControl.Resources>
        <sys:Double xmlns:sys="clr-namespace:System;assembly=mscorlib" x:Key="PivotHeaderItemFontSize">16</sys:Double>
        <Style x:Key="CompactPivotTabItem" TargetType="TabItem" BasedOn="{StaticResource TabItemPivotStyle}">
            <Setter Property="Height" Value="32" />
        </Style>
    </TabControl.Resources>
    <TabControl.ItemContainerStyle>
        <Style TargetType="TabItem" BasedOn="{StaticResource CompactPivotTabItem}" />
    </TabControl.ItemContainerStyle>
    <TabItem Header="{i18n:I18n Key=Board_EraserShape_Circle}" />
    <TabItem Header="{i18n:I18n Key=Board_EraserShape_Blackboard}" />
</TabControl>
```

**关键点：**
- 不要使用 TextBlock 包裹 Header 文本，否则会破坏 Pivot 样式的颜色变化
- 使用 `PivotHeaderItemFontSize` 资源来调整字体大小
- 可通过 ItemContainerStyle 调整 TabItem 高度

## 其他 Popup

所有弹出菜单应遵循以下统一模式：

1. **使用 PopupShellContent 或 PopupTabShellContent**：保持统一的 UI 风格
2. **使用 InnerContentHost + Shell 模式**：便于设计时预览
3. **使用 i18n 资源**：便于国际化
4. **与主窗口双向同步**：通过访问器属性暴露控件给 MainWindow
