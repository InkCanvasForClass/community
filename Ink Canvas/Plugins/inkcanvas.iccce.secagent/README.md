# ICC-CE HTTP Plugin

ICC-CE 侧插件，为 SecAgent 连接插件提供普通 HTTP JSON 服务。它不实现 MCP，也不修改 SecAgent 工作区配置。

插件启动后监听 `127.0.0.1:18790`，提供 `/health`、`/tools` 和 `/tools/{name}` 接口，保留 ICC-CE 版本、设置路径、设置读取和安全更新工具。

SecAgent 端请安装 `ICC-CE-SecAgent-Connector`；连接插件会自动重试并在服务可用时注册工具和 Skill。
