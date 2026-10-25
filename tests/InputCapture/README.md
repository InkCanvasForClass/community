# 橡皮层清理的输入捕获回归

在仓库根目录运行：

```powershell
dotnet run --project tests/InputCapture/InputCapture.csproj
```

测试会短暂显示两个实际 WPF 窗口，需要 Windows 桌面会话，直接编译生产代码 `Ink Canvas/Helpers/InputCaptureHelper.cs`。

`DisableEraserOverlay()` 原先在橡皮层未持有手写笔捕获时也执行 `ReleaseStylusCapture()`。实际 WPF 环境中，该调用会清除同一 UI 线程上另一窗口的鼠标捕获。若发生在按钮按下与松开之间，按钮不会触发 Click；ContextMenu 的捕获也会受到影响。外部放映每 50 毫秒重复清理非活动橡皮层时暴露了该问题，但缺陷位于宿主的通用清理逻辑。

回归检查先复现旧行为，再验证重复清理已折叠的橡皮层不会释放另一窗口 Button 或 ContextMenu 的捕获，并验证橡皮层自己的鼠标捕获仍会释放。另使用 WPF `TouchDevice` 测试替身验证自身触摸捕获会释放、另一窗口的触摸及鼠标捕获会保留，包括自身触摸和另一窗口鼠标同时捕获的情况。

测试未注入真实鼠标按键，也不模拟手写笔硬件或加载 EN 课件，不能替代实机验收。建议在放映中检查按钮按住超过 50 毫秒后松开、账号菜单、窗口拖动，以及工具切换和结束放映后的鼠标、手写笔与触摸输入。
