# 数学白板路线 C 开发进度与上下文交接

> 最后更新：2026-07-31
> 工作区：`C:\Users\memzb\Documents\ICCCE++`
> 当前结论：离线结构化数学白板已完成三阶段扩展并持续接受实机调试。AI 功能尚未开始。

## 2026-07-31 当前交接摘要（优先于后续历史记录）

以下是当前可继续开发或调试的准确状态；文档中较早的 A–E、旧发布目录和旧测试计数仅保留为历史背景，不得当作当前基线。

### 已实现的范围

- 平面：点、线段、直线、射线、圆、三角形、角、标签、坐标系；吸附、引用同步、水平/垂直/平行/垂直于/等长/共线/点在线上或圆上约束；三角形内切圆、外接圆。
- 函数：安全离线表达式解析、采样/缓存、零点/极值/截距/单调区间分析，以及同一坐标系内的交点。
- 立体：正方体、长方体、棱柱、棱锥、圆柱、圆锥、球；每种立体显示符合其语义的尺寸输入，而非统一长宽高；投影视图与正视图；90 度平行/垂直旋转吸附；内切球、外接球的严格条件构造。
- 附着几何：在立体表面/顶点创建的点、线段、直线、射线、圆、三角形、角和标签保留局部三维坐标，随父立体平移、缩放、旋转、投影和页面恢复同步。
- 数学模式：空白白板点击可退出数学选择并切回笔；底栏数学入口有选中视觉；用户“清除笔迹”会同步清除当前页数学场景；常规工具切换会退出数学输入。

### 本轮实机问题与已修复路径

1. **对象快捷菜单点击穿透/切回笔**
   - 独立 WPF `Popup` 在透明顶层白板窗口中出现点击穿透。
   - 当前改为主窗口可视树内的 `MathObjectActionsLayer`，不再依赖该快捷菜单的独立 Popup HWND。
   - `MW_Math.cs` 对菜单后代来源的 Preview Mouse/Touch Down、Move、Up 全部放行，避免白板的选择逻辑在按钮 `Click` 前吞掉抬起事件。
   - 用户已反馈按钮可按下；随后发现 `MouseUp` 被吞导致不执行，已在 `v3` 修复。后续包均包含该修复。

2. **圆锥、球、内外接球的立体表达错误**
   - 圆锥不再使用通用网格中固定的两条母线；渲染器在投影后的底面椭圆上求两条轮廓切线，并将底面按深度分为前实线、后虚线。
   - 球使用闭合外轮廓；赤道按深度拆为前半实线、后半虚线。内切球、外接球复用该球面渲染，并跟随父立体的局部球心、尺寸、旋转、缩放和投影。
   - 关键代码：`MathStrokeRenderer.RenderCone`、`RenderSphere`、`AddDepthSplitCurve`；`SolidProjectionService.TransformModelPoint` 提供旋转后的深度。

### 当前验证证据

- `dotnet run --project "InkCanvas.Math.Tests\\InkCanvas.Math.Tests.csproj" --configuration Release`：**70 项通过**。
- `dotnet run --project "InkCanvas.Math.UiSmoke.Tests\\InkCanvas.Math.UiSmoke.Tests.csproj" --configuration Release`：**11 项通过**；包含主视觉树像素渲染、菜单输入路由、球外轮廓/赤道、圆锥两条轮廓母线回归检查。
- `dotnet build "Ink Canvas.sln" --configuration Release --disable-build-servers -maxcpucount:1`：**0 警告、0 错误**。
- `git diff --check`：通过（工作区已有 CRLF 提示，不是空白错误）。
- Native Ink 58 项在本阶段早先版本通过；**球/圆锥 v4 渲染改动后尚未重新运行 Native Ink 契约测试**。

### 当前调试包

最新且唯一推荐的包：

