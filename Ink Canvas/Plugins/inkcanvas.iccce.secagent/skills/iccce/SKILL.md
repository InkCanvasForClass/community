---
name: iccce
description: 执行 ICC-CE 设置操作，例如查询版本、读取设置、查找设置字段和安全更新 ICC-CE 配置。
---

# ICC-CE 设置操作

本 Skill 适用于 Ink Canvas / ICC-CE（以下简称 ICC-CE）的设置调整。插件提供本机 MCP 服务，服务名为 `iccce`，地址为 `http://127.0.0.1:18790/mcp`。

修改主配置前必须先阅读本目录下的 [`SETTINGS_REFERENCE.md`](SETTINGS_REFERENCE.md)，再按下面的顺序调用工具。不要猜测配置文件路径、字段名或工具名，也不要直接访问 ICC-CE 文件系统。

## 工具契约

所有工具默认隐藏，但仍可通过 MCP 调用：

- `iccce__get_iccce_version_status`：查询 ICC-CE 版本、运行进程、根目录和设置文件状态；参数 `{}`。
- `iccce__list_iccce_setting_paths`：列出可读写设置路径；参数可为 `{}` 或 `{"prefix":"canvas"}`。
- `iccce__read_iccce_settings`：读取完整设置或指定路径；参数 `{"path":"canvas"}`，完整设置使用 `{"path":""}`。默认会遮蔽敏感字段。
- `iccce__update_iccce_settings`：更新设置；使用 `{"path":"canvas.inkWidth","value":3.0}` 修改单字段，或使用 `{"patch":{"canvas":{"inkWidth":3.0}}}` 做递归差量更新。

## 推荐流程

1. 调用 `iccce__get_iccce_version_status`，确认 ICC-CE 设置文件存在。
2. 阅读 `SETTINGS_REFERENCE.md`；不确定字段时调用 `iccce__list_iccce_setting_paths`，必要时指定 `prefix`。
3. 调用 `iccce__read_iccce_settings` 读取将要修改的准确路径，确认当前值、数据类型和影响范围。
4. 只有用户明确要求修改时，才调用 `iccce__update_iccce_settings`。参数只包含用户要求改变的字段。
5. 检查返回值中的 `written`、`updated_paths`、`applied_runtime` 和 `runtime_message`，如有重启提示，明确告知用户。

## 更新约束

- `patch` 对象会递归合并；数组会整体替换，不会按索引猜测合并。
- 更新前必须读取目标路径，不要用完整设置快照覆盖整个 `Settings.json`。
- 插件会校验路径是否属于 ICC-CE `Settings` 类型、值是否可解析为对应 C# 类型，并在写入前创建 `Settings.json.bak`。
- 插件会尽量同步运行时设置；部分只在启动时读取的设置仍需重启 ICC-CE。
- 不要读取、回显或修改密码哈希、盐、TOTP、Token、Secret 等敏感字段；插件默认遮蔽这些字段并拒绝通过 MCP 更新。
- 修改设置属于真实状态变更。用户只询问当前值时只能使用读取工具。

