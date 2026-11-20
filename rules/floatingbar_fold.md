# 浮动栏收纳 / 展开规范

> 适用范围：浮动工具栏（`ViewboxFloatingBar`）从屏幕内收进侧边栏、以及从侧边栏展开。
> 相关文件：`MainWindow_cs/MW_AutoFold.cs`（状态机）、`MainWindow_cs/MW_FloatingBarPlacement.cs`（落位场景入口）、`MW_FloatingBarIcons.cs`（位置计算与白板切换）。

## 一句话规则

**收纳/展开只能通过 `MW_AutoFold.cs` 的入口调用；展开后的位置只能通过 `MW_FloatingBarPlacement.cs` 的场景入口写。不要在别处直接改 `Margin` / `Visibility` / `isFloatingBarFolded`。**

历史 bug（"折叠了但工具栏还在屏幕上""隐藏按钮点不动""退出白板后又弹回来"）几乎都是因为有人绕过入口，
自己写了一次 `ViewboxFloatingBar.Margin = ...` 或提前把标志置了。

## 唯一入口

| 方法 | 用途 | byUser |
|------|------|--------|
| `FoldFloatingBar(sender, isAutoFoldCommand, instant)` | 折叠；`sender` 非 null 表示用户点击 | `sender != null` |
| `UnFoldFloatingBar(sender)` | 展开 | `sender != null` |
| `FoldFloatingBarByUser(instant)` | 热键 / 托盘菜单 / URI 触发的折叠 | true |
| `UnFoldFloatingBarByUser()` | 热键 / 托盘菜单 / URI 触发的展开 | true |
| `FoldFloatingBarByCode(instant)` | 程序决策（白板、PPT 退出、启动初始化） | false |
| `UnFoldFloatingBarByCode()` | 程序决策（PPT 开始放映、退出恢复） | false |
| `FoldFloatingBar_MouseUp` / `UnFoldFloatingBar_MouseUp` | 纯事件壳，只给 XAML / 插件 API 用 | 取决于 sender |

自动化行动（`Automation/ActionHandlers/FoldActionHandler.cs`，行动 ID `inkcanvas.fold`）也走
`FoldFloatingBarByUser()` / `UnFoldFloatingBarByUser()`；行动只表达"折叠"或"展开"，
不参与"跟随进入前形态"这类策略判断。

两者最终都进 `TransitionFloatingBarAsync(folded, instant, trigger)`，再分派到 `ApplyFoldedAsync` / `ApplyUnfoldedAsync`。

## 落位场景入口（ShowFloatingBar）

折叠/展开只决定“在不在屏幕里”；“展开后摆在哪”现在也只有一处入口：
`MainWindow_cs/MW_FloatingBarPlacement.cs`。

| API | 用途 |
|-----|------|
| `FloatingBarPlacement` | 场景枚举：`Folded` / `DesktopExpanded` / `PptExpanded` / `FullScreenExpanded`（白板、黑板） |
| `ShowFloatingBar(placement, skipAnimation)` | 按场景写位置；`Folded` → `RestoreFoldedAppearance()`；折叠态下的展开请求直接返回 |
| `ApplyFloatingBarPlacementForCurrentMode(skipAnimation)` | 先 `ReapplyFoldPositionIfFolded()`，否则按实时场景落位；返回是否处于折叠态 |
| `ResetFloatingBarToDefaultPlacement(placement)` | “恢复默认位置”：清拖动坐标后按场景居中；折叠态只重建折叠外观 |
| `CurrentFloatingBarPlacement` | 实时场景。折叠优先于 `IsInPPTPresentationMode`（退出放映时它可能还是过期值） |
| `IsPptPlacementScene` | 是否按放映档落位：`IsInPPTPresentationMode \|\| _pptSlideShowBeginning`，覆盖放映开始事件窗口期 |
| `PostToUiThread(action, context)` | 唯一封送实现：已在 UI 线程就执行，否则记 Trace 日志后 `BeginInvoke`；Dispatcher 关闭时返回 false |

