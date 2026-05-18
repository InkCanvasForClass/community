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
