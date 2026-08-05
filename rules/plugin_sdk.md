# 插件 SDK 开发规范

## 位置与命名空间

插件 SDK 位于 `InkCanvas.PluginSdk/`，命名空间 **`Ink_Canvas.Plugins`**，目标框架 net6.0-windows10.0.19041.0。
SDK 已发布到 nuget.org（包名 `InkCanvas.PluginSdk`），用 Nerdbank.GitVersioning 生成版本号（自引用 NBGV，防 NU1504）。

> 给 SDK 加接口/改接口 = 对外契约变更，发布后旧插件可能不兼容。新增能力优先**加新接口**而非改旧接口；接口方法签名变更需同步更新插件市场内受影响插件。

## 插件入口

### IPlugin 接口

每个插件必须有一个实现 `IPlugin` 的类，并用 `[PluginEntrance]` 标记：

```csharp
[PluginEntrance]
public class MyPlugin : IPlugin
{
    public string Id => "com.example.myplugin";
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Description => "示例";
    public string Author => "作者";
    public int Order => 0;

    public void Initialize(IPluginHost host) { /* 注册服务、注册 UI、注册 URI 处理器等 */ }
    public void Shutdown() { }
    public object GetMainView() => null;
    public object GetSettingsView() => null;
}
```

### manifest.json

插件包内必须有 `manifest.json`，字段见 `PluginManifest.cs`：

| 字段 | 说明 |
|------|------|
| `id` | 唯一标识（如 `com.example.myplugin`） |
| `name` / `version` / `description` / `author` | 基础元数据 |
| `entranceAssembly` | 入口程序集文件名（如 `MyPlugin.dll`） |
| `apiVersion` | 目标 SDK API 版本 |
| `minHostVersion` | 最低宿主版本，宿主实际编译版本（`HostApiRequirement.HostVersion`）低于此值拒绝加载 |
| `versionRange` | 宿主版本范围（如 `>=1.7.18,<2.0.0`），与 `minHostVersion` 同时填需同时满足 |
| `permissions` | 申请的权限列表（`Settings`/`Hotkeys`/`Network`/`FileSystem` 等），宿主加载时可提示用户 |
| `dependencies` | 插件依赖列表（`PluginDependency`） |
| `icon` / `license` / `tags` / `sourceUrl` | 元信息 |

### 加载时序

1. 宿主读取 manifest.json，校验 `minHostVersion` / `versionRange` / `apiVersion`
2. 加载入口程序集，实例化 `[PluginEntrance]` 标记的 `IPlugin`
3. 调用 `Initialize(IPluginHost)` —— **所有注册动作必须在 Initialize 阶段完成**（服务、工具栏项、URI 处理器、IPC 处理器）
4. 卸载时调用 `Shutdown()`

## IPluginHost（宿主 API）

插件在 `Initialize` 拿到的 `host` 是 `PluginHostProxy`，每插件一个独立实例，见 `Ink Canvas/Plugins/PluginHostProxy.cs`。

### 日志

```csharp
host.Log("普通日志");
host.LogError("出错了", ex);
```

**插件日志路由到 `PluginLogs/<plugin-id>/<yyyy-MM-dd>.log`，不混入宿主日志 `PluginLogs/host/`，也不进入主程序日志。** 插件禁止自行写文件，也禁止往宿主日志目录写。

### 服务注册 / 获取（DI）

```csharp
// Initialize 阶段
host.Services.AddSingleton<IMyService>(new MyService());
host.RegisterService<T>(service);            // 兼容旧接口，仅 Initialize 有效

// Initialize 之后
var svc = host.GetService<T>();              // 从 DI 容器取服务
var sdk = host.GetService<ISettingsService>(); // 取宿主提供的服务（见下）
```

### 工具栏注册