调用方只声明场景，不要自己写 `if (IsInPPTPresentationMode) 60 else 100`：
这是同一场景在 N 处的重复编码；退出放映的窗口期内 `IsInPPTPresentationMode` 还是 true，
按它选 60 就是“退出后停在任务栏附近、没有重新居中”。
同样，`await Task.Delay(...)` 之后再判 `isFloatingBarFolded` 也不可靠——
折叠否决已经下沉到 `ShowFloatingBar`，它读的是真正写值那一刻的标志。

### 进 / 出放映不留中间态

- 进放映时工具栏宽度会变：退出按钮、PPT 专用项由 `PPTUIManager.UpdateSlideShowStatus` 在
  `Normal` 优先级的续体里才应用，晚于展开流程。若展开时先按旧宽度居中（偏右），按钮出现后
  再补一次居中，用户看到的就是“先居中、随后左移”的横移动画。
- 因此展开流程在放映开始的窗口期**不落位**：`ApplyUnfoldedAsync` 里
  `_pptSlideShowBeginning && !IsInPPTPresentationMode` 时 `marginTask` 保持 `CompletedTask`，
  位置交给 `OnPPTSlideShowBegin` 尾部统一落一次。
- 这一次落位用调度器优先级排队，不用固定延时：`await Dispatcher.InvokeAsync(...,
  DispatcherPriority.ApplicationIdle)`。此时 `Normal`（→ `IsInPPTPresentationMode` 与可见性）与
  `Render`（→ 布局把新可见性算进 `ActualWidth`）都已排完；窗口全屏 `MoveWindow` 由
  `PPTUIManager` 排在 `ApplicationIdle`，同优先级 FIFO 保证本回调排在它之后，
  Margin 的屏幕坐标系才与窗口一致。
- 该回调先幂等 `UpdateToolbarComponentVisibility()`、再 `ViewboxFloatingBar.UpdateLayout()`、
  最后 `ShowFloatingBar(IsInPPTPresentationMode ? PptExpanded : DesktopExpanded)`：
  即使状态更新排在更晚，也按 PPT 模式的最终宽度居中，绝不按旧宽度写一次中间位置。
- 该段决策期间仍用 `_pptSlideShowBeginning` 作短生命周期场景提示，统一用
  `IsPptPlacementScene`（`IsInPPTPresentationMode || _pptSlideShowBeginning`）判断场景：
  `ViewboxFloatingBarMarginAnimationCore` 的贴边分支和 `pointPPT` 选择也走放映档。
  只改 `ApplyUnfoldedAsync` 不够——Core 里若仍只认 `IsInPPTPresentationMode`，
  就会算出桌面档位置，再被陈旧的 `pointDesktop` 覆盖，表现为“进了 PPT 工具栏停在任务栏上方、没有居中”。
  不要把 `IsInPPTPresentationMode` 提前置 true：`PPTUIManager.UpdateSlideShowStatus` 用它计算
  `wasInSlideShow`，提前置位会吞掉“进入放映”的 1000ms 提示。
- 开始事件尾部不要写“进 PPT 前是否已折叠”的分支：设为自动展开时，收纳态进 PPT 同样会被展开，
  需要这次落位把位置收敛到放映档；同时尾部也不允许 `await Task.Delay(...)` 后再落位——
  延时只是在猜“多久算稳定”，宽度未定型时先摆一次就是一次多余的横移。
- `ExitPPTPresentation` 必须在 `CursorIcon_Click` 之前定下目标形态：策略要求收纳时先
  `FoldFloatingBarByCode(hold: true)`。否则 `CursorIcon_Click` 尾部会先按展开态摆一次位置，
  随后 `OnPPTSlideShowEnd` 再折叠——即“先移回任务栏上方再收起”。折叠入口的否决会让中途的展开写值失效。
