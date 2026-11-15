# 插件摄像头提供者（Camera Provider）

> 适用宿主：ICCCE ≥ 1.8.0.11（`HostApiRequirement.CurrentApiVersion` ≥ 1.14.0）
> 相关接口：`ICameraProviderService`、`IPluginCameraProvider`、`IPluginCameraFrameSink`、`PluginCameraDescriptor`、`PluginCameraCapability`

## 1. 这个能力解决什么问题

宿主的「视频展台」摄像头下拉框原本只有一条数据来源：DirectShow
`FilterCategory.VideoInputDevice` 枚举。任何非 DirectShow 的画面来源（网络摄像头、
无线视频展台、RTSP/HTTP 流、视频采集卡自定义 SDK、屏幕采集……）都无法进入这个下拉框——
除非在系统层注册一个虚拟 DirectShow 摄像头（原生 COM + 注册表），代价是对全系统可见、
需要原生代码、还要处理 VMR9/D3D 渲染协商。

**摄像头提供者（camera provider）** 让插件把自己采集的帧直接交给宿主：

```
插件采集（RTSP/H.264/...）
        │  byte[] BGR24 帧
        ▼
IPluginCameraFrameSink.Submit()
        │
        ▼
宿主 CameraProviderRegistry（最近一帧缓存）
        │
        ├── 视频展台全屏预览：WPF Image（与 VideoCaptureElement 共享缩放/旋转/镜像变换）
        └── 拍照：取最近一帧 → 现有「插入画布 / 照片栏」逻辑
```

插件不需要写任何 DirectShow/COM 代码，也不需要管理员权限或系统级注册；摄像头只对宿主可见，
随插件安装/卸载。

## 2. 最小接入（3 步）

### 2.1 实现 `IPluginCameraProvider`

```csharp
using Ink_Canvas.Plugins;

public sealed class MyCameraProvider : IPluginCameraProvider
{
    // 建议直接复用插件 manifest 的 Id：宿主卸载插件时会把该 Id 作为清理依据
    public string ProviderId => "com.example.boothcam";

    private IPluginCameraFrameSink _sink;
    private MyCaptureSession _session;

    public IReadOnlyList<PluginCameraDescriptor> GetCameras() => new[]
    {
        new PluginCameraDescriptor
        {
            Id = "my-booth",
            // 建议带可辨识后缀，与普通 USB 摄像头区分；该名称也会写入"上次选中的摄像头"
            DisplayName = "无线视频展台（插件）",
            Capabilities = new List<PluginCameraCapability>
            {
                new PluginCameraCapability { Width = 1920, Height = 1080, FrameRate = 30 },
                new PluginCameraCapability { Width = 1280, Height = 720,  FrameRate = 30 },
            },
            // 当前宿主版本只支持 DirectShow 摄像头的属性调节，保持 false → 展台属性滑块自动置灰
            SupportsPropertyControl = false,
        },
    };

    public bool StartCamera(string cameraId, IPluginCameraFrameSink sink)
    {
        if (cameraId != "my-booth") return false;
        _sink = sink;
        // 启动采集；网络连接放到后台线程，先返回 true（宿主显示"正在启动摄像头…"直到首帧到达）
        _ = Task.Run(async () =>
        {
            _session = await MyCaptureSession.StartAsync();
            _session.FrameReady += OnFrame;   // 采集内核回调（后台线程）
            await _session.ConnectAndStreamAsync();
        });
        return true;
    }

    public void StopCamera(string cameraId)
    {
        _sink = null;
        var session = _session;
        _session = null;
        session?.Stop();          // 尽快返回，不要在这里长时间阻塞
    }

    public bool SetCameraFormat(string cameraId, int width, int height, int frameRate)
    {
        // 能即时切换就返回 true；不支持就返回 false（宿主会重建推流，并按实际帧尺寸渲染）
        return _session?.RequestFormat(width, height, frameRate) ?? false;
    }

    private void OnFrame(MyFrame frame)
    {
        var sink = _sink;
        if (sink == null) return;
        // frame.Buffer 为 BGR24、top-down，stride = (w*3+3)&~3
        if (!sink.Submit(frame.Buffer, frame.Width, frame.Height, frame.Stride, frame.TimestampMs))
        {
            // false = 宿主已停止（切换摄像头/退出展台/卸载插件），插件可以停采集
        }
    }
}
```

### 2.2 在 `Initialize` 中注册

