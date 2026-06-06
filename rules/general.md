# 通用开发规范

## InkFadeManager — 墨迹渐隐管理器

### 核心逻辑

激光笔的渐隐行为：

- **显示时长**：由 `InkFadeTime` 滑块固定设置
- **渐隐动画时长**：由 `书写时长 / 倍速` 动态计算
- **总时长** = `显示时长` + `渐隐动画时长`

### 记录书写时长

在 `MainWindow.xaml.cs` 的 `inkCanvas_StylusDown` 中记录落笔时间：

```csharp
private void inkCanvas_StylusDown(object sender, StylusDownEventArgs e)
{
    _stylusDownTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    if (IsCurrentPageFrozen && IsFreezeMutatingMode(inkCanvas.EditingMode))
    {
        // ...
    }
}
```

在 `MW_SimulatePressureAndInkToShape.cs` 的 `inkCanvas_StrokeCollected` 中计算并传递：

```csharp
long strokeDurationMs = 0;
if (_stylusDownTimestamp > 0)
{
    strokeDurationMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _stylusDownTimestamp;
    _stylusDownTimestamp = 0;
}
_inkFadeManager.AddFadingStroke(e.Stroke, startPoint, endPoint, strokeDurationMs);
```

### InkFadeManager API

#### 1. AddFadingStroke

```csharp
public void AddFadingStroke(Stroke stroke, Point startPoint, Point endPoint, long strokeDurationMs = 0)
```

- `strokeDurationMs`：书写时长（毫秒），不传则使用默认设置

#### 2. FadeSpeedMultiplier

倍速属性，控制渐隐动画快慢。

#### 3. FadeTime

显示时长属性（毫秒），控制墨迹显示多久后开始渐隐。

## DrawingAttributes.IsHighlighter 规则

| 笔类型 | IsHighlighter 规则 |
|-------|------------------|
| **普通笔** | `false`，始终使用正常绘制模式 |
| **荧光笔** | `!HighlighterOverlapEnabled`，关闭重叠加深时使用荧光笔模式 |
| **激光笔** | `false`，始终使用正常绘制模式 |

## 代码组织

- 主窗口代码按功能拆分为多个文件，见 `MainWindow_cs/` 目录
- 每个功能模块有单独的文件，如 `MW_Settings.cs`、`MW_Colors.cs` 等
- 新建功能时，应放在对应的功能文件中，或新建功能文件

## 国际化

所有用户可见的文本都应使用 `i18n` 资源：

```xml
<Label Content="{i18n:I18n Key=Some_Text_Key}" />
```

不要在 XAML 或代码中直接写死中文/英文文本。

## 命名规范

- 方法名使用 PascalCase
- 变量名使用 camelCase
- 私有字段使用 _ 前缀，如 `_stylusDownTimestamp`
- XAML 控件名使用 PascalCase，如 `CardEnableInkFade`
- XAML 资源键使用 PascalCase，如 `PivotHeaderItemFontSize`

## WPF 常见陷阱

### Geometry 冻结（只读）问题

`Geometry.Parse()` 返回的对象被 WPF 冻结为只读，不能直接设置 `Transform` 等属性：

```csharp
// ❌ 错误：InvalidOperationException - 无法在对象上设置属性，因为它处于只读状态
drawing.Geometry.Transform = new ScaleTransform(1.5, 1.5);

// ✅ 正确：先 Clone() 再修改
var geo = drawing.Geometry.Clone();
geo.Transform = new ScaleTransform(1.5, 1.5);
drawing.Geometry = geo;
```

### AfterBuild 阶段控件未初始化

在 `AfterBuild` 回调中，WPF 控件（如 `GeometryDrawing`）可能尚未完全初始化。如果需要操作视觉树，必须在 `Loaded` 事件中延迟处理：

```csharp
// ❌ 错误：AfterBuild 中直接操作视觉树，异常被 try-catch 吞掉
protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
{
    var drawing = view.FindVisualChild<GeometryDrawing>(); // 可能为 null
    drawing.Geometry.Transform = ...; // 可能抛异常
}

// ✅ 正确：在 Loaded 事件中延迟处理
protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
{
    view.Loaded += (s, e) =>
    {
        // 此时控件已完全初始化
    };
}
```

### Page 命名空间冲突

`iNKORE.UI.WPF.Modern.Controls.Page` 和 `System.Windows.Controls.Page` 之间存在歧义，需要显式 using：

```csharp
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using SimpleStackPanel = iNKORE.UI.WPF.Controls.SimpleStackPanel;
```

### Thickness 构造函数

.NET 6 SDK 中 `Thickness` 不支持双参数构造函数 `Thickness(double, double)`，必须使用四参数：

```csharp
// ❌ 错误：编译报错
new Thickness(4, 2)

// ✅ 正确
new Thickness(4, 2, 4, 2)
```

### SegoeFluentIcons 图标键不存在

并非所有 SegoeFluentIcons 枚举值都可用，使用前需确认图标键存在。例如 `SegoeFluentIcons.Whiteboard` 不存在，应改用 `SegoeFluentIcons.Edit`。

### 设置页面导航失败无报错

`NavigateToPage()` 中如果页面类型未注册，方法直接 `return`，不会抛异常也不会有任何提示。建议添加日志：

```csharp
if (!_pageTypes.TryGetValue(pageTag, out Type pageType))
{
    LogHelper.WriteLogToFile($"NavigateToPage 找不到页面类型 [{pageTag}]", LogHelper.LogType.Warning);
    return;
}
```