- 开始事件尾部统一 `if (!isFloatingBarFolded) ShowFloatingBar(PptExpanded)`，**不要**再按“进 PPT 前是否已折叠”
  分支：设为自动展开时，收纳态进 PPT 同样会被展开，需要这次落位把位置收敛到放映档。

### 放映联动事件必须封送 UI 线程

PPT 联动事件（`SlideShowBegin/End`、`PPTConnectionChanged`、`PresentationOpen/Close`、
`SlideShowStateChanged`、`NextSlide`）不得在触发线程上直接处理：

- 事件源在任意线程 raise：`PPTManager` 的 COM 回调（STA）、`ROTPPTManager` 的轮询线程、
  `ComPPTLinkManager` 的转发、`PPTAgentLinkManager` 的后台线程。
- 处理器是 `async void`，首句 `await Dispatcher.InvokeAsync(...)` 之后没有
  `SynchronizationContext`，后续代码全部落在线程池上，再访问 `ViewboxFloatingBar` /
  `LeftSidePanel` 就抛“调用线程无法访问此对象”。异常被 `ReapplyFoldPositionIfFolded` 的
  `catch` 吞掉后，折叠标志仍是 true、外观却没恢复——日志里那条
  “重设折叠位置失败: 调用线程无法访问此对象”就是它。
- 唯一做法：订阅处一律 `xxx += arg => DispatchPPTEvent(() => OnXxx(arg))`，
  `DispatchPPTEvent` 内部只调 `PostToUiThread(handler, "PPT 联动事件")`。
- 同理，落位/外观入口（`ShowFloatingBar`、`ResetFloatingBarToDefaultPlacement`、
  `RestoreFoldedAppearance`、`ApplyFloatingBarFinalPosition`、`SettleFloatingBarAfterExitAsync`）
  自己也会检测 `Dispatcher.CheckAccess()` 并排回 UI 队列。新入口照此办理，
  不要在调用方逐个判断线程。

### 为什么区分 ByUser / ByCode

`byUser` 只影响两件事，不要用它做别的判断：

1. 折叠时是否弹"正在清除墨迹并折叠"提示（`IsAnnotating && strokes > 2`）；
2. 展开时侧边栏是否播动画（`AnimateSidePanelsAsync(SidePanelEdgeHidden, !unfoldFloatingBarByUser)`）。

`instant: true` 只用于启动初始化：直接落到折叠位，不播 0.35s 动画，否则启动时工具栏会从屏幕内划过。

## 状态字段语义

| 字段 | 含义 | 谁改 |
|------|------|------|
| `isFloatingBarFolded` | 目标/当前折叠态。**必须在第一个 await 之前同步置位** | `ApplyFoldedAsync` / `ApplyUnfoldedAsync` 开头 |
| `isFloatingBarChangingHideMode` | 过渡进行中。外部只读：`MW_Timer` 的边缘提示、`MW_Notification`、`MW_FloatingBarIcons` 的居中判定会读它 | `TransitionFloatingBarAsync` 的 try/finally |
| `_pendingFoldTarget` | 过渡中收到的最后一次意图（-1 无 / 0 展开 / 1 折叠） | `TransitionFloatingBarAsync` |
| `_foldTransitionRunning` | 过渡循环是否在跑，防止并发进入 | 同上 |
| `foldFloatingBarByUser` / `unfoldFloatingBarByUser` | 最近一次是用户操作还是程序触发 | 入口方法 |

**关键约定**：`isFloatingBarFolded` 必须先落、后 await。
调用方（退出白板、切工具）在入口返回后会立刻用 `!isFloatingBarFolded` 判断是否要"居中展开"，
状态晚一拍就会往折叠位上写一次展开 Margin，结果就是"标志是折叠的、工具栏却在屏幕里"。