```csharp
host.RegisterToolbarItem(new PluginToolbarItemInfo
{
    Id = "btn1",
    DisplayName = "按钮",
    IconGeometry = "M0,0 L24,0 ...",
    ViewFactory = () => new MyView(),
    PopupContentFactory = () => new MyPopup(),   // 点击自动弹 Popup
    CustomSettings = { ... },                     // 见 PluginToolbarSettingInfo
});
host.RegisterBoardToolbarItem(...);   // 白板工具栏，行为同浮动工具栏
```

`PluginToolbarSettingInfo` 支持三类型（`ComboBox` / `Slider` / `Toggle`）：

```csharp
new PluginToolbarSettingInfo
{
    Key = "size",
    DisplayName = "大小",
    Type = PluginToolbarSettingType.Slider,
    MinValue = 1,        // Slider 最小值，默认 0
    MaxValue = 100,      // Slider 最大值，默认 100
    StepSize = 1,        // 步长，设置后吸附，默认 1
    DefaultValue = "10",
}
```

ComboBox 可用 `Options`（显示文本）+ `OptionValues`（保存值）分离：两者数量一致时 Options 当显示文本、OptionValues 当保存值；否则 Options 兼当两者。

### IPC（IPluginIpcBus）

宿主与插件（或插件间）通过 IPC 总线通信，JSON 透明传输，`IpcMessage` 结构见 `IPluginHost.cs`：

```csharp
host.RegisterIpcHandler("myMethod", args => { /* 返回 object，null 亦可 */ });
await host.Ipc.InvokeAsync("otherMethod", args, TimeSpan.FromSeconds(5));
host.Ipc.MessageReceived += (s, msg) => { ... };
```

`RegisterIpcHandler` 返回前确保未注册相同 `method`（重复注册会抛错）。IPC 只在 Initialize 之后可用。

### 安装前安全评估

```csharp
SecurityVerdict verdict = host.EvaluateTrust(packagePath, expectedSha256, declaredPluginId);
// TrustLevel: Unknown / Known / Trusted；Permissions / Reasons 用于安装前提示
```

插件包安装校验 SHA256，从非官方镜像下载的包安全等级低，宿主安装前会向用户提示。

## 宿主提供的服务（经 DI 获取）

约 38 个 `I*Service` 接口，按能力分组。插件通过 `host.GetService<T>()` 获取。

### 画布 / 墨迹

| 接口 | 能力 |
|------|------|
| `ICanvasInkService` | 画布墨迹：切笔/写墨迹/清空（`PluginInkTool`：Pen/Highlighter/Eraser/Select 等） |
| `ICanvasElementService` | 画布元素：插入/移除任意 WPF 控件（拖动/缩放/旋转/撤销历史/冻结页保护） |
| `IRecognitionService` | 墨迹识别（`PluginRecognitionEngine`，中文/英文/数学 等） |
| `IInkEffectService` | 墨迹特效（笔迹发光等） |
| `ICanvasCompositionService` | 画布合成：导出 PNG / 插入图片 / 粘贴剪贴板图 |

### 通知 / 消息

| 接口 | 能力 |
|------|------|
| `INotificationService` | 应用内通知（`NotificationLevel`：Info/Warning/Error，可带点击回调 `Action`） |
| `IAnnouncementService` | 公告中心（映射 `AnnouncementCenterItem`） |
| `IQuoteService` | 名言（白板一言）轮换 |

### 系统 / 环境

| 接口 | 能力 |
|------|------|
| `IAppInfoService` | 应用信息（版本、路径） |
| `ISystemInfoService` | 系统信息（OS、CPU、内存） |
| `IScreenInfoService` | 屏幕信息（分辨率、DPI、工作区） |
| `IThemeService` | 主题检测（`PluginTheme`：Light/Dark） |
| `ITrayService` | 托盘图标控制 |
| `IWindowService` | 窗口控制（全屏切换，`FullScreenHelper`） |
| `IWindowOverviewService` | 窗口总览 |
| `IHotkeyService` | 全局热键注册 |
| `IAppRestartService` | 重启应用（带重启模式） |

### PPT / 演示

