# 浮动工具栏开发规范

## 工具栏按钮管理

工具栏按钮使用 `ToolbarRegistry` 来管理，支持显示/隐藏、排序等功能。

## 按钮类型

所有工具栏按钮都继承自 `IToolbarItem` 接口。常见类型包括：

| 按钮类型 | 说明 |
|---------|------|
| `PenToolItem` | 画笔工具按钮 |
| `EraserToolItem` | 橡皮擦工具按钮 |
| `ClearToolItem` | 清除按钮 |
| `RedoToolItem` / `UndoToolItem` | 重做/撤销按钮 |
| `ShapeDrawToolItem` | 图形绘制按钮 |
| 更多类型见 `Controls/Toolbar/Items/` |  |

## 添加新工具栏按钮

1. 在 `Controls/Toolbar/Items/` 下创建新的按钮类，继承自基类
2. 在 `ToolbarRegistry` 中注册
3. 在设置页面添加管理入口
4. 在 `ToolbarHost` 中处理按钮点击

## 主窗口访问器

在 `MW_Toolbar.cs` 中提供工具栏控件的访问器：

```csharp
internal PenPalettePopupContent PenPalettePopupContent => (PenPalettePopupContent)MainPenPalettePopupContent.Content;
internal PenPalettePopupContent BoardPenPalettePopupContent => (PenPalettePopupContent)BoardPenPalettePopupContent.Content;
```