**过渡合并**：过渡进行中再次请求不会排队，只覆盖 `_pendingFoldTarget`；
正在跑的 `while` 循环读完最后一次意图后再跑一轮。因此不要在过渡中依赖某次调用"一定执行"。

## 位置常量

`MW_AutoFold.cs` 顶部集中定义，不要在调用点写魔法数字：

| 常量 | 值 | 含义 |
|------|-----|------|
| `FloatingBarEdgeExpandedDesktop` | 100 | 桌面模式展开后的居中位置 |
| `FloatingBarEdgeExpandedPpt` | 60 | PPT 放映模式展开位置（留出翻页按钮） |
| `FloatingBarEdgeHiddenBase` | -60 | 折叠基准位，实际折叠位还要减去任务栏高度 |
| `SidePanelEdgeHidden` | -50 | 侧边栏完全收起 |
| `SidePanelEdgePeek` | -10 | 折叠态露出展开按钮 |

折叠位统一由 `ComputeFoldEdge()` 计算：`FloatingBarEdgeHiddenBase - 任务栏高度(rcWork)`。

## 白板 / PPT 退出后的形态

由设置项 `Automation.ExitWhiteboardFoldMode` / `ExitPPTFoldMode`（枚举 `ExitFoldMode`）决定：

| 值 | 行为 |
|----|------|
| `RestorePrevious`（默认） | 按进入前的形态恢复；进入来源未知时按展开处理 |
| `Fold` | 退出后统一收纳 |
| `Unfold` | 退出后统一展开 |

白板：进入前在 `ImageBlackboard_MouseUp` 记录 `_floatingBarFoldedBeforeBoard`（必须在展开动作之前记录）。
PPT：进入放映前在 `OnPPTSlideShowBegin` 记录 `_floatingBarFoldedBeforePPT`（必须在 `IsAutoFoldInPPTSlideShow` 处理之前记录）。

## 禁止事项

1. **不要**在别处直接写 `ViewboxFloatingBar.Margin` / `Visibility = Hidden|Collapsed`。
   折叠必须走 `FoldFloatingBarByCode(instant: true)` —— 它会同时处理位置、可见性、侧栏、折叠标志。
   手写 `Collapsed` 会让 `ActualWidth/ActualHeight` 变成 0，后续重建/主题刷新按 0 尺寸布局，
   工具栏就会以"未初始化"的样子出现在屏幕里。
2. **不要**用固定延时（`Task.Delay(100/200/349/600)`）等待折叠/展开完成。
   入口返回的 `Task` 在**弹簧自然静止**后才完成，需要顺序就 `await` 它。
   侧边栏用 `AnimateSidePanelsAsync`，它同样等弹簧静止，没有任何写死的时长。
3. **不要**在折叠态下做"居中"。
   位置一律走 `ShowFloatingBar` / `ApplyFloatingBarPlacementForCurrentMode`，折叠否决在入口内完成；
   不要用 `if (!isFloatingBarFolded) { ... }` 包一层就自己写位置——await 之后标志可能已经变了。
4. **不要**用 `IsFloatingBarUiAbsentFromScreens()` 反向推断"是否需要折叠"。
   它在 `Visibility != Visible` 时直接返回 true，位置其实还在屏幕里也会被判为"已隐藏"。

## 相关坑

- `ViewboxFloatingBarMarginAnimation*` 带版本号（`_marginAnimationVersion`），后发起的调用会作废旧调用。
  所以连续两次位置写入只有最后一次生效——需要"先折叠再展开"时必须 `await` 前一次。
- `SidePannelMarginAnimation` 是 `AnimateSidePanelsAsync` 的即发即忘包装，与折叠入口共用 `_sidePanelAnimVersion`：
  后发起的侧栏动画会覆盖前一次，落位只由最后一次执行。
- 托盘图标 / 热键 / URI 三条入口都走 `ByUser`，它们的 sender 是 `FloatingBarUserTrigger`，不是真实控件。

## 折叠否决与退出收尾

