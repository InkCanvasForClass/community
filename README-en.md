<div align="center">

<img src="icc.png" width="128">

# InkCanvasForClass<br/>Community Edition

The final stance of stubbornness based on the `InkCanvas` control...

![GitHub License](https://img.shields.io/github/license/InkCanvasForClass/community)
![GitHub top language](https://img.shields.io/github/languages/top/InkCanvasForClass/community)
[![Using iNKORE.UI.WPF.Modern](https://github.com/iNKORE-NET/UI.WPF.Modern/blob/main/assets/images/badges/UI.WPF.Modern_Main_Shield.svg?raw=true)](https://github.com/iNKORE-NET/UI.WPF.Modern)
![GitHub Repo stars](https://img.shields.io/github/stars/InkCanvasForClass/community)
![GitHub forks](https://img.shields.io/github/forks/InkCanvasForClass/community)
[![All Contributors](https://img.shields.io/github/all-contributors/InkCanvasForClass/community?color=ee8449)](#贡献者)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/InkCanvasForClass/community)

[![Discord](https://img.shields.io/discord/1383039050184917053?label=Discord&logo=discord)](https://discord.gg/ahj7eJWhEG)
[![QQ](https://img.shields.io/badge/-1054377349-white?logo=qq&label=QQ)](https://qm.qq.com/q/qo32AclNh6)
[![STCN](https://img.shields.io/badge/icc--ce-8a2be2?label=%E6%99%BA%E6%95%99%E8%AE%BA%E5%9D%9B&link=https%3A%2F%2Fforum.smart-teach.cn%2Ft%2Ficc-ce)](https://forum.smart-teach.cn/t/icc-ce)

<img src="Images/icc ce.png" width="2048">

</div>

## 💫 Software Disclaimer

By using this version of InkCanvasForClass, you agree to assume all potential issues and risks at your own discretion. It is highly recommended NOT to use Beta versions—which haven't been extensively tested and optimized—in public or formal settings (e.g., open classes, recorded courses, live streams, major conferences). Any consequences, issues, or risks arising from using Beta versions (e.g., getting scolded by your homeroom teacher, penalized by the principal, chaotic scenes caused by software crashes, global sea level rise, etc.) **shall be borne solely by the user**. [CJKmkp](https://github.com/CJKmkp) and all project maintainers provide no warranties or guarantees whatsoever.

♥️ **The copyright of this project belongs to [CJKmkp](https://github.com/CJKmkp). [CJKmkp](https://github.com/CJKmkp) reserves the right of final interpretation.**

**Smart Education Alliance InkCanvasForClass Community Edition Section:** [forum.smart-teach.cn/t/icc-ce](https://forum.smart-teach.cn/t/icc-ce). This is where we post version update logs. You are also welcome to ask questions or share your experience here, provided you comply with the forum management rules and the section's terms of service.

## ⚠️ Important Notice

Before using and distributing this software, please make sure you understand the relevant open-source licenses. This software is modified based on <https://github.com/InkCanvasForClass/icc-20240610-stable>, which in turn is modified based on <https://github.com/ChangSakura/Ink-Canvas>. Meanwhile, ICA is based on <https://github.com/WXRIW/Ink-Canvas> with additional features including, but not limited to, hiding to the sidebar, alongside modified UI and software interaction logic. For feedback regarding ink writing functionality or features unique to ICA, it is recommended to check <https://github.com/WXRIW/Ink-Canvas/issues> first. **Please bring your brain along before using.**

# 💬 Tips & Notes

- For constructive feedback and reasonable suggestions on new features, developers will respond and implement them in due course. This software is neither commercial nor driven by any profit-seeking organization. Please do not rush the developers; patience leads to fewer bugs and a more stable experience.
- This software is for personal use only. Please do not use it for commercial purposes. Updates won't be exceptionally frequent. If you have the capability, please contribute code via Pull Requests instead of throwing an impotent rage in the Issues section.
- Welcome to try out other members of the InkCanvas family, including [Ink Canvas Plus](https://khyan.top/ic+) and [Ink Canvas Artistry](https://github.com/InkCanvas/Ink-Canvas-Artistry). Your word-of-mouth promotion will help more users discover our software.
- **It is strongly recommended to use InkCanvasForClass alongside Microsoft Office 365 PowerPoint for the best performance and compatibility!!!**

## 📗 FAQ

### Why do some icons show up as "□" on systems below Windows 10?

[Click here to download](https://aka.ms/SegoeFonts "SegoeFonts") the SegoeFonts files. Install the `SegMDL2.ttf` font from the zip archive and restart your system to resolve the issue.

### The application crashes immediately upon turning a page in PowerPoint slideshow mode

Please [activate Microsoft Office](https://www.coolhub.top/archives/14).

### The canvas application does not switch to PPT mode after starting the slideshow

1. PowerPoint is running in Protected View (Read-Only). Please exit Protected View by doing the following:
   1. Open PowerPoint and click "File" in the top-left corner.
   2. In the "Info" tab, click the "Enable Editing" button on the right.
2. WPS Office was previously installed, which corrupted the COM components. To fix this, completely uninstall WPS Office and reinstall Microsoft Office Mondo 2016.
3. Ensure both PowerPoint and this application are running with the same permission levels. If PowerPoint runs as Administrator while this app runs as a standard user, it will fail to switch to PPT mode. You can fix this by checking PowerPoint's compatibility settings or running this application with elevated privileges.
4. If none of the above methods work, please refer to this link: [【Click here to redirect】](https://www.inkeys.top/tutorial/ppt-com.html)

### The application fails to launch normally

Please check if `.Net Runtime 6.0` or higher is installed on your computer. If not, please [visit the official website](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) to download and install it.

If it still doesn't run, please [install `Microsoft Office`](https://www.coolhub.top/archives/11).

## ✏️ Contribution Guidelines

**Please note that when contributing code, you _must_ submit all changes to the _net6_ branch to ensure that the net6 version is always ahead of the main branch.**

## Todo LIST

1. Prepare for version 2.0 development
2. CI plugin integration

## Contributors

> [!NOTE]
> This list is maintained and generated via [All Contributors](https://allcontributors.org/).

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="20%"><a href="https://github.com/CJKmkp"><img src="https://avatars.githubusercontent.com/u/113243675?v=4?s=100" width="100px;" alt="CJK_mkp"/><br /><sub><b>CJK_mkp</b></sub></a><br /><a href="#maintenance-CJKmkp" title="Maintenance">🚧</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=CJKmkp" title="Documentation">📖</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=CJKmkp" title="Code">💻</a> <a href="#design-CJKmkp" title="Design">🎨</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/CreeperAWA"><img src="https://avatars.githubusercontent.com/u/134939494?v=4?s=100" width="100px;" alt="CreeperAWA"/><br /><sub><b>CreeperAWA</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=CreeperAWA" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/2-2-3-trimethylpentane"><img src="https://avatars.githubusercontent.com/u/141403762?v=4?s=100" width="100px;" alt="2,2,3-三甲基戊烷"/><br /><sub><b>2,2,3-三甲基戊烷</b></sub></a><br /><a href="#blog-2-2-3-trimethylpentane" title="Blogposts">📝</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=2-2-3-trimethylpentane" title="Documentation">📖</a> <a href="#design-2-2-3-trimethylpentane" title="Design">🎨</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=2-2-3-trimethylpentane" title="Tests">⚠️</a> <a href="#tutorial-2-2-3-trimethylpentane" title="Tutorials">✅</a> <a href="#video-2-2-3-trimethylpentane" title="Videos">📹</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/Alan-CRL"><img src="https://avatars.githubusercontent.com/u/92425617?v=4?s=100" width="100px;" alt="Alan-CRL"/><br /><sub><b>Alan-CRL</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=Alan-CRL" title="Code">💻</a> <a href="#infra-Alan-CRL" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=Alan-CRL" title="Documentation">📖</a> <a href="#financial-Alan-CRL" title="Financial">💵</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/MKStoler1024"><img src="https://avatars.githubusercontent.com/u/158786854?v=4?s=100" width="100px;" alt="MKStoler1024"/><br /><sub><b>MKStoler1024</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=MKStoler1024" title="Documentation">📖</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=MKStoler1024" title="Code">💻</a> <a href="#design-MKStoler1024" title="Design">🎨</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="20%"><a href="https://github.com/awesome-iwb"><img src="https://avatars.githubusercontent.com/u/184760810?v=4?s=100" width="100px;" alt="Awesome Iwb"/><br /><sub><b>Awesome Iwb</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=awesome-iwb" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/PrefacedCorg"><img src="https://avatars.githubusercontent.com/u/129855423?v=4?s=100" width="100px;" alt="PrefacedCorg"/><br /><sub><b>PrefacedCorg</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=PrefacedCorg" title="Code">💻</a> <a href="#design-PrefacedCorg" title="Design">🎨</a></td>
      <td align="center" valign="top" width="20%"><a href="http://blog.jursin.top"><img src="https://avatars.githubusercontent.com/u/127487914?v=4?s=100" width="100px;" alt="Jursin"/><br /><sub><b>Jursin</b></sub></a><br /><a href="#design-Jursin" title="Design">🎨</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/Tayasui-rainnya"><img src="https://avatars.githubusercontent.com/u/156585442?v=4?s=100" width="100px;" alt="tayasui rainnya!"/><br /><sub><b>tayasui rainnya!</b></sub></a><br /><a href="#design-Tayasui-rainnya" title="Design">🎨</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=Tayasui-rainnya" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/doudou0720"><img src="https://avatars.githubusercontent.com/u/98651603?v=4?s=100" width="100px;" alt="doudou0720"/><br /><sub><b>doudou0720</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=doudou0720" title="Code">💻</a> <a href="#blog-doudou0720" title="Blogposts">📝</a> <a href="#infra-doudou0720" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="20%"><a href="https://github.com/PANDAJSR"><img src="https://avatars.githubusercontent.com/u/170189561?v=4?s=100" width="100px;" alt="PANDAJSR"/><br /><sub><b>PANDAJSR</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=PANDAJSR" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="http://lyxwx.top"><img src="https://avatars.githubusercontent.com/u/66517348?v=4?s=100" width="100px;" alt="流焰xwx"/><br /><sub><b>流焰xwx</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=LiuYan-xwx" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/Super-Yyt"><img src="https://avatars.githubusercontent.com/u/206630707?v=4?s=100" width="100px;" alt="Super-Yyt"/><br /><sub><b>Super-Yyt</b></sub></a><br /><a href="#infra-Super-Yyt" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a> <a href="#blog-Super-Yyt" title="Blogposts">📝</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/Hao3288"><img src="https://avatars.githubusercontent.com/u/119276078?v=4?s=100" width="100px;" alt="NoobHao"/><br /><sub><b>NoobHao</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=Hao3288" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="https://github.com/AstrZero"><img src="https://avatars.githubusercontent.com/u/135413163?v=4?s=100" width="100px;" alt="AstrZero"/><br /><sub><b>AstrZero</b></sub></a><br /><a href="#ideas-AstrZero" title="Ideas, Planning, & Feedback">🤔</a> <a href="https://github.com/InkCanvasForClass/community/commits?author=AstrZero" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="20%"><a href="http://lrsgzs.top"><img src="https://avatars.githubusercontent.com/u/99574908?v=4?s=100" width="100px;" alt="lrs2187"/><br /><sub><b>lrs2187</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=lrsgzs" title="Code">💻</a></td>
      <td align="center" valign="top" width="20%"><a href="http://jbyc.cc"><img src="https://avatars.githubusercontent.com/u/177214309?v=4?s=100" width="100px;" alt="Jbyccc"/><br /><sub><b>Jbyccc</b></sub></a><br /><a href="https://github.com/InkCanvasForClass/community/commits?author=Braydenccc" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->


## 🤝 Acknowledgments

Thanks to [yuwenhui2020](https://github.com/yuwenhui2020) for their contributions to the `Ink Canvas User Guide`!  
Thanks to [CN-Ironegg](https://github.com/CN-Ironegg), [jiajiaxd](https://github.com/jiajiaxd), [Kengwang](https://github.com/kengwang), [Raspberry Kan](https://github.com/Raspberry-Monster), [clover-yan](https://github.com/clover-yan), [STBBRD](https://github.com/STBBRD), and [ChangSakura](https://github.com/WuChanging) for contributing code to this project!

## License

GPLv3

## References

[Alan-CRL/DesktopDrawpadBlocker](https://github.com/Alan-CRL/DesktopDrawpadBlocker)  
[Alan-CRL/Inkeys](https://github.com/Alan-CRL/Inkeys)

## Star History

<a href="https://www.star-history.com/?repos=InkCanvasForClass%2Fcommunity&type=timeline&logscale=&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=InkCanvasForClass/community&type=timeline&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=InkCanvasForClass/community&type=timeline&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=InkCanvasForClass/community&type=timeline&legend=top-left" />
 </picture>
</a>