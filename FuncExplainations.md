iccce
│   App.config
│   app.manifest
│   App.xaml
│   App.xaml.cs
│   AssemblyInfo.cs
│   FloatingWindowInterceptorManager.cs
│   FodyWeavers.xml
│   FodyWeavers.xsd
│   HotkeyConfig.json
│   IACore.dll
│   IALoader.dll
│   IAWinFX.dll
│   InkCanvasForClass.csproj
│   InkCanvasForClass.csproj.user
│   MainWindow.xaml
│   MainWindow.xaml.cs
│
├───bin
│   └───Debug
│       └───net472
├───Helpers
│   │   AdvancedBezierSmoothing.cs
│   │   AnimationsHelper.cs
│   │   AutoBackupManager.cs
│   │   AutoUpdateHelper.cs
│   │   AvoidFullScreenHelper.cs
│   │   CameraService.cs
│   │   Converters.cs
│   │   DelAutoSavedFiles.cs
│   │   DelayActionHelper.cs
│   │   DeviceIdentifier.cs
│   │   EdgeGestureUtil.cs
│   │   FileAssociationManager.cs
│   │   FloatingWindowInterceptor.cs
│   │   ForegroundWindowInfo.cs
│   │   FullScreenHelper.cs
│   │   FullScreenHelper.Win32.cs
│   │   GlobalHotkeyManager.cs
│   │   HardwareAcceleratedInkProcessor.cs
│   │   Hotkey.cs
│   │   IACoreDllExtractor.cs
│   │   ImprovedBezierSmoothing.cs
│   │   InkFadeManager.cs
│   │   InkRecognizeHelper.cs
│   │   InkSmoothingConfig.cs
│   │   InkSmoothingManager.cs
│   │   IsOutsideOfScreenHelper.cs
│   │   LogHelper.cs
│   │   MultiPPTInkManager.cs
│   │   MultiTouchInput.cs
│   │   PPTInkManager.cs
│   │   PPTManager.cs
│   │   PPTUIManager.cs
│   │   ScreenDetectionHelper.cs
│   │   SoftwareLauncher.cs
│   │   StartupCount.cs
│   │   TimeMachine.cs
│   │   WindowZOrderManager.cs
│   │   WinTabWindowsChecker.cs
│   │
│   └───Plugins
│       │   EnhancedPluginBase.cs
│       │   EnhancedPluginBaseV2.cs
│       │   IActionService.cs
│       │   ICCPPPluginAdapter.cs
│       │   IEnhancedPlugin.cs
│       │   IGetService.cs
│       │   IPlugin.cs
│       │   IPluginService.cs
│       │   IWindowService.cs
│       │   PluginBase.cs
│       │   PluginConfigurationManager.cs
│       │   PluginManager.cs
│       │   PluginServiceManager.cs
│       │   PluginTemplate.cs
│       │
│       └───BuiltIn
│           │   SuperLauncherPlugin.cs
│           │
│           └───SuperLauncher
│                   LauncherButton.cs
│                   LauncherModels.cs
│                   LauncherSettingsControl.xaml
│                   LauncherSettingsControl.xaml.cs
│                   LauncherWindow.xaml
│                   LauncherWindow.xaml.cs
│
├───MainWindow_cs
│   ├── ConfigHelper.cs
|   │    └── 定义了一个名为 ConfigHelper 的空类
│   ├── MW_AutoFold.cs
│   |    ├── isFloatingBarFolded (bool): 指示浮动栏是否折叠
│   |    ├── isFloatingBarChangingHideMode (bool): 指示浮动栏是否正在改变隐藏模式
│   |    ├── CloseWhiteboardImmediately(): 立即关闭白板，切换到深色主题
│   |    ├── FoldFloatingBar_MouseUp(object sender, MouseButtonEventArgs e): “折叠浮动栏”鼠标抬起事件处理程序
│   |    ├── FoldFloatingBar(object sender, bool isAutoFoldCommand = false): 折叠浮动栏的异步方法
│   |    ├── LeftUnFoldButtonDisplayQuickPanel_MouseUp(object sender, MouseButtonEventArgs e): 左侧展开按钮鼠标抬起事件处理程序
│   |    ├── RightUnFoldButtonDisplayQuickPanel_MouseUp(object sender, MouseButtonEventArgs e): 右侧展开按钮鼠标抬起事件处理程序
│   |    ├── HideLeftQuickPanel(): 隐藏左侧快速面板
│   |    ├── HideRightQuickPanel(): 隐藏右侧快速面板
│   |    ├── HideQuickPanel_MouseUp(object sender, MouseButtonEventArgs e): 隐藏快速面板鼠标抬起事件处理程序
│   |    ├── UnFoldFloatingBar_MouseUp(object sender, MouseButtonEventArgs e): 展开浮动栏鼠标抬起事件处理程序
│   |    ├── UnFoldFloatingBar(object sender): 展开浮动栏的异步方法
│   |    └── SidePannelMarginAnimation(int MarginFromEdge, bool isNoAnimation = false): 侧面板边距动画
│   ├── MW_AutoStart.cs
│   |    ├── StartAutomaticallyCreate(string exeName): 在 Windows 启动文件夹中创建应用程序的快捷方式，以实现开机自启
│   |    └── StartAutomaticallyDel(string exeName): 从 Windows 启动文件夹中删除应用程序的快捷方式，以取消开机自启
│   ├── MW_AutoTheme.cs
│   |    ├── FloatBarForegroundColor (Color): 存储浮动栏图标的前景色，该颜色根据当前主题变化
│   |    ├── SetTheme(string theme): 根据传入的主题名称（“Light”或“Dark”）加载相应的资源字典，切换整个应用的视觉主题
│   |    ├── InitializeFloatBarForegroundColor(): 从当前主题资源中加载浮动栏的前景色，并调用方法刷新按钮颜色
│   |    ├── RefreshQuickPanelIcons(): 强制重绘快速面板和侧边栏，以确保其图标在主题更改后正确显示
│   |    ├── RefreshFloatingBarButtonColors(): 根据当前选中的工具和主题颜色，更新浮动工具栏上各个按钮的图标颜色
│   |    ├── SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e): 响应系统主题更改事件，并根据应用设置（亮色、暗色或跟随系统）自动切换应用主题
│   |    └── IsSystemThemeLight(): 通过读取 Windows 注册表，判断系统当前是否为亮色主题
│   ├── MW_BoardControls.cs
│   |    ├── strokeCollections (StrokeCollection[]): 【似乎已废弃】用于存储每页笔迹的数组
│   |    ├── whiteboadLastModeIsRedo (bool[]): 【似乎已废弃】用于存储每页最后操作是否为重做的数组
│   |    ├── lastTouchDownStrokeCollection (StrokeCollection): 【似乎已废弃】用于存储上次触摸时的笔迹集合
│   |    ├── CurrentWhiteboardIndex (int): 记录当前显示的白板页面索引
│   |    ├── WhiteboardTotalCount (int): 记录白板的总页面数量
│   |    ├── TimeMachineHistories (TimeMachineHistory[][]): 核心数据结构，一个二维数组，用于存储每一页白板的完整操作历史（包括笔迹和UI元素），是实现页面内容保存与恢复的关键
│   |    ├── savedMultiTouchModeStates (bool[]): 用于记录并恢复每一页的多点触控书写模式状态的数组
│   |    ├── SaveStrokes(bool isBackupMain = false): 保存当前画布的内容（笔迹和元素）到 `TimeMachineHistories` 数组中对应页码的位置。在保存前，会确保画布上所有内容都已提交到时间机器历史记录中。同时也会保存当前页的多点触控状态
│   |    ├── ClearStrokes(bool isErasedByCode): 清除当前画布上的所有笔迹（Strokes），但不清除图片等其他 UI 元素
│   |    ├── RestoreStrokes(bool isBackupMain = false): 恢复指定页码（由 `CurrentWhiteboardIndex` 决定）的画布内容。它会先清空当前画布，然后从 `TimeMachineHistories` 数组中加载对应页码的历史记录，并逐一应用到画布上，从而重建笔迹和元素。同时恢复该页的多点触控状态
│   |    ├── RestoreMultiTouchModeState(int pageIndex): 根据指定页码保存的状态，恢复多点触控书写模式（启用或禁用），并同步更新 UI 上的开关状态
│   |    ├── BtnWhiteBoardPageIndex_Click(object sender, EventArgs e): 处理白板两侧页码列表按钮的点击事件，用于显示或隐藏页面列表面板，并自动滚动到当前页
│   |    ├── BtnWhiteBoardSwitchPrevious_Click(object sender, EventArgs e): “上一页”按钮的点击事件处理程序。保存当前页内容，切换到前一页，加载该页内容，并更新页码显示
│   |    ├── BtnWhiteBoardSwitchNext_Click(object sender, EventArgs e): “下一页”按钮的点击事件处理程序。如果当前是最后一页，则功能同“新建页面”；否则，保存当前页，切换到下一页，并加载该页内容
│   |    ├── BtnWhiteBoardAdd_Click(object sender, EventArgs e): “新建页面”按钮的点击事件处理程序。在当前页之后插入一个空白新页面，并自动切换到该新页面
│   |    ├── BtnWhiteBoardDelete_Click(object sender, RoutedEventArgs e): “删除页面”按钮的点击事件处理程序。删除当前白板页面，并加载相邻页面
│   |    └── UpdateIndexInfoDisplay(): 更新 UI 上的页码显示（如“2/5”），并根据当前页码和总页数，动态调整“上一页”、“下一页/新页面”、“删除”等按钮的可用状态和文本
│   ├── MW_BoardIcons.cs
│   |    ├── BackgroundPalette (Border): 一个私有属性，用于持有动态创建的背景设置面板的引用
│   |    ├── CustomBackgroundColor (Color?): 一个私有属性，用于存储用户选择的自定义背景颜色
│   |    ├── BoardChangeBackgroundColorBtn_MouseUp(object sender, RoutedEventArgs e): 背景切换按钮的鼠标抬起事件。主要功能是创建（如果需要）并显示/隐藏背景设置面板
│   |    ├── CreateBackgroundPalette(): 动态地在代码中创建整个背景设置UI面板，包括“白板/黑板”模式切换、用于自定义颜色的RGB滑块和颜色预览、以及“应用”按钮
│   |    ├── UpdateBackgroundButtonsState(): 更新背景设置面板中“白板”和“黑板”按钮的视觉状态（如高亮），以反映当前的活动模式
│   |    ├── UpdateColorPreview(Border colorPreview, ...): 当用户拖动RGB滑块时，实时更新背景设置面板中的颜色预览框
│   |    ├── ApplyCustomBackgroundColor(Color color): 应用用户通过RGB滑块选择的自定义颜色，将其设置到画布背景上，并保存到配置文件中
│   |    ├── LoadCustomBackgroundColor(): 从配置文件中加载之前保存的自定义背景色，并在程序启动或面板创建时应用它
│   |    ├── UpdateRGBSliders(Color color): 根据一个给定的颜色值，反向更新RGB滑块的位置，用于同步UI状态（例如加载设置或选择默认颜色后）
│   |    ├── BoardLassoIcon_Click(object sender, RoutedEventArgs e): 套索选择工具图标的点击事件，将画布的编辑模式切换到“选择”
│   |    ├── BoardEraserIconByStrokes_Click(object sender, RoutedEventArgs e): 笔画橡皮擦图标的点击事件，将画布的编辑模式切换到“按笔画擦除”
│   |    ├── BoardSymbolIconDelete_MouseUp(object sender, RoutedEventArgs e): “清空画布”图标的点击事件。清除所有笔迹，并根据用户设置决定是否同时清除图片等元素
│   |    ├── BoardSymbolIconDeleteInkAndHistories_MouseUp(object sender, RoutedEventArgs e): “清空画布和历史记录”图标的点击事件。除了清空画布外，还会清空所有的撤销/重做历史
│   |    ├── BoardLaunchEasiCamera_MouseUp(object sender, MouseButtonEventArgs e): “启动希沃展台”图标的点击事件，用于启动外部的视频展台应用程序
│   |    └── BoardLaunchDesmos_MouseUp(object sender, MouseButtonEventArgs e): “启动Desmos”图标的点击事件，用于在浏览器中打开Desmos科学计算器网站
│   ├── MW_ClipboardHandler.cs
│   |    ├── isClipboardMonitoringEnabled (bool): 指示剪贴板监控是否已启用的标志位
│   |    ├── lastClipboardImage (BitmapSource): 存储剪贴板中检测到的上一张图片，用于避免对同一张图片重复弹出通知
│   |    ├── InitializeClipboardMonitoring(): 初始化剪贴板监控，开始通过定时器监听剪贴板内容的变化
│   |    ├── OnClipboardUpdate(): 剪贴板内容更新时的事件处理程序。当检测到新的图片时，会显示一个粘贴提示
│   |    ├── ShowPasteNotification(): 在界面上显示一个通知，提示用户剪贴板中有图片可以粘贴
│   |    ├── ShowPasteContextMenu(Point position): 在指定的鼠标位置显示一个包含“粘贴图片”选项的右键上下文菜单
│   |    ├── PasteImageFromClipboard(Point? position = null): 核心功能函数，从剪贴板获取图片，将其创建为一个可交互的 Image 元素，添加到画布上，并为其绑定移动、缩放等操作事件
│   |    ├── InkCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e): 处理在画布上的鼠标右键抬起事件，如果剪贴板有图片，则触发显示粘贴菜单
│   |    ├── HandleGlobalPaste(object sender, ExecutedRoutedEventArgs e): 处理全局粘贴命令（如 Ctrl+V 快捷键），调用粘贴图片的核心逻辑
│   |    ├── CleanupClipboardMonitoring(): 在程序关闭或功能禁用时，清理并停止剪贴板监控，以释放资源
│   |    └── ClipboardNotification (static class): 一个辅助类，通过定时轮询的方式模拟实现了一个“剪贴板更新”事件，因为WPF本身不直接提供此事件
│   ├── MW_Colors.cs
│   |    ├── inkColor (int): 存储当前普通笔选择的颜色代码（例如 0=黑, 1=红, ...）
│   |    ├── isUselightThemeColor (bool): 标志位，指示当前是否应使用亮色系的颜色集
│   |    ├── isDesktopUselightThemeColor (bool): 标志位，专门记录桌面模式下的亮/暗色系偏好
│   |    ├── penType (int): 笔的类型 (0 = 普通笔, 1 = 荧光笔)
│   |    ├── lastDesktopInkColor (int): 记录在桌面模式下最后使用的普通笔颜色
│   |    ├── lastBoardInkColor (int): 记录在白板模式下最后使用的普通笔颜色
│   |    ├── highlighterColor (int): 存储当前荧光笔选择的颜色代码
│   |    ├── ColorSwitchCheck(): 在选择一个新颜色后被调用的核心处理函数。它负责将新颜色应用到已选择的笔迹上，提交颜色更改历史，并将画布切换回书写模式
│   |    ├── CheckColorTheme(bool changeColorTheme = false): 关键的UI更新函数。根据当前模式（桌面/白板）、笔类型、颜色代码和亮/暗色系，设置正确的画笔颜色、更新调色板中所有颜色按钮的背景色，并高亮显示当前选中的颜色
│   |    ├── CheckLastColor(int inkColor, bool isHighlighter = false): 一个辅助函数，用于将用户新选择的颜色代码保存到对应的“最后使用颜色”变量中（区分桌面/白板模式和荧光笔）
│   |    ├── CheckPenTypeUIState(): 更新颜色选择面板的UI，根据当前是普通笔模式还是荧光笔模式，显示/隐藏对应的颜色和属性设置区域，并更新Tab按钮的视觉样式
│   |    ├── SwitchToDefaultPen(object sender, MouseButtonEventArgs e): “普通笔”Tab按钮的点击事件，切换到普通笔模式，并更新笔的物理属性（如笔尖形状）
│   |    ├── SwitchToHighlighterPen(object sender, MouseButtonEventArgs e): “荧光笔”Tab按钮的点击事件，切换到荧光笔模式，并更新笔的物理属性
│   |    ├── BtnColor..._Click(...): 一系列普通笔颜色按钮（黑、红、绿等）的点击事件处理程序。它们调用 `CheckLastColor` 和 `ColorSwitchCheck` 来更新和应用颜色
│   |    ├── BtnHighlighterColor..._Click(...): 一系列荧光笔颜色按钮的点击事件处理程序。它们调用 `CheckLastColor` 和 `ColorSwitchCheck` 来更新和应用颜色
│   |    ├── StringToColor(string colorStr): 工具函数，将#AARRGGBB格式的十六进制颜色字符串转换为WPF的Color对象
│   |    └── toByte(char c): `StringToColor` 使用的辅助函数，将单个十六进制字符转换为其对应的字节值
│   ├── MW_ElementsControls.cs
│   |    ├── currentSelectedElement (FrameworkElement): 存储当前被用户选中的UI元素（如一张图片）
│   |    ├── isDragging (bool): 标志位，指示当前是否正在拖动一个元素
│   |    ├── dragStartPoint (Point): 记录拖动操作开始时的鼠标位置
│   |    ├── #region Image
│   |    |    ├── BtnImageInsert_Click(...): “插入图片”按钮的点击事件。打开文件选择对话框，让用户选择图片并将其插入到画布中
│   |    |    ├── InitializeElementTransform(FrameworkElement element): 为新元素初始化变换组（TransformGroup），这是实现移动、缩放和旋转的基础
│   |    |    ├── BindElementEvents(FrameworkElement element): 为新元素绑定所有必要的鼠标和触摸事件处理器，使其变得可交互
│   |    |    ├── Element_MouseLeftButtonDown(...): 元素上鼠标左键按下的事件。用于选中元素并准备开始拖动
│   |    |    ├── Element_MouseLeftButtonUp(...): 元素上鼠标左键抬起的事件。用于结束拖动操作
│   |    |    ├── Element_MouseMove(...): 元素上鼠标移动的事件。在拖动状态下，根据鼠标位移实时更新元素位置
│   |    |    ├── Element_MouseWheel(...): 元素上鼠标滚轮滚动的事件。用于以元素中心为基点进行缩放
│   |    |    ├── Element_TouchDown(...): 触摸按下事件，功能同鼠标左键按下
│   |    |    ├── Element_TouchUp(...): 触摸抬起事件，功能同鼠标左键抬起
│   |    |    ├── Element_ManipulationDelta(...): 核心的触摸手势处理事件。它能同时处理单指拖动、双指缩放和旋转
│   |    |    ├── Element_ManipulationCompleted(...): 触摸手势完成后的事件
│   |    |    ├── ApplyTranslate/Scale/RotateTransform(...): 底层的变换应用函数，将平移、缩放或旋转应用到元素的变换组中
│   |    |    ├── SelectElement(FrameworkElement element): 选中一个元素的处理逻辑，会显示对应的操作工具栏（如图片工具栏）和缩放手柄
│   |    |    ├── UnselectElement(FrameworkElement element): 取消选中一个元素的处理逻辑，隐藏操作工具栏和缩放手柄
│   |    |    ├── Apply...Transform(...): 一系列更底层的、基于矩阵（Matrix）的变换函数，用于实现更精确的拖动和缩放控制
│   |    |    ├── CommitTransformHistory(...): 将元素的变换操作提交到时间机器，以支持撤销/重做（此功能目前可能未完全实现）
│   |    |    └── CreateAndCompressImageAsync(...): 一个异步辅助方法。它负责将用户选择的图片复制到本地依赖文件夹、根据设置进行压缩，并创建一个WPF Image对象
│   |    ├── #region Media
│   |    |    ├── BtnMediaInsert_Click(...): “插入媒体”按钮的点击事件，用于插入视频文件
│   |    |    └── CreateMediaElementAsync(...): 异步辅助方法，用于创建WPF MediaElement对象并处理视频文件
│   |    ├── #region Image Operations
│   |    |    ├── RotateImage(Image image, double angle): 旋转指定图片一定角度
│   |    |    ├── CloneImage(Image image): 在当前页面克隆（复制）一张图片
│   |    |    ├── CloneImageToNewBoard(Image image): 将图片克隆到一个新的白板页面
│   |    |    ├── ScaleImage(Image image, double scaleFactor): 缩放指定图片
│   |    |    └── DeleteImage(Image image): 删除指定的图片，并记录到历史记录中
│   |    ├── CenterAndScaleElement(FrameworkElement element): 将新插入的元素自动缩放到合适的大小并放置在画布中央
│   |    ├── InitializeInkCanvasSelectionSettings(): 初始化画布的选择设置，主要是为了禁用InkCanvas自带的选择框，使用自定义的UI
│   |    ├── UpdateImageSelectionToolbarPosition(...): 当图片移动或缩放后，更新其下方操作工具栏的位置
│   |    ├── GetElementActualBounds(FrameworkElement element): 获取元素在画布上的实际边界矩形，会综合考虑其位置、尺寸和所有变换效果
│   |    ├── #region Image Selection Toolbar Event Handlers
│   |    |    ├── BorderImageClone_MouseUp(...): 图片工具栏上“克隆”按钮的点击事件
│   |    |    ├── BorderImageCloneToNewBoard_MouseUp(...): 图片工具栏上“克隆到新页面”按钮的点击事件
│   |    |    ├── BorderImageRotateLeft/Right_MouseUp(...): 图片工具栏上“左/右旋转”按钮的点击事件
│   |    |    ├── GridImageScaleDecrease/Increase_MouseUp(...): 图片工具栏上“缩小/放大”按钮的点击事件
│   |    |    ├── BorderImageDelete_MouseUp(...): 图片工具栏上“删除”按钮的点击事件
│   |    |    ├── CreateClonedImage(...): 创建图片副本的辅助方法
│   |    |    ├── CloneStrokes(...): 克隆选中的笔迹
│   |    |    └── CloneStrokesToNewBoard(...): 将选中的笔迹克隆到新页面
│   |    └── #region Image Resize Handles
│   |         ├── isResizingImage (bool): 标志位，指示当前是否正在通过缩放手柄调整图片大小
│   |         ├── imageResizeStartPoint (Point): 记录缩放操作开始时的鼠标位置
│   |         ├── activeResizeHandle (string): 记录当前正在被拖动的缩放手柄的名称（如“左上角”、“右边”等）
│   |         ├── Show/Hide/UpdateImageResizeHandlesPosition(...): 控制图片周围八个缩放手柄的显示、隐藏和位置更新
│   |         ├── ImageResizeHandle_Mouse...(...): 缩放手柄自身的鼠标事件（按下、抬起、移动），用于启动和执行缩放操作
│   |         └── ResizeImageByHandle(...): 核心的图片缩放逻辑。根据被拖动的不同手柄，精确计算并应用图片的尺寸和位置变化
│   ├── MW_Eraser.cs
│   |    ├── isUsingGeometryEraser (bool): 核心状态标志，指示当前是否正在进行一次擦除操作（从鼠标按下到抬起）。用于防止重入和管理状态。
│   |    ├── hitTester (IncrementalStrokeHitTester): WPF的高性能类，用于在橡皮擦移动时持续检测与笔迹的碰撞。这是几何橡皮擦（切割笔迹）功能的核心。
│   |    ├── eraserWidth (double): 当前橡皮擦的宽度（像素）。
│   |    ├── isEraserCircleShape (bool): 标志位，决定橡皮擦的形状是圆形还是矩形。
│   |    ├── isUsingStrokesEraser (bool): 标志位，用于在两种主要橡皮擦模式间切换：几何擦除（切割笔迹）和笔画擦除（碰触即删除整条笔画）。
│   |    ├── scaleMatrix (Matrix): 用于缩放橡皮擦视觉反馈图像的变换矩阵。
│   |    ├── eraserOverlayCanvas (Canvas): 一个透明的、覆盖在主InkCanvas之上的画布，专门用于捕获所有与橡皮擦相关的鼠标/触摸输入。
│   |    ├── eraserFeedback (Image): 在橡皮擦覆盖层上显示的图像，作为橡皮擦的自定义光标，为用户提供视觉反馈。
│   |    ├── IsLockGuid (Guid): 一个唯一的标识符，用于给笔画添加属性，以实现“锁定”功能，防止它们被擦除。
│   |    ├── EraserOverlayCanvas_Loaded(...): 橡皮擦覆盖层加载时的初始化函数。它获取UI控件的引用并为覆盖层绑定所有必要的鼠标和手写笔事件。
│   |    ├── UpdateEraserStyle(): 根据当前的橡皮擦形状（圆形/矩形）设置，更新`eraserFeedback`图像的显示源。
│   |    ├── EraserOverlay_PointerDown(...): 当在覆盖层上按下鼠标或触摸时触发。它会启动一次擦除会话，设置状态标志，并初始化`hitTester`准备进行碰撞检测。
│   |    ├── EraserOverlay_PointerUp(...): 当在覆盖层上释放鼠标或触摸时触发。它负责结束擦除会-话，清理资源（如`hitTester`），隐藏橡皮擦光标，并向时间机器提交擦除历史以支持撤销。
│   |    ├── EraserOverlay_PointerMove(...): 当鼠标或触摸在覆盖层上移动时触发。这是擦除操作的核心执行部分，它根据`isUsingStrokesEraser`标志进行分支：
│   |    |    └── 如果是“笔画擦除”模式，它会直接检测并删除光标下的整个笔画。
│   |    |    └── 如果是“几何擦除”模式，它会更新橡皮擦光标的位置，并将移动的坐标点喂给`hitTester`进行碰撞检测。
│   |    ├── EraserGeometry_StrokeHit(...): 当`hitTester`检测到橡皮擦路径与笔画发生碰撞时触发的回调函数。它使用`GetPointEraseResults()`计算出笔画被“切割”后的剩余部分，然后用这些新笔画替换掉画布上的原始笔画，从而实现精确擦除效果。
│   |    ├── EnableEraserOverlay(): 激活橡皮擦功能。使覆盖层可见并可以接收输入，用户此时的操作将被解释为擦除。
│   |    ├── DisableEraserOverlay(): 禁用橡皮擦功能。隐藏覆盖层，重置所有状态，将输入交还给主InkCanvas进行书写等操作。
│   |    ├── UpdateEraserSize(): 根据用户在设置中选择的尺寸等级（小、中、大等）和形状，计算出橡皮擦的实际像素宽度。
│   |    ├── ToggleEraserShape(): 切换橡皮擦的形状（圆形/矩形）。
│   |    ├── ToggleEraserMode(): 切换橡皮擦的模式（几何擦除/笔画擦除）。
│   |    ├── ApplyAdvancedEraserShape(): 将当前高级橡皮擦的配置（尺寸、形状）应用到InkCanvas自带的`EraserShape`属性上。这可能是为了在某些模式下保持与内置功能的兼容性。
│   |    └── GetEraserStatusInfo(): 一个调试辅助函数，返回一个包含当前橡皮擦所有状态（尺寸、形状、模式等）的字符串。
│   ├── MW_Eraser.xaml
│   |    ├── RectangleEraserImageSource: 矩形橡皮擦图像资源
│   |    └── EllipseEraserImageSource: 圆形橡皮擦图像资源
│   ├── MW_FloatingBarIcons.cs
│   |    ├── _currentToolMode (string): 一个重要的内部变量，用于缓存当前激活的工具模式（如 "pen", "eraser", "select"），确保UI状态同步的准确性，避免直接依赖可能延迟更新的 `inkCanvas.EditingMode`。
│   |
│   |    ├── #region "手勢"按鈕 (Gesture Button Region)
│   |    |    ├── TwoFingerGestureBorder_MouseUp(...): “手势”按钮的点击事件，用于显示或隐藏双指手势设置面板。
│   |    |    ├── CheckEnableTwoFingerGestureBtnColorPrompt(): 更新“手势”按钮的视觉样式（图标、颜色、背景），以反映当前手势功能是启用、禁用，还是因“多指同书”模式而被强制禁用。
│   |    |    └── CheckEnableTwoFingerGestureBtnVisibility(...): 根据当前模式（如PPT放映、白板、桌面）和用户设置，决定是否应该显示“手势”按钮。
│   |    ├── #region 浮動工具欄的拖動實現 (Floating Bar Dragging Implementation)
│   |    |    ├── isDragDropInEffect (bool): 标志位，指示当前是否正在拖动浮动工具栏。
│   |    |    ├── pos, downPos, pointDesktop, pointPPT (Point): 用于存储拖动过程中的坐标，并分别记忆在桌面模式和PPT模式下的最后位置。
│   |    |    ├── SymbolIconEmoji_MouseMove(...): 拖动按钮上的鼠标移动事件，实现工具栏跟随鼠标移动。
│   |    |    ├── SymbolIconEmoji_MouseDown(...): 拖动按钮上的鼠标按下事件，启动拖动状态。
│   |    |    └── SymbolIconEmoji_MouseUp(...): 拖动按钮上的鼠标抬起事件。它不仅结束拖动，还兼具“单击”功能：如果移动距离很小，则切换主工具栏的显示/隐藏。
│   |    ├── #region 隱藏子面板和按鈕背景高亮 (Hide Sub-panels and Button Highlighting)
│   |    |    ├── CollapseBorderDrawShape(): 隐藏形状绘制面板。
│   |    |    ├── HideSubPanelsImmediately(): 立即隐藏所有可能弹出的二级面板，无动画效果。
│   |    |    └── HideSubPanels(string mode, bool autoAlignCenter): **核心多功能函数**。负责：
│   |    |         └── 1. 用动画隐藏所有二级面板（颜色盘、工具箱、设置页等）。
│   |    |         └── 2. 根据传入的 `mode` 参数，重置所有工具按钮的样式，然后高亮当前激活的工具按钮（如点击画笔按钮时，传入"pen"来高亮它）。
│   |    |         └── 3. 根据 `autoAlignCenter` 参数决定是否在操作后自动将浮动工具栏居中。
│   |    ├── #region 主要的工具按鈕事件 (Main Tool Button Events)
│   |    |    ├── CursorIcon_Click(...): “鼠标/选择”按钮点击事件。将程序切换到穿透模式，允许用户操作桌面或PPT，同时根据设置隐藏或保留笔迹。
│   |    |    ├── PenIcon_Click(...): “画笔”按钮点击事件。激活书写模式，显示画布，并处理点击时展开/收起颜色面板的逻辑。
│   |    |    ├── EraserIcon_Click(...): “区域擦除”按钮点击事件。激活高级几何橡皮擦功能，并处理点击时展开/收起橡皮擦尺寸面板的逻辑。
│   |    |    ├── EraserIconByStrokes_Click(...): “笔画擦除”按钮点击事件。切换到按整条笔画擦除的模式。
│   |    |    ├── SymbolIconSelect_MouseUp(...): “套索选择”按钮点击事件。将画布切换到选择模式。
│   |    |    ├── SymbolIconUndo_MouseUp / SymbolIconRedo_MouseUp(...): “撤销”和“重做”按钮的点击事件。
│   |    |    ├── ImageBlackboard_MouseUp(...): “进入/退出白板”按钮点击事件。管理桌面模式和白板模式之间的切换，包括保存/恢复笔迹、UI布局变化、背景切换等复杂逻辑。
│   |    |    ├── SymbolIconDelete_MouseUp(...): “清空”按钮点击事件。清除画布上的笔迹（和可选的图片），并根据设置自动截图保存。
│   |    |    ├── SymbolIconTools_MouseUp(...): “更多工具”按钮点击事件。用于显示/隐藏包含截图、计时器等功能的工具面板。
│   |    |    ├── SymbolIconSettings_Click(...): “设置”按钮点击事件，调用逻辑显示或隐藏设置侧边栏。
│   |    |    ├── SymbolIconScreenshot_MouseUp(...): “截图”按钮点击事件，调用截图功能。
│   |    |    └── QuickColor..._Click(...): 一系列快捷调色盘颜色按钮的点击事件，用于快速切换画笔颜色。
│   |    ├── #region 动态按钮位置计算和高光显示 (Dynamic Button Position and Highlight)
│   |    |    ├── SetFloatingBarHighlightPosition(string mode): **核心UI函数**。精确计算浮动工具栏上高亮指示器的位置，使其能准确对齐到当前激活的工具按钮下方。它会动态考虑快捷调色盘是否显示及其宽度，确保对齐的精确性。
│   |    |    ├── HideFloatingBarHighlight(): 隐藏高亮指示器。
│   |    |    ├── UpdateCurrentToolMode(string mode): 更新 `_currentToolMode` 缓存变量的值。
│   |    |    └── Get.../Is.../Update...: 其他用于位置计算和状态更新的辅助函数。
│   |    ├── #region 墨迹重播 (Ink Replay Region)
│   |    |    ├── GridInkReplayButton_MouseUp(...): “墨迹重播”按钮的点击事件。启动一个新线程，在一个临时画布上逐点绘制当前画布的笔迹，实现动画效果。
│   |    |    ├── isStop/Pause/RestartInkReplay (bool), inkReplaySpeed (double): 控制重播过程的状态变量。
│   |    |    └── InkReplay..._OnMouseUp(...): 重播控制面板上“暂停/播放”、“停止”、“重播”、“倍速”按钮的事件处理。
│   |    └── #region 其他功能与辅助方法
│   |         ├── ViewboxFloatingBarMarginAnimation(...), Pure...Animation(...): 控制浮动工具栏在不同模式（桌面/PPT）下自动归位到屏幕底部的动画效果。
│   |         ├── FloatingBarToolBtnMouseDown/LeaveFeedback_Panel(...): 为浮动栏按钮提供点击时的视觉反馈（背景色变化）。
│   |         ├── AddTouchSupportToFloatingBarButtons(): 为浮动栏按钮添加触摸和笔输入支持，使其行为与鼠标点击一致。
│   |         ├── 其他如随机点名 `SymbolIconRand_MouseUp`、计时器 `ImageCountdownTimer_MouseUp` 等独立小工具的启动入口。
│   |         └── 各种辅助函数，如 `Btn_IsEnabledChanged` (根据按钮可用性改变其透明度)，`ResetTouchStates` (重置触摸状态) 等。
│   ├── MW_FloatingWindowInterceptor.cs
│   |    ├── #region 悬浮窗拦截功能 (Floating Window Interception Feature)
│   |    |    ├── InitializeFloatingWindowInterceptor(): 初始化悬浮窗拦截系统的总入口。它会创建一个 `FloatingWindowInterceptorManager` 实例，订阅相关事件，并根据用户的设置启动拦截器。
│   |    |    ├── LoadFloatingWindowInterceptorUI(): 在程序加载时，根据保存的设置来更新设置界面中所有相关的开关（ToggleSwitch）的状态，确保UI与配置同步。
│   |    |    ├── UpdateFloatingWindowInterceptorUI(): 更新设置界面中关于拦截器状态的文本（如“拦截器运行中 - 已启用 5/13 个规则”），并根据主开关状态决定是否显示详细的规则设置。
│   |    |    ├── OnFloatingWindowIntercepted(...): 当 `FloatingWindowInterceptorManager` 成功检测并隐藏了一个目标悬浮窗时触发的事件回调。它会调用 `UpdateFloatingWindowInterceptorUI` 来刷新界面状态。
│   |    |    └── OnFloatingWindowRestored(...): 当被隐藏的悬浮窗被（由用户或其他程序）恢复显示时触发的事件回调。同样用于更新UI状态。
│   |    └── #region 悬浮窗拦截事件处理 (Floating Window Interception Event Handlers)
│   |         ├── ToggleSwitchFloatingWindowInterceptorEnabled_Toggled(...): “启用悬浮窗拦截”主开关的切换事件。当用户打开或关闭此开关时，会启动或停止整个拦截服务，并保存设置。
│   |         ├── ToggleSwitchSeewo... / ToggleSwitchHite... / etc. _Toggled(...): 针对**每一种**特定软件（如希沃白板5、鸿合屏幕书写等）的拦截规则开关的切换事件。每个开关都对应一个具体的拦截目标。
│   |         └── SetInterceptRule(FloatingWindowInterceptor.InterceptType type, bool enabled): 核心的规则设置函数。当任何一个规则开关被切换时，它会被调用来：
│   |              └── 1. 通知 `FloatingWindowInterceptorManager` 启用或禁用对特定类型悬浮窗的拦截。
│   |              └── 2. 更新内存中的设置对象。
│   |              └── 3. 处理规则之间的父子关系（例如，一个总开关可以控制多个子开关）。
│   |              └── 4. 更新UI并保存设置到文件。
│   ├── MW_Hotkeys.cs
│   |    ├── Window_MouseWheel(object sender, MouseWheelEventArgs e): 监听整个窗口的鼠标滚轮事件。当处于PPT放映模式时，将滚轮向上滚动映射为“上一页”（PageUp），向下滚动映射为“下一页”（PageDown），并将这些按键消息直接发送给PPT放映窗口。
│   |    ├── Main_Grid_PreviewKeyDown(object sender, KeyEventArgs e): 监听主网格的键盘按下事件。当处于PPT放映模式时，拦截常见的翻页键（如方向键、PageUp/Down、空格键等），并将它们转换为对应的“上一页”或“下一页”命令发送给PPT。
│   |    ├── HotKey_Undo(object sender, ExecutedRoutedEventArgs e): 全局撤销快捷键的处理函数，调用撤销按钮的点击逻辑。
│   |    ├── HotKey_Redo(object sender, ExecutedRoutedEventArgs e): 全局重做快捷键的处理函数，调用重做按钮的点击逻辑。
│   |    ├── HotKey_Clear(object sender, ExecutedRoutedEventArgs e): 全局清屏快捷键的处理函数，调用清空画布按钮的点击逻辑。
│   |    ├── KeyExit(object sender, ExecutedRoutedEventArgs e): 退出快捷键（通常是Esc）的处理函数，如果当前在PPT放映模式，则调用退出放映的逻辑。
│   |    ├── KeyChangeToDrawTool(object sender, ExecutedRoutedEventArgs e): 切换到“画笔工具”的快捷键处理函数。
│   |    ├── KeyChangeToQuitDrawTool(object sender, ExecutedRoutedEventArgs e): 退出“绘制模式”的快捷键（通常是Alt+Q）处理函数。它的行为是智能的：如果在白板模式，则退出白板；如果在桌面批注模式，则切换到鼠标穿透模式。
│   |    ├── KeyChangeToSelect(object sender, ExecutedRoutedEventArgs e): 切换到“选择工具”的快捷键处理函数。
│   |    ├── KeyChangeToEraser(object sender, ExecutedRoutedEventArgs e): 切换到“橡皮擦工具”的快捷键处理函数。它会智能地在“区域擦除”和“笔画擦除”之间切换。
│   |    ├── KeyChangeToBoard(object sender, ExecutedRoutedEventArgs e): 切换“白板模式”的快捷键处理函数，相当于点击了进入/退出白板的按钮。
│   |    ├── KeyCapture(object sender, ExecutedRoutedEventArgs e): 截图快捷键的处理函数，调用截图并保存到桌面的功能。
│   |    ├── KeyDrawLine(object sender, ExecutedRoutedEventArgs e): 绘制“直线”形状的快捷键处理函数。
│   |    └── KeyHide(object sender, ExecutedRoutedEventArgs e): 隐藏/显示“主工具栏”的快捷键处理函数，功能等同于单击浮动工具栏的拖动区域。
│   ├── MW_Icons.cs
│   |    └── XamlGraphicsIconGeometries (static class): 一个静态类，充当图标几何数据的命名空间或容器。
│   |         ├── LinedCursorIcon (string): 存储“线性/描边风格”鼠标光标图标的路径数据。
│   |         ├── SolidCursorIcon (string): 存储“实心填充风格”鼠标光标图标的路径数据。
│   |         ├── LinedPenIcon (string): 存储“线性”画笔图标的路径数据。
│   |         ├── SolidPenIcon (string): 存储“实心”画笔图标的路径数据。
│   |         ├── ... (以此类推): 文件中包含了各种工具（橡皮擦、套索选择、手势等）的多种风格（Lined/Solid，新版/旧版Legacy）的图标路径数据。
│   |         └── EnabledGestureIconBadgeCheck (string): 这是一个特殊的图标片段，代表一个“选中”状态的对勾角标，可以与其他图标组合使用。
│   ├── MW_ImageInsert.cs
│   |    ├── ScreenshotResult (struct): 一个自定义的数据结构，用于封装截图操作的结果，可以包含截图的矩形区域、不规则截图的路径点、或者来自摄像头的图像数据。
│   |    ├── CaptureScreenshotAndInsert(): **核心入口函数之一**。它协调整个“截图并插入”的流程：
│   |    |    └── 1. 临时隐藏主窗口，避免截到自己。
│   |    |    └── 2. 调用 `ShowScreenshotSelector()` 启动截图界面。
│   |    |    └── 3. 截图结束后，恢复主窗口显示。
│   |    |    └── 4. 分析截图结果，如果是屏幕截图，则调用 `CaptureScreenArea()` 截取图像；如果是任意形状截图，则再调用 `ApplyShapeMask()` 进行裁剪。
│   |    |    └── 5. 最后，调用 `InsertScreenshotToCanvas()` 或 `InsertBitmapSourceToCanvas()` 将最终的图像插入画布。
│   |    ├── CaptureFullScreenAndInsert(): 另一个核心入口，用于实现“全屏截图并插入”的功能，流程与上面类似，但跳过了区域选择步骤。
│   |    ├── ShowScreenshotSelector(): 异步方法，负责创建并显示一个专门的截图选择窗口（`ScreenshotSelectorWindow`，代码未在此文件中）。它等待用户完成截图操作（选择区域、绘制形状或使用摄像头）并返回 `ScreenshotResult` 结果。
│   |    ├── CaptureScreenArea(Rectangle area): 一个底层工具函数，使用 `System.Drawing` 的 `CopyFromScreen` 方法来截取屏幕上指定矩形区域的图像，并返回一个 `Bitmap` 对象。
│   |    ├── InsertScreenshotToCanvas(Bitmap bitmap): 将一个 `System.Drawing.Bitmap` 对象（通常来自屏幕截图）转换为WPF可用的 `Image` 控件，并将其添加到画布中。此过程包括：
│   |    |    └── 1. 调用 `ConvertBitmapToBitmapSource()` 进行格式转换。
│   |    |    └── 2. 创建 `Image` 控件，并为其绑定交互事件（拖动、缩放等）。
│   |    |    └── 3. 调用 `CenterAndScaleScreenshot()` 将图片自动居中并缩放到合适大小。
│   |    |    └── 4. 将插入操作记录到时间机器中以支持撤销。
│   |    ├── InsertBitmapSourceToCanvas(BitmapSource bitmapSource): 功能与上一个方法类似，但它直接处理WPF的 `BitmapSource` 对象，主要用于插入来自摄像头捕获的图像。
│   |    ├── InitializeScreenshotTransform(Image image): 为新插入的截图图片初始化变换组（TransformGroup），为后续的移动、缩放等交互做准备。
│   |    ├── BindScreenshotEvents(Image image): 为截图图片绑定所有必要的鼠标和触摸事件，使其可交互（复用了 `MW_ElementsControls.cs` 中的事件处理器）。
│   |    ├── CenterAndScaleScreenshot(Image image): 专门为截图优化的居中和缩放方法。它会将截图缩放至画布尺寸的80%以内并居中，以获得更好的初始展示效果。
│   |    ├── ApplyShapeMask(Bitmap bitmap, List<Point> path, Rectangle area): **高级功能**。当用户进行不规则形状（如手绘套索）截图时，此方法会根据用户绘制的路径点，创建一个图形蒙版，并将其应用到矩形截图中，从而裁剪出用户想要的任意形状。
│   |    ├── ConvertBitmapToBitmapSource(...): 一个健壮的格式转换函数，负责将 `System.Drawing.Bitmap` 转换为WPF的 `BitmapSource`。它包含了主方法、备用方法（使用内存流）和最简单的方法（使用临时文件），以确保在各种情况下都能成功转换。
│   |    └── GetDpiScale(): 一个辅助函数，用于获取当前屏幕的DPI缩放比例，这对于在不同分辨率和缩放设置的屏幕上进行精确坐标计算至关重要。
│   ├── MW_Notification.cs
│   |    ├── lastNotificationShowTime (int): 记录上一次通知显示时的时间戳。用于确保只有最新的通知在显示时间结束后才会被隐藏，避免旧通知的隐藏计时器错误地关闭了新弹出的通知。
│   |    ├── notificationShowTime (int): 定义通知在屏幕上停留的默认时长（毫秒）。
│   |    ├── ShowNewMessage(string notice, bool isShowImmediately = true): 一个公共静态方法，提供了一种从程序中任何地方调用以显示通知的便捷方式。它会自动查找当前的主窗口实例，并调用其 `ShowNotification` 方法来显示消息。
│   |    └── ShowNotification(string notice, bool isShowImmediately = true): 核心的通知显示函数。它负责将传入的文本设置到通知UI上，使用动画（从底部滑入并淡入）显示通知面板，并启动一个后台线程。该线程会在等待预设时间后，以动画（滑出并淡出）方式自动隐藏通知面板。
│   ├── MW_PageListView.cs
│   |    ├── PageListViewItem (private class): 一个内部数据结构，用于表示页面列表中的单个项目。它包含页面的索引（`Index`）和一个用于在缩略图中显示的笔迹集合（`Strokes`）。
│   |    ├── blackBoardSidePageListViewObservableCollection (ObservableCollection): 一个可观察集合，作为两侧页面缩略图列表的动态数据源。对这个集合的任何更改（增、删、改）都会自动更新UI。
│   |    ├── RefreshBlackBoardSidePageListView(): 核心的缩略图列表刷新函数。它会遍历所有白板页面的历史记录（`TimeMachineHistories`），为每一页生成一个包含笔迹的缩略图数据（`PageListViewItem`），并用这些数据填充或更新 `blackBoardSidePageListViewObservableCollection`。它会特别处理当前正在编辑的页面，确保其缩略图实时反映最新笔迹。
│   |    ├── ScrollViewToVerticalTop(FrameworkElement element, ScrollViewer scrollViewer): 一个静态工具方法，用于将滚动视图（`ScrollViewer`）自动滚动，使得指定的目标元素（`element`）正好位于可见区域的顶部。常用于在打开页面列表时，自动定位到当前页的缩略图。
│   |    ├── BlackBoardLeftSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e): 左侧页面缩略图列表的鼠标抬起事件处理程序。当用户点击列表中的某个页面缩略图时，它会触发完整的页面切换逻辑：保存当前页 -> 清空画布 -> 加载新选择的页面 -> 更新页码显示。
│   |    └── BlackBoardRightSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e): 右侧页面缩略图列表的鼠标抬起事件处理程序，其功能与左侧列表的事件处理完全相同。
│   ├── MW_PPT.cs
│   |    ├── #region Win32 API Declarations
│   |    |    └── (各种 DllImport): 导入一系列底层的 Windows API 函数。这些函数是与外部应用程序（如PowerPoint）窗口进行交互的基础，用于查找窗口、获取窗口信息（标题、类名、位置）以及判断窗口状态（是否可见、最小化等）。
│   |    ├── #region PPT Application Variables
│   |    |    └── pptApplication, presentation, ... (static): 定义了一组静态变量，用于在代码中持有对 PowerPoint 应用程序本身、当前打开的演示文稿、幻灯片集合等核心COM对象的引用。
│   |    ├── #region PPT State Management
│   |    |    ├── wasFloatingBarFoldedWhenEnterSlideShow (bool): 记录进入PPT放映前，主工具栏是否处于折叠状态，以便在退出放映时能够恢复原状。
│   |    |    ├── isEnteredSlideShowEndEvent (bool): 一个防止重复执行的标志位。由于PPT的事件可能被多次触发，此标志确保“退出放映”的核心清理逻辑只执行一次。
│   |    |    ├── _longPressTimer (DispatcherTimer) & 相关变量: 实现“长按翻页”功能。当用户按住上一页/下一页按钮时，此计时器会启动，并以固定间隔连续发送翻页命令。
│   |    |    ├── _powerPointProcessMonitorTimer (DispatcherTimer): “PowerPoint增强功能”的一部分，一个守护定时器，定期检查后台的PowerPoint进程是否依然存活，如果意外关闭则尝试重新启动，以保证连接的稳定性。
│   |    |    ├── _lastPlaybackPage, _shouldNavigateToLastPage: 用于实现“记住上次播放位置”的功能。记录上次演示文稿关闭时的页码，并在下次打开时询问用户是否跳转。
│   |    |    └── _slideSwitchDebounceTimer (Timer): **关键的防抖机制**。PowerPoint在切换带有动画的页面时，可能会连续触发多次“下一页”事件。此计时器确保在短时间内只处理最后一次事件，防止墨迹保存和加载逻辑被错误地执行多次。
│   |    ├── #region PPT Managers
│   |    |    ├── _pptManager (PPTManager): **核心连接与控制管理器**。封装了所有与PPT/WPS应用程序的底层通信逻辑，如启动监控、检测连接、发送“上一页/下一页/开始放映/结束放映”等命令，并对外触发标准化事件。
│   |    |    ├── _multiPPTInkManager (MultiPPTInkManager): **核心墨迹管理器**。专门负责在PPT模式下的墨迹数据。它的“Multi”特性意味着能为每一个不同的演示文稿（根据文件名和哈希值区分）独立保存和加载全套的笔迹，实现了“一文稿一笔迹”的功能。
│   |    |    └── _pptUIManager (PPTUIManager): **核心UI管理器**。封装了所有与PPT模式相关的界面变化，如显示/隐藏PPT专用按钮、更新页码显示、调整浮动栏透明度和位置等，将UI逻辑与业务逻辑解耦。
│   |    ├── #region PPT Manager Initialization
│   |    |    ├── InitializePPTManagers(): 总初始化函数。创建上述三个核心管理器的实例，并把它们通过事件订阅（+=）关联起来，构建起整个PPT集成系统。
│   |    |    ├── Start/StopPPTMonitoring(): 启动和停止对PPT应用程序的监控。
│   |    |    └── DisposePPTManagers(): 在程序关闭时，负责彻底清理和释放所有PPT相关的资源和COM对象，防止内存泄漏。
│   |    ├── #region PowerPoint Application Management
│   |    |    ├── Start/StopPowerPointProcessMonitoring(): 启用或禁用“PowerPoint增强功能”的守护进程。
│   |    |    ├── CreatePowerPointApplication(): “增强功能”的核心。在后台创建一个私有、不可见的PowerPoint应用程序实例。这提供了一个极其稳定和可靠的连接，绕过了系统COM注册可能出现的不稳定问题。
│   |    |    ├── IsPowerPointApplicationValid(): 一个工具函数，通过尝试访问COM对象的某个属性来“Ping”一下，判断后台的PowerPoint实例是否仍然有效。
│   |    |    └── ClosePowerPointApplication(): 安全地关闭并释放由本程序创建的后台PowerPoint实例。
│   |    ├── #region New PPT Event Handlers (事件驱动核心)
│   |    |    ├── OnPPTConnectionChanged(bool isConnected): 当与PPT的连接状态发生变化时触发，主要调用 `_pptUIManager` 来更新UI（如PPT按钮的可用性）。
│   |    |    ├── OnPPTPresentationOpen(Presentation pres): 当用户打开一个新的PPT文件时触发。会为这个新文件初始化一套独立的墨迹存储，并执行一些辅助功能，如询问是否跳转到上次播放位置、检查并提示取消隐藏的幻灯片等。
│   |    |    ├── OnPPTPresentationClose(Presentation pres): 当PPT文件关闭时触发，负责将该文件对应的所有墨迹自动保存到本地。
│   |    |    ├── OnPPTSlideShowStateChanged(bool isInSlideShow): 当PPT进入或退出“编辑模式”和“放映模式”之间切换时触发，用于更新UI状态。
│   |    |    ├── OnPPTSlideShowBegin(SlideShowWindow wn): **进入放映模式的核心事件**。执行一系列复杂的UI切换（如自动折叠工具栏、调整透明度、显示翻页按钮）、加载第一页的墨迹，并准备好进行批注。
│   |    |    ├── OnPPTSlideShowNextSlide(SlideShowWindow wn): **翻页时的核心事件**。它不直接处理逻辑，而是调用 `HandleSlideSwitchWithDebounce` 方法，利用防抖机制来确保墨迹切换的准确性。
│   |    |    └── OnPPTSlideShowEnd(Presentation pres): **退出放映模式的核心事件**。保存最后一页的墨迹，然后执行一系列复杂的UI恢复操作（恢复工具栏折叠状态、恢复透明度、隐藏翻-页按钮、清空画布等）。
│   |    ├── #region Helper Methods
│   |    |    ├── HandlePresentationOpenNavigation(...) / ShowPreviousPageNotification(...): 实现打开PPT时询问是否跳转到上次播放页面的功能。
│   |    |    ├── CheckAndNotifyHiddenSlides(...) / CheckAndNotifyAutoPlaySettings(...): 两个用户体验优化功能。在打开PPT时自动检测文件中是否有隐藏的幻灯片或自动播放设置，并弹窗询问用户是否需要修正，避免了讲课时可能遇到的尴尬。
│   |    |    ├── LoadCurrentSlideInk(int slideIndex): 从 `_multiPPTInkManager` 中加载指定页码的墨迹并显示在画布上。
│   |    |    ├── HandleSlideSwitchWithDebounce(...) / SwitchSlideInk(int newSlideIndex): **翻页墨迹处理的核心逻辑**。`HandleSlideSwitchWithDebounce` 负责防抖，它会在用户翻页操作稳定后，调用 `SwitchSlideInk`。`SwitchSlideInk` 则负责：1. 保存当前页的墨迹；2. 清空画布；3. 加载新页面的墨迹。
│   |    |    └── GetFileHash(string filePath): 一个工具函数，用于为PPT文件路径生成一个简短的哈希值，确保为不同位置的同名文件创建不同的墨迹存储目录。
│   |    └── (UI事件处理)
│   |         ├── BtnCheckPPT_Click(...): “手动连接PPT”按钮的点击事件。
│   |         ├── ToggleSwitch..._Toggled(...): 设置界面中“启用PPT增强”、“支持WPS”等开关的事件处理。
│   |         ├── BtnPPTSlidesUp/Down_Click(...): “上一页/下一页”按钮的单击事件。核心逻辑是先保存当前页墨迹，然后命令 `_pptManager` 去执行翻页动作。
│   |         ├── PPTNavigationBtn_MouseDown/Leave/Up(...): 屏幕四角的上一页/下一页/目录按钮的完整鼠标事件（按下、离开、抬起），实现了点击时的视觉反馈效果和长按翻页的触发。
│   |         ├── BtnPPTSlideShow/End_Click(...): “开始放映/结束放映”按钮的点击事件，调用 `_pptManager` 执行相应命令。
│   |         └── GridPPTControlPrevious/Next_MouseDown/Leave/Up(...): 侧边栏的上一页/下一页按钮的鼠标事件，同样实现了视觉反馈和长按翻页功能。
│   ├── MW_Save&OpenStrokes.cs
│   |    ├── CanvasElementInfo (public class): 一个数据结构，用于序列化（保存到文件）画布上非笔迹元素（如图片）的关键信息，包括类型、源文件路径、位置、尺寸和拉伸方式。这是实现保存/加载图片等功能的基础。
│   |    ├── SymbolIconSaveStrokes_MouseUp(...): “保存”图标的点击事件处理程序，是用户发起保存操作的入口，它会调用核心的保存逻辑 `SaveInkCanvasStrokes`。
│   |    ├── SaveInkCanvasStrokes(bool newNotice, bool saveByUser): **核心保存分发函数**。它首先根据当前模式（桌面批注/白板）和触发方式（用户/自动）确定保存路径。然后，根据用户的设置 (`IsSaveFullPageStrokes`) 决定采用哪种保存策略：
│   |    |    └── 如果是“全页面保存”模式且检测到多页内容（在PPT或白板中），则调用 `SaveMultiPageStrokesAsZip` 进行多页打包保存。
│   |    |    └── 否则，采用传统方式，将当前页的笔迹保存为 `.icstk` 文件，并将图片等元素信息序列化为 `.elements.json` 文件。
│   |    ├── SaveMultiPageStrokesAsZip(List<StrokeCollection> allPageStrokes, ...): **多页打包保存功能**。它将所有页面的笔迹分别存为独立的 `.icstk` 文件，同时创建一个包含上下文信息（如总页数、当前模式、PPT文件名等）的 `metadata.txt` 文件，最后将所有这些文件压缩成一个 `.zip` 压缩包。
│   |    ├── SaveSinglePageStrokesAsImage(...): 当“全页面保存”设置启用但只有单页时，此函数会将当前画布内容渲染成一张PNG图片进行保存，同时也会保存一份原始的 `.icstk` 文件以保证数据可恢复。
│   |    ├── SavePageAsImage(StrokeCollection strokes, Stream outputStream): 一个辅助工具函数，用于将一个给定的笔迹集合渲染成PNG图片并写入到指定的输出流中。
│   |    ├── SymbolIconOpenStrokes_MouseUp(...): “打开”图标的点击事件处理程序。它会弹出文件选择对话框，让用户选择要加载的墨迹文件（`.icstk` 或 `.zip`），然后根据文件扩展名分发给相应的加载函数。
│   |    ├── OpenICCZipFile(string zipFilePath): **多页压缩包加载功能**。它解压 `.zip` 文件，读取 `metadata.txt` 以了解保存时的上下文。**关键特性**：它会进行**模式匹配检查**，只有当保存时的模式（如PPT模式）与当前应用的模式相同时，才会继续执行恢复操作，防止数据错乱。
│   |    ├── ReadMetadataFile(string metadataPath): 一个辅助函数，用于解析 `metadata.txt` 文件，将其中的键值对信息读取到一个字典中，供加载逻辑使用。
│   |    ├── RestorePPTStrokesFromZip(...): 当确认要恢复的是PPT墨迹时被调用。它会清空当前演示文稿的所有墨迹，然后从解压的文件中逐页加载笔迹数据，重建整个演示文稿的批注。
│   |    ├── RestoreWhiteboardStrokesFromZip(...): 当确认要恢复的是白板墨迹时被调用。它会重置整个白板的状态（总页数、当前页），然后从解压的文件中逐页加载数据，完全恢复多页白板的内容和结构。
│   |    └── OpenSingleStrokeFile(string filePath): **单页文件加载功能**。它会从 `.icstk` 文件中加载笔迹。同时，它会查找同名的 `.elements.json` 文件，如果存在，则反序列化其中的信息，并根据这些信息在画布上重新创建图片等元素，恢复其位置和大小。
│   ├── MW_Screenshot.cs
│   |    ├── SaveScreenShot(bool isHideNotification, string fileName = null): **核心截图保存函数**。当程序内部需要自动截图时（例如在PPT翻页时），会调用此方法。它根据用户设置决定是将截图保存在按日期分类的文件夹中还是默认文件夹中，然后调用底层的截图逻辑，并根据设置决定是否在截图后自动保存当前笔迹。
│   |    ├── SaveScreenShotToDesktop(): 一个专门的函数，用于响应用户的“截图到桌面”快捷键或按钮操作。它直接将截图以PNG格式保存到用户的桌面。
│   |    ├── CaptureAndSaveScreenshot(string savePath, bool isHideNotification): **底层的截图与保存实现**。此函数封装了实际的屏幕捕捉技术：
│   |    |    └── 1. 使用 `SystemInformation.VirtualScreen` 获取包含所有显示器的完整虚拟屏幕的尺寸。
│   |    |    └── 2. 创建一个与虚拟屏幕同样大小的位图（Bitmap）对象。
│   |    |    └── 3. 使用 `Graphics.CopyFromScreen` 方法将整个屏幕的内容复制到这个位图对象中。
│   |    |    └── 4. 最后，将这个位图对象以PNG格式保存到指定的路径，并根据参数决定是否显示一个“保存成功”的通知。
│   |    ├── GetDateFolderPath(string fileName): 一个辅助函数，根据用户“截图按日期文件夹保存”的设置，构建出完整的、包含日期子目录的截图文件保存路径。
│   |    └── GetDefaultFolderPath(): 另一个辅助函数，用于构建默认的截图文件保存路径，即所有截图都保存在同一个文件夹下（例如 `.../Screenshots/2023-10-27_14-30-05.png`）。
│   ├── MW_SelectionGestures.cs
│   |    ├── #region Floating Control (选中笔迹后的浮动工具栏)
│   |    |    ├── Border_MouseDown(object sender, ...): 一个通用的鼠标按下事件，用于记录当前哪个按钮被按下，以确保在 `MouseUp` 事件中只响应正确的按钮。
│   |    |    ├── BorderStrokeSelectionClone_MouseUp(...): “克隆”按钮的点击事件，复制当前选中的所有笔迹，并在原位置附近创建一份副本。
│   |    |    ├── BorderStrokeSelectionCloneToNewBoard_MouseUp(...): “克隆到新页面”按钮的点击事件，将选中的笔迹复制到一个新的白板页面。
│   |    |    ├── BorderStrokeSelectionDelete_MouseUp(...): “删除”按钮的点击事件，功能等同于清空选中的笔迹。
│   |    |    ├── GridPenWidthDecrease/Increase_MouseUp(...): “减小/增大笔迹宽度”按钮的点击事件，调用 `ChangeStrokeThickness` 来调整选中笔迹的粗细。
│   |    |    ├── ChangeStrokeThickness(double multipler): 核心的笔迹粗细调整逻辑。它遍历所有选中的笔迹，将其 `DrawingAttributes` 的宽度和高度乘以一个系数，并向时间机器提交更改历史以支持撤销。
│   |    |    ├── GridPenWidthRestore_MouseUp(...): “恢复默认宽度”按钮的点击事件，将选中笔迹的粗细恢复为当前画笔的默认设置。
│   |    |    ├── ImageFlipHorizontal/Vertical_MouseUp(...): “水平/垂直翻转”按钮的点击事件。计算选中区域的中心点，然后创建一个围绕该中心点进行缩放（值为-1）的变换矩阵，并将其应用于所有选中的笔迹。
│   |    |    └── ImageRotate45/90_MouseUp(...): “旋转45/90度”按钮的点击事件。与翻转类似，它创建一个围绕选中区域中心点进行旋转的变换矩阵，并应用于所有选中的笔迹。
│   |    ├── (核心选择、拖动与手势处理)
│   |    |    ├── isStrokeDragging (bool), strokeDragStartPoint (Point): 用于鼠标拖动状态管理的核心变量。
│   |    |    ├── GridInkCanvasSelectionCover_MouseDown/MouseMove/MouseUp(...): **鼠标拖动逻辑**。`GridInkCanvasSelectionCover` 是一个覆盖在选中笔迹上的透明层。这组事件实现了经典的拖动操作：`MouseDown` 启动拖动状态，`MouseMove` 计算位移并实时应用平移变换，`MouseUp` 结束拖动。
│   |    |    ├── GridInkCanvasSelectionCover_ManipulationDelta(...): **多点触控手势核心**。此事件能同时处理平移、缩放和旋转。它从事件参数中获取这三种变换量，构建一个复合变换矩阵，并将其一次性应用于所有选中笔迹，实现了流畅的“捏合缩放”、“双指旋转”等高级手势。
│   |    |    ├── GridInkCanvasSelectionCover_Touch...(...): 原始触摸事件处理，主要用于精确地处理单指拖动，并管理多点触控的设备ID，以区分单指、双指等不同手势。
│   |    |    ├── BtnSelect_Click / LassoSelect_Click(...): “套索选择”工具按钮的点击事件，负责将 `InkCanvas` 的编辑模式切换到 `Select`，让用户可以开始选择笔迹。
│   |    |    ├── inkCanvas_SelectionChanged(...): **至关重要的事件**。当画布上的选择内容发生变化时触发。它的核心职责是：
│   |    |    |    └── 1. 判断当前选中的是笔迹还是图片元素。
│   |    |    |    └── 2. 根据判断结果，显示正确的操作UI（如笔迹操作工具栏或图片操作工具栏），并隐藏不相关的UI。
│   |    |    |    └── 3. 如果没有任何东西被选中，则隐藏所有选择相关的UI。
│   |    |    └── updateBorderStrokeSelectionControlLocation(): 一个辅助UI函数，用于计算并更新“选中笔迹浮动工具栏”的位置，确保它总是出现在选中区域的下方（或上方，如果下方空间不足）。
│   |    └── #region Selection Display and Resize Handles (选择框与缩放手柄)
│   |         ├── isResizing (bool), currentResizeHandle (string), ...: 用于管理通过缩放手柄调整大小的状态变量。
│   |         ├── UpdateSelectionDisplay() / HideSelectionDisplay(): 控制选择框（一个虚线矩形）和八个缩放手柄的显示、隐藏和位置更新。
│   |         ├── SelectionHandle_MouseDown/MouseMove/MouseUp(...): **拖拽缩放逻辑**。这是一组附加在八个缩放手柄上的事件。当用户拖动一个手柄时，它们会启动缩放状态，实时计算新的边界矩形，并调用 `ApplyBoundsToStrokes` 将选中的笔迹进行缩放和平移以匹配新的边界。
│   |         ├── CalculateNewBounds(...): 缩放逻辑的“大脑”。根据用户拖动的具体是哪个手柄（如左上角、右边中点等），精确地计算出选择框新的位置和尺寸。
│   |         └── ApplyBoundsToStrokes(Rect newBounds): 将 `CalculateNewBounds` 计算出的新边界应用到实际笔迹上的执行函数。它通过比较新旧边界，计算出所需的缩放比例和平移量，并构建一个变换矩阵来一次性更新所有选中笔迹的形状和位置。
│   ├── MW_Settings.cs
│   |    ├── #region Behavior
│   |    |    ├── ToggleSwitchIsAutoUpdate_Toggled(...): 响应用于控制“自动检查更新”的开关。当用户切换时，它会更新 `Settings.Startup.IsAutoUpdate` 的值，保存设置，并根据此开关的状态决定是否显示“静默更新”的子选项。
│   |    |    ├── ToggleSwitchIsAutoUpdateWithSilence_Toggled(...): 响应用于控制“静默更新”的开关。它更新 `Settings.Startup.IsAutoUpdateWithSilence` 的值，保存设置，并控制“静默更新时间段”设置项的显示与隐藏。
│   |    |    ├── AutoUpdateWithSilenceStartTimeComboBox_SelectionChanged(...): 响应“静默更新开始时间”下拉框的选择变化。它将用户选择的时间字符串保存到 `Settings.Startup.AutoUpdateWithSilenceStartTime` 并保存设置。
│   |    |    ├── AutoUpdateWithSilenceEndTimeComboBox_SelectionChanged(...): 响应“静默更新结束时间”下拉框的选择变化。它将用户选择的时间字符串保存到 `Settings.Startup.AutoUpdateWithSilenceEndTime` 并保存设置。
│   |    |    ├── ToggleSwitchRunAtStartup_Toggled(...): 响应“开机自启”的开关。当启用时，它调用 `StartAutomaticallyCreate` 在系统启动文件夹中创建快捷方式；禁用时，调用 `StartAutomaticallyDel` 删除快捷方式。
│   |    |    ├── ToggleSwitchFoldAtStartup_Toggled(...): 响应“启动时自动折叠”的开关。它更新 `Settings.Startup.IsFoldAtStartup` 的值并保存设置。
│   |    |    ├── ToggleSwitchSupportPowerPoint_Toggled(...): 响应“启用PPT支持”的主开关。启用时，会初始化并启动PPT监控服务 (`StartPPTMonitoring`)；禁用时，则停止该服务 (`StopPPTMonitoring`)。
│   |    |    └── ToggleSwitchShowCanvasAtNewSlideShow_Toggled(...): 响应“进入PPT放映时自动显示画板”的开关。它更新 `Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow` 的值并保存设置。
│   |    ├── #region Startup
│   |    |    └── ToggleSwitchEnableNibMode_Toggled(...): 响应“启用细笔尖/手指模式切换”的开关。它会同步两个不同位置（主工具栏和白板工具栏）的相同开关状态，更新 `Settings.Startup.IsEnableNibMode` 的值，并根据当前模式设置笔迹的物理宽度阈值 (`BoundsWidth`)。
│   |    ├── #region Appearance
│   |    |    ├── ToggleSwitchEnableDisPlayNibModeToggle_Toggled(...): 响应“在主界面显示细笔尖/手指切换按钮”的开关。它更新 `Settings.Appearance.IsEnableDisPlayNibModeToggler` 的值，并立即显示或隐藏主界面上的相应按钮。
│   |    |    ├── ToggleSwitchEnableQuickPanel_Toggled(...): 响应“启用屏幕边缘快速面板”的开关。它更新 `Settings.Appearance.IsShowQuickPanel` 的值并保存设置。
│   |    |    ├── ToggleSwitchEnableSplashScreen_Toggled(...): 响应“启用启动动画”的开关。它更新 `Settings.Appearance.EnableSplashScreen` 的值并保存设置。
│   |    |    ├── ComboBoxSplashScreenStyle_SelectionChanged(...): 响应“启动动画样式”下拉框的选择。它将选择的索引保存到 `Settings.Appearance.SplashScreenStyle` 中。
│   |    |    ├── ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(...): 响应“主工具栏缩放”滑块的变化。它更新 `Settings.Appearance.ViewboxFloatingBarScaleTransformValue` 的值，实时应用缩放变换到主工具栏上，并通过 `Dispatcher` 延迟调用 `ViewboxFloatingBarMarginAnimation` 来重新计算并居中工具栏。
│   |    |    ├── ViewboxFloatingBarOpacityValueSlider_ValueChanged(...): 响应“主工具栏不透明度”滑块的变化。它更新 `Settings.Appearance.ViewboxFloatingBarOpacityValue` 的值，并实时应用到主工具栏的 `Opacity` 属性上。
│   |    |    ├── ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(...): 响应“PPT模式下工具栏不透明度”滑块的变化。它只更新 `Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue` 的值，该值将在进入PPT模式时被应用。
│   |    |    ├── ToggleSwitchEnableTrayIcon_Toggled(...): 响应“启用系统托盘图标”的开关。它更新 `Settings.Appearance.EnableTrayIcon` 的值，并立即显示或隐藏系统托盘区的图标。
│   |    |    ├── ComboBoxUnFoldBtnImg_SelectionChanged(...): 响应“侧边展开按钮图标样式”下拉框的选择。它更新 `Settings.Appearance.UnFoldButtonImageType` 的值，并立即更改侧边展开按钮上显示的图片资源。
│   |    |    ├── ComboBoxChickenSoupSource_SelectionChanged(...): 响应白板模式下“心灵鸡汤水印”内容来源的下拉框选择。它更新 `Settings.Appearance.ChickenSoupSource` 的值，并立即从指定的文本数组中随机选择一条显示。
│   |    |    ├── ToggleSwitchEnableViewboxBlackBoardScaleTransform_Toggled(...): 响应“启用白板模式工具栏缩放”的开关。它更新 `Settings.Appearance.EnableViewboxBlackBoardScaleTransform` 的值并重新加载所有设置以应用。
│   |    |    ├── ComboBoxFloatingBarImg_SelectionChanged(...): 响应主工具栏“拖动区图标样式”下拉框的选择。它更新 `Settings.Appearance.FloatingBarImg` 的值，并调用 `UpdateFloatingBarIcon` 来切换显示的图片。
│   |    |    ├── UpdateFloatingBarIcon(): 根据当前设置 (`Settings.Appearance.FloatingBarImg`) 的值，从内置资源或用户自定义列表中加载并设置主工具栏拖动区的图标。
│   |    |    ├── UpdateCustomIconsInComboBox(): 动态更新“拖动区图标样式”下拉框。它会清空并重新填充列表，将所有用户自定义的图标作为新选项添加进去。
│   |    |    ├── ButtonAddCustomIcon_Click(...): “添加自定义图标”按钮的点击事件。它会弹出一个新的对话框 (`AddCustomIconWindow`) 来让用户添加新的图标。
│   |    |    ├── ButtonManageCustomIcons_Click(...): “管理自定义图标”按钮的点击事件。它会弹出一个新的对话框 (`CustomIconWindow`) 来让用户编辑或删除已添加的图标。
│   |    |    ├── ToggleSwitchEnableTimeDisplayInWhiteboardMode_Toggled(...): 响应“在白板模式下显示时间”的开关。它更新设置值，并根据当前是否在白板模式中，立即显示或隐藏时间水印。
│   |    |    ├── ToggleSwitchEnableChickenSoupInWhiteboardMode_Toggled(...): 响应“在白板模式下显示水印”的开关。它更新设置值，并根据当前是否在白板模式中，立即显示或隐藏“心灵鸡汤”水印。
│   |    |    ├── (PPT按钮显示与样式设置): 这一大组 `_OnToggled`, `_IsCheckChanged`, `_ValueChanged`, `_Clicked` 事件处理程序，共同构成了对PPT模式下屏幕四角翻页按钮的精细化控制。它们分别负责：
│   |    |    |    ├── `ToggleSwitchShowPPTButton_OnToggled`: 总开关，控制所有PPT翻页按钮的显示与隐藏。
│   |    |    |    ├── `CheckboxEnable...PPTButton_IsCheckChanged`: 分别控制左下、右下、左侧、右侧四个位置按钮的独立显示与隐藏。
│   |    |    |    ├── `Checkbox...DisplayPage/HalfOpacity/BlackBackground_IsCheckChange`: 控制侧边栏按钮和底部按钮的样式，如是否显示页码、是否半透明、是否使用深色背景。
│   |    |    |    ├── `PPTButton...PositionValueSlider_ValueChanged`: 控制四个位置按钮的精确位置偏移（上下或左右）。
│   |    |    |    ├── `PPTBtn...Plus/Minus/Sync/ResetBtn_Clicked`: 为位置滑块提供“+1/-1/同步/重置”的便捷操作按钮。
│   |    |    |    ├── `UpdatePPTUIManagerSettings()`: 一个核心辅助函数，当任何PPT按钮相关设置改变时，它会将最新的设置值批量传递给 `_pptUIManager`，由其在PPT模式下统一应用这些UI变化。
│   |    |    |    └── `UpdatePPTBtnPreview()`: **设置界面的实时预览功能**。此函数读取所有PPT按钮相关的设置，并将其动态地应用到设置面板中的一个微缩屏幕预览图上，让用户可以直观地看到调整效果。
│   |    |    ├── ToggleSwitchShowCursor_Toggled(...): 响应“显示自定义光标”的开关。它更新 `Settings.Canvas.IsShowCursor` 的值，并立即应用或移除自定义光标。
│   |    |    ├── ToggleSwitchEnablePressureTouchMode_Toggled(...): 响应“启用压感触屏模式”的开关。它更新设置值，并有一个**联动逻辑**：启用此项会自动禁用“屏蔽压感”。
│   |    |    ├── ToggleSwitchDisablePressure_Toggled(...): 响应“屏蔽压感”的开关。它更新设置值，并有一个**联动逻辑**：启用此项会自动禁用“压感触屏模式”。
│   |    |    ├── ToggleSwitchAutoStraightenLine_Toggled(...): 响应“笔迹自动修正为直线”的开关。它更新 `Settings.Canvas.AutoStraightenLine` 的值并保存。
│   |    |    ├── AutoStraightenLineThresholdSlider_ValueChanged(...): 响应“直线修正灵敏度”滑块。它更新 `Settings.Canvas.AutoStraightenLineThreshold` 的值并保存。
│   |    |    ├── ToggleSwitchLineEndpointSnapping_Toggled(...): 响应“直线端点吸附”的开关。它更新 `Settings.Canvas.LineEndpointSnapping` 的值并保存。
│   |    |    ├── LineEndpointSnappingThresholdSlider_ValueChanged(...): 响应“端点吸附范围”滑块。它更新 `Settings.Canvas.LineEndpointSnappingThreshold` 的值并保存。
│   |    |    ├── LineStraightenSensitivitySlider_ValueChanged(...): 响应高级“直线修正灵敏度”滑块。它更新 `Settings.InkToShape.LineStraightenSensitivity` 的值并保存。
│   |    |    └── ToggleSwitchHighPrecisionLineStraighten_Toggled(...): 响应“启用高精度直线修正”的开关。它更新 `Settings.Canvas.HighPrecisionLineStraighten` 的值并保存。
│   |    ├── #region Canvas
│   |    |    ├── ComboBoxPenStyle_SelectionChanged(...): 响应“笔尖样式”下拉框的选择。它同步两个位置的相同控件，更新 `Settings.Canvas.InkStyle` 的值并保存。
│   |    |    ├── ComboBoxEraserSize_SelectionChanged(...): 响应“橡皮擦尺寸”下拉框（设置页内）的选择。它更新 `Settings.Canvas.EraserSize` 的值，并调用 `ApplyAdvancedEraserShape` 来应用新的橡皮擦尺寸。
│   |    |    ├── ComboBoxEraserSizeFloatingBar_SelectionChanged(...): 响应“橡皮擦尺寸”下拉框（工具栏上）的选择。它会同步所有橡皮擦尺寸下拉框的值，更新设置，并应用新的橡皮擦尺寸。
│   |    |    ├── SwitchToCircleEraser(...): 响应“圆形橡皮擦”选项卡的点击。它将 `Settings.Canvas.EraserShapeType` 设置为0（圆形），更新UI选项卡，并应用形状。
│   |    |    ├── SwitchToRectangleEraser(...): 响应“矩形橡皮擦”选项卡的点击。它将 `Settings.Canvas.EraserShapeType` 设置为1（矩形），更新UI选项卡，并应用形状。
│   |    |    ├── InkWidthSlider_ValueChanged(...): 响应“画笔粗细”滑块的变化。它同步两个位置的滑块，更新 `drawingAttributes` 使其立即生效，并将值保存到 `Settings.Canvas.InkWidth`。
│   |    |    ├── HighlighterWidthSlider_ValueChanged(...): 响应“荧光笔粗细”滑块的变化。它更新 `drawingAttributes`，并将值保存到 `Settings.Canvas.HighlighterWidth`。
│   |    |    ├── InkAlphaSlider_ValueChanged(...): 响应“画笔不透明度”滑块的变化。它只改变当前 `drawingAttributes` 的Alpha通道值，实现实时预览，但似乎没有持久化保存该设置。
│   |    |    └── ComboBoxHyperbolaAsymptoteOption_SelectionChanged(...): 响应绘制双曲线时“渐近线”的选项。它更新 `Settings.Canvas.HyperbolaAsymptoteOption` 的值并保存。
│   |    ├── #region Automation
│   |    |    ├── StartOrStoptimerCheckAutoFold(): 一个辅助函数，根据 `Settings.Automation.IsEnableAutoFold` 的总开关状态，来启动或停止用于检测特定软件窗口的定时器。
│   |    |    ├── (一系列 ToggleSwitchAutoFoldIn..._Toggled): 这一组十多个函数，每一个都对应一个特定软件（如希沃白板、WPS等）的“自动折叠”开关。它们都执行相同的逻辑：更新 `Settings.Automation` 中对应的布尔值，保存设置，并调用 `StartOrStoptimerCheckAutoFold` 确保监控定时器在需要时运行。
│   |    |    ├── (一系列 ToggleSwitchAutoKill..._Toggled): 这一组函数，每一个都对应一个特定进程（如PPT服务、希沃授课助手等）的“自动结束”开关。它们都执行相同的逻辑：更新 `Settings.Automation` 中对应的布尔值，保存设置，并根据是否有任何“自动结束”选项被启用，来启动或停止用于查杀进程的 `timerKillProcess` 定时器。
│   |    |    ├── ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode_Toggled(...): 响应“退出折叠模式时自动进入批注模式”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchAutoFoldWhenExitWhiteboard_Toggled(...): 响应“退出白板时自动折叠”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchSaveScreenshotsInDateFolders_Toggled(...): 响应“截图按日期文件夹保存”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(...): 响应“截图时自动保存墨迹”的开关。更新设置并保存，同时还会动态修改另一个设置项的显示文本。
│   |    |    ├── ToggleSwitchAutoSaveStrokesAtClear_Toggled(...): 响应“清屏时自动截图”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchHideStrokeWhenSelecting_Toggled(...): 响应“鼠标穿透时隐藏笔迹”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchClearCanvasAndClearTimeMachine_Toggled(...): 响应“清空画布时同时清空历史记录”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchFitToCurve_Toggled(...): 响应“启用笔迹平滑(FitToCurve)”的开关。它更新 `drawingAttributes` 和设置值，并与“高级贝塞尔平滑”互斥（启用此项会禁用另一项）。
│   |    |    ├── ToggleSwitchAdvancedBezierSmoothing_Toggled(...): 响应“启用高级贝塞尔平滑”的开关。它更新设置值，并与`FitToCurve`互斥。
│   |    |    ├── ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(...): 响应“在PPT中自动保存每页墨迹”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchNotify...Page_Toggled(...): 响应几个PPT辅助提示功能的开关（如提示上次播放位置、提示有隐藏页面等）。
│   |    |    ├── SideControlMinimumAutomationSlider_ValueChanged(...): 响应“自动化操作的最小笔画数”滑块。更新设置并保存。
│   |    |    ├── AutoSavedStrokesLocationTextBox_TextChanged(...): 响应“自动保存路径”文本框的输入。实时更新设置并保存。
│   |    |    ├── AutoSavedStrokesLocationButton_Click(...): “浏览”按钮的点击事件，弹出一个文件夹选择对话框来设置保存路径。
│   |    |    ├── SetAutoSavedStrokesLocationTo...Button_Click(...): 两个快捷按钮，用于一键将保存路径设置为D盘或“我的文档”。
│   |    |    ├── ToggleSwitchAutoDelSavedFiles_Toggled(...): 响应“自动删除旧的存档文件”的开关。更新设置并保存。
│   |    |    ├── ComboBoxAutoDelSavedFilesDaysThreshold_SelectionChanged(...): 响应“旧文件天数阈值”下拉框。更新设置并保存。
│   |    |    ├── ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(...): 响应“在PPT翻页时自动截图”的开关。更新设置并保存。
│   |    |    └── ToggleSwitchSaveFullPageStrokes_Toggled(...): 响应“启用全页面保存模式”的开关（即将笔迹保存为图片或压缩包）。更新设置并保存。
│   |    ├── #region Gesture
│   |    |    ├── ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(...): 响应“启用手指在屏幕边缘滑动翻页”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchAutoSwitchTwoFingerGesture_Toggled(...): 响应“进入/退出白板时自动切换手势模式”的开关。更新设置并保存。
│   |    |    ├── ToggleSwitchEnableTwoFingerZoom_Toggled(...): 响应“启用双指缩放”的开关。与“多指同书”模式互斥。
│   |    |    ├── ToggleSwitchEnableMultiTouchMode_Toggled(...): 响应“启用多指同书”的开关。这是个**核心功能切换**，它会动态地添加或移除用于多点触控的底层事件处理器，并强制禁用所有其他双指手势。
│   |    |    ├── ToggleSwitchEnableTwoFingerTranslate_Toggled(...): 响应“启用双指平移”的开关。与“多指同书”模式互斥。
│   |    |    ├── ToggleSwitchEnableTwoFingerRotation_Toggled(...): 响应“启用双指旋转”的开关。与“多指同书”模式互斥。
│   |    |    └── ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(...): 响应“在PPT模式下也启用双指手势”的开关。更新设置并保存。
│   |    ├── #region Reset
│   |    |    ├── SetSettingsToRecommendation(): **核心重置逻辑**。它会创建一个全新的、包含所有预设推荐值的`Settings`对象，并用它覆盖当前的设置。在覆盖前，它会巧妙地保存并恢复几个用户特定的设置（如自动删除天数），以提供更好的体验。
│   |    |    ├── BtnResetToSuggestion_Click(...): “重置为推荐设置”按钮的点击事件。它调用 `SetSettingsToRecommendation`，然后保存并重新加载所有设置到UI，最后弹出一个通知。
│   |    |    └── SpecialVersionResetToSuggestion_Click(): 一个用于特殊版本的、稍微不同的重置逻辑，可能会在特定条件下被调用。
│   |    ├── #region Ink To Shape
│   |    |    ├── ToggleSwitchEnableInkToShape_Toggled(...): “启用笔迹变图形”的总开关。
│   |    |    ├── ToggleSwitchEnableInkToShapeNoFakePressure..._Toggled(...): 控制在变换出的三角形或矩形上是否模拟压感效果的开关。
│   |    |    └── ToggleCheckboxEnableInkToShape..._CheckedChanged(...): 分别控制是否启用对三角形、矩形、圆形的识别。
│   |    ├── #region Advanced
│   |    |    ├── ToggleSwitchIsSpecialScreen_OnToggled(...): 响应“为特殊触摸屏优化”的开关，主要控制“触摸点缩放系数”滑块的可见性。
│   |    |    ├── TouchMultiplierSlider_ValueChanged(...): 响应“触摸点缩放系数”滑块，用于调整触摸点大小的识别。
│   |    |    ├── BorderCalculateMultiplier_TouchDown(...): 一个**交互式辅助功能**。用户触摸此区域，程序会测量触摸点的物理大小，并计算出一个推荐的“缩放系数”显示给用户参考。
│   |    |    ├── ToggleSwitchIsEnableFullScreenHelper_Toggled(...): 响应“启用全屏程序辅助”的开关。
│   |    |    ├── ToggleSwitchIsEnableAvoidFullScreenHelper_OnToggled(...): 响应“启用防全屏覆盖辅助”的开关，启用后会调用 `AvoidFullScreenHelper` 来防止窗口被其他全屏程序覆盖。
│   |    |    ├── ToggleSwitchIsEnableEdgeGestureUtil_Toggled(...): 响应“禁用系统屏幕边缘手势”的开关，用于防止Win10/11的侧边栏手势与程序操作冲突。
│   |    |    ├── ToggleSwitchIsEnableForceFullScreen_Toggled(...): 响应“强制全屏”的开关。
│   |    |    ├── ToggleSwitchIsEnableDPIChangeDetection_Toggled(...): 响应“启用DPI变化检测”的开关。
│   |    |    ├── ToggleSwitchIsEnableResolutionChangeDetection_Toggled(...): 响应“启用分辨率变化检测”的开关。
│   |    |    ├── ToggleSwitchEraserBindTouchMultiplier_Toggled(...): 响应“橡皮擦大小绑定触摸点缩放系数”的开关。
│   |    |    ├── NibModeBoundsWidthSlider_ValueChanged(...): 响应“细笔尖模式宽度阈值”的滑块。
│   |    |    ├── FingerModeBoundsWidthSlider_ValueChanged(...): 响应“手指模式宽度阈值”的滑块。
│   |    |    ├── ToggleSwitchIsQuadIR_Toggled(...): 响应“是否为四边红外屏”的开关，这会影响触摸点大小的计算方式。
│   |    |    ├── ToggleSwitchIsLogEnabled_Toggled(...): 响应“启用日志记录”的开关。
│   |    |    ├── ToggleSwitchIsSaveLogByDate_Toggled(...): 响应“日志按日期保存”的开关。
│   |    |    ├── ToggleSwitchIsSecondConfimeWhenShutdownApp_Toggled(...): 响应“关闭程序时二次确认”的开关。
│   |    |    ├── ToggleSwitchIsAutoBackupBeforeUpdate_Toggled(...): 响应“更新前自动备份设置”的开关。
│   |    |    ├── ToggleSwitchIsAutoBackupEnabled_Toggled(...): 响应“启用自动备份”的开关。
│   |    |    ├── ComboBoxAutoBackupInterval_SelectionChanged(...): 响应“自动备份间隔”下拉框的选择。
│   |    |    ├── BtnManualBackup_Click(...): “手动备份”按钮的点击事件。它会将当前的 `Settings` 对象序列化为JSON，并以带时间戳的文件名保存到 `Backups` 目录。
│   |    |    └── BtnRestoreBackup_Click(...): “还原备份”按钮的点击事件。它会弹出文件选择对话框，让用户选择一个备份文件，然后反序列化JSON，在用户确认后覆盖当前设置，并重新加载UI。
│   |    ├── #region RandSettings
│   |    |    ├── ToggleSwitchDisplayRandWindowNamesInputBtn_OnToggled(...): 响应“在点名窗口显示名单导入按钮”的开关。
│   |    |    ├── RandWindowOnceCloseLatencySlider_ValueChanged(...): 响应“单人点名窗口自动关闭延迟”的滑块。
│   |    |    ├── RandWindowOnceMaxStudentsSlider_ValueChanged(...): 响应“多人点名时一次最多抽取人数”的滑块。
│   |    |    ├── ToggleSwitchUseLegacyTimerUI_Toggled(...): 响应“使用旧版计时器界面”的开关。
│   |    |    ├── TimerVolumeSlider_ValueChanged(...): 响应“计时器铃声音量”的滑块。
│   |    |    ├── ButtonSelectCustomTimerSound_Click(...): “选择自定义铃声”按钮，弹出一个文件选择对话框让用户选择WAV音频文件。
│   |    |    ├── ButtonResetTimerSound_Click(...): “重置为默认铃声”按钮，将自定义铃声路径清空。
│   |    |    ├── ToggleSwitchShowRandomAndSingleDraw_Toggled(...): 响应“在工具栏显示多人/单人点名按钮”的开关。
│   |    |    ├── ToggleSwitchExternalCaller_Toggled(...): 响应“启用外部点名器”的开关。
│   |    |    ├── ComboBoxExternalCallerType_SelectionChanged(...): 响应“外部点名器类型”的下拉框选择。
│   |    |    ├── UpdateFloatingBarIcons(): 根据 `Settings.Appearance.UseLegacyFloatingBarUI` 的设置，更新主工具栏上所有核心工具（画笔、橡皮等）的图标为新版或旧版样式。
│   |    |    └── GetCorrectIcon(...): 一个辅助函数，根据图标类型（如"pen"）和是否需要实心填充，并结合新/旧版UI的设置，返回正确的图标几何路径字符串。
│   |    ├── #region 浮动栏按钮显示控制 (Floating Bar Button Visibility Control)
│   |    |    ├── (一系列 CheckBox..._Checked / _Unchecked): 这一组函数对应设置面板中用于控制主工具栏上各个按钮（如形状、撤销、重做、清空等）是否显示的复选框。每个函数都会更新 `Settings.Appearance` 中对应的布尔值，然后调用 `UpdateFloatingBarButtonsVisibility` 来应用更改。
│   |    |    ├── ComboBoxQuickColorPaletteDisplayMode_SelectionChanged(...): 响应“快捷调色盘显示模式”（单行/双行）的下拉框。
│   |    |    ├── ComboBoxEraserDisplayOption_SelectionChanged(...): 响应“橡皮擦按钮显示模式”（都显示/只显示面积擦/只显示线擦/都不显示）的下拉框。
│   |    |    └── UpdateFloatingBarButtonsVisibility(): **核心UI更新函数**。它读取 `Settings.Appearance` 中所有与按钮可见性相关的设置，然后逐一设置主工具栏上对应按钮的 `Visibility` 属性。在更新后，它会**非常关键地**重新计算主工具栏的位置和高亮指示器的位置，因为按钮的增减会改变工具栏的总宽度。
│   |    ├── (杂项函数)
│   |    |    ├── SaveSettingsToFile(): **至关重要的核心函数**。几乎被此文件中所有的事件处理器调用。它负责将内存中的 `Settings` 对象序列化为格式化的JSON字符串，并将其写入到配置文件（`settings.json`）中，实现设置的持久化存储。
│   |    |    ├── SCManipulationBoundaryFeedback(...): 阻止设置面板滚动到边界时的“橡皮筋”回弹效果，提升体验。
│   |    |    ├── HyperlinkSourceTo...Repository_Click(...): “关于”页面中的三个超链接点击事件，分别用于在浏览器中打开此项目的不同代码仓库地址。
│   |    |    ├── UpdateChannelSelector_Checked(...): 响应“更新通道”（正式版/测试版）的单选按钮。切换后，如果自动更新是开启的，它会立即触发一次新的更新检查。
│   |    |    ├── FixVersionButton_Click(...): “版本修复”按钮的点击事件。它会强制下载并安装当前选择通道的最新版本，用于解决可能出现的更新问题。
│   |    |    ├── UpdatePickNameBackgroundsInComboBox() / UpdatePickNameBackgroundDisplay(): 用于动态更新“点名背景”下拉框的选项，将用户自定义的背景添加进去。
│   |    |    ├── ComboBoxPickNameBackground_SelectionChanged(...): 响应“点名背景”下拉框的选择。
│   |    |    ├── ButtonAddCustomBackground_Click(...) / ButtonManageBackgrounds_Click(...): “添加/管理自定义背景”按钮的点击事件，用于弹出相应的管理窗口。
│   |    |    ├── (剩余的 ToggleSwitch...): 处理一些其他独立的开关设置，如“退出WPS时结束wpp进程”、“清屏时也清除图片”、“压缩上传的图片”、“退出PPT后自动折叠”等。
│   |    |    └── #region 文件关联管理 (File Association Management)
│   |    |         ├── BtnUnregisterFileAssociation_Click(...): “取消文件关联”按钮的点击事件。调用 `FileAssociationManager` 来移除 `.icstk` 文件与本程序的关联。
│   |    |         ├── BtnCheckFileAssociation_Click(...): “检查关联状态”按钮的点击事件。调用 `FileAssociationManager` 来检查当前系统中的文件关联状态并更新UI文本。
│   |    |         └── BtnRegisterFileAssociation_Click(...): “注册文件关联”按钮的点击事件。调用 `FileAssociationManager` 来在系统中创建 `.icstk` 文件与本程序的关联。
│   ├── MW_SettingsToLoad.cs
│   |    ├── LoadSettings(bool isStartup = false): **这是整个文件的核心入口函数**。它的职责是：
│   |    |    ├── 1. **健壮地加载配置文件**：
│   |    |    |    ├── 尝试读取并反序列化 `settings.json` 文件。
│   |    |    |    ├── **容错处理**：如果文件不存在、内容损坏或解析失败，它会触发 `AutoBackupManager.TryRestoreFromBackup()` 尝试从最新的自动备份中恢复。
│   |    |    |    └── **最终保障**：如果连备份恢复都失败了，它会调用 `BtnResetToSuggestion_Click`，强制使用一套预设的推荐设置来初始化，确保程序总能以一个可用状态启动。
│   |    |    ├── 2. **应用配置到UI**：
│   |    |    |    ├── 它会按设置类别（如 `Startup`, `Appearance`, `Canvas` 等）逐一检查 `Settings` 对象中对应的部分是否存在。
│   |    |    |    ├── 如果存在，则将该类别下的每一项设置值，赋给设置面板中对应的UI控件（如 `ToggleSwitch.IsOn`, `Slider.Value`, `ComboBox.SelectedIndex` 等）。
│   |    |    |    └── 如果某个设置类别（如 `Settings.Startup`）不存在（可能是旧版配置文件），它会创建一个新的默认实例，避免程序崩溃。
│   |    |    └── 3. **执行启动时操作**：
│   |    |         └── 如果 `isStartup` 参数为 `true`，它会执行一些只有在程序刚启动时才需要做的操作，例如：
│   |    |              ├── 检查并删除过期的自动存档文件。
│   |    |              ├── 根据设置决定启动时是否自动折叠工具栏。
│   |    |              ├── 触发一次自动更新检查。
│   |    |              └── 根据“仅PPT模式”的设置，决定是否在启动时隐藏主窗口。
│   |    └── LoadInkFadeSettings(): 一个专门用于加载“墨迹渐隐”功能的设置的辅助函数。它被 `LoadSettings` 调用，负责：
│   |         ├── 将 `Settings.Canvas.EnableInkFade` 的值同步到设置面板和工具栏子面板中所有相关的开关上。
│   |         ├── 将 `Settings.Canvas.InkFadeTime` 的值同步到对应的滑块上。
│   |         └── 将最新的启用状态和渐隐时间更新到 `_inkFadeManager` 实例中，使其能够按照用户的最新配置来工作。
│   ├── MW_ShapeDrawing.cs
│   |    ├── #region State Management & Key Variables (未在代码中明确分区，但逻辑上存在)
│   |    |    ├── drawingShapeMode (int): **核心状态机变量**。这是一个整数标志，用于决定当前正在绘制哪种几何图形。每个数字都映射到一个特定的形状（例如，1=直线, 2=箭头, 3=矩形, ...）。整个文件的核心逻辑都围绕这个变量的 `switch` 语句展开。
│   |    |    ├── isLongPressSelected (bool): 实现“**工具粘滞**”功能的关键标志。当用户通过长按选择一个形状工具时，此标志为`true`，意味着在绘制完一个形状后，工具将保持激活状态，允许用户连续绘制多个相同形状，而不会自动切换回普通画笔。
│   |    |    ├── lastTempStroke (Stroke) / lastTempStrokeCollection (StrokeCollection): **实时预览的灵魂**。这两个变量用于存储用户在拖动过程中看到的“临时”或“预览”形状。在`MouseMove`或`TouchMove`事件中，代码会不断地删除旧的临时笔迹，并根据新的鼠标/手指位置创建一个新的临时笔迹添加到画布上，从而产生流畅的“橡皮筋”式拖拽效果。
│   |    |    ├── drawMultiStepShape... (相关变量): 用于处理需要多步操作才能完成的复杂图形（如双曲线、长方体）的状态管理。例如，`drawMultiStepShapeCurrentStep` 记录当前是第一笔还是第二笔，而 `drawMultiStepShapeSpecialStrokeCollection` 则会暂存第一笔绘制的辅助线（如双曲线的渐近线）。
│   |    |    └── isFirstTouchCuboid, CuboidFrontRect...: 专门用于绘制长方体的状态变量，记录第一步绘制的正面矩形的位置和尺寸。
│   |    ├── #region Floating Bar Control & UI Events (用户交互入口)
│   |    |    ├── ImageDrawShape_MouseUp(...): “形状”主按钮的点击事件。它是一个总开关，用于**显示或隐藏**包含所有具体形状工具的子面板（`BorderDrawShape`）。
│   |    |    ├── SymbolIconPinBorderDrawShape_MouseUp(...): 形状工具面板上的“图钉”图标点击事件。用于切换该面板的**自动隐藏**行为。
│   |    |    ├── Image_MouseDown(...): **长按选择功能的核心实现**。当用户在某个形状按钮上按下鼠标时，此函数会启动一个短暂的延时（500ms）。如果在延时后鼠标仍未抬起，它就会将 `isLongPressSelected` 设为 `true`，并立即激活对应的形状绘制模式，同时通过动画降低按钮透明度以提供视觉反馈。
│   |    |    └── BtnDraw[ShapeName]_Click(...): **所有具体形状按钮的单击事件处理程序**（如 `BtnDrawLine_Click`, `BtnDrawRectangle_Click` 等）。它们遵循一个标准模式：
│   |    |         ├── 1. `CheckIsDrawingShapesInMultiTouchMode()`: 检查并妥善处理与“多指书写”模式的冲突，暂时禁用多指事件以保证图形绘制的稳定性。
│   |    |         ├── 2. `EnterShapeDrawingMode(mode)`: 调用一个辅助函数，将核心状态变量 `drawingShapeMode` 设置为对应形状的ID，并将画布的 `EditingMode` 设为 `None`，正式进入图形绘制准备状态。
│   |    |         └── 3. `CancelSingleFingerDragMode()`: 取消可能存在的单指拖动画布模式，确保输入用于图形绘制。
│   |    ├── #region Core Drawing Logic (引擎核心)
│   |    |    ├── inkCanvas_TouchMove(...) / inkCanvas_MouseMove(...): **实时绘制的触发器**。这两个函数监听手指或鼠标在画布上的移动。它们的核心任务是获取当前的坐标点，然后调用下面最关键的 `MouseTouchMove` 函数来更新预览。
│   |    |    ├── MouseTouchMove(Point endP): **这是整个文件的“心脏”**。它根据当前的 `drawingShapeMode` 值，进入一个巨大的 `switch` 语句，为每一种图形执行实时计算和渲染：
│   |    |    |    ├── **简单图形 (case 1, 2, 3, ...)**: 根据起始点 (`iniP`) 和当前点 (`endP`)，通过几何计算生成构成该形状（直线、箭头、矩形、椭圆等）的所有点，创建一个新的 `Stroke` 或 `StrokeCollection`。
│   |    |    |    ├── **复杂/多步图形 (case 9, 24, 25)**: 逻辑会根据 `drawMultiStepShapeCurrentStep` 等状态变量来判断当前是第几步。
│   |    |    |    |    └── **双曲线 (Hyperbola)**: 第一步，根据拖拽绘制两条虚线作为渐近线并保存其斜率。第二步，根据新的拖拽点和已保存的渐近线，通过双曲线方程 (`x²/a² - y²/b² = 1`) 实时计算并绘制出双曲线的四条分支。
│   |    |    |    |    └── **长方体 (Cuboid)**: 第一步，绘制一个矩形作为正面。第二步，根据新的拖拽点确定深度，并绘制出连接前后两个面的所有棱线（包括虚线表示的被遮挡部分）。
│   |    |    |    └── **更新预览**: 在计算出新形状的笔迹后，会调用 `UpdateTempStrokeSafely` 或 `UpdateTempStrokeCollectionSafely`，将新的预览形状添加到画布上，同时移除上一步的预览，实现流畅的动态效果。
│   |    |    └── inkCanvas_MouseDown(...) / inkCanvas_MouseUp(...): **绘制周期的开始与结束**。
│   |    |         ├── `MouseDown`: 记录绘制的**起始点 (`iniP`)**，并捕获鼠标，准备开始绘制。
│   |    |         └── `MouseUp`: **标志着一次绘制操作的完成**。这是一个非常关键的函数，负责：
│   |    |             ├── **状态转换**: 对于多步图形，它会更新步骤计数器（例如，从第一步进入第二步）。对于单步图形或已完成的多步图形，它会根据 `isLongPressSelected` 的值决定是**保持当前工具激活**还是**调用 `BtnPen_Click` 切换回普通画笔模式**。
│   |    |             ├── **提交历史**: 将最终生成的 `lastTempStroke` 或 `lastTempStrokeCollection` 从“临时”状态固化，并调用 `timeMachine.Commit...` 方法，将其作为一次有效的用户操作**提交到撤销/重做（Undo/Redo）堆栈**中。
│   |    |             └── **清理**: 将所有临时变量（`lastTempStroke` 等）重置为 `null`，为下一次绘制操作做准备。
│   |    └── #region Helper & Generation Functions (工具箱)
│   |         ├── EnterShapeDrawingMode(int mode): 一个简单的辅助函数，用于集中设置进入特定形状绘制模式所需的状态（`drawingShapeMode`, `forceEraser`, `inkCanvas.EditingMode`）。
│   |         ├── Generate...(...) 函数簇: (例如 `GenerateEllipseGeometry`, `GenerateDashedLineStrokeCollection`, `GenerateArrowLineStroke`): 这些是**纯粹的几何计算函数**。它们是 `MouseTouchMove` 的底层工具，负责将简单的输入（如起点、终点）转换为构成复杂形状（如椭圆、虚线、箭头）所需的点集 (`List<Point>`) 或完整的笔迹集合 (`StrokeCollection`)。这种分离使得核心逻辑更清晰。
│   |         ├── UpdateTempStrokeSafely(...) / UpdateTempStrokeCollectionSafely(...): **性能与体验优化函数**。它们实现了防止屏幕闪烁的关键技术。通过“先添加新笔迹，再移除旧笔迹”的顺序，并结合 `Dispatcher.BeginInvoke` 来确保UI操作在正确的时机执行，极大地提升了用户在拖拽时的视觉平滑度。同时，内部的**节流阀 (`UpdateThrottleMs`)** 机制限制了UI更新的频率（约60fps），避免了因过高频率的鼠标移动事件而导致程序卡顿。
│   |         └── DrawCircleCenter(...): 一个用户体验增强功能。当用户启用“显示圆心”设置时，在绘制完一个圆后，此函数会被调用，以在圆的中心位置额外绘制一个小点作为标记。
│   ├── MW_SimulatePressure&InkToShape.cs
│   |    ├── #region Core Event Handler & Processing Pipeline
│   |    |    └── inkCanvas_StrokeCollected(...): **核心事件处理器与总调度中心**。这是整个文件的入口点。当用户在画布上完成一笔（`Stroke`）时，此函数被触发。它像一个流水线一样，将这笔新的笔迹依次送入多个处理模块进行加工。
│   |    ├── #region Module 1: Ink Enhancement & Simulation (笔迹美化与模拟)
│   |    |    ├── (墨迹渐隐处理): 如果启用了“墨迹渐隐”功能，笔迹会立即被交给 `_inkFadeManager` 处理，并从主画布上移除，实现书写后逐渐消失的效果。此模式会**跳过**后续所有处理。
│   |    |    ├── (屏蔽压感): 如果启用，此功能会遍历笔迹上的每一个点，将其压力值（`PressureFactor`）强制设为统一的中间值（0.5f），实现无论用户用多大力，笔迹粗细都完全一致的效果。
│   |    |    ├── (模拟压感): **核心的笔锋模拟算法**。当用户使用触摸屏（无物理压感）书写时，此功能被激活。它通过两种不同的算法来模拟真实的书写笔锋：
│   |    |    |    ├── **速度模式 (InkStyle 1)**: 通过 `GetPointSpeed` 计算书写时每个点的瞬时速度。**写得快的地方笔迹变细，写得慢（如起笔、收笔、转折处）的地方笔迹变粗**，模拟出毛笔或钢笔的运笔效果。
│   |    |    |    └── **笔尾模式 (InkStyle 0)**: 专注于模拟收笔时的“出锋”效果。它会让笔迹的最后一段（约10个点）的压力值逐渐减小，形成一个漂亮的由粗到细的笔尾。
│   |    ├── #region Module 2: Ink-to-Shape Recognition Engine (智能图形识别引擎)
│   |    |    ├── **Sub-Engine A: Automatic Line Straightening (自动直线修正)**
│   |    |    |    ├── IsPotentialStraightLine(Stroke stroke): **第一道快速筛选器**。它使用一些简单的启发式规则（如笔迹长度、复杂度、是否为明显曲线）来快速判断一个笔迹**有没有可能是**一条直线。这可以避免对复杂的曲线或文字进行不必要的、耗时的直线度计算。
│   |    |    |    ├── ShouldStraightenLine(Stroke stroke): **第二道精细决策器**。这是直线修正的**核心判断逻辑**。它通过多种复杂的数学计算来给笔迹的“直线度”打分，包括：
│   |    |    |    |    ├── 计算所有点到理论直线（首尾点连线）的**最大偏差**和**平均偏差**。
│   |    |    |    |    ├── 计算偏差的**方差**，判断笔迹是均匀弯曲（曲线）还是随机抖动（不完美的直线）。
│   |    |    |    |    ├── 计算**直线度评分 (`CalculateStraightnessScore`)**，综合评估偏差、方向一致性和路径效率。
│   |    |    |    |    └── 最终根据用户设置的**灵敏度 (`LineStraightenSensitivity`)** 和计算结果，做出是否将其拉直的最终决定。
│   |    |    |    ├── GetSnappedEndpoints(...): **端点吸附功能**。在决定拉直一条线之前，它会检查这条线的起点和终点附近是否存在其他笔迹的端点。如果存在，它会自动将这条新线的端点“吸附”过去，使得用户可以轻松绘制出**完美闭合和连接**的图形。
│   |    |    |    └── CreateStraightLine(...): **直线生成器**。一旦决定拉直，此函数会创建一个仅包含起点和终点（以及可能的中间点）的完美直线 `Stroke`，并用它替换掉用户原来手绘的、不完美的笔迹。
│   |    |    ├── **Sub-Engine B: Microsoft Ink Analysis API (复杂图形识别)**
│   |    |    |    └── (InkToShapeProcess): 利用微软强大的墨迹分析库来识别更复杂的图形。它会维护一个最近笔迹的缓存 (`newStrokes`)，并将其发送给 `InkRecognizeHelper`。
│   |    |    |         ├── **识别与修正**: 如果识别出是**圆形、椭圆、三角形、矩形**等，它会用一个通过几何计算生成的、完美的图形来替换掉用户手绘的草图。
│   |    |    |         └── **智能对齐**: 在修正图形时，它还会进行额外的智能处理，比如将新画的圆自动对齐到已有圆的**同心圆**或**相切圆**位置，极大地提升了绘图的规整性。
│   |    |    └── **Sub-Engine C: Custom Rectangle Guide Line System (自定义矩形参考线系统)**
│   |    |         ├── RectangleGuideLine (private class): 一个内部数据结构，用于存储被识别为“可能是矩形一边”的直线及其属性（如角度、端点、创建时间）。
│   |    |         ├── ProcessRectangleGuideLines(...): **状态机入口**。每当用户画出一条直线，此函数就会被调用。它会将这条直线包装成一个 `RectangleGuideLine` 对象并存入一个列表中。
│   |    |         ├── CheckForRectangleFormation(): **矩形成型检查器**。在添加了新的参考线后，此函数会立即检查当前所有的参考线（未过期的）是否能**组合**成一个矩形（即是否存在两对相互平行且垂直的线，并且它们的端点能够近似连接）。
│   |    |         └── CreateRectangleFromLines(...): 如果成功检测到四条可以构成矩形的参考线，此函数会**一次性地将这四条独立的直线替换为一个完整的、完美的矩形**。这是一个非常高级的功能，它不是基于单次笔画，而是基于用户**连续的绘图意图**进行识别。
│   |    ├── #region Module 3: Advanced Ink Smoothing (高级笔迹平滑)
│   |    |    └── ProcessStrokeAsync(Stroke originalStroke): 如果启用了高级贝塞尔平滑，并且笔迹未被识别为直线，它会在一个**后台线程**中对笔迹进行平滑处理，使得书写轨迹更加圆润自然，而不会阻塞UI线程导致卡顿。
│   |    └── #region Utility & Math Helpers (数学与工具函数)
│   |         ├── GetDistance(...), GetPointSpeed(...): 基础的几何和物理计算函数。
│   |         ├── FixPointsDirection(...): 一个修正工具，用于将两个点之间的连线强制修正为完美的水平或垂直线。
│   |         ├── GenerateFakePressure...(...): 为通过图形识别生成的完美形状（如三角形、矩形）的边框**添加模拟的笔锋效果**，使其看起来不那么生硬，更像是手绘的。
│   |         └── GetResolutionScale(): 一个适配函数，用于根据屏幕分辨率调整某些阈值，确保在不同分辨率的设备上功能表现一致。
│   ├── MW_TimeMachine.cs
|   │    ├── CommitReason (enum): 枚举类型，用于区分引发画布更改的原因（例如，用户输入、代码操作、形状识别等）
|   │    ├── _currentCommitType (CommitReason): 字段，跟踪当前提交到历史记录的更改类型，以防止代码操作触发用户输入历史
|   │    ├── IsEraseByPoint (bool): 只读属性，判断当前 `InkCanvas` 的编辑模式是否为“按点擦除”
|   │    ├── ReplacedStroke (StrokeCollection): 字段，用于在特定操作（如形状识别或点擦除）中临时存储被替换的笔画
|   │    ├── AddedStroke (StrokeCollection): 字段，用于在点擦除操作中临时存储新产生的笔画
|   │    ├── StrokeManipulationHistory (Dictionary): 字段，记录笔画在被移动、缩放或旋转等操作中，其顶点集合的初始状态和最终状态
|   │    ├── StrokeInitialHistory (Dictionary): 字段，存储每个笔画被添加到画布时的初始顶点集合，用于后续操作（如变换）的比较
|   │    ├── DrawingAttributesHistory (Dictionary): 字段，记录笔画的绘制属性（如颜色、粗细）更改前后的状态
|   │    ├── DrawingAttributesHistoryFlag (Dictionary): 字段，用作标志，确保每个笔画的每种属性更改只记录一次初始值
|   │    ├── timeMachine (TimeMachine): 字段，`TimeMachine` 类的实例，是实现撤销/重做功能的核心对象
|   │    ├── ApplyHistoryToCanvas(TimeMachineHistory item, InkCanvas applyCanvas = null): 将单个历史记录项（一个操作）应用（或撤销）到指定的 `InkCanvas` 上
|   │    ├── ApplyHistoriesToNewStrokeCollection(TimeMachineHistory[] items): 通过在临时的 `InkCanvas` 上应用一系列历史记录，来生成一个新的笔画集合，常用于生成页面预览
|   │    ├── GetPageImageElements(TimeMachineHistory[] items): 从一系列历史记录项中提取出所有被添加的 UI 元素（如图片、媒体）
|   │    ├── TimeMachine_OnUndoStateChanged(bool status): `timeMachine` 撤销状态改变时的事件处理程序，用于更新UI（如撤销按钮的可见性和可用性）
|   │    ├── TimeMachine_OnRedoStateChanged(bool status): `timeMachine` 重做状态改变时的事件处理程序，用于更新UI（如重做按钮的可见性和可用性）
|   │    ├── StrokesOnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e): `InkCanvas` 的 `StrokesChanged` 事件核心处理程序，捕捉笔画的添加和删除，并根据操作类型提交到 `timeMachine`
|   │    ├── Stroke_DrawingAttributesChanged(object sender, PropertyDataChangedEventArgs e): 笔画的 `DrawingAttributesChanged` 事件处理程序，当笔画的颜色、粗细等属性改变时触发，用于记录属性变更历史
|   │    ├── Stroke_StylusPointsReplaced(object sender, StylusPointsReplacedEventArgs e): 笔画的 `StylusPointsReplaced` 事件处理程序，当笔画的顶点集合被完全替换时更新其初始状态记录
|   │    └── Stroke_StylusPointsChanged(object sender, EventArgs e): 笔画的 `StylusPointsChanged` 事件处理程序，当笔画被变换（移动、缩放等）时触发，负责收集所有相关笔画的变化，并将它们作为一个整体操作提交到 `timeMachine`
│   ├── MW_Timer.cs
|   │    ├── TimeViewModel (class): 一个实现了 `INotifyPropertyChanged` 接口的视图模型类，用于在UI上显示时间和日期，当时间和日期值改变时能自动通知UI更新
|   │    │    ├── nowTime (string): 属性，表示当前的时间字符串
|   │    │    ├── nowDate (string): 属性，表示当前的日期字符串
|   │    │    └── OnPropertyChanged(): 方法，当属性值改变时，触发 `PropertyChanged` 事件
|   │    ├── timerCheckPPT (Timer): 定时器（已注释停用），原用于周期性检查PPT进程
|   │    ├── timerKillProcess (Timer): 定时器，周期性地根据用户设置检查并终止指定的竞争软件进程（如希沃白板、鸿合等）
|   │    ├── timerCheckAutoFold (Timer): 定时器，周期性地检查当前前台窗口，如果匹配到用户设置的特定软件（如希沃白板），则自动折叠主窗口的浮动栏
|   │    ├── AvailableLatestVersion (string): 字段，存储通过网络检查获取到的最新可用版本号
|   │    ├── timerCheckAutoUpdateWithSilence (Timer): 定时器，周期性地检查是否满足静默更新的条件（如在指定时间段内、软件处于空闲状态）并执行自动更新流程
|   │    ├── isHidingSubPanelsWhenInking (bool): 标志位，用于在用户书写时避免重复触发二级面板的隐藏动画
|   │    ├── timerDisplayTime (Timer): 定时器，每秒触发一次，用于更新UI上的时间显示
|   │    ├── timerDisplayDate (Timer): 定时器，每小时触发一次，用于更新UI上的日期显示
|   │    ├── timerNtpSync (Timer): 定时器，周期性地（如每2小时）从NTP服务器同步网络时间，以校准本地时间显示
|   │    ├── nowTimeVM (TimeViewModel): `TimeViewModel` 的实例，作为UI上时间和日期控件的数据上下文
|   │    ├── cachedNetworkTime (DateTime): 字段，缓存最近一次成功从NTP服务器获取的网络时间
|   │    ├── lastNtpSyncTime (DateTime): 字段，记录上一次成功进行NTP同步的本地时间
|   │    ├── lastDisplayedTime (string): 字段，记录上一次更新到UI上的时间字符串，用于避免不必要的UI刷新
|   │    ├── useNetworkTime (bool): 标志位，指示当前是否应使用网络时间来校准显示（通常在本地时间与网络时间差异较大时为true）
|   │    ├── networkTimeOffset (TimeSpan): 字段，存储本地时间与网络时间的精确差值
|   │    ├── lastLocalTime (DateTime): 字段，记录上一次检查时的本地时间，用于检测系统时间是否发生了大的跳变
|   │    ├── isNtpSyncing (bool): 标志位，防止多个NTP同步任务同时进行
|   │    ├── GetNetworkTimeAsync(): 异步方法，通过NTP协议从指定服务器获取当前的网络标准时间
|   │    ├── InitTimers(): 方法，初始化并启动文件中定义的所有定时器
|   │    ├── TimerNtpSync_ElapsedAsync(): `timerNtpSync` 定时器的异步事件处理程序，负责执行NTP时间同步逻辑
|   │    ├── TimerDisplayTime_Elapsed(object sender, ElapsedEventArgs e): `timerDisplayTime` 定时器的事件处理程序，负责计算并更新显示的时间，处理时间跳变及应用网络时间校准
|   │    ├── TimerDisplayDate_Elapsed(object sender, ElapsedEventArgs e): `timerDisplayDate` 定时器的事件处理程序，负责更新显示的日期
|   │    ├── TimerKillProcess_Elapsed(object sender, ElapsedEventArgs e): `timerKillProcess` 定时器的事件处理程序，根据用户设置构建 `taskkill` 命令并执行，以关闭其他白板软件
|   │    ├── foldFloatingBarByUser (bool): 标志位，记录用户是否手动进行了折叠操作，以避免自动折叠逻辑覆盖用户意图
|   │    ├── unfoldFloatingBarByUser (bool): 标志位，记录用户是否手动进行了展开操作，以避免自动折叠逻辑覆盖用户意图
|   │    ├── IsAnnotationWindow(): 方法，通过分析前台窗口的进程名、标题和尺寸等特征，判断其是否为常见白板软件的批注工具栏窗口
|   │    ├── timerCheckAutoFold_Elapsed(object sender, ElapsedEventArgs e): `timerCheckAutoFold` 定时器的事件处理程序，核心逻辑，检查当前激活窗口是否为需要自动折叠浮动栏的目标软件，并执行相应操作
|   │    └── timerCheckAutoUpdateWithSilence_Elapsed(object sender, ElapsedEventArgs e): `timerCheckAutoUpdateWithSilence` 定时器的事件处理程序，负责执行静默更新的完整逻辑，包括检查更新文件、时间段、软件状态，并最终触发安装
│   ├── MW_TouchEvents.cs
|   │    ├── isInMultiTouchMode (bool): 标志位，指示当前是否处于允许多个手指同时书写的多点触控模式
|   │    ├── dec (List<int>): 字段，用于存储当前屏幕上所有触摸点的设备ID，通过其数量来判断是单指、双指还是多指操作
|   │    ├── isSingleFingerDragMode (bool): 标志位，指示当前是否处于单指拖动画布的模式
|   │    ├── centerPoint (Point): 字段，用于在多点触控操作（如缩放、旋转）中记录手势的中心点
|   │    ├── lastInkCanvasEditingMode (InkCanvasEditingMode): 字段，用于在进入手势操作（如平移、缩放）前，保存当前的编辑模式，以便手势结束后恢复
|   │    ├── lastTouchDownTime (DateTime): 字段，记录上一次触摸按下的时间，用于判断是否为快速的多点触控启动
|   │    ├── MULTI_TOUCH_DELAY_MS (const double): 常量，定义了识别为多点触控手势的延迟时间（毫秒），以防止误触
|   │    ├── isMultiTouchTimerActive (bool): 标志位，指示用于延迟切换到多点手势模式的计时器是否正在运行
|   │    ├── PreserveNonStrokeElements(): 方法，在清空画布等操作前，遍历并保存画布上所有非笔画的UI元素（如图片、视频）
|   │    ├── CloneUIElement(UIElement originalElement): 方法，创建一个UI元素的深拷贝副本，用于在恢复元素时避免父子关系冲突
|   │    ├── RestoreNonStrokeElements(List<UIElement> preservedElements): 方法，将之前保存的非笔画UI元素重新添加到画布上
|   │    ├── BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e): "多指书写"模式切换按钮的事件处理程序，用于启用或禁用多点触控书写功能
|   │    ├── MainWindow_TouchDown(object sender, TouchEventArgs e): 在多指书写模式下，处理触摸按下事件
|   │    ├── MainWindow_StylusDown(object sender, StylusDownEventArgs e): 在多指书写模式下，处理手写笔按下事件，包括处理笔尾的橡皮擦功能和启动自定义的笔迹预览
|   │    ├── MainWindow_StylusUp(object sender, StylusEventArgs e): 在多指书写模式下，处理手写笔抬起事件，将自定义预览的笔画正式添加到画布中
|   │    ├── MainWindow_StylusMove(object sender, StylusEventArgs e): 在多指书写模式下，处理手写笔移动事件，更新自定义的笔迹实时预览
|   │    ├── GetStrokeVisual(int id): 辅助方法，为每个触摸或手写笔ID获取或创建一个用于实时预览笔迹的 `StrokeVisual` 对象
|   │    ├── GetVisualCanvas(int id): 辅助方法，获取承载 `StrokeVisual` 的自定义 `VisualCanvas` 容器
|   │    ├── GetTouchDownPointsList(int id): 辅助方法，获取指定ID的触摸点的编辑模式状态
|   │    ├── TouchDownPointsList (Dictionary): 字典，存储每个触摸ID及其对应的编辑模式
|   │    ├── StrokeVisualList (Dictionary): 字典，存储每个触摸ID及其对应的自定义笔迹预览对象 `StrokeVisual`
|   │    ├── VisualCanvasList (Dictionary): 字典，存储每个触摸ID及其对应的承载笔迹预览的 `VisualCanvas`
|   │    ├── Main_Grid_TouchDown(object sender, TouchEventArgs e): 在标准模式下，处理触摸按下事件的底层逻辑
|   │    ├── isPalmEraserActive (bool): 标志位，指示“手掌擦除”功能当前是否被激活
|   │    ├── palmEraserLastEditingMode (InkCanvasEditingMode): 字段，在激活“手掌擦除”前，保存当前的编辑模式，以便之后恢复
|   │    ├── palmEraserLastIsHighlighter (bool): 字段，在激活“手掌擦除”前，保存当前画笔是否为荧光笔状态
|   │    ├── GetTouchBoundWidth(TouchEventArgs e): 方法，获取触摸点的接触面积大小，用于判断是否为手掌等大面积接触
|   │    ├── inkCanvas_PreviewTouchDown(object sender, TouchEventArgs e): 核心的触摸按下预览事件。负责检测触摸面积以激活“手掌擦除”功能，并跟踪触摸点数量，在检测到多于一个触摸点时将画布模式切换为 `None` 以准备进行手势操作
|   │    ├── inkCanvas_PreviewTouchMove(object sender, TouchEventArgs e): 触摸移动预览事件，主要用于在“手掌擦除”激活时实时更新橡皮擦覆盖层的位置
|   │    ├── inkCanvas_PreviewTouchUp(object sender, TouchEventArgs e): 核心的触摸抬起预览事件。负责在最后一个触摸点离开时，取消“手掌擦除”并恢复之前的画笔状态，或在手势结束后恢复之前的编辑模式
|   │    ├── inkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e): 手势开始事件，设置手势识别模式（平移、缩放、旋转）
|   │    ├── Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e): 手势完成事件，当所有触摸点都离开后，将画布恢复到之前的编辑模式
|   │    ├── Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e): 核心的手势处理事件，在手指移动时被连续触发。它计算平移、缩放和旋转的变化量，并应用到选中的笔画或整个画布内容（包括图片等元素）上
|   │    ├── TransformCanvasImages(Matrix matrix): 辅助方法，将一个变换矩阵应用到画布上所有的图片和媒体元素，使它们能与笔画同步进行平移、缩放和旋转
|   │    ├── ApplyMatrixTransformToImage(Image image, Matrix matrix): 将变换矩阵应用到单个图片元素上
|   │    ├── ApplyMatrixTransformToMediaElement(MediaElement mediaElement, Matrix matrix): 将变换矩阵应用到单个媒体元素上
|   │    ├── ExitMultiTouchModeIfNeeded(): 辅助方法，用于退出“多指书写”模式，并恢复标准的事件处理和“手掌擦除”功能
|   │    └── EnterMultiTouchModeIfNeeded(): 辅助方法，用于进入“多指书写”模式，并切换事件处理逻辑，同时临时禁用“手掌擦除”功能以避免冲突
│   └── MW_TrayIcon.cs
|        └── (This file is part of the App class, not MainWindow, and handles logic for the system tray icon's context menu)
|        ├── SysTrayMenu_Opened(object sender, RoutedEventArgs e): 在系统托盘菜单打开前的事件处理程序。它会根据主窗口的当前状态（例如浮动栏是否折叠）动态更新菜单项的文本、图标和可用性，并临时取消窗口置顶以确保菜单能正确显示。
|        ├── SysTrayMenu_Closed(object sender, RoutedEventArgs e): 在系统托盘菜单关闭后的事件处理程序。主要用于恢复在菜单打开时被临时取消的窗口置顶状态。
|        ├── CloseAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e): “退出程序”菜单项的点击事件处理程序，用于关闭并退出整个应用程序。
|        ├── RestartAppTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e): “重启程序”菜单项的点击事件处理程序，它会启动一个自身的新实例，然后关闭当前实例。
|        ├── ForceFullScreenTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e): “强制全屏”菜单项的点击事件处理程序，用于将主窗口强制调整为占据整个主屏幕的大小。
|        ├── FoldFloatingBarTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e): “切换为收纳模式/退出收纳模式”菜单项的点击事件处理程序，用于在主窗口的浮动栏展开和折叠状态之间切换。
|        ├── ResetFloatingBarPositionTrayIconMenuItem_Clicked(object sender, RoutedEventArgs e): “重置浮动栏位置”菜单项的点击事件处理程序，用于将浮动栏的位置恢复到其默认状态。
|        ├── HideICCMainWindowTrayIconMenuItem_Checked(object sender, RoutedEventArgs e): 当“隐藏主窗口”菜单项被勾选时的事件处理程序。它会隐藏主窗口，并禁用其他与窗口操作相关的菜单项。
|        ├── HideICCMainWindowTrayIconMenuItem_UnChecked(object sender, RoutedEventArgs e): 当“隐藏主窗口”菜单项被取消勾选时的事件处理程序。它会重新显示主窗口，并启用之前被禁用的相关菜单项。
|        └── DisableAllHotkeysMenuItem_Clicked(object sender, RoutedEventArgs e): “禁用/启用所有快捷键”菜单项的点击事件处理程序。它作为一个开关，用于通过反射来禁用或重新启用程序中注册的所有全局热键。
│
├───obj
│   │   InkCanvasForClass.csproj.nuget.dgspec.json
│   │   InkCanvasForClass.csproj.nuget.g.props
│   │   InkCanvasForClass.csproj.nuget.g.targets
│   │   project.assets.json
│   │   project.nuget.cache
│   │
│   └───Debug
│       └───net472
│           │   .NETFramework,Version=v4.7.2.AssemblyAttributes.cs
│           │   App.g.i.cs
│           │   GeneratedInternalTypeHelper.g.i.cs
│           │   InkCanvasForClass.assets.cache
│           │   InkCanvasForClass.csproj.AssemblyReference.cache
│           │   InkCanvasForClass.csproj.ResolveComReference.cache
│           │   InkCanvasForClass.exe.withSupportedRuntime.config
│           │   InkCanvasForClass.GeneratedMSBuildEditorConfig.editorconfig
│           │   InkCanvasForClass_MarkupCompile.i.cache
│           │   Interop.IWshRuntimeLibrary.dll
│           │   MainWindow.g.i.cs
│           │
│           ├───Helpers
│           │   └───Plugins
│           │       └───BuiltIn
│           │           └───SuperLauncher
│           │                   LauncherSettingsControl.g.i.cs
│           │                   LauncherWindow.g.i.cs
│           │
│           ├───MainWindow_cs
│           ├───Resources
│           │   └───Styles
│           └───Windows
│               │   AddCustomIconWindow.g.i.cs
│               │   AddPickNameBackgroundWindow.g.i.cs
│               │   CountdownTimerWindow.g.i.cs
│               │   CustomIconWindow.g.i.cs
│               │   CycleProcessBar.g.i.cs
│               │   HasNewUpdateWindow.g.i.cs
│               │   HistoryRollbackWindow.g.i.cs
│               │   HotkeyItem.g.i.cs
│               │   HotkeySettingsWindow.g.i.cs
│               │   ManagePickNameBackgroundsWindow.g.i.cs
│               │   NamesInputWindow.g.i.cs
│               │   OperatingGuideWindow.g.i.cs
│               │   PluginSettingsWindow.g.i.cs
│               │   RandWindow.g.i.cs
│               │   ScreenshotSelectorWindow.g.i.cs
│               │   SplashScreen.g.i.cs
│               │   YesOrNoNotificationWindow.g.i.cs
│               │
│               └───SettingsViews
│                   │   SettingsWindow.g.i.cs
│                   │
│                   └───SettingsViews
│                           AboutPanel.g.i.cs
│                           AppearancePanel.g.i.cs
│                           FloatingBarDnDSettingsPanel.g.i.cs
│                           SettingsBaseView.g.i.cs
│
├───Properties
│   │   AssemblyInfo.cs
│   │   Resources.Designer.cs
│   │   Resources.resx
│   │   Settings.Designer.cs
│   │   Settings.settings
│   │
│   └───PublishProfiles
│           FolderProfile.pubxml
│           FolderProfile.pubxml.user
│
├───Resources
│   │   ChickenSoup.cs
│   │   contributors.png
│   │   DrawShapeImageDictionary.xaml
│   │   GeometryIcons.xaml
│   │   hatsune-miku1.png
│   │   ICC Start.png
│   │   icc.ico
│   │   ICCConfiguration.cs
│   │   IconImageDictionary.xaml
│   │   qrcodes.png
│   │   SeewoImageDictionary.xaml
│   │   Settings.cs
│   │   TimerDownNotice.wav
│   │
│   ├───Cursors
│   │       close-hand-cursor.cur
│   │       cursor-move.cur
│   │       cursor-resize-lr.cur
│   │       cursor-resize-lt-rb.cur
│   │       cursor-resize-rt-lb.cur
│   │       cursor-resize-tb.cur
│   │       Cursor.cur
│   │       open-hand-cursor.cur
│   │       Pen.cur
│   │
│   ├───DeveloperAvatars
│   │       （忽略：开发者）
│   │
│   ├───IACore
│   │       IACore.dll
│   │       IALoader.dll
│   │       IAWinFX.dll
│   │
│   ├───Icons
│   │       （忽略：资源文件）
│   │
│   ├───Icons-Fluent
│   │       （忽略：资源文件）
│   │
│   ├───Icons-png
│   │   │   （忽略：资源文件）
│   │   │
│   │   ├───classic-icons
│   │   │       （忽略：资源文件）
│   │   │
│   │   └───geo-icons
│   │           （忽略：资源文件）
│   │
│   ├───Illustrations
│   │       （忽略：资源文件）
│   │
│   ├───new-icons
│   │       （忽略：资源文件）
│   │
│   ├───PresentationExample
│   │       （忽略：资源文件）
│   │
│   └───Styles
│           Dark.xaml
│           Light.xaml
│
└───Windows
    │   AddCustomIconWindow.xaml
    │   AddCustomIconWindow.xaml.cs
    │   AddPickNameBackgroundWindow.xaml
    │   AddPickNameBackgroundWindow.xaml.cs
    │   CountdownTimerWindow.xaml
    │   CountdownTimerWindow.xaml.cs
    │   CustomIconWindow.xaml
    │   CustomIconWindow.xaml.cs
    │   CycleProcessBar.xaml
    │   CycleProcessBar.xaml.cs
    │   HasNewUpdateWindow.xaml
    │   HasNewUpdateWindow.xaml.cs
    │   HistoryRollbackWindow.xaml
    │   HistoryRollbackWindow.xaml.cs
    │   HotkeyItem.xaml
    │   HotkeyItem.xaml.cs
    │   HotkeySettingsWindow.xaml
    │   HotkeySettingsWindow.xaml.cs
    │   ManagePickNameBackgroundsWindow.xaml
    │   ManagePickNameBackgroundsWindow.xaml.cs
    │   NamesInputWindow.xaml
    │   NamesInputWindow.xaml.cs
    │   OperatingGuideWindow.xaml
    │   OperatingGuideWindow.xaml.cs
    │   PluginSettingsWindow.xaml
    │   PluginSettingsWindow.xaml.cs
    │   RandWindow.xaml
    │   RandWindow.xaml.cs
    │   ScreenshotSelectorWindow.xaml
    │   ScreenshotSelectorWindow.xaml.cs
    │   SplashScreen.xaml
    │   SplashScreen.xaml.cs
    │   YesOrNoNotificationWindow.xaml
    │   YesOrNoNotificationWindow.xaml.cs
    │
    └───SettingsViews
        │   SettingsWindow.xaml
        │   SettingsWindow.xaml.cs
        │
        └───SettingsViews
                AboutPanel.xaml
                AboutPanel.xaml.cs
                AppearancePanel.xaml
                AppearancePanel.xaml.cs
                FloatingBarDnDSettingsPanel.xaml
                FloatingBarDnDSettingsPanel.xaml.cs
                SettingsBaseView.xaml
                SettingsBaseView.xaml.cs