# 浮动工具栏拆独立窗口 + 动画单一进度钟重写（仅规划）

## Context

工具栏显隐动画经过十几轮修补，问题仍在冒：打断竞态（迟到的 `Completed` 复位背景）、单帧跳变（居中跟随补偿）、测量中间态（动画途中量按钮位置摆高光）。根因不是某个分支写错，而是三套系统同时在改同一条栏的几何：

1. **跨度补间**（`ToolbarRegistry` 逐帧写段 `Width`）
2. **图标通道补间**（逐帧写 `TranslateTransform` 做 FLIP）
3. **居中跟随**（逐帧写 `RenderTransform` 补偿，收尾原子提交 `Margin`）
4. 外加 `FloatingBarSpring` 占用 `ViewboxFloatingBar.RenderTransform`

任何一对交叠都会产生"谁最后写谁生效"的竞态。继续在原地打补丁只会增加分支数。

**目标**：把浮动工具栏从 MainWindow 可视树搬进独立无边框窗口（与画布解耦），然后把动画重写成**单一进度钟**——一个 `Progress` 派生全部视觉，中断时各属性从当前视觉值重新设目标，收起=展开的严格倒放。已确认的用户决策：

- 窗口尺寸用**容量包络**：动画开始前一次扩到最大跨度（含 `EaseOutBack` 回弹余量 ~6%），动画全程不动窗口矩形，静止后才收缩。不逐帧改尺寸、也不用全屏大透明窗。
- 动画**彻底重写**为单一进度钟，不保留现有三套结构。
- **打断**：每个属性开新动画时把起点设为**当前视觉值**，复用剩余时长；过半程才发生的新改动开新批次。
- **两阶段交付**：先拆窗+旧动画桥接（非动画行为零回归），再换新动画核。

