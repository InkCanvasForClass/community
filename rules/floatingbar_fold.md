# 浮动栏收纳 / 展开规范

> 适用范围：浮动工具栏（`ViewboxFloatingBar`）从屏幕内收进侧边栏、以及从侧边栏展开。
> 相关文件：`MainWindow_cs/MW_AutoFold.cs`（状态机）、`MW_FloatingBarIcons.cs`（位置计算与白板切换）。

## 一句话规则

**收纳/展开只能通过 `MW_AutoFold.cs` 的入口调用，不要在别处直接改 `Margin` / `Visibility` / `isFloatingBarFolded`。**

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

两者最终都进 `TransitionFloatingBarAsync(folded, instant, trigger)`，再分派到 `ApplyFoldedAsync` / `ApplyUnfoldedAsync`。

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
2. **不要**用固定延时（`Task.Delay(300/600/700)`）等待折叠/展开完成。
   入口返回的 `Task` 在动画与最终 Margin 落地后才完成，需要顺序就 `await` 它。
   侧边栏动画用 `AnimateSidePanelsAsync`，等待的是动画时长本身（0.175s）。
3. **不要**在折叠态下做"居中"。
   `HideSubPanels` 里的居中分支已经加了 `!isFloatingBarFolded`；新增居中逻辑时同样要判断。
4. **不要**用 `IsFloatingBarUiAbsentFromScreens()` 反向推断"是否需要折叠"。
   它在 `Visibility != Visible` 时直接返回 true，位置其实还在屏幕里也会被判为"已隐藏"。

## 相关坑

- `ViewboxFloatingBarMarginAnimation*` 带版本号（`_marginAnimationVersion`），后发起的调用会作废旧调用。
  所以连续两次位置写入只有最后一次生效——需要"先折叠再展开"时必须 `await` 前一次。
- `SidePannelMarginAnimation` 是 `AnimateSidePanelsAsync` 的即发即忘包装，与折叠入口共用 `_sidePanelAnimVersion`：
  后发起的侧栏动画会覆盖前一次，落位只由最后一次执行。
- 托盘图标 / 热键 / URI 三条入口都走 `ByUser`，它们的 sender 是 `FloatingBarUserTrigger`，不是真实控件。
