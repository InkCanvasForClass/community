#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
iNKORE.UI.WPF.Modern 本地 MCP 服务器
基于 Gallery 源码验证的代码片段查询服务

数据来源: C:\\Users\\PrefacedCorg\\Documents\\GitHub\\UI.WPF.Modern (本地源码)

可用工具:
  - list_controls(): 列出所有可用控件
  - get_control_snippets(control_name): 获取指定控件的代码片段
  - search_snippets(keyword): 按关键词搜索代码片段
  - get_setup_guide(): 获取安装配置指南
  - get_namespaces(): 获取 XAML 命名空间声明

TRAE 配置方法:
  在 TRAE 设置 -> MCP 服务器 中添加:
    - 名称: inkore-ui-wpf-modern
    - 类型: stdio
    - 命令: python
    - 参数: C:\\Users\\PrefacedCorg\\Documents\\GitHub\\community\\inkore-mcp\\server.py

  或使用 mcp_config.json 中的配置。
"""

import json
import os
from pathlib import Path
from mcp.server.fastmcp import FastMCP

# 加载数据文件
DATA_FILE = Path(__file__).parent / "snippets.json"
with open(DATA_FILE, "r", encoding="utf-8") as f:
    DATA = json.load(f)

# 创建 MCP 服务器
mcp = FastMCP(
    "inkore-ui-wpf-modern",
    instructions="iNKORE.UI.WPF.Modern 本地文档服务器。提供从 Gallery 源码验证的控件使用代码片段。"
)


@mcp.tool()
def list_controls() -> str:
    """列出所有可用的控件及其描述。

    返回所有控件的名称、命名空间和简短描述。
    使用 get_control_snippets 获取具体控件的代码片段。
    """
    controls = DATA.get("controls", {})
    lines = []
    for name, info in controls.items():
        ns = info.get("namespace", "")
        desc = info.get("description", "")
        snippet_count = len(info.get("snippets", []))
        lines.append(f"- {name} [{ns}] ({snippet_count} snippets): {desc}")
    return "\n".join(lines)


@mcp.tool()
def get_control_snippets(control_name: str) -> str:
    """获取指定控件的代码片段。

    参数:
        control_name: 控件名称 (如 Button, TextBox, TabControl, SettingsCard 等。
                      先调用 list_controls 查看所有可用控件。

    返回该控件的所有已验证代码片段，包括标题、XAML 代码和来源文件。
    """
    controls = DATA.get("controls", {})
    key = control_name.strip()

    # 精确匹配
    if key in controls:
        return _format_control(key, controls[key])

    # 大小写不敏感匹配
    for name in controls:
        if name.lower() == key.lower():
            return _format_control(name, controls[name])

    # 模糊匹配
    matches = [n for n in controls if key.lower() in n.lower()]
    if matches:
        if len(matches) == 1:
            return _format_control(matches[0], controls[matches[0]])
        return f"找到多个匹配的控件:\n" + "\n".join(f"- {m}" for m in matches) + "\n\n请指定确切的控件名称。"

    return f"未找到控件 '{control_name}'。请调用 list_controls 查看所有可用控件。"


@mcp.tool()
def search_snippets(keyword: str) -> str:
    """按关键词搜索代码片段。

    参数:
        keyword: 搜索关键词 (如 "spacing", "icon", "binding", "theme" 等

    在所有控件的代码片段中搜索匹配的内容，返回包含该关键词的片段。
    """
    keyword_lower = keyword.lower()
    controls = DATA.get("controls", {})
    results = []

    for ctrl_name, ctrl_info in controls.items():
        for snippet in ctrl_info.get("snippets", []):
            title = snippet.get("title", "")
            code = snippet.get("code", "")
            if keyword_lower in code.lower() or keyword_lower in title.lower() or keyword_lower in ctrl_name.lower():
                results.append({
                    "control": ctrl_name,
                    "title": title,
                    "code": code,
                    "source": snippet.get("source", ""),
                    "note": snippet.get("note", "")
                })

    if not results:
        return f"未找到包含 '{keyword}' 的代码片段。"

    lines = [f"找到 {len(results)} 个匹配的代码片段:\n"]
    for r in results:
        lines.append(f"## {r['control']} - {r['title']}")
        lines.append(f"来源: {r['source']}")
        if r["note"]:
            lines.append(f"注意: {r['note']}")
        lines.append(f"```xml\n{r['code']}\n```")
        lines.append("")
    return "\n".join(lines)


@mcp.tool()
def get_setup_guide() -> str:
    """获取 iNKORE.UI.WPF.Modern 的安装和配置指南。

    返回 NuGet 安装命令、App.xaml 配置、命名空间声明和窗口样式设置。
    """
    setup = DATA.get("setup", {})
    ns = DATA.get("namespaces", {})

    lines = [
        "# iNKORE.UI.WPF.Modern 安装配置指南",
        "",
        "## 1. NuGet 安装",
        "```",
        setup.get("nuget_install", ""),
        "```",
        "",
        "## 2. 命名空间声明",
        "在 XAML 文件中添加以下命名空间:",
        "",
        ns.get("ui", ""),
        ns.get("ikw", ""),
        "",
        "## 3. App.xaml 配置 (最小配置)",
        "```xml",
        setup.get("app_xaml_minimal", ""),
        "```",
        "",
        "## 4. App.xaml 配置 (完整配置，含主题字典)",
        "```xml",
        setup.get("app_xaml_full", ""),
        "```",
        "",
        "## 5. 现代窗口样式",
        "在 Window 标签上添加以下附加属性:",
        setup.get("window_modern_style", ""),
        "",
        "## 6. 完整窗口示例",
        "```xml",
        '<Window x:Class="MyApp.MainWindow"',
        '    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
        '    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"',
        '    xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"',
        '    xmlns:ikw="http://schemas.inkore.net/lib/ui/wpf"',
        '    ui:WindowHelper.UseModernWindowStyle="True"',
        '    ui:WindowHelper.SystemBackdropType="Mica"',
        '    Title="My App" Height="450" Width="800">',
        '    <ikw:SimpleStackPanel Spacing="10" Margin="20">',
        '        <TextBlock Text="Hello Fluent!" FontSize="24"/>',
        '        <Button Content="Click me"/>',
        '    </ikw:SimpleStackPanel>',
        '</Window>',
        "```",
    ]
    return "\n".join(lines)


@mcp.tool()
def get_namespaces() -> str:
    """获取所有可用的 XAML 命名空间声明。

    返回 ui 和 ikw 命名空间的完整声明字符串。
    """
    ns = DATA.get("namespaces", {})
    lines = [
        "# XAML 命名空间声明",
        "",
        f"ui (Modern 控件): {ns.get('ui', '')}",
        f"ikw (基础控件): {ns.get('ikw', '')}",
        "",
        "说明:",
        "- ui: 前缀用于 iNKORE.UI.WPF.Modern 控件 (如 ui:NavigationView, ui:ToggleSwitch, ui:ProgressBar 等)",
        "- ikw: 前缀用于 iNKORE.UI.WPF 基础控件 (如 ikw:SimpleStackPanel)",
        "- 原生 WPF 控件 (Button, TextBox, CheckBox 等) 无需前缀，会自动应用 Modern 样式",
    ]
    return "\n".join(lines)


def _format_control(name: str, info: dict) -> str:
    """格式化控件信息为可读文本。"""
    lines = [
        f"# {name}",
        f"命名空间: {info.get('namespace', '')}",
        f"描述: {info.get('description', '')}",
    ]

    props = info.get("properties", {})
    if props:
        lines.append("\n## 属性")
        for prop, desc in props.items():
            lines.append(f"- {prop}: {desc}")

    snippets = info.get("snippets", [])
    lines.append(f"\n## 代码片段 ({len(snippets)} 个)")
    for i, snippet in enumerate(snippets, 1):
        lines.append(f"\n### {i}. {snippet.get('title', '')}")
        lines.append(f"来源: {snippet.get('source', '')}")
        note = snippet.get("note", "")
        if note:
            lines.append(f"注意: {note}")
        lines.append(f"```xml\n{snippet.get('code', '')}\n```")

    return "\n".join(lines)


if __name__ == "__main__":
    mcp.run(transport="stdio")
