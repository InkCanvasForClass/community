# 工具栏开发规范

## 概述

项目有两套工具栏系统，架构类似但数据模型不同：

| 工具栏 | 命名空间 | 数据结构 | 设置页面 |
|--------|---------|---------|---------|
| 浮动工具栏 | `Controls/Toolbar/FloatingToolbar/` | 扁平：`Components → List<ToolbarComponentEntry>` | `ToolbarPage` |
| 白板工具栏 | `Controls/Toolbar/BoardToolbar/` | 三层：`Areas → Groups → Components` | `BoardToolbarPage` |

## 浮动工具栏

### 按钮管理

工具栏按钮使用 `ToolbarRegistry` 来管理，支持显示/隐藏、排序、拖拽等功能。

### 按钮类型

所有工具栏按钮都继承自 `IToolbarItem` 接口。常见类型包括：

| 按钮类型 | 说明 |
|---------|------|
| `PenToolItem` | 画笔工具按钮 |
| `EraserToolItem` | 橡皮擦按钮 |
| `ClearToolItem` | 清除按钮 |
| `RedoToolItem` / `UndoToolItem` | 重做/撤销按钮 |
| `ShapeDrawToolItem` | 图形绘制按钮 |
| 更多类型见 `Controls/Toolbar/Items/` |  |

### 数据模型

```
ToolbarLayoutSettings
└── Components: List<ToolbarComponentEntry>
    ├── Id: string (如 "builtin.pen")
    ├── InstanceId: string (唯一实例 ID)
    ├── ShowSeparateBorder: bool
    ├── HidingRuleset: ToolbarRuleset (高级隐藏规则)
    ├── Settings: Dictionary<string, object>
    └── Children: List<ToolbarComponentEntry> (分组子项)
```

分组是特殊的 `ToolbarComponentEntry`，其 `Id == "builtin.group"`，子组件放在 `Children` 中。

### 添加新浮动工具栏按钮

1. 在 `Controls/Toolbar/Items/` 下创建新的按钮类，继承自基类
2. 在 `ToolbarRegistry` 中注册（通过 `Discover()` 自动发现）
3. 在设置页面添加管理入口
4. 在 `ToolbarHost` 中处理按钮点击

### 主窗口访问器

在 `MW_Toolbar.cs` 中提供工具栏控件的访问器：

```csharp
internal PenPalettePopupContent PenPalettePopupContent => (PenPalettePopupContent)MainPenPalettePopupContent.Content;
internal PenPalettePopupContent BoardPenPalettePopupContent => (PenPalettePopupContent)BoardPenPalettePopupContent.Content;
```

## 白板工具栏

### 按钮管理

白板工具栏按钮使用 `BoardToolbarRegistry` 来管理，支持区域分组、组件排序等功能。

### 按钮类型

所有白板工具栏按钮都继承自 `IBoardToolbarItem` 接口，基类为 `BoardToolbarImageButtonItemBase`。

| 按钮类型 | 说明 |
|---------|------|
| `BoardPenToolItem` | 画笔 |
| `BoardEraserToolItem` | 橡皮擦 |
| `BoardSelectToolItem` | 选择 |
| `BoardGestureToolItem` | 手势 |
| `BoardInkFreezeToolItem` | 冻结 |
| `BoardVideoBoothToolItem` | 视频展台（使用 FluentSystemIcons 字体图标） |
| `BoardPreviousPageToolItem` / `BoardNextPageToolItem` | 翻页 |
| `BoardUndoToolItem` / `BoardRedoToolItem` | 撤销/重做 |
| 更多类型见 `Controls/Toolbar/BoardToolbar/Items/` |  |

### 数据模型（三层结构）

```
BoardToolbarLayoutSettings
└── Areas: List<BoardToolbarAreaEntry>
    ├── Id: string ("left" / "center" / "right")
    ├── Components: List<BoardToolbarComponentEntry> (区域独立组件)
    └── Groups: List<BoardToolbarGroupEntry>
        ├── Id: string (如 "tools", "navigation")
        └── Components: List<BoardToolbarComponentEntry>
            ├── Id: string (如 "board.pen")
            ├── Position: string ("First"/"Middle"/"Last"/"Single")
            └── Settings: Dictionary<string, object>
```

### ButtonPosition 枚举

定义在 `Ink_Canvas.Controls` 命名空间（`InkCanvas.Controls` 项目）：

