# 笔工具稳定性修复

荧光笔的 WinRT 样式此前用 `IsHighlighter || penType == 1` 强制启用高亮混合，覆盖了“重叠加深”开关。现在由 `PenToolStylePolicy` 同时确定宽高、矩形笔尖及混合方式：开启重叠加深时普通透明混合，关闭时使用高亮混合。宽度滑块保存后显式同步 WPF 默认属性和原生样式，避免属性事件读到尚未更新的设置。

WinRT 在原生线程成功应用样式后保存配置，落笔时捕获该配置，按指针跟踪到转干结束。每条笔画保留自己的激光标记、样式和书写时长；快速切换画笔或延迟转干不会把激光笔变成永久墨迹，也不会把此前的普通笔按新选择的激光笔处理。暂停拉直清理不再清掉尚未转干的样式队列。

渐隐管理器增加了不依赖动画完成回调的单调计时清理。清空和移除在 UI 线程直接处理已记录的视觉，不再异步枚举已清空的字典。延后的视觉挂载会核对当前注册，防止清空后重现；同一笔画重复注册先移除旧视觉。分段渐隐容器也一并清理，渐隐过程不会改变当前编辑工具。

激光笔的加入和自然移除均不进入永久墨迹的撤销历史，避免撤销恢复出没有渐隐计时器的旧激光笔迹。

验证：

```powershell
dotnet run --project build/PenToolTests/PenToolTests.csproj -c Release
dotnet run --project build/ReliabilityTests/ReliabilityTests.csproj -c Release
dotnet build "Ink Canvas.sln" -c Debug -p:Platform=x64
```

专项检查实际创建 Windows Ink 绘制属性，比较 3 档宽度、100/150/200% 缩放及两种重叠模式的湿墨和干墨尺寸；使用真实 WPF 调度器验证快速切换、清空、重新注册、未显示窗口上的计时过期和历史过滤。测试不打开生产窗口。
