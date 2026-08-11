# ICC-CE Settings 参考

ICC-CE 的主设置文件是应用目录下的 `Configs/Settings.json`。当前运行时设置类型为 `Ink_Canvas.Settings`，设置分组和 JSON 路径名称以源码中的 `JsonProperty` 标注为准。

## 顶层分组

常用顶层路径如下：

| 路径 | 用途 |
| --- | --- |
| `advanced` | 高级行为和诊断选项 |
| `appearance` | 外观、主题、背景和显示效果 |
| `automation` | 自动化规则与工作流 |
| `behavior` | PowerPoint / 演示行为 |
| `canvas` | 画布、笔迹、橡皮擦和墨迹平滑 |
| `gesture` | 手势和触控行为 |
| `inkToShape` | 墨迹转图形 |
| `startup` | 启动、自动运行和窗口启动行为 |
| `randSettings` | 随机抽选和计时器设置 |
| `modeSettings` | 工作模式设置 |
| `camera` | 摄像头设置 |
| `dlass` | DLASS / 云服务相关设置 |
| `upload` | 上传和存储设置 |
| `security` | 密码保护、TOTP 和进程保护 |
| `notification` | 公告、通知和提示时长 |
| `toolbar` | 浮动工具栏布局 |
| `toolbarConfigName` | 当前浮动工具栏配置名称 |
| `boardToolbarConfigName` | 当前白板工具栏配置名称 |
| `performance` | 性能监测和历史记录 |
| `miniWhiteboard` | 小白板设置 |

## 类型和生效规则

- 布尔值使用 `true` / `false`，整数和小数使用 JSON number，文本使用 JSON string。
- `canvas` 中的宽度、透明度、阈值和延迟字段通常是 number；修改前必须先读取当前值，不能凭界面标签猜单位。
- 颜色、列表、工具栏布局和自动化规则等复杂值要整体读取后再按原结构修改；数组整体替换。
- `security`、`dlass`、`upload` 中可能包含密码材料、访问令牌或其他敏感值。不要让模型读取或回显这些值，也不要通过 MCP 修改它们。
- 设置文件更新会创建 `Configs/Settings.json.bak`。运行时能同步的字段会立即应用；只在启动阶段读取的字段需要重启 ICC-CE。

## 以工具返回为准

源码会持续增加或调整设置字段，本文件只提供分组和安全规则，不是完整字段清单。具体字段必须使用 `iccce__list_iccce_setting_paths` 获取，字段当前值必须使用 `iccce__read_iccce_settings` 获取。