位置写入入口自带折叠否决，折叠期间任何路径都不可能把工具栏“拉回”屏幕内：

- `ViewboxFloatingBarMarginAnimationAsync` / `ViewboxFloatingBarMarginAnimationCore`：
  `IsExpandedEdgeDuringFold(MarginFromEdge)`（= `isFloatingBarFolded && MarginFromEdge > FloatingBarEdgeHiddenBase`）
  直接返回——折叠位是负值，正向边距只可能是“展开/居中”。折叠自检和 `ReapplyFoldPositionIfFolded` 传负值，不受影响。
- `PureViewboxFloatingBarMarginAnimationInDesktopMode` / `...InPPTMode`：入口判折叠、UI 块内再判一次，
  `await` 弹簧任务之后再次判折叠并校验 `_marginAnimationVersion`，最后复位
  `isViewboxFloatingBarMarginAnimationRunning`。
- `CursorIcon_Click`：`HideSubPanels("cursor", true)` 的 50ms 延时后调用
  `ApplyFloatingBarPlacementForCurrentMode()`，由入口在写值前判折叠
  （PPT 退出时 SlideShowEnd 往往正在这一刻收起工具栏）。
- `UpdateToolbarPosition`：`RebuildToolbar()` 后若已折叠（且处于批注模式）就走 `RestoreFoldedAppearance()`
  并直接返回；否则按实时场景落位。
- 所有落位入口都只接受 `FloatingBarPlacement`，不要写字面量 60/100
  （60 在 `ViewboxFloatingBarMarginAnimationAsync` 里会被强制改成 55）。

### 折叠态外观不变量（位置 + 把手）

折叠标志为 true 只代表“应该折叠”，不代表界面已经折叠到位：

- `IsFoldedAppearanceIntact()` = 侧边栏把手（`LeftSidePanel`）可见 **且** 工具栏不在屏幕内
  （`IsFloatingBarUiAbsentFromScreens()`）。两者缺一，说明折叠流程被后来的展开/居中写值覆盖过。
- `ApplyFoldedAsync` 的“已经折叠就直接返回”分支会先做这个检查：外观不完整时调用
  `RestoreFoldedAppearance()` 而不是静默返回。否则用户看到的就是
  “托盘显示收纳态、工具栏却停在屏幕里、又找不到展开把手”——想展开都没有入口。
- `RestoreFoldedAppearance()` 只重建外观（折叠位 + 把手），不重放切换回放 UI、清墨迹、关白板等副作用；
  快捷面板正被用户打开（`Left/RightUnFoldButtonQuickPanel` 可见）时把手本应收起，不强行拉回。
- 把手由 `AnimateSidePanelsAsync` 驱动：`SidePanelEdgePeek`（-10）显示，`SidePanelEdgeHidden`（-50）
  收起并 `Collapsed`。**只重设工具栏位置不够**，折叠收尾必须同时把把手恢复到 peek。
- `ViewboxFloatingBarMarginAnimationAsync` 在弹簧静止后除版本号外还会再判一次折叠
  （期间可能刚进入折叠态），并复位 `isViewboxFloatingBarMarginAnimationRunning`。
- `ViewboxFloatingBarMarginAnimationCore` 的非动画落位会先 `BeginAnimation(MarginProperty, null)`：
  WPF 里动画层优先于属性值，不摘掉动画，同步写入的折叠位会被残留的旧动画覆盖。
- `ReapplyFoldPositionIfFolded` 写折叠位前会 `Interlocked.Increment(ref _marginAnimationVersion)`，
  作废那些还在等待、只校验版本号的正向写值。

退出收尾集中在状态机文件 `MW_AutoFold.cs`：

- `ApplyFloatingBarFinalPosition()`：按当前折叠标志落最终位置——折叠态 `RestoreFoldedAppearance()`
  （折叠位 + 把手），展开态 `PureViewboxFloatingBarMarginAnimationInDesktopMode()`。只用于流程收尾，不做过渡。
  展开目标固定为桌面位置：退出放映的窗口期内 `IsInPPTPresentationMode` 可能仍是过期值。