| 值 | 说明 |
|----|------|
| `First` | 分组首个按钮 |
| `Middle` | 分组中间按钮 |
| `Last` | 分组末个按钮 |
| `Single` | 独立按钮（不在分组中） |

### 添加新白板工具栏按钮

1. 在 `Controls/Toolbar/BoardToolbar/Items/` 下创建新类，继承 `BoardToolbarImageButtonItemBase`
2. 实现 `Id`、`LocalizationKey`、`Description`、`DefaultPosition`
3. 实现 `OnClick` 方法处理按钮点击
4. 可选重写 `AfterBuild` 进行视图后处理
5. `BoardToolbarRegistry.Discover()` 会自动发现

### 视频展台按钮特殊处理

视频展台按钮使用 `FluentSystemIcons.Video_28_Regular` 字体图标而非 Path 几何体，需要在 `AfterBuild` 的 `Loaded` 事件中替换 Image 为 FontIcon：

```csharp
protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
{
    host.RegisterView(Id, view);
    view.Loaded += (s, e) =>
    {
        var grid = view.ButtonBorderControl.Child as Grid;
        if (grid == null || grid.Children.Count == 0) return;
        var oldIcon = grid.Children[0] as Image;
        if (oldIcon == null) return;
        grid.Children.RemoveAt(0);
        var fontIcon = new FontIcon
        {
            Icon = FluentSystemIcons.Video_28_Regular,
            Width = 24, Height = 24,
            FontSize = 20
        };
        grid.Children.Insert(0, fontIcon);
    };
}
```

**注意：** 不能在 `AfterBuild` 中直接操作 `GeometryDrawing`，因为此时 WPF 控件尚未完全初始化。必须在 `Loaded` 事件中延迟处理。

### 配置文件系统

白板工具栏配置存储在 `Configs/BoardToolbarConfigs/` 目录下，每个配置是一个 JSON 文件。

| 方法 | 说明 |
|------|------|
| `BoardToolbarRegistry.LoadActiveConfig()` | 加载当前活动配置 |
| `BoardToolbarRegistry.LoadConfigFile(name)` | 加载指定配置 |
| `BoardToolbarRegistry.SaveConfigFile(name, layout)` | 保存配置 |
| `BoardToolbarRegistry.ListConfigFiles()` | 列出所有配置 |
| `BoardToolbarRegistry.DeleteConfigFile(name)` | 删除配置 |
| `BoardToolbarRegistry.EnsureDefaultConfigExists()` | 确保默认配置存在 |

当前活动配置名存储在 `SettingsManager.Settings.BoardToolbarConfigName`。

### 主窗口刷新

修改白板工具栏配置后，需调用 `MainWindow.RebuildBoardToolbar()` 实时刷新：

```csharp
private void RebuildMainWindowBoardToolbar()
{
    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
    {
        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        mainWindow?.RebuildBoardToolbar();
    }));
}
```

## 设置页面架构（两套工具栏通用）

### ToolbarPage 架构

浮动工具栏设置页面的核心架构：

- **MVVM 绑定**：`ObservableCollection<ToolbarComponentEntry>` + `DataContext = this`
- **拖拽排序**：实现 `IDropTarget` 接口，使用 `GongSolutions.Wpf.DragDrop`
- **组件设置面板**：选中组件后显示属性编辑（尺寸/对齐/外观/边距/隐藏规则）
- **分组子项**：`GroupChildrenDropHandler` 处理分组内拖拽
- **配置文件管理**：新建/复制/删除配置，通过 `InputDialog` 输入名称
- **实时刷新**：保存后调用 `RebuildMainWindowToolbar()`
- **防误触**：`_suppressSave` / `_suppressConfigChange` 标志

### BoardToolbarPage 架构

白板工具栏设置页面模仿 ToolbarPage，适配三层结构：

- **区域切换**：RadioButton 选择左/中/右区域
- **区域独立组件**：`ObservableCollection<BoardToolbarComponentEntry>` + 拖拽
- **区域分组**：`ObservableCollection<BoardToolbarGroupEntry>` + `DataTemplate`
- **分组内组件**：`BoardGroupChildrenDropHandler` 处理分组内拖拽
- **组件设置面板**：位置/尺寸/外观/边距
- **SyncAreaBack()**：切换区域前同步当前编辑回布局数据