```text
build\InkCanvasForClass-CE-solid-render-v4-win-x64.zip
SHA-256: 21FF6D3A55543FC5325B41FC15678BF0BD35942F27B83F0AEF2B6F6CE297D52A
```

- `win-x64`、未签名、便携调试包、7 个发布文件。
- 依赖目标机已有 `.NET 6 Desktop Runtime`，不是自包含安装器，也不是正式签名发布。
- `build\release-solid-render-v4\` 是对应解压发布目录。
- `math-actions-v3` 以及更早 `popup-fix`、`v2` 包只用于问题回溯，不应作为当前测试基线。

### 尚未验收/下一步

1. 用 v4 实测普通圆锥、旋转圆锥、普通球、各类可构造内切球/外接球的视觉关系；特别检查虚线是否位于后半面、外接球是否包住原立体。
2. 重跑 `InkCanvas.NativeInk.Tests`，然后实测鼠标、触摸和电磁笔对新内嵌快捷菜单的点击、拖动、按住与取消。
3. 完成 100/125/150/200% DPI、多显示器、切页/撤销/重做、保存/重开、多页 sidecar 和大场景的端到端验证。
4. 未经用户确认，不启动 AI、OCR、联网能力、.NET 升级、安装器签名或无关重构。

## 历史发布基线：2026-07-29 数学教师交互修复

以下条目已被 2026-07-31 摘要取代，仅保留问题演进背景：

- 切换到“笔”、橡皮、选择等常规工具栏菜单时，会通过共用关闭路径退出数学输入模式。
- 数学对象快捷操作栏已设为可聚焦，打开前临时解除无焦点模式；编辑、测量、重置视图、删除均已接线。
- 自动吸附优先级为“显式点/中点 → 交点 → 最近几何位置”。它覆盖点、线段边、直线、射线、圆周，以及立体投影的顶点与边，并保留屏幕反馈。
- 点击立体工具后，先显示中文“长、宽、高”设置面板；确认有效尺寸后插入。球以三者最大值确定半径，圆柱/圆锥以长宽中的较大值确定半径。
- 新立体采用平行斜投影，不显示 XYZ 坐标轴，对应平行边保持平行；柱体、锥体、球仅生成关键轮廓与必要虚线。
- 函数记录所属坐标系 ID；移动函数时同步移动坐标系和同坐标系函数。旧场景中的函数会先识别其所在坐标系后建立关联。
- 选中对象支持区域命中、横向/纵向独立拉伸、旋转和移动。

本轮未启动或操作主程序 GUI。已验证：Release 构建 0 警告、0 错误；数学核心 61 项、WPF UI smoke 8 项、Native Ink 58 项全通过；`git diff --check` 通过。实际鼠标/触控/电磁笔手感、不同 DPI 和立体视觉效果仍待人工验收。

## 历史记录：2026-07-29 教师交互阶段 A–E

- 修复数学 Popup 旧隐藏动画完成后关闭新 Popup 的竞争；打开条件失败时提供明确提示。
- 数学工具显示持续步骤说明；拖动构造显示实时预览，吸附显示目标和类型。
- 支持 Esc/右键取消与 Backspace 回退多步骤选择。
- 对象创建后自动进入选择模式；单击保持选中，显示选框、缩放/旋转柄和编辑、测量、重置视图、删除快捷菜单。
- 立体默认缩放提高到 55，默认视图使用 `RotationX=-35.264`、`RotationY=45`、`RotationZ=45`，显示 X/Y/Z 正向坐标轴。
- 函数零点、极值和共享坐标框架中的交点显示类型与坐标；不同原点、比例或旋转的函数不显示误导性交点。
- 数学菜单按平面几何、测量与约束、立体几何分组，并扩大触控目标、补充差异化图标。
- 设置页增加数学独立分组和立体坐标轴开关。
- 当前 Release 验证：数学核心 61 项、WPF UI smoke 8 项、Native Ink 58 项通过；解决方案构建 0 警告、0 错误。
- 本轮按要求未启动或操作主程序 GUI；真实鼠标、触控、电磁笔、主题和高 DPI 仍属于人工验收边界。

## 1. 当前目标和范围

当前阶段只开发离线数学白板功能，不开发 OCR、云端视觉识别、自然语言生成或其他 AI 功能。

路线 C 的核心原则：

1. `MathScene` 是数学对象的唯一结构化数据源。
2. 数学对象通过原生 WPF `StrokeCollection` 投影到白板。
3. 显示层使用不接收输入的 `InkPresenter`，普通笔迹继续使用原有 `InkCanvas`。
4. 数学对象的选择、拖动、缩放和旋转修改结构化对象，然后重新生成显示笔迹。
5. 数学对象单独持久化，不混入普通 ISF 笔迹，避免破坏现有白板存储格式。
6. 所有数学变更进入现有时间机器撤销/重做链路。

## 2. 已完成并验证的功能

### 2.1 数学菜单和输入

- 白板工具栏已加入“数学”入口。
- 数学弹出菜单中的按钮可以点击。
- 原生 Wet Ink 输入路由认识 `LogicalInkTool.Math`，数学菜单和数学操作不会再被笔迹输入吞掉。
- 默认无焦点模式下，打开数学菜单会临时恢复窗口焦点。
- 函数表达式和定义域文本框可以输入。
- 关闭数学菜单后恢复原有无焦点模式。

### 2.2 坐标系和函数图像

- 坐标系可直接插入白板中心。
- 函数输入后会直接插入函数图像；如果当前位置没有坐标系，会同时创建坐标系。
- 支持的表达式由离线 `MathExpressionParser` 解析，不执行任意代码。
- 函数包含定义域、每单位像素、旋转角度、采样质量和标记设置。
- 支持函数零点、极值和交点标记。
- 函数采样结果有缓存。
- 用户已实际确认对象能够显示并拖动。

### 2.3 平面几何

当前结构化对象包括：

- 点
- 线段
- 直线
- 射线
- 圆
- 文本标签
- 角度
- 坐标系

已有的几何能力包括：

- 命中测试
- 平移
- 点引用同步
- 中点和交点吸附
- 水平、垂直、等长、共线、点在线上、点在圆上等约束
- 距离、角度和半径标注
- 场景序列化与兼容读取

### 2.4 立体几何

目前支持七种立体：

- 正方体
- 长方体
- 棱柱
- 棱锥
- 圆柱
- 圆锥
- 球

支持：

- 正交投影和透视投影
- 隐藏边虚线
- 尺寸标签
- 绕 X、Y、Z 轴旋转
- 整体缩放
- 体积和表面积计算

七种立体均已验证能生成带对象分组标记的原生笔迹。

### 2.5 交互

- “选择”模式下可命中并拖动数学对象。
- 鼠标滚轮可缩放函数、坐标系和立体。
- `Shift + 鼠标滚轮` 可旋转函数或立体。
- “旋转立体”模式下可拖动调整立体的 X/Y 旋转角。
- “编辑函数”可选择已有函数并修改表达式和定义域。
- “删除”可删除结构化数学对象。
- 白板冻结状态下会阻止数学对象修改。

### 2.6 历史、页面和保存

- 数学场景变化使用 `TimeMachineHistoryType.MathSceneChange`。
- 支持数学场景撤销和重做。
- 页面切换时恢复该页的数学场景。
- 页面历史扁平化时保留最新数学场景。
- 页面缩略图包含数学对象投影笔迹。
- 白板漫游会同步移动普通笔迹、图片和数学对象。
- 普通笔迹仍使用原有 ISF/白板保存链路。
- 数学场景使用独立 `.math.json` sidecar 文件。
- 支持旧版数学场景 Schema 的读取和未来版本拒绝策略。

## 3. 当前显示技术栈

### 3.1 主应用

- Windows 桌面应用
- WPF
- C# 10
- .NET 6
- 目标框架：`net6.0-windows10.0.19041.0`
- 解决方案：`Ink Canvas.sln`
- 主项目：`Ink Canvas/InkCanvasForClass.csproj`
- 目标运行时：`win-x86`、`win-x64`、`win-arm64`

### 3.2 数学结构和算法

- 结构化场景：`MathScene`
- JSON：`System.Text.Json`
- 函数解析：项目内离线递归下降表达式解析器
- 函数采样：自适应离线采样和缓存
- 几何计算：项目内服务类
- 立体建模：顶点、边、面组成的轻量网格
- 立体投影：项目内正交/透视投影
- 约束求解：项目内约束服务

### 3.3 显示与输入

- 数学显示数据：`System.Windows.Ink.StrokeCollection`
- 数学显示控件：`System.Windows.Controls.InkPresenter`
- 普通笔迹：原有 `InkCanvas` 和 Native Wet Ink 流水线
- 数学输入：主白板容器的 Preview Mouse/Touch 事件
- 原生输入分流：`NativeInkInputRouter`
- 弹出菜单：现有 `PopupShellContent` 和 `PopupManagerHelper`

实际层级为：

1. 白板背景
2. 动态页面元素画布
3. 数学 `InkPresenter`，`Panel.ZIndex=5`
4. 普通笔迹 `InkCanvas`，`Panel.ZIndex=10`
5. 橡皮擦、选择框、弹窗和工具栏

## 4. 关键代码位置

### 4.1 主窗口集成

- `Ink Canvas/MainWindow_cs/MW_Math.cs`
  - 菜单事件
  - 数学插入模式
  - 函数输入和编辑
  - 鼠标/触摸交互
  - 拖动、缩放和旋转
  - 数学场景刷新
  - 页面 sidecar 保存和读取
  - 自适应前景色

- `Ink Canvas/MainWindow.xaml`
  - `MathInkPresenter`
  - 普通 `inkCanvas`
  - 数学弹出菜单

- `Ink Canvas/MainWindow_cs/MW_NativeWetInk.cs`
  - 数学工具的 Native Wet Ink 路由判定

- `Ink Canvas/MainWindow_cs/MW_TimeMachine.cs`
  - 数学历史应用
  - 页面缩略图笔迹生成
  - 页面历史扁平化

- `Ink Canvas/MainWindow_cs/MW_Save&OpenStrokes.cs`
  - 数学 sidecar 文件读写
  - 多页保存和恢复

- `Ink Canvas/MainWindow_cs/MW_PageListView.cs`
  - 数学对象进入页面缩略图

- `Ink Canvas/MainWindow_cs/MW_BoardRoaming.cs`
  - 数学对象随白板漫游移动

### 4.2 数学核心

- `Ink Canvas/Math/Models/`
  - 数学对象和场景模型

- `Ink Canvas/Math/Services/`
  - 几何、约束、函数、立体、测量、吸附和颜色服务

- `Ink Canvas/Math/Persistence/`
  - Schema、JSON 序列化、迁移和文件存储

- `Ink Canvas/Math/Rendering/MathStrokeRenderer.cs`
  - 将所有结构化数学对象投影为分组原生笔迹
  - 每条生成笔迹带对象 ID 和数学生成标记

- `Ink Canvas/Math/Rendering/MathCanvasControl.cs`
  - 当前仅作为 `MathScene` 宿主兼容层
  - 旧路线 A 的自定义 `OnRender` 已删除
  - 后续可在确认没有兼容依赖后，用 `MainWindow` 私有 `_mathScene` 字段替代并删除此控件

### 4.3 菜单和设置

- `Ink Canvas/Controls/Popups/MathInsertPopupContent.xaml`
- `Ink Canvas/Controls/Popups/MathInsertPopupContent.xaml.cs`
- `Ink Canvas/Controls/Toolbar/BoardToolbar/Items/BoardMathToolItem.cs`
- `Ink Canvas/Windows/SettingsViews/Pages/CanvasPage.xaml`
- `Ink Canvas/Resources/Settings.cs`
- `Ink Canvas/Properties/CanvasStrings*.resx`
- `Ink Canvas/Properties/FloatingBarStrings*.resx`

### 4.4 测试

- `InkCanvas.Math.Tests/`
  - 数学场景、几何、函数、约束、立体、序列化和历史契约测试

- `InkCanvas.Math.UiSmoke.Tests/`
  - 数学菜单
  - 输入焦点
  - Native Wet Ink 路由源代码约束
  - 真实 WPF `InkPresenter` 像素呈现
  - 主窗口动态页面、数学层和普通笔迹层的真实层级组合
  - 多函数性能

- `InkCanvas.NativeInk.Tests/NativeInkCoreTests.cs`
  - 数学逻辑工具路由回归

## 5. 重要 Bug 根因记录

### 5.1 菜单按钮不能点击

原因：

- 原生 Wet Ink 路由把数学菜单输入按普通画布笔迹处理。

修复：

- 添加 `LogicalInkTool.Math`。
- 数学菜单打开或数学模式激活时返回 `DeferToWpfUi`。

### 5.2 函数输入框无法输入

原因：

- 用户配置默认开启 `isNoFocusMode=true`。
- 主窗口带 `WS_EX_NOACTIVATE`，弹出菜单中的 `TextBox` 无法获得键盘焦点。

修复：

- 数学菜单打开时临时关闭无焦点模式并激活窗口。
- 关闭菜单时恢复原设置。

### 5.3 数学对象创建成功但屏幕空白

实际日志曾显示：

```text
Math objects inserted: added=1, total=...
Math presenter refreshed: objects=5, strokes=360, visible=Collapsed, size=0x0
```

原因：

- 数学对象和笔迹均已成功生成。
- 模式切换中的 `ApplyMathSettings()` 调用早于白板模式状态最终生效。
- 数学显示层一直保持 `Visibility.Collapsed`，布局尺寸为 `0x0`。
- 早期离屏测试只验证了独立 `InkCanvas`，没有验证主窗口的真实动态层级和模式状态。

最终修复：

- 使用 `InkPresenter` 替代独立数学 `InkCanvas`。
- 显式设置数学层与普通笔迹层的 `Panel.ZIndex`。
- `RefreshMathScene()` 每次刷新都根据
  `Settings.Canvas.EnableMathCanvas && currentMode == 1`
  自校正显示层可见性。
- 日志记录对象数、笔迹数、可见性、显示尺寸、宿主尺寸、模式和开关。
- 回归测试模拟主窗口的背景、动态页面画布、数学层和普通笔迹层。

用户已确认最终版本可以显示并拖动数学对象。

## 6. 当前测试结果

最近一次完整验证：

- `dotnet build "Ink Canvas.sln" --configuration Release`
  - 0 警告
  - 0 错误

- 数学核心契约测试
  - 61 项通过

- Native Ink 契约测试
  - 58 项通过

- WPF UI/呈现测试
  - 8 项通过

- 20 个函数原生笔迹刷新
  - 约 2.6 ms/帧

建议继续开发前运行：

```powershell
dotnet build "Ink Canvas.sln" --configuration Release
dotnet run --project "InkCanvas.Math.Tests/InkCanvas.Math.Tests.csproj" --configuration Release
dotnet run --project "InkCanvas.Math.UiSmoke.Tests/InkCanvas.Math.UiSmoke.Tests.csproj" --configuration Release
dotnet run --project "InkCanvas.NativeInk.Tests/InkCanvas.NativeInk.Tests.csproj" --configuration Release
```

## 7. 当前可用程序目录

当前 GUI 调试基线：

```text
C:\Users\memzb\Documents\ICCCE++\artifacts\InkCanvasForClass-win-x64-portable-20260729-math-workflow-fix
```

核心 DLL：

```text
InkCanvasForClass.dll
SHA-256: 680FF04CF64E1DAC2B167B6BA4EB91B10BDA31AC64E54754F1A8633E7BFF68E7
```

该目录为：

- `win-x64`
- 自包含发布
- 503 个文件
- 约 185.74 MB
- 不包含 PDB
- 未进行数字签名

旧目录 `math-route-c` 和 `math-visible-layer-fix` 不应继续作为测试基准。

## 8. 已知限制和未验证边界

以下内容不能视为已经完成：

1. 尚未系统验证不同 DPI、缩放比例和多显示器下的数学命中位置。
2. 尚未完成真实触控屏和电磁笔的完整数学交互回归。
3. 尚未验证极大场景，例如数千个数学对象的持续拖动和页面切换。
4. 当前对象缩放主要通过鼠标滚轮，没有可见的缩放控制柄。
5. 函数和立体旋转缺少可见旋转中心与角度反馈。
6. 文本标签编辑体验仍较基础。
7. 几何构造流程主要依赖菜单模式，没有完整的对象属性面板。
8. 数学 sidecar 与主文件的打包、重命名、移动和异常恢复需要更多端到端验证。
9. 白板漫游同时修改普通笔迹和数学场景时会产生各自的历史记录，单次操作的撤销粒度仍可优化。
10. `MathCanvasControl` 仍作为隐藏的场景宿主存在，尚未完成最终清理。
11. 主程序目标框架仍是已经停止支持的 .NET 6；本阶段未升级框架。
12. 发布程序未签名，也未验证安装包升级和回滚。

## 9. 下一阶段建议顺序

### P0：真实交互稳定性

1. 验证鼠标、触摸、电磁笔分别执行插入、选择、拖动、缩放和旋转。
2. 验证 100%、125%、150%、200% DPI。
3. 验证切页、撤销、重做、保存、关闭、重新打开的完整闭环。
4. 为 `MathInkPresenter` 增加可见性、尺寸和笔迹数的自动运行时断言或诊断状态。

### P1：交互完善

1. 给选中数学对象增加选框和控制柄。
2. 提供统一的移动、缩放、旋转操作，不再主要依赖滚轮组合键。
3. 增加函数和立体属性编辑面板。
4. 增加当前数学工具和选中对象的视觉状态。
5. 统一白板漫游的撤销事务。

### P2：架构收口

1. 将 `MathScene` 从隐藏的 `MathCanvasControl.Scene` 迁移为 `MainWindow` 或独立控制器字段。
2. 删除确认无兼容用途的 `MathCanvasControl`。
3. 为场景刷新增加脏对象或局部重绘，避免大型场景每次全部重建。
4. 将结构化数学对象的历史、页面和存储协调逻辑提取到职责明确的服务，但不要借机重构无关主代码。

### P3：后续功能

1. 完善几何构造工具和对象属性编辑。
2. 增加函数多图层、图例、坐标范围编辑和不连续点显示。
3. 增加立体视角控制、剖切或展开图。
4. 完成离线数学功能后，再单独设计 AI 阶段。

## 10. 继续开发时的注意事项

- 不要再恢复旧路线 A 的自定义 `OnRender` 渲染器。
- 不要把数学对象仅保存为普通笔迹，否则会失去编辑、约束和函数表达式信息。
- 不要把生成的数学笔迹加入普通 `inkCanvas.Strokes`；应保持独立显示层。
- 每次修改模式切换或白板生命周期时，都要验证 `MathInkPresenter.Visibility` 和实际尺寸。
- 不能只验证对象数量；必须同时验证最终像素呈现。
- 保留当前工作区中与数学功能无关的已有修改。
- 用户确认之前，不提交 PR、不删除兼容数据、不升级主框架。