- `SettleFloatingBarAfterExitAsync(shouldFold, applyFinalPosition, context)`：按调用方策略幂等地折叠/展开
  （折叠走 `FoldFloatingBarByCode(hold: true)`），可选落位，内部记录失败日志。
- `RestoreFloatingBarAfterBoardExitAsync(restoreFolded)`：白板退出专用。恢复收纳走
  `FoldFloatingBarByCode(instant: true, hold: true)`；要求展开且当前已折叠时走
  `UnFoldFloatingBarByCode()`（必须真的搬回屏幕内）；只有本来就展开时才原地恢复——
  不重新居中，位置留给用户。
- `MW_PPT.cs` 的 `SettleFloatingBarAfterPPTSlideShowAsync(bool applyFinalPosition = true)` 只是薄委托，
  传入 `ShouldFoldAfterPPTSlideShow()`。`ExitPPTPresentation` 正常/异常路径、`OnPPTSlideShowEnd`
  （`applyFinalPosition: false`，位置交给清理段尾部）、`HandleManualSlideShowEnd`、连接断开全部共用。

**不要**再写“读一次 `isFloatingBarFolded` 就决定是否居中”或“固定延时后写 -60 把工具栏藏起来”的收尾：
COM 的 SlideShowEnd 事件可能早到也可能晚到，两次判断的结果会互相覆盖，最后就剩
“托盘显示收纳态、工具栏停在任务栏附近”这种自相矛盾的状态。

## 位移动画：弹簧 + 合成层

所有浮动部件的位移（主工具栏、左右把手、快捷面板、迷你栏）都走同一套弹簧：
`Helpers/Motion/SpringMotion.cs`（求解器）+ `MainWindow_cs/MW_FloatingBarMotion.cs`（接入）。

**为什么不直接用 `ThicknessAnimation` 动 `Margin`**：
`Margin` 是布局属性，每帧变化都会重新测量与排列，动画期间 UI 线程被布局吃满，
长距离滑动就掉帧；而 `CircleEase` / `CubicEase` 这类固定曲线从零速度起步，
被下一次操作打断时只能硬切，观感是"顿一下再来"。

**现在的做法**：

- 布局属性**一次**写到终点（只触发布局一次）。
- 视觉位移交给 `TranslateTransform` 上的弹簧，由合成线程插值，逐帧不再有布局开销。
- 打断即重定向：新弹簧接手当前偏移与**速度**，运动是连续的，不会回跳。
- 每个元素一个 `SpringTranslateAnimator`（`SpringTranslateAnimator.For` 缓存），
  同一元素上的后一次调用接管前一次。
- 手感由 `Settings.Appearance.ToolbarMotionPreset` 三档预设控制
  （柔和 / 标准 / 轻快，对应 `SpringSpec.Gentle` / `Standard` / `Snappy`），
  阻尼比与角频率集中在 `SpringSpec`，不要在调用点写死数字。
- `DisableToolbarAnimation` 打开时一律直接到位，并 `SnapToLayout()` 清掉残留偏移。

**"等动画结束"必须等弹簧静止**：入口返回的 `Task` 在弹簧满足位移与速度阈值后才完成
（`SpringSpec.PositionTolerance` / `VelocityTolerance`）。不要再写 `Task.Delay(200/349)`
去猜动画放完了没有——那既可能打断动画，也会白等，还不受手感预设影响。

**直接接管布局位置的交互要先 `SnapToLayout()`**：拖动、启动首帧定位这类操作是直接改
`Margin` 的，若此时弹簧还留着偏移，两者会叠加（表现为拖起来发飘）。这些入口在写
`Margin` 前先调 `FloatingBarSpring.SnapToLayout()`。