```csharp
[PluginEntrance]
public sealed class MyCameraPlugin : PluginBase
{
    private ICameraProviderService _cameraProvider;
    private MyCameraProvider _provider;

    public override void Initialize(IPluginHost host)
    {
        base.Initialize(host);

        // 与工具栏/服务注册一致：所有注册动作必须在 Initialize 内完成
        _cameraProvider = host.GetService<ICameraProviderService>();
        _provider = new MyCameraProvider();
        _cameraProvider?.RegisterProvider(_provider);
    }

    public override void Shutdown()
    {
        _cameraProvider?.UnregisterProvider(_provider);   // 宿主卸载插件时也会兜底清理
    }
}
```

### 2.3 打开展台并预选（可选）

```csharp
// 1) 写"上次选中的摄像头"（大小写不敏感匹配 DisplayName）
Host.GetService<ISettingsService>()?
    .Set("Canvas.VideoPresenterLastCameraName", "无线视频展台（插件）");

// 2) 打开内置视频展台（主窗口内联全屏预览）
Host.GetService<IVideoBoothService>()?.Toggle();
```

完成。用户打开「视频展台」→ 摄像头下拉框里就能看到「无线视频展台（插件）」，
选中即可预览、拍照、切分辨率，展台原有的缩放/旋转/镜像/批注/照片栏全部照常工作。

## 3. API 参考

### 3.1 `ICameraProviderService`

通过 `host.GetService<ICameraProviderService>()` 获取（宿主单例）。

| 成员 | 说明 |
|------|------|
| `RegisterProvider(IPluginCameraProvider)` | 注册提供者（必须在 `Initialize` 调用）。重复注册同一 `ProviderId` 会覆盖。 |
| `UnregisterProvider(IPluginCameraProvider)` | 注销：立即停止其推流并从下拉框移除全部摄像头。 |
| `NotifyCamerasChanged(IPluginCameraProvider)` | 摄像头列表变化（增删/改名/能力变化）后调用，宿主刷新下拉框；任意线程可调。 |

### 3.2 `IPluginCameraProvider`（插件实现）

| 成员 | 线程 | 说明 |
|------|------|------|
| `string ProviderId` | 任意 | 唯一标识，建议 = 插件 `manifest.Id`。宿主卸载插件时按它兜底清理。 |
| `IReadOnlyList<PluginCameraDescriptor> GetCameras()` | UI 线程 | 快速返回缓存的摄像头列表；可为空列表（设备未连接）。 |
| `bool StartCamera(string cameraId, IPluginCameraFrameSink sink)` | 后台线程 | 启动采集并保存 `sink`。返回 `false` = 启动失败（宿主提示用户）。允许先返回 `true` 再异步连接。 |
| `void StopCamera(string cameraId)` | 后台线程 | 停止采集、释放资源；返回后不得再调用 `Submit`。 |
| `bool SetCameraFormat(string cameraId, int w, int h, int fps)` | 后台线程 | 分辨率下拉框切换时调用；返回 `false` 表示不支持运行时切换（宿主会重建推流，按实际帧尺寸渲染）。 |

生命周期：**同一时刻宿主最多向一路摄像头推流**。切换摄像头、切换分辨率、退出展台、
卸载插件都会调用 `StopCamera`。

### 3.3 `IPluginCameraFrameSink`（宿主实现，插件调用）

```csharp
bool IsActive { get; }
bool Submit(byte[] bgr24Buffer, int width, int height, int stride, long timestampMs = 0L);
```

- **像素格式固定 BGR24**（字节序 B、G、R），**top-down**（第 0 行 = 图像顶部）。
- `stride >= width * 3`，通常取 `(width * 3 + 3) & ~3`；缓冲区长度必须 ≥ `stride * height`。
- 宿主在 `Submit` 返回前完成拷贝：插件可以立即复用缓冲区。
- 宿主只保留最近一帧（不支持帧队列），推快了旧帧会被丢弃，插件不需要自己节流。
- 返回 `false` = 宿主已停止或参数非法，插件应结束推帧（等价于紧接着收到 `StopCamera`）。

### 3.4 `PluginCameraDescriptor` / `PluginCameraCapability`

- `Id`：摄像头 Id，同一提供者内唯一。
- `DisplayName`：下拉框显示名，建议带「（插件）」之类后缀；也是"上次选中的摄像头"的持久化值。
- `Capabilities`：`Width/Height/FrameRate` 列表，用于填充分辨率下拉框；为空时下拉框显示
  "加载中…"，预览仍按实际推送的帧尺寸渲染。
- `SupportsPropertyControl`：保留字段；当前宿主版本对插件摄像头不做 DirectShow 属性映射，
  保持 `false`（默认）即可，展台属性滑块会自动置灰。

