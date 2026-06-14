# ICC CE 设置完整目录

```
应用设置
├── 首页
├── ── ICC CE 设置 ──
├── 通用
│   ├── 基本
│   │   └── TextBlock "行为"
│   │       ├── SettingsCard: 开机自启 → ToggleSwitch
│   │       ├── SettingsCard: 注册 Url 协议 → ToggleSwitch
│   │       ├── SettingsExpander: 托盘图标 → ToggleSwitch（开则展开）
│   │       │   ├── SettingsCard: 鼠标左键/触屏单击时 → ComboBox
│   │       │   └── SettingsCard: 鼠标右键/触屏长按时 → ComboBox
│   │       ├── SettingsCard: 教学安全模式 → ComboBox
│   │       └── SettingsExpander: 显示启动加载界面 → ToggleSwitch（开则展开）
│   │           ├── SettingsCard: 启动画面风格 → ComboBox
│   │           ├── SettingsCard: 自定义图片
│   │           └── SettingsCard: 文字位置
│   ├── 时钟
│   │   └── TextBlock "时钟"
│   │       └── SettingsCard: 使用24小时制显示时间 → ComboBox
│   ├── 隐私
│   │   └── TextBlock "隐私与遥测"
│   │       └── SettingsExpander: 隐私与遥测（默认展开）
│   │           ├── SettingsCard → CheckBox: 隐私协议
│   │           └── SettingsCard: 遥测级别 → ComboBox
│   ├── 安全
│   │   ├── TextBlock "安全密码"
│   │   │   ├── InfoBar: 安全密码说明
│   │   │   ├── LabeledSettingsCard: 启用密码保护 → ToggleSwitch
│   │   │   ├── SettingsCard: 密码管理 → Button
│   │   │   ├── LabeledSettingsCard: 启用 TOTP → ToggleSwitch
│   │   │   ├── SettingsCard: TOTP 密钥 → TextBox + Button
│   │   │   ├── LabeledSettingsCard: 退出时要求密码 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 进入设置时要求密码 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 重置配置时要求密码 → ToggleSwitch
│   │   │   └── LabeledSettingsCard: 修改/清空名单时要求密码 → ToggleSwitch
│   │   ├── TextBlock "U盘验证"
│   │   │   ├── InfoBar: U盘验证说明
│   │   │   ├── LabeledSettingsCard: 启用 U盘验证 → ToggleSwitch
│   │   │   ├── SettingsCard: 已授权 U盘序列号 → TextBox
│   │   │   └── SettingsCard: 检测并授权 U盘 → ComboBox + Button
│   │   └── TextBlock "进程保护"
│   │       ├── InfoBar: 进程保护说明
│   │       └── LabeledSettingsCard: 进程保护 → ToggleSwitch
│   └── 高级
│       ├── TextBlock "高级"
│       │   ├── LabeledSettingsCard: 特殊屏幕模式 → ToggleSwitch
│       │   ├── LabeledSettingsCard: 禁用硬件加速 → ToggleSwitch
│       │   ├── SettingsExpander: 触控倍率（默认展开）
│       │   │   ├── Slider + TextBlock
│       │   │   └── SettingsCard: 触控倍率校准 → Border + TextBlock
│       │   ├── LabeledSettingsCard: 橡皮擦绑定触控倍率 → ToggleSwitch
│       │   ├── SettingsCard: 笔尖模式边界宽度 → Slider
│       │   ├── SettingsCard: 手指模式边界宽度 → Slider
│       │   └── LabeledSettingsCard: 四点红外模式 → ToggleSwitch
│       ├── TextBlock "日志"
│       │   ├── LabeledSettingsCard: 启用日志 → ToggleSwitch
│       │   ├── LabeledSettingsCard: 按日期保存日志 → ToggleSwitch
│       │   └── LabeledSettingsCard: 退出时确认 → ToggleSwitch
│       └── TextBlock "配置"
│           ├── TextBlock: 配置说明
│           ├── SettingsCard: 配置方案 → ComboBox
│           └── SettingsCard → Button: 删除 / Button: 另存为
│   └── 性能
│       ├── TextBlock "性能监测"
│       │   └── LabeledSettingsCard: 启用监测 → ToggleSwitch
│       ├── TextBlock "当前运行状态"
│       │   └── SettingsCard: 当前状态 → TextBlock + Panel
│       ├── TextBlock "历史记录"
│       │   ├── SettingsCard: 历史摘要 → TextBlock + Panel
│       │   ├── SettingsCard: 墨迹平滑历史 → TextBlock + Panel
│       │   └── SettingsCard: 清除历史 → Button
│       ├── TextBlock "设备性能评估"
│       │   ├── SettingsCard: 设备评分 → TextBlock + Panel
│       │   └── SettingsCard: 运行设备测试 → Button
│       └── TextBlock "墨迹纠正耗时"
│           └── SettingsCard: 墨迹平滑统计 → TextBlock + Panel
├── 主界面
│   ├── 窗口
│   │   └── TextBlock "窗口设置"
│   │       ├── LabeledSettingsCard: 无焦点模式 → ToggleSwitch
│   │       ├── LabeledSettingsCard: 无边框模式 → ToggleSwitch
│   │       ├── LabeledSettingsCard: 窗口 Chrome 渲染 → ToggleSwitch
│   │       ├── LabeledSettingsCard: 避免全屏辅助 → ToggleSwitch
│   │       ├── LabeledSettingsCard: 多屏支持 → ToggleSwitch
│   │       ├── LabeledSettingsCard: 跟随鼠标屏幕 → ToggleSwitch（多屏开时可见）
│   │       └── SettingsExpander: 窗口置顶 → ToggleSwitch（开则展开）
│   │           ├── SettingsCard: 置顶模式 → RadioButton × 2
│   │           └── SettingsCard: 重启按钮 → Button
│   ├── 个性化
│   │   ├── TextBlock "主题"
│   │   │   ├── SettingsCard: 主题 → ComboBox
│   │   │   ├── SettingsCard: 窗口背景材质 → ComboBox
│   │   │   └── SettingsCard: 语言 → ComboBox
│   │   ├── TextBlock "浮动栏图标"
│   │   │   ├── SettingsCard: 浮动栏图标 → ComboBox
│   │   │   ├── Button: 上传自定义 + Button: 管理自定义
│   │   │   ├── SettingsCard: 黑板缩放比例 → Slider
│   │   │   ├── LabeledSettingsCard: 画板模式显示时间 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 使用24小时制 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 画板模式显示鸡汤 → ToggleSwitch
│   │   │   ├── SettingsCard: 鸡汤来源 → ComboBox + Button
│   │   │   ├── LabeledSettingsCard: 启用快捷面板 → ToggleSwitch
│   │   │   ├── SettingsCard: 快捷面板底部偏移 → Slider
│   │   │   └── SettingsCard: 展开按钮图标 → ComboBox
│   │   └── TextBlock "浮动栏按钮"
│   │       ├── LabeledSettingsCard: 使用旧版浮动栏 UI → ToggleSwitch
│   │       └── SettingsCard: 浮动栏按钮 → Clickable
│   └── 快捷键
│       ├── InfoBar: 快捷键说明
│       ├── TextBlock "鼠标模式"
│       │   └── LabeledSettingsCard: 鼠标模式下启用快捷键 → ToggleSwitch
│       ├── TextBlock "基本操作"
│       │   ├── HotkeyItem: 撤销
│       │   ├── HotkeyItem: 重做
│       │   ├── HotkeyItem: 清除
│       │   └── HotkeyItem: 粘贴
│       ├── TextBlock "工具切换"
│       │   ├── HotkeyItem: 选择工具
│       │   ├── HotkeyItem: 画笔工具
│       │   ├── HotkeyItem: 橡皮擦工具
│       │   ├── HotkeyItem: 黑板工具
│       │   └── HotkeyItem: 退出画笔工具
│       ├── TextBlock "画笔设置"
│       │   ├── HotkeyItem: 画笔 1
│       │   ├── HotkeyItem: 画笔 2
│       │   ├── HotkeyItem: 画笔 3
│       │   ├── HotkeyItem: 画笔 4
│       │   └── HotkeyItem: 画笔 5
│       ├── TextBlock "功能快捷键"
│       │   ├── HotkeyItem: 画直线
│       │   ├── HotkeyItem: 截图
│       │   ├── HotkeyItem: 快速画笔
│       │   ├── HotkeyItem: 隐藏
│       │   └── HotkeyItem: 退出
│       └── Button: 恢复默认 + Button: 保存设置
├── 画板设置
│   ├── 画布
│   │   ├── TextBlock "画布"
│   │   │   ├── LabeledSettingsCard: 显示笔光标 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 启用压感触控 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 忽略压感 → ToggleSwitch
│   │   │   ├── SettingsCard: 橡皮擦大小 → ComboBox
│   │   │   ├── LabeledSettingsCard: 选择时隐藏墨迹 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 清除画布同时清除历史 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 清除画布同时清除图片 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 压缩上传图片 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 白板展台启动希沃视频展台 → ToggleSwitch
│   │   │   ├── SettingsCard: 保留双曲线渐近线 → ComboBox
│   │   │   ├── LabeledSettingsCard: 显示圆心 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 显示坐标单位刻度 → ToggleSwitch
│   │   │   ├── SettingsCard: 曲线平滑模式 → ComboBox
│   │   │   ├── SettingsExpander: 画笔自动恢复 → ToggleSwitch（开则展开）
│   │   │   │   ├── SettingsCard: 自动恢复时间点 → TextBox
│   │   │   │   ├── SettingsCard: 恢复颜色 → ComboBox
│   │   │   │   ├── SettingsCard: 恢复宽度 → Slider
│   │   │   │   └── SettingsCard: 恢复透明度 → Slider
│   │   │   └── SettingsExpander: 橡皮擦后自动切回 → ToggleSwitch（开则展开）
│   │   │       └── SettingsCard: 切回延迟 → Slider
│   │   └── TextBlock "手势"
│   │       ├── LabeledSettingsCard: 允许旋转缩放 → ToggleSwitch
│   │       └── SettingsExpander: 手掌擦除 → ToggleSwitch（开则展开）
│   │           └── SettingsCard: 手掌擦除灵敏度 → ComboBox
│   └── 墨迹识别
│       └── TextBlock "墨迹识别"
│           ├── LabeledSettingsCard: 启用墨迹识别 → ToggleSwitch
│           ├── SettingsCard: 形状识别引擎 → ComboBox
│           ├── LabeledSettingsCard: 手写美化 → ToggleSwitch
│           ├── SettingsCard: 手写美化字体 → ComboBox（手写美化开时可见）
│           ├── LabeledSettingsCard: 矩形块假压感 → ToggleSwitch
│           ├── LabeledSettingsCard: 三角形块假压感 → ToggleSwitch
│           ├── SettingsExpander: 形状修正（墨迹识别开则展开）
│           │   ├── SettingsCard → CheckBox: 修正三角形
│           │   ├── SettingsCard → CheckBox: 修正矩形
│           │   └── SettingsCard → CheckBox: 修正椭圆
│           ├── SettingsExpander: 自动直线 → ToggleSwitch（开则展开）
│           │   ├── SettingsCard: 长度阈值 → Slider
│           │   ├── SettingsCard: 灵敏度 → Slider
│           │   ├── SettingsCard: 高精度直线 → ToggleSwitch
│           │   ├── SettingsCard: 暂停直线 → ToggleSwitch
│           │   └── SettingsCard: 暂停延迟 → Slider
│           └── SettingsExpander: 线段端点吸附 → ToggleSwitch（开则展开）
│               └── SettingsCard: 吸附距离 → Slider
├── PPT联动
│   ├── TextBlock "PPT联动"
│   │   ├── LabeledSettingsCard: 支持 PowerPoint → ToggleSwitch
│   │   ├── LabeledSettingsCard: PPT增强 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 跳过动画 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 使用 Rot PPT 链接 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 支持 WPS → ToggleSwitch
│   │   └── LabeledSettingsCard: 杀死 WPP 进程 → ToggleSwitch
│   ├── TextBlock "翻页按钮"
│   │   ├── LabeledSettingsCard: 显示翻页按钮 → ToggleSwitch
│   │   ├── SettingsExpander: 左侧按钮 → CheckBox（翻页按钮开则展开）
│   │   │   ├── SettingsCard: 左侧偏移 → Slider
│   │   │   └── SettingsCard: 左侧透明度 → Slider
│   │   ├── SettingsExpander: 右侧按钮 → CheckBox（翻页按钮开则展开）
│   │   │   ├── SettingsCard: 右侧偏移 → Slider
│   │   │   └── SettingsCard: 右侧透明度 → Slider
│   │   ├── SettingsExpander: 左下按钮 → CheckBox（翻页按钮开则展开）
│   │   │   ├── SettingsCard: 左下偏移 → Slider
│   │   │   └── SettingsCard: 左下透明度 → Slider
│   │   ├── SettingsExpander: 右下按钮 → CheckBox（翻页按钮开则展开）
│   │   │   ├── SettingsCard: 右下偏移 → Slider
│   │   │   └── SettingsCard: 右下透明度 → Slider
│   │   ├── SettingsCard: 侧边组 → CheckBox × 3
│   │   ├── SettingsCard: 底部组 → CheckBox × 3
│   │   ├── LabeledSettingsCard: 页码按钮可点击 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 增强预览 → ToggleSwitch
│   │   └── LabeledSettingsCard: 长按翻页 → ToggleSwitch
│   ├── TextBlock "进入放映时进入批注"
│   │   └── LabeledSettingsCard: 进入放映时进入批注 → ToggleSwitch
│   ├── TextBlock "PPT设置"
│   │   ├── LabeledSettingsCard: 双指手势 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 手势翻页 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 时间胶囊 → ToggleSwitch
│   │   ├── SettingsCard: 时间胶囊位置 → ComboBox
│   │   ├── SettingsCard: 时间胶囊透明度 → Slider
│   │   ├── SettingsCard: 时间胶囊缩放 → Slider
│   │   ├── SettingsCard: 时间胶囊重置位置 → Button
│   │   └── LabeledSettingsCard: 放映时显示快捷面板 → ToggleSwitch
│   ├── TextBlock "自动截图"
│   │   ├── LabeledSettingsCard: 自动截图 → ToggleSwitch
│   │   └── LabeledSettingsCard: 自动保存墨迹 → ToggleSwitch
│   └── TextBlock "记住上次页面"
│       ├── LabeledSettingsCard: 记住上次页面 → ToggleSwitch
│       ├── LabeledSettingsCard: 重新进入时跳到第一页 → ToggleSwitch
│       ├── LabeledSettingsCard: 通知隐藏页面 → ToggleSwitch
│       └── LabeledSettingsCard: 通知自动播放 → ToggleSwitch
├── 更新
│   ├── 状态横幅（当前版本/更新状态）
│   └── TabControl
│       ├── Tab: 更新日志
│       │   └── MarkdownScrollViewer
│       ├── Tab: 更新设置
│       │   ├── TextBlock "自动更新"
│       │   │   ├── LabeledSettingsCard: 自动更新 → ToggleSwitch
│       │   │   ├── LabeledSettingsCard: 静默更新 → ToggleSwitch（自动更新开时可见）
│       │   │   ├── LabeledSettingsCard: 智能更新 → ToggleSwitch
│       │   │   └── SettingsExpander: 静默更新时间范围（静默更新开则展开）
│       │   │       ├── SettingsCard: 时间范围设置 → ComboBox × 2
│       │   │       └── SettingsCard: 时间段说明
│       │   ├── TextBlock "更新通道"
│       │   │   ├── SettingsCard: 更新通道 → ComboBox
│       │   │   └── SettingsCard: 更新包架构 → ComboBox
│       │   └── TextBlock "维护"
│       │       └── SettingsCard: 版本修复 → Button
│       └── Tab: 历史版本
│           ├── SettingsCard: 选择版本 → ComboBox
│           ├── MarkdownScrollViewer
│           └── Button: 回滚到此版本
├── 通知
│   ├── 通知设置
│   │   ├── TextBlock "通知"
│   │   │   ├── LabeledSettingsCard: 启用公告 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 启用强制弹窗 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 启用动态 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 启用 Windows 通知 → ToggleSwitch
│   │   │   └── SettingsExpander: 听写免打扰 → ToggleSwitch（开则展开）
│   │   │       ├── SettingsCard: PPT 模式免打扰 → CheckBox
│   │   │       └── SettingsCard: 白板模式免打扰 → CheckBox
│   │   ├── TextBlock "通知提供商"
│   │   │   ├── SettingsCard: 通知提供商管理 → Button
│   │   │   └── ItemsControl: 提供商列表（动态）
│   │   ├── TextBlock "行为"
│   │   │   ├── SettingsCard: 通知位置 → ComboBox
│   │   │   ├── SettingsCard: 动画模式 → ComboBox
│   │   │   ├── SettingsCard: 更新通知持续时间 → Slider
│   │   │   ├── SettingsCard: 紧急通知持续时间 → Slider
│   │   │   ├── SettingsCard: 重要通知持续时间 → Slider
│   │   │   ├── SettingsCard: 提醒通知持续时间 → Slider
│   │   │   └── SettingsCard: 其他通知持续时间 → Slider
│   │   └── TextBlock "操作"
│   │       └── SettingsCard: 测试通知 → Button
│   └── 公告中心
├── 实验性
│   └── TextBlock "无"
│       ├── LabeledSettingsCard: 全屏辅助 → ToggleSwitch
│       ├── LabeledSettingsCard: 边缘手势工具 → ToggleSwitch
│       ├── LabeledSettingsCard: 强制全屏 → ToggleSwitch
│       ├── LabeledSettingsCard: DPI 变更检测 → ToggleSwitch
│       └── LabeledSettingsCard: 分辨率变更检测 → ToggleSwitch
├── 存储
│   ├── 存储管理
│   │   ├── TextBlock "存储管理"
│   │   │   ├── 概览卡片（总用量 + 占比柱状图 + 图例）
│   │   │   └── Button: 刷新 + Button: 打开应用文件夹
│   │   └── TextBlock "分类详情"
│   │       ├── SettingsExpander: 核心文件（默认展开）
│   │       │   └── SettingsCard: 不可清理
│   │       ├── SettingsCard: 日志 → TextBlock + Button: 清理
│   │       ├── SettingsCard: 墨迹 → TextBlock + Button: 清理
│   │       ├── SettingsCard: 备份 → TextBlock + Button: 清理
│   │       ├── SettingsExpander: 自定义文件（默认展开）
│   │       │   └── SettingsCard: 自定义文件说明
│   │       ├── SettingsExpander: 插件（默认展开）
│   │       │   └── SettingsCard: 插件说明
│   │       ├── SettingsCard: 自动更新 → TextBlock + Button: 清理
│   │       └── SettingsCard: 其他 → TextBlock
│   └── 备份与还原
│       └── TextBlock "无"
│           ├── LabeledSettingsCard: 更新前自动备份 → ToggleSwitch
│           ├── SettingsExpander: 定期自动备份 → ToggleSwitch（开则展开）
│           │   └── SettingsCard: 备份间隔 → ComboBox
│           └── SettingsExpander: 手动操作（默认展开）
│               ├── SettingsCard: 手动备份 → Clickable
│               └── SettingsCard: 还原备份 → Clickable
│   └── 云存储
│       ├── TextBlock "云存储管理"
│       │   ├── SettingsCard: 上传延迟 → TextBox
│       │   └── SettingsExpander: 上传提供商（默认展开）
│       │       └── SettingsCard: 提供商列表 → ItemsControl
│       ├── TextBlock "Dlass"
│       │   ├── SettingsExpander: 用户令牌（默认展开）
│       │   │   ├── SettingsCard: 已保存令牌 → ComboBox
│       │   │   ├── SettingsCard: 新令牌 → TextBox
│       │   │   └── SettingsCard: 令牌操作 → Button × 3
│       │   ├── SettingsCard: 连接状态 → TextBlock + Button
│       │   ├── SettingsCard: 班级选择 → ComboBox
│       │   └── LabeledSettingsCard: 自动上传笔记 → ToggleSwitch
│       └── TextBlock "WebDAV"
│           └── SettingsExpander: WebDAV 设置（默认展开）
│               ├── SettingsCard: WebDAV URL → TextBox
│               ├── SettingsCard: 用户名 → TextBox
│               ├── SettingsCard: 密码 → PasswordBox
│               ├── SettingsCard: 根目录 → TextBox
│               └── SettingsCard: 操作 → Button × 2
├── 工具栏
│   ├── 组件
│   │   ├── TextBlock "配置方案"
│   │   │   ├── ComboBox: 配置方案选择
│   │   │   └── Button: 新建 + Button: 复制 + Button: 删除
│   │   ├── TextBlock "已添加组件"
│   │   │   └── ListBox: 已添加组件（可拖拽排序）
│   │   ├── StackPanel: 分组内组件（选中分组时可见）
│   │   │   └── ListBox: 分组内组件
│   │   ├── TabControl
│   │   │   ├── Tab: 组件库
│   │   │   │   └── ListBox: 可用组件库
│   │   │   ├── Tab: 组件设置
│   │   │   │   ├── TextBlock "组件属性"
│   │   │   │   │   ├── SettingsCard: 分隔边框 → CheckBox
│   │   │   │   │   ├── SettingsCard: 红色样式 → CheckBox
│   │   │   │   │   └── StackPanel: 快速调色板显示模式 → ComboBox
│   │   │   │   ├── TextBlock "尺寸"
│   │   │   │   │   └── Grid: 固定宽高/最小最大宽高 → TextBox × 6
│   │   │   │   ├── TextBlock "对齐"
│   │   │   │   │   └── Grid: 水平/垂直对齐 → ComboBox × 2
│   │   │   │   ├── TextBlock "外观"
│   │   │   │   │   └── Grid: 字号/图标大小/透明度 → TextBox × 3
│   │   │   │   ├── TextBlock "边距"
│   │   │   │   │   └── Grid: 左/上/右/下 → TextBox × 4
│   │   │   │   └── Button: 重置组件设置
│   │   │   └── Tab: 高级设置
│   │   │       ├── TextBlock "按规则隐藏"
│   │   │       ├── ComboBox: 规则集模式 + CheckBox: 反转 + Button: 添加组
│   │   │       └── ItemsControl: 条件组列表（动态）
│   │   └── Button: 重置布局
│   ├── 外观
│   │   └── TextBlock "基本"
│   │       ├── SettingsCard: 浮动栏缩放 → Slider
│   │       └── SettingsExpander: 浮动栏透明度（默认展开）
│   │           ├── SettingsCard: 浮动栏透明度 → Slider
│   │           └── SettingsCard: PPT 中浮动栏透明度 → Slider
│   └── 菜单
│       ├── TextBlock "已添加的菜单项"
│       │   └── ListBox: 已添加菜单项（3×3 布局，可拖拽排序，最多 9 个）
│       ├── TextBlock "可添加的菜单项"
│       │   └── ListBox: 可用菜单项库
│       └── Button: 恢复默认布局
├── 白板
│   ├── 组件
│   │   ├── TextBlock "配置方案"
│   │   │   ├── ComboBox: 配置方案选择
│   │   │   └── Button: 新建 + Button: 复制 + Button: 删除
│   │   ├── RadioButton: 左侧/中央/右侧区域选择
│   │   ├── Button: 添加组
│   │   ├── ItemsControl: 分组列表（含拖拽排序）
│   │   │   └── ListBox: 分组内组件（可拖拽排序）
│   │   ├── TabControl
│   │   │   ├── Tab: 组件库
│   │   │   │   └── ListBox: 可用组件库
│   │   │   └── Tab: 组件设置
│   │   │       ├── TextBlock "尺寸"
│   │   │       │   └── Grid: 固定宽高/最小最大宽高 → TextBox × 4
│   │   │       ├── TextBlock "外观"
│   │   │       │   └── Grid: 字号/透明度 → TextBox × 2
│   │   │       ├── TextBlock "边距"
│   │   │       │   └── Grid: 左/上/右/下 → TextBox × 4
│   │   │       └── Button: 重置组件设置
│   │   └── Button: 恢复默认布局
│   ├── 外观
│   │   ├── TextBlock "白板工具栏透明度"
│   │   │   └── SettingsExpander: 白板工具栏透明度（默认展开）
│   │   │       ├── SettingsCard: 左侧 → Slider
│   │   │       ├── SettingsCard: 中央 → Slider
│   │   │       └── SettingsCard: 右侧 → Slider
│   │   └── TextBlock "黑板缩放 80%"
│   │       └── SettingsExpander: 黑板缩放 80%（默认展开）
│   │           ├── SettingsCard: 左侧 → Slider
│   │           ├── SettingsCard: 中央 → Slider
│   │           └── SettingsCard: 右侧 → Slider
│   └── 菜单
│       ├── TextBlock "已添加的菜单项"
│       │   └── ListBox: 已添加菜单项（3×3 布局，可拖拽排序，最多 9 个）
│       ├── TextBlock "可添加的菜单项"
│       │   └── ListBox: 可用菜单项库
│       └── Button: 恢复默认布局
├── 自动化 (AutomationWorkflowPage)
│   ├── (左侧导航栏)
│   │   ├── 预设自动化（固定项）
│   │   └── 工作流列表（动态）
│   ├── (预设面板 - 选择"预设自动化"时显示)
│   │   ├── TextBlock "自动折叠"
│   │   │   ├── SettingsExpander: 希沃系列（默认展开）
│   │   │   │   ├── SettingsCard: 希沃白板 5 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃摄像 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃白板 3 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃白板 3C → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃白板 5C → ToggleSwitch
│   │   │   │   └── SettingsCard: 希沃 Pinco → ToggleSwitch
│   │   │   ├── SettingsExpander: 鸿合系列（默认展开）
│   │   │   │   ├── SettingsCard: 鸿合白板 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 鸿合摄像 → ToggleSwitch
│   │   │   │   └── SettingsCard: 鸿合灯板 → ToggleSwitch
│   │   │   ├── SettingsExpander: 其他（默认展开）
│   │   │   │   ├── SettingsCard: 文香白板 → ToggleSwitch
│   │   │   │   ├── SettingsCard: Microsoft Whiteboard → ToggleSwitch
│   │   │   │   ├── SettingsCard: Admox 白板 → ToggleSwitch
│   │   │   │   ├── SettingsCard: Admox 展台 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 易云白板 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 易云展台 → ToggleSwitch
│   │   │   │   ├── SettingsCard: MaxHub 白板 → ToggleSwitch
│   │   │   │   └── SettingsCard: 旧版中银白板 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: PPT 放映时自动折叠 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 退出白板后自动折叠 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 退出 PPT 后自动折叠 → ToggleSwitch
│   │   │   └── LabeledSettingsCard: 软件退出后保持折叠 → ToggleSwitch
│   │   ├── TextBlock "自动结束"
│   │   │   ├── LabeledSettingsCard: PPT 工具 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: EasiNote 5 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 鸿合批注 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 幼教 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 希沃桌面2批注 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: InkCanvas IC → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: ICA → ToggleSwitch
│   │   │   └── LabeledSettingsCard: Inkeys → ToggleSwitch
│   │   ├── TextBlock "折叠模式"
│   │   │   ├── LabeledSettingsCard: 退出折叠时进入批注 → ToggleSwitch
│   │   │   └── LabeledSettingsCard: 结束鸿合后进入批注 → ToggleSwitch
│   │   ├── TextBlock "自动保存"
│   │   │   ├── LabeledSettingsCard: 截图按日期文件夹保存 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 截图时自动保存墨迹 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 清除时自动截图 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 保存墨迹为 XML → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 自动保存墨迹 → ToggleSwitch
│   │   │   ├── SettingsCard: 自动保存间隔 → ComboBox
│   │   │   ├── LabeledSettingsCard: 自动删除保存文件 → ToggleSwitch
│   │   │   ├── SettingsCard: 自动删除天数阈值 → ComboBox
│   │   │   ├── SettingsCard: 最少自动化墨迹数 → Slider
│   │   │   ├── LabeledSettingsCard: 保存整页墨迹 → ToggleSwitch
│   │   │   ├── LabeledSettingsCard: 使用自定义保存文件名 → ToggleSwitch
│   │   │   └── SettingsExpander: 保存文件名格式（默认展开）
│   │   │       ├── ComboBox: 文件名格式预设
│   │   │       └── SettingsCard: 自定义模板 → TextBox
│   │   ├── TextBlock "浮动栏拦截"
│   │   │   ├── SettingsExpander: 希沃系列（默认展开）
│   │   │   │   ├── SettingsCard: 希沃白板 3 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃白板 5 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃白板 5C → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃 Pinco → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃 Pinco 绘画 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃 PPT 工具 → ToggleSwitch
│   │   │   │   ├── SettingsCard: 希沃桌面批注 → ToggleSwitch
│   │   │   │   └── SettingsCard: 希沃桌面侧栏 → ToggleSwitch
│   │   │   └── SettingsExpander: 其他（默认展开）
│   │   │       ├── SettingsCard: AiClass → ToggleSwitch
│   │   │       ├── SettingsCard: 鸿合批注 → ToggleSwitch
│   │   │       ├── SettingsCard: 畅言智慧课堂 → ToggleSwitch
│   │   │       ├── SettingsCard: 畅言 PPT → ToggleSwitch
│   │   │       └── SettingsCard: 天喻教育云 → ToggleSwitch
│   │   └── TextBlock "文件关联"
│   │       └── SettingsExpander: 文件关联检查（默认展开）
│   │           ├── TextBlock + Button: 检查状态
│   │           ├── SettingsCard: 注册文件关联 → Button
│   │           └── SettingsCard: 取消注册文件关联 → Button
│   └── (工作流编辑器 - 选择工作流时显示)
│       └── TextBlock "自定义自动化规则"
│           └── SettingsCard: 创建自定义的触发器→条件→行动规则 → Clickable
├── 随机点名
│   ├── TextBlock "随机点名"
│   │   ├── LabeledSettingsCard: 显示编辑名单按钮 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 启用随机和单人抽取 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 启用快速抽取 → ToggleSwitch
│   │   ├── LabeledSettingsCard: 使用外部调用 → ToggleSwitch
│   │   ├── SettingsCard: 外部调用类型 → ComboBox
│   │   ├── SettingsCard: 单次关闭延迟 → Slider
│   │   └── SettingsCard: 单次最大人数 → Slider
│   ├── TextBlock "背景设置"
│   │   └── SettingsCard: 背景选择 → Button × 2 + ComboBox
│   ├── TextBlock "新 UI"
│   │   ├── LabeledSettingsCard: 使用新点名 UI → ToggleSwitch
│   │   ├── LabeledSettingsCard: 避免重复抽取 → ToggleSwitch
│   │   ├── SettingsCard: 历史记录数 → Slider
│   │   └── SettingsCard: 权重 → Slider
│   └── TextBlock "计时器"
│       ├── SettingsCard: 计时器样式 → ComboBox
│       ├── LabeledSettingsCard: 超时正计时 → ToggleSwitch
│       ├── LabeledSettingsCard: 超时高亮 → ToggleSwitch
│       ├── SettingsCard: 音量 → Slider
│       ├── SettingsCard: 自定义提示音 → Button × 2
│       ├── LabeledSettingsCard: 渐进提醒 → ToggleSwitch
│       ├── SettingsCard: 渐进提醒音量 → Slider
│       └── SettingsCard: 渐进提醒自定义音 → Button × 2
├── Debug
│   ├── TextBlock "Debug"
│   │   └── LabeledSettingsCard: 显示控制台 → ToggleSwitch
│   └── TextBlock "图标设置"
│       └── SettingsExpander: SettingsExpander 示例（默认展开）
│           ├── CopyButton
│           └── SettingsCard: Customization
├── ── 插件设置 ──
├── 插件
│   ├── TextBlock "无"
│   │   ├── Border: 插件数量状态
│   │   └── StackPanel: 插件容器（动态加载）
│   └── （插件设置页面动态加载 → PluginSettingsPage）
├── ── 底部 ──
├── 友情链接
│   └── TextBlock "无"
│       └── （动态内容）
└── 关于 Ink Canvas
    └── TextBlock "无"
        └── （动态内容）
```
