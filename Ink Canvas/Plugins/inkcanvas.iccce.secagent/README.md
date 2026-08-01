# ICC-CE SecAgent Plugin

这是 ICC-CE 的插件，为 SecAgent 注册一个本机 MCP 服务和 `iccce` Skill，让 SecAgent 可以在用户明确要求时读取、校验和调整 ICC-CE 设置。

## 功能

- 向 `~/SecAgentWorkspace`（或 `SECAGENT_WORKSPACE` 指定的目录）注册 `skills/iccce` 和 `mcp/iccce-server.json`。
- 自动维护 `secagent.yaml` 中的 `iccce` HTTP MCP 服务，并默认启用该服务。
- 在 ICC-CE 设置页显示注册状态，支持手动注册 / 修复。
- MCP 只绑定 `127.0.0.1:18790`，工具默认隐藏，由 Skill 说明调用流程。
- 设置更新会校验 ICC-CE `Settings` 类型、保留未修改字段，并创建 `.bak` 备份。

## 构建

需要 .NET 6 SDK 和 Windows Desktop targeting pack：

```powershell
dotnet build .\ICC-CE.SecAgent.Plugin.csproj -c Debug -p:Platform=AnyCPU
```

将构建输出目录中的全部文件复制到 ICC-CE 安装目录的：

```text
Plugins\inkcanvas.iccce.secagent\
```

目录中必须保留 `manifest.json`、`ICC-CE.SecAgent.Plugin.dll`、`skills\iccce\SKILL.md` 和 `skills\iccce\SETTINGS_REFERENCE.md`。重启 ICC-CE 后，在插件管理页启用插件。

首次发现 SecAgent 工作区时，插件会弹窗请求写入注册文件；也可以在 ICC-CE 的插件设置页点击“注册 / 修复 SecAgent”。

