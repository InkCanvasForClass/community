<div align="center">

<img src="icc.png" width="128">

# InkCanvasForClass<br/>Community Edition

最后一次基于 `InkCanvas` 控件的倔强...

![GitHub License](https://img.shields.io/github/license/InkCanvasForClass/community)
![GitHub top language](https://img.shields.io/github/languages/top/InkCanvasForClass/community)
![GitHub Repo stars](https://img.shields.io/github/stars/InkCanvasForClass/community)
![GitHub forks](https://img.shields.io/github/forks/InkCanvasForClass/community)
[![All Contributors](https://img.shields.io/github/all-contributors/InkCanvasForClass/community?color=ee8449)](#贡献者)

[![Discord](https://img.shields.io/discord/1383039050184917053?label=Discord&logo=discord)](https://discord.gg/ahj7eJWhEG)
[![QQ](https://img.shields.io/badge/-1054377349-white?logo=qq&label=QQ)](https://qm.qq.com/q/qo32AclNh6)
[![STCN](https://img.shields.io/badge/icc--ce-8a2be2?label=%E6%99%BA%E6%95%99%E8%AE%BA%E5%9D%9B&link=https%3A%2F%2Fforum.smart-teach.cn%2Ft%2Ficc-ce)](https://forum.smart-teach.cn/t/icc-ce)

<img src="Images/icc ce.png" width="2048">

</div>

## 💫 软件说明

使用该版本 InkCanvasForClass，意味着您同意自行承担任何可能存在的问题与风险。建议不要在公众场合（例如公开课，录播课，线上直播课，大型会议）使用未经广泛测试和优化的 Beta 版本，对使用 Beta 版本而带来的任何问题和风险（例如：被班主任批斗，被校长处罚，崩溃而导致的场面混乱，全球海平面上升等），**将由使用者自行承担**，[CJKmkp](https://github.com/CJKmkp) 及其项目的所有维护者不提供任何担保。

♥️ **本项目版权归 [CJKmkp](https://github.com/CJKmkp) 所有。[CJKmkp](https://github.com/CJKmkp) 拥有最终解释权。**

**智教联盟 InkCanvasForClass Community Edition 板块：** [forum.smart-teach.cn/t/icc-ce](https://forum.smart-teach.cn/t/icc-ce) ，我们会在此处发布版本更新日志，同时，您也可以在遵守论坛对应管理规则与InkCanvasForClass Community Edition 板块管理条约的情况下，在该板块内提问或发表自己的使用体验。

## ⚠️ 使用须知

在使用和分发本软件前，请务必了解相关开源协议。本软件基于 <https://github.com/InkCanvasForClass/icc-20240610-stable> 修改而来，而 icc-20240610-stable 基于 <https://github.com/ChangSakura/Ink-Canvas> 修改，ica 则基于 <https://github.com/WXRIW/Ink-Canvas> 修改，增加了包括但不限于隐藏到侧边栏等功能，更改了相关UI和软件操作逻辑。对于墨迹书写功能以及 ica 独有功能的相关问题反馈，建议优先查阅 <https://github.com/WXRIW/Ink-Canvas/issues> 。**使用前建议戴上大脑使用。**

# 💬 提示
- 对于新功能的有效意见和合理建议，开发者会适时回复并进行开发。本软件并非商业性质软件或由营利性机构驱动，请不要催促开发者，耐心等待能让功能少些Bug，更加稳定。
- 此软件仅用于个人使用，请勿商用。更新速度不会很快，如果有能力请通过PR贡献代码，而不是在 Issue 里无能狂怒。
- 欢迎尝试 InkCanvas 家族的其他成员，包括 [Ink Canvas Plus](https://khyan.top/ic+) 和 [Ink Canvas Artistry](https://github.com/InkCanvas/Ink-Canvas-Artistry) 。您的大力宣传能让更多用户发现我们的软件。
- **强烈建议使用 Microsoft Office 365 的 PowerPoint 搭配 InkCanvasForClass 使用，效果更好！！！**

## 📗 FAQ
### 在 Windows 10 以下版本系统中，部分图标显示为 “□” 怎么办？
[点击下载](https://aka.ms/SegoeFonts "SegoeFonts") SegoeFonts 文件，安装压缩包中 `SegMDL2.ttf` 字体后重启即可解决。

### 点击放映后一翻页就闪退
请[激活 Microsoft Office](https://www.coolhub.top/archives/14)。

### 放映后画板程序不会切换到 PPT 模式
1. PowerPoint 处在保护模式下（只读），请退出保护模式，方法如下：
   1. 打开 PowerPoint，点击左上角的“文件”选项；
   2. 在“信息”标签内，点击右侧的“启用编辑”按钮。
2. 曾经安装过 WPS Office 办公软件，导致 COM 组件被破坏，解决方法为完全卸载 WPS Office 后重新安装 Microsoft Office Mondo 2016 即可解决。
3. 请确保 PowerPoint 和本应用运行在同一权限下，如果 PowerPoint 以管理员身份运行而本应用以普通用户身份运行，也会出现无法切换到 PPT 模式的现象，您可以通过检查 PowerPoint 的兼容性设置或提权本应用运行来解决该问题。
4. 如果上述方法不能解决你的问题，请参考这个链接[【点击此处以跳转】](https://www.inkeys.top/tutorial/ppt-com.html)

### 程序无法正常启动
请检查你的电脑上是否安装了 `.Net Framework 4.7.2` 或更高版本。若没有，请[前往官网](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/thank-you/net472-offline-installer "下载 .Net Framework 4.7.2")下载安装。

如果仍无法运行，请[安装 `Microsoft Office`](https://www.coolhub.top/archives/11)。

## ✏️ 贡献指南

**请注意，在贡献代码时，_务必_ 将所有代码提交到 _beta_ 分支，以保证beta版本总是新于main版本。**

## TODO LIST
1. 预备2.0版本开发
2. Ci联动插件

## 贡献者
> [!NOTE]
> 此列表通过[All Contributers](https://allcontributors.org/)实现。
<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/CJKmkp"><img src="https://avatars.githubusercontent.com/u/113243675?v=4?s=100" width="100px;" alt="CJK_mkp"/><br /><sub><b>CJK_mkp</b></sub></a><br /><a href="#maintenance-CJKmkp" title="Maintenance">🚧</a> <a href="#doc-CJKmkp" title="Documentation">📖</a> <a href="#code-CJKmkp" title="Code">💻</a> <a href="#design-CJKmkp" title="Design">🎨</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/CreeperAWA"><img src="https://avatars.githubusercontent.com/u/134939494?v=4?s=100" width="100px;" alt="CreeperAWA"/><br /><sub><b>CreeperAWA</b></sub></a><br /><a href="#code-CreeperAWA" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/2-2-3-trimethylpentane"><img src="https://avatars.githubusercontent.com/u/141403762?v=4?s=100" width="100px;" alt="2,2,3-三甲基戊烷"/><br /><sub><b>2,2,3-三甲基戊烷</b></sub></a><br /><a href="#blog-2-2-3-trimethylpentane" title="Blogposts">📝</a> <a href="#doc-2-2-3-trimethylpentane" title="Documentation">📖</a> <a href="#design-2-2-3-trimethylpentane" title="Design">🎨</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Alan-CRL"><img src="https://avatars.githubusercontent.com/u/92425617?v=4?s=100" width="100px;" alt="Alan-CRL"/><br /><sub><b>Alan-CRL</b></sub></a><br /><a href="#code-Alan-CRL" title="Code">💻</a> <a href="#infra-Alan-CRL" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a> <a href="#doc-Alan-CRL" title="Documentation">📖</a> <a href="#financial-Alan-CRL" title="Financial">💵</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/MKStoler1024"><img src="https://avatars.githubusercontent.com/u/158786854?v=4?s=100" width="100px;" alt="MKStoler1024"/><br /><sub><b>MKStoler1024</b></sub></a><br /><a href="#doc-MKStoler1024" title="Documentation">📖</a> <a href="#code-MKStoler1024" title="Code">💻</a> <a href="#design-MKStoler1024" title="Design">🎨</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/awesome-iwb"><img src="https://avatars.githubusercontent.com/u/184760810?v=4?s=100" width="100px;" alt="Awesome Iwb"/><br /><sub><b>Awesome Iwb</b></sub></a><br /><a href="#doc-awesome-iwb" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/PrefacedCorg"><img src="https://avatars.githubusercontent.com/u/129855423?v=4?s=100" width="100px;" alt="PrefacedCorg"/><br /><sub><b>PrefacedCorg</b></sub></a><br /><a href="#code-PrefacedCorg" title="Code">💻</a> <a href="#design-PrefacedCorg" title="Design">🎨</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="http://blog.jursin.top"><img src="https://avatars.githubusercontent.com/u/127487914?v=4?s=100" width="100px;" alt="Jursin"/><br /><sub><b>Jursin</b></sub></a><br /><a href="#design-Jursin" title="Design">🎨</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Tayasui-rainnya"><img src="https://avatars.githubusercontent.com/u/156585442?v=4?s=100" width="100px;" alt="tayasui rainnya!"/><br /><sub><b>tayasui rainnya!</b></sub></a><br /><a href="#design-Tayasui-rainnya" title="Design">🎨</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/doudou0720"><img src="https://avatars.githubusercontent.com/u/98651603?v=4?s=100" width="100px;" alt="doudou0720"/><br /><sub><b>doudou0720</b></sub></a><br /><a href="#code-doudou0720" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

## 🤝 感谢
 
感谢 [yuwenhui2020](https://github.com/yuwenhui2020) 为 `Ink Canvas 使用说明` 做出的贡献！  
感谢 [CN-Ironegg](https://github.com/CN-Ironegg)、[jiajiaxd](https://github.com/jiajiaxd)、[Kengwang](https://github.com/kengwang)、[Raspberry Kan](https://github.com/Raspberry-Monster)、[clover-yan](https://github.com/clover-yan)、[STBBRD](https://github.com/STBBRD)、[ChangSakura](https://github.com/WuChanging) 为本项目贡献代码！  

## License
GPLv3

## 项目引用
[Alan-CRL/DesktopDrawpadBlocker](https://github.com/Alan-CRL/DesktopDrawpadBlocker)  
