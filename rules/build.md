# 编译规范

## dotnet 路径

dotnet 不在系统 PATH 中，完整路径为：

```
C:\Program Files\dotnet\dotnet.exe
```

## 编译命令

### 编译主项目

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\PrefacedCorg\Documents\GitHub\community\Ink Canvas\InkCanvasForClass.csproj"
```

### 编译单个子项目

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\PrefacedCorg\Documents\GitHub\community\InkCanvas.Controls\InkCanvas.Controls.csproj"
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\PrefacedCorg\Documents\GitHub\community\InkCanvas.SettingsTreeView\InkCanvas.SettingsTreeView.csproj"
```

### 编译整个解决方案

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build "c:\Users\PrefacedCorg\Documents\GitHub\community\Ink Canvas.sln"
```

## 项目列表

| 项目 | csproj 路径 | 目标框架 |
|------|-------------|----------|
| Ink Canvas (主应用) | `Ink Canvas/InkCanvasForClass.csproj` | net6.0-windows10.0.19041.0 |
| InkCanvas.Controls | `InkCanvas.Controls/InkCanvas.Controls.csproj` | net6.0-windows10.0.19041.0 |
| InkCanvas.PluginSdk | `InkCanvas.PluginSdk/InkCanvas.PluginSdk.csproj` | net6.0-windows10.0.19041.0 |
| InkCanvas.IACoreHelper | `InkCanvas.IACoreHelper/InkCanvas.IACoreHelper.csproj` | net472 |
| InkCanvas.SettingsTreeView | `InkCanvas.SettingsTreeView/InkCanvas.SettingsTreeView.csproj` | net6.0-windows10.0.19041.0 |

## 编译前检查

1. 确保没有 CS0246（缺少 using）错误
2. 确保没有 CS0103（找不到名称）错误
3. 确保没有 CS0102（重复定义）错误
4. 确保所有 resx 资源键在默认 resx、en-US、zh-ME 三个文件中完全一致
5. 确保没有未使用的 resx 资源键

## 常见编译错误修复

| 错误 | 原因 | 修复方法 |
|------|------|----------|
| CS0246 找不到类型 | 缺少 using 指令 | 添加 `using Ink_Canvas.Properties;` 等 |
| CS0103 找不到名称 | 未引用正确命名空间 | 检查是否需要 `using iNKORE.UI.WPF.Modern.Controls;` |
| CS0102 重复定义 | resx Designer.cs 中重复添加属性 | 删除重复的属性声明 |
| XAML 解析错误 | XML 格式错误 | 检查标签闭合、属性引号等 |