参考实现：本地仓库 `C:\Users\Admin\Documents\GitHub\Inkeys`（C++20 + Direct2D 分层窗口），核心在 `Inkeys\Inkeys\UI\Bar\`：

- `Bar.Animation.cppm`：`BarUiTimelineClass`（线性 `progress`，曲线只在取值时套用）+ `BarUiValueClass`（`val/tar/startV/dur/timelineStartProgress/continueTimelinePhase`，`SetTar` 内 `startV = val`）；**无完成回调，拉取式落值**。
- `Bar.RenderLoop.cpp`：`fold` 时主栏 `x→0`、`w→80`、按钮 `x/y→40`、`pct→0`；所有属性共享一条时钟、统一 0.4s、无逐图标错峰；容量包络计算在 :9928-9934。
- `Bar.WindowGeometry.cppm`：容量围绕主按钮锚点对称、`Back` 曲线 ×1.06、动画期间只增不减、进入 idle 最后一帧才收缩。
- 窗口样式：`WS_POPUP|WS_EX_LAYERED|WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW`，显示用 `SW_SHOWNOACTIVATE`，移动用 `SWP_NOACTIVATE`；拖动是手动 `SetCapture` 票据，不是 `HTCAPTION`。

WPF 无法照搬 ULW 与独立渲染线程，但值模型、容量包络、打断规则可整体平移。

## 现状关键事实（已核实）

- 可视树：`MainWindow.xaml:894-936`　`FloatingbarUIForInkReplay` → `ViewboxFloatingBar`（`Margin` 定位、`LayoutTransform` 缩放、`RenderTransform` 弹簧）→ `GridFloatingBarContainer` → `StackPanelFloatingBarRoot`（首个孩子是拖动头 `BorderFloatingBarMoveControls`，其后由 `ToolbarRegistry.Populate` 追加段）+ `CanvasFloatingBarSelection`（`SelectionBGFloatingBar` / `IndicatorBarFloatingBar`）。兄弟元素 `IdleMiniBar`（:938）、Popup 组（:1006-1033）、拖动遮罩 `GridForFloatingBarDraging`。
- 子树成分干净：`GridFloatingBarContainer` 只含上述两元素；阴影挂在 `StackPanelFloatingBarRoot.Effect`。**可整棵搬走**。
- 定位链（拆窗断点）：`MW_FloatingBarIcons.cs:465-501` 头部点击、`:2433-2611` `PlaceFloatingBarAfterHeadToggle`（横/纵，直接读屏幕宽高+DPI 写 `ViewboxFloatingBar.Margin`）、`MW_FloatingBarMotion.cs:60-107` `MoveFloatingBarTo`（Margin + 弹簧）。
- 动画系统：`ToolbarRegistry.cs` 全静态；`MW_FloatingBarCenterFollow.cs` 整文件是补偿机制。
- Popup：7 个声明在 `MainWindow.xaml:1006-1033`，`Placement=Custom`、目标为工具栏按钮（`MW_Toolbar.cs:56-76`）。插件自建 Popup 注册依赖 `Window.GetWindow(btn) is MainWindow`（`ToolbarRegistry.cs:4511-4518`）——**拆窗后会断**。
- 基建可复用：`Helpers\PerformanceTransparentWin.cs`、`Helpers\WindowTopmostManager.cs`（`RegisterWindow/UnregisterWindow`）、`Helpers\NativeWindowHelper.cs`（`WS_EX_NOACTIVATE` 常量）、`Helpers\PopupManagerHelper.cs`、`Helpers\AnimationsHelper.cs`（token.Progress 范式）。
- DPI 已是 PerMonitorV2（`app.manifest:50`、`csproj:43`、`App.xaml.cs:431`）。
- 位置**不落盘**：`pointDesktop/pointPPT` 纯内存（`MW_FloatingBarIcons.cs:223-227`），落盘只有 `Settings.Appearance.ToolbarPosition`（边侧枚举）。阶段一无设置迁移。
- 倍率：`Settings.Advanced.AnimationSpeedMultiplier`（0.1x–10x，`AnimationSpeed.cs:21/24`）；禁动画 `Settings.Appearance.DisableToolbarAnimation`；日志开关 `toolbar_motion_trace.flag`。

---

## 阶段一：拆窗 + 旧动画桥接

**约束：ToolbarRegistry 的三套动画、CenterFollow、弹簧、AutoFold 状态机、定位回调一行不改。**

### 1. 新窗口

新建 `Ink Canvas\Windows\FloatingToolbarWindow.xaml(.cs)`，继承 `PerformanceTransparentWin`：

```
WindowStyle=None / AllowsTransparency / ShowActivated=False / ShowInTaskbar=False
Focusable=False / ResizeMode=NoResize / WindowStartupLocation=Manual / Background=Transparent
```

- `SourceInitialized`：给 HWND 加 `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`（后者保证不进 Alt+Tab）；`Show()` 时不激活，全程不调 `Activate()`；`WindowTopmostManager.RegisterWindow(this)`。Inkeys 不靠 `WM_MOUSEACTIVATE`，靠 `WS_EX_NOACTIVATE` + `SW_SHOWNOACTIVATE` + `SWP_NOACTIVATE` 三件套。
- 根布局：`Canvas` 内放搬来的 `Viewbox`，**四周留 64 DIP 内边距**承接弹簧过冲（否则窗口裁掉过冲）。窗口逻辑位置 = 布局位置 − padding。

### 2. XAML 搬迁

把 `MainWindow.xaml:896-936` 的 `ViewboxFloatingBar` 整段剪到新窗口；把 7 个 Popup（:1006-1033）一并搬入（`x:Name` 不变）。MainWindow 原位留注释占位，`IdleMiniBar`、拖动遮罩留在主窗不动。拖动头的 `MouseDown/MouseUp` 改指向新窗口壳方法，内部仍调 `MainWindow` 的 `SymbolIconEmoji_MouseDown/Up`。

### 3. 桥接层（唯一新增机制）

新建 `Ink Canvas\MainWindow_cs\MW_FloatingBarWindowBridge.cs`：

- MainWindow 暴露同名 passthrough 属性：`ViewboxFloatingBar`、`ViewboxFloatingBarScaleTransform`、`GridFloatingBarContainer`、`StackPanelFloatingBarRoot`、`BorderFloatingBarMoveControls`、`FloatingbarHeadIconImg`、`CanvasFloatingBarSelection`、`SelectionBGFloatingBar`、`IndicatorBarFloatingBar`、7 个 Popup —— 全部 `=> _toolbarWindow?.XXX`。**这样 MainWindow 既有全部代码（含插件 `ToolbarHost` 与 `ToolbarRegistry.Populate`）不改一行即可编译通过。**
- 位置镜像：`ViewboxFloatingBar.Margin` 仍是唯一逻辑真值。监听 `MarginProperty` 变更 + 主窗 `LocationChanged`，换算：

```
window.Left = mainWindow.Left + margin.Left - padding
window.Top  = mainWindow.Top  + margin.Top  - padding
```

- 尺寸镜像：同样用**容量包络**，不逐帧跟随内容。窗口宽度取 `max(起点跨度, 目标跨度) + 2×padding`，只在两种时机更新：① 一次显隐同步开始前（`IsExtentAnimating` 由 false 变 true）；② `ExtentSettled` 后且已静止。动画期间窗口矩形冻结，避免逐帧重排与边缘闪跳。
- DPI 换算由 WPF 完成（PerMonitorV2 安全）。

### 4. 周边适配

- **拖动**：保留手动移动。`SymbolIconEmoji_MouseDown` 加 `Mouse.Capture(BorderFloatingBarMoveControls)`，`MouseUp` 加 `Mouse.Capture(null)`（拖出窗口后事件不断流）。
- **插件 Popup**：`ToolbarRegistry.cs:4514` 的 `window is MainWindow mw` 放宽为"主窗或工具栏窗"（`FloatingToolbarWindow` 上取 `MainWindow.Instance`）。一行改动。
- **显隐联动**：新窗口订阅主窗 `IsVisibleChanged` 镜像 Show/Hide（覆盖托盘 Hide、AutoFold 深藏）。主窗 `Closing/Closed` 里 `UnregisterWindow` + `Close()`。
- **弹层跟随**：`_popupManager.MarkNeedsUpdate` 调用点不动；主窗 Hide 时显式关闭所有 Popup。

### 5. 阶段一验收（非动画零回归）

展开/收起、双向打断、收起中重点同一工具、双独立方形、竖排、PPT 模式、全部弹层开合与跟随、拖动+自动翻边+贴边、多屏跨 DPI、托盘 Hide/Show、IdleMiniBar 交接、0.1x 慢放；`toolbar_motion_trace.flag` 与拆窗前基线做同序列日志 diff（应一致）。

---

## 阶段二：单一进度钟

### 1. 新类

`Ink Canvas\Controls\Toolbar\FloatingToolbar\FloatingBarClock.cs`，**实例类**，由 `FloatingToolbarWindow` 拥有：

- 字段：`Progress`、`Direction`、`Duration`、`Stopwatch`、`CompositionTarget.Rendering` 句柄。
- 时长 = 基准 × `AnimationSpeed.ScaleMilliseconds`；`DisableToolbarAnimation` → `Snap(0/1)`。
- 事件 `Frame` / `Settled`（沿用现名，消费者平移成本最低）。
- **不使用 WPF `Storyboard`/`DoubleAnimation`**：每个渲染帧直接读 `Progress` 算值写属性。DoubleAnimation 的 `Completed` 正是当前竞态的源头。
- `Progress` 保持线性，曲线只在读值时套用（照 Inkeys `BarUiTimelineClass`/`BarUiValueClass` 分离时间与形状）。

### 2. 每帧派生公式

时间轴只持有**线性** `Progress`；曲线只在读值时套用（Inkeys 的做法，避免缓动污染时间轴）。

- 展开：`E = EaseOutBack(p, 1.1)`；收起：`E = EaseInBack(p, 1.1)`（即 `1 − EaseOutBack(1−p)`，严格互为倒放）
- 段跨度：`Wseg = Wcol + (Wexp − Wcol)·E`
- 图标：`opacity = E`、`scale = 0.82 + 0.18·E`
- 工具栏宽度：`W = Whead + Σ Wseg`
- 工具栏位置：居中 `Left = CenterLeft(W(1)) − W(p)/2`；贴边 `Left = HeadLeft`（锚点=头部按钮中心，与 Inkeys `frameAnchor` 一致）

以上全是窗口内部元素坐标，不牵动窗口矩形。

### 2.1 容量包络（窗口矩形）

照 Inkeys `Bar.WindowGeometry.cppm` 的规则：

- 锚点 = 头部按钮中心在当前屏幕的位置。窗口**围绕锚点对称**：`Left = anchorX − Wmax/2`，`Top = anchorY − Hmax/2`。
- `Wmax = Whead + Σ Wexp`；因展开曲线含回弹，再乘 **1.06**。
- **动画开始前**一次 `SetWindowPos` 设到 `Wmax × Hmax`（`SWP_NOACTIVATE`）。
- 动画期间窗口矩形**只允许扩张**（预测不足时按本帧真实外框补），绝不收缩。
- **进入 idle 的最后一帧**才按当前实际外框收缩一次。
- 位置仍逐帧更新（Inkeys 也允许尺寸不变时用 `SetWindowPos` 直移），只是尺寸不参与。

### 3. 打断

完全照 Inkeys 的"目标制"：

- 时间轴**不重启**，只把剩余时长作为新时长：`dur = duration × (1 − progress)`。
- 每个属性 `SetTar` 时 `startV = 当前显示值`，新方向第一帧值不跳。
- 相位映射：`absolute = startProgress + (1 − startProgress) × p`，再按曲线取剩余段（Inkeys `BarUiApplyCurveRange`）。中途反向**不重播 EaseOut 的陡起步**，只在剩余时间里重走回弹形状。
- 接管规则：进度 `≤ 0.5` 可加入当前批次；> 0.5 或折叠/换边这类大动作**开新批次**（新时长、新曲线），不压缩到旧截止时间。
- **没有 `Completed`**：到点只在本帧把值落到目标，不存在延迟写入的旧终点 → 迟到回调与代际号机制从设计上消失。

### 4. 退役删除

- `ToolbarRegistry.cs`：`SegmentExtentMotion` / `BeginExtentMotion` / `RunExtentMotion` / `OnExtentMotionFrame` / `FinishExtentMotion` / `StopExtentMotion`；通道补间 `StartChannelMotion` / `StopChannelMotion` / `ResetMotionChannels` + FLIP 测量 `TryMeasureLayoutPosition` / `AnimateChildrenToNewOffsets` / `ApplyFlipOffsets`；独立方形 `AnimateSegmentUnitEnter` / `AnimateSegmentUnitExit` / `ExitPlan` / 代际号 / `GetExitingVisualExtent` / `GetEnteringVisualExtent`。
- `MW_FloatingBarCenterFollow.cs` 整文件删除。
- `MW_FloatingBarMotion.cs:60-107` 的跨度中特殊分支删除。
- `IsExtentAnimating` / `IsMotionActive` 改读 clock。

### 5. 消费者平移

- 高亮补位 `HookFloatingBarHighlightRefresh`（`MW_FloatingBarIcons.cs:5491`）→ `clock.Settled`
- 阴影让路 `HookFloatingBarShadowMotion`（`MW_Toolbar.cs:648`）→ `clock.Frame`
- 弹层跟随 `_popupManager.MarkNeedsUpdate` → `clock.Frame`
- 折叠/换边落位：`MoveFloatingBarTo` 保留，但改为驱动窗口 Left/Top（弹簧只作用于整窗位移）

### 6. 规则文档

`rules\floatingbar_fold.md`：新增"单一进度钟"章节（唯一自变量、起点=当前视觉值、过半程开新批次、收起=倒放、禁止 `Completed`/代际号/中间态测量）；补容量包络与主窗显隐镜像条款；删除居中跟随/三方补偿描述。

---

## 风险

1. 逐帧 `SetWindowPos`：已通过容量包络规避——动画期间窗口矩形冻结，只在静止或动画开始前调整一次；位置仍逐帧跟 `Margin`（等效于原来的 RenderTransform 位移，代价不变）。
2. `WS_EX_NOACTIVATE` 下插件 Popup 的 `Keyboard.Focus` 可能失效 → 回归项。
3. Popup 跨窗 Custom 布局技术上可行，但"随哪个窗口关闭"的语义变化 → 主窗 Hide 时显式全关。
4. 拖动跨屏跨 DPI 的 delta 漂移（备选 `WM_NCLBUTTONDOWN/HTCAPTION`，仅作回退）。
5. 多屏迁移须靠主窗 `LocationChanged` 及时联动，否则工具栏窗滞留原屏。
6. 退出/崩溃清理：`Closed` 里 `UnregisterWindow` + `Close()`，防孤儿顶层窗口。
7. 容量包络的透明区域：WPF `AllowsTransparency` 每帧整窗重绘，包络越大代价越高。包络只需覆盖"工具栏展开跨度"（几百 px），不做全屏窗口；若实测掉帧，退一步用"仅宽度包络、高度贴内容"。
8. 0.1x 下 40 秒动画：窗口矩形全程冻结，无 Completed 竞态，慢放主要用于验证打断连续性。

## 不建议做

不回到"固定大透明窗"；不搬 IdleMiniBar / 左右把手；不把 `ToolbarRegistry` 静态改实例；不保留每图标错峰 stagger；不新增设置项；不动 `MW_AutoFold` 状态机。

## 验证

- 构建：`Ink Canvas` 下 `dotnet build "InkCanvasForClass.csproj" -c Debug -v q -nologo`。
- 阶段一：见上文第 5 节，含日志 diff。
- 阶段二：全量回归 + 单帧跳变类日志应绝迹 + `clock` 打断日志与 `AnimationsHelper` 同构 + 禁动画直落 + 0.1x/10x 两倍率。

## 关键文件

- `Ink Canvas\MainWindow.xaml`（:894-936、:1006-1033）
- `Ink Canvas\MainWindow_cs\MW_FloatingBarIcons.cs`（:465-501、:2433-2611、:5387-5570）
- `Ink Canvas\MainWindow_cs\MW_FloatingBarMotion.cs`（:17-107）
- `Ink Canvas\Controls\Toolbar\FloatingToolbar\ToolbarRegistry.cs`
- `Ink Canvas\MainWindow_cs\MW_FloatingBarCenterFollow.cs`（阶段二删除）
- `Ink Canvas\Helpers\AnimationsHelper.cs` / `WindowTopmostManager.cs` / `NativeWindowHelper.cs` / `PerformanceTransparentWin.cs`
- 新增：`Ink Canvas\Windows\FloatingToolbarWindow.xaml(.cs)`、`Ink Canvas\MainWindow_cs\MW_FloatingBarWindowBridge.cs`、`Ink Canvas\Controls\Toolbar\FloatingToolbar\FloatingBarClock.cs`