## 4. 宿主侧实现（给维护者）

| 位置 | 改动 |
|------|------|
| `InkCanvas.PluginSdk/ICameraProviderService.cs` | 新增 SDK 接口与 DTO；`HostApiRequirement.CurrentApiVersion` 1.13.0 → 1.14.0 |
| `Ink Canvas/Helpers/CameraProviderRegistry.cs` | 提供者注册表 + `IPluginCameraFrameSink` 实现（双缓冲最近帧）+ 活动推流生命周期 |
| `Ink Canvas/Plugins/Services/CameraProviderService.cs` | SDK 服务的宿主实现（DI 单例） |
| `Ink Canvas/App.xaml.cs` | 注册 `ICameraProviderService` |
| `Ink Canvas/Helpers/DirectShowCameraService.cs` | 下拉框列表合并插件摄像头；`EnumerateResolutionsAsync` 用插件能力；`StartPreviewAsync` 分支转发帧到 `FrameReceived`；属性探测短路 |
| `Ink Canvas/MainWindow.xaml` | 新增 `VideoPresenterProviderImage`（WPF `Image`） |
| `Ink Canvas/MainWindow_cs/MW_VideoPresenter.cs` | 非 DirectShow 渲染路径、拍照取最近帧、生命周期与列表刷新 |
| `Ink Canvas/Plugins/PluginManager.cs` | 插件卸载时按 `ProviderId` 兜底清理 |

渲染细节：

- 全屏预览时 `VideoPresenterProviderImage` 的 `LayoutTransform` / `RenderTransform` 会被**指向
  `VideoCaptureElement` 的同名实例**，因此以下既有逻辑对插件画面同样生效，无需分支：
  - `VideoPresenterFullCanvasMirror`（水平/垂直镜像）
  - `VideoPresenterFullCanvasRotation`（旋转 90°×N）
  - `VideoPresenterFullCanvasScale` / `Translate`（`ApplyBoothPreviewTransform` 的缩放/平移）
  - 墨迹批注、照片预览页的变换复用（照片页会 Collapsed 实时画面，插件画面同样隐藏）
- 帧到达时在插件线程触发事件，UI 线程用合并标志去重后 `WriteableBitmap.WritePixels` 渲染；
  尺寸变化时自动重建 `WriteableBitmap`。
- 拍照：`BtnCapturePhoto_Click` 在插件摄像头激活时取
  `CameraProviderRegistry.GetLatestFrameBitmapSource()`，随后走**现有的**旋转/镜像/纸框矫正/
  插入画布/照片栏逻辑，不依赖 D3D 回读。
- 属性滑块：`ProbeCameraPropertiesAsync` 对插件摄像头直接返回"全部不支持"，滑块置灰。
- 分辨率：`AllResolutionFpsCombos` 由插件能力填充，默认选中与 1920×1080 曼哈顿距离最小的能力项，
  与 DirectShow 路径一致。

## 5. 与两条既有路径的对比

| | DirectShow USB 摄像头 | 虚拟 DirectShow 摄像头（系统级） | **Camera Provider（本功能）** |
|---|---|---|---|
| 进入宿主下拉框 | ✅ 原生 | ✅ 系统层注册 | ✅ 宿主内注册 |
| 预览渲染 | VMR9 + D3DImage | VMR9 + D3DImage | WPF Image + 插件推帧 |
| 拍照 | D3DImage 回读 | D3DImage 回读 | 取最近一帧 |
| 分辨率/旋转/镜像/批注 | ✅ | ✅ | ✅（共享同一套变换） |
| 需要原生 COM / 注册表 | — | ✅ | ❌ |
| 对全系统可见 | — | ✅ | ❌（仅宿主） |
| 与 OBS 等虚拟设备冲突 | — | 可能 | 不会 |
| 需要宿主发版 | — | 否 | **是**（本 PR） |

## 6. 已知限制

1. 只在宿主内可见：相机/会议软件看不到插件摄像头（通常正是想要的效果）。
2. 属性调节（亮度/对比度/…）当前不支持，展台滑块置灰。
3. 宿主只保留最近一帧，不做帧队列/时间戳播放；插件按自己的节奏推即可。
4. 首帧到达前宿主显示"正在启动摄像头…"；插件长时间无帧时用户看到的是占位文字，
   不像 DirectShow 那样有 `MediaFailed` 自动重试链。
5. 插件必须在 `Initialize` 阶段注册；插件热加载后展台下拉框会通过
   `CameraRegistry.CamerasChanged` 刷新，无需重启宿主。