| 接口 | 能力 |
|------|------|
| `IPowerPointService` | PPT 控制（`IPPTLinkManager`，含 `GetPresentationPath`） |
| `IPresentationSourceService` | 演示源（`PresentationSourceDescriptor`，`PresentationNavigation`：上一页/下一页/跳转） |

### 数据 / 文件 / 其它

| 接口 | 能力 |
|------|------|
| `ISettingsService` | 读取/写入设置（Settings.json 的 JsonKey 结构） |
| `IConfigProfileService` | 配置方案（列表/切换） |
| `IBackupService` | 自动备份 |
| `IFileAssociationService` | 文件关联（HKCU 泛化注册，受保护扩展名黑名单） |
| `IFileDialogService` | 文件对话框（**无 Owner 属性**，对话框默认以主窗口为宿主） |
| `IClipboardService` | 剪贴板读写（`PasteClipboardImageAsync` 返回 `Task`） |
| `IScreenshotService` | 截图 |
| `ICameraService` | 摄像头 |
| `INameRosterService` | 花名册 |
| `IUpdateService` | 应用更新（`PluginUpdateChannel`） |
| `IEventService` | 宿主事件订阅（`TopMostChanged`/`AppExiting`/`CanvasInkChanged` 等） |
| `IPluginCanvasGestureHandler` | 画布手势处理（作为接口由宿主识别并回调） |

## 深链接：icc://plugin/...

插件可注册 URI 处理器，宿主把 `icc://plugin/<pluginId>/<subPath>?<query>` 派发给对应插件。

### 注册（`IPluginUriService`）

```csharp
// 必须在 Initialize 阶段注册（与 RegisterService<T> 约束一致）
var uri = host.GetService<IPluginUriService>();

// 最长前缀匹配，忽略大小写；空字符串 = 接收该插件全部子路径
uri.RegisterHandler("open", req => {
    // req.PluginId / req.Path / req.Query（键忽略大小写，值已 URL 解码）/ req.RawUri
    return true;   // true=已处理；false=宿主记「未处理」日志
});

// 主动打开深链接（受宿主设置「启用 URI 协议」控制，关闭时不生效）
uri.OpenUri("icc://settings/CanvasPage?key=inkFadeSpeedMultiplier");
```

### 宿主路由（`MainWindow_cs/MW_UriHandler.cs`）

- 开关：`Settings.Advanced.IsEnableUriScheme`（默认关闭），关闭时直接拒绝并写 Warning
- `icc://plugin/<pluginId>/<subPath>` → `HandlePluginUriNavigation` → `PluginManager.Instance.TryDispatchUri(pluginId, subPath, uri)`
- 未处理（插件未注册/未加载/处理器返回 false）仅写日志，不弹通知
- 处理器与 `OpenUri` 均在 **UI 线程**执行，可安全操作画布/窗口

## 插件目录结构（宿主侧）

| 目录 | 用途 |
|------|------|
| `Plugins/` | 已安装插件 |
| `PluginPackages/` | 插件安装包缓存 |
| `PluginConfigs/` | 插件配置 |
| `PluginMarketCache/` | 插件市场缓存 |
| `PluginLogs/` | 日志：`<plugin-id>/` 每插件独立，`host/` 宿主 |

以上目录全部走 `App.RootPath` 解析（单文件发布下不改用 BaseDirectory/ApplicationBase）。

## 规范要点

1. **命名空间固定 `Ink_Canvas.Plugins`**，不得另起
2. 所有注册动作在 `Initialize` 内完成；`Shutdown` 释放资源
3. 插件日志只走 `host.Log/LogError`，禁止自行写文件
4. 新增服务接口 = 对外契约变更，走 NuGet 发版，同步更新插件市场
5. UI 文案做 i18n（默认/en-US/zh-ME），插件内同理
6. 修改 `InkCanvas.PluginSdk` 后构建校验：主解决方案 Debug x64 + PowerPointAddIn 双构建
