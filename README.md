<div align="center">

<img src="icc.png" width="128">

# ICC CE

Elegant by Default. Based on `dubi906w/icc-0610fix` .

**致力于更稳定、持续更新的ICC**

[![UPSTREAM](https://img.shields.io/badge/UpStream-ChangSakura%2FInk--Canvas-red.svg "LICENSE")](https://github.com/Awesome-Iwb/icc-0610fix)
![LICENSE](https://img.shields.io/badge/License-GPL--3.0-red.svg "LICENSE")

![Screenshot-1](./Images/icc1.png)
![Screenshot-2](./Images/icc2.png)

</div>

## 公告
ICC CE创立的初衷是因为作者使用ICC的过程中一些小BUG，于是作者想尝试在icc-0610fix版本的基础上修改。
说白了，我比较懒README懒得改了，就直接用之前ICC的了
高中生，更新不定期/doge

### 项目特点
由初学者结合AI技术实现代码(说白了就是不会)

### TO DO LIST


### 前言
在使用和分发本软件前，请务必了解相关开源协议。本软件基于 https://github.com/Awesome-Iwb/icc-0610fix 修改而来，而ICC基于 https://github.com/ChangSakura/Ink-Canvas 修改，ICA则基于 https://github.com/WXRIW/Ink-Canvas 修改，增加了包括但不限于隐藏到侧边栏等功能，更改了相关UI和软件操作逻辑。对于墨迹书写功能以及ICA独有功能的相关问题反馈，建议优先查阅 https://github.com/WXRIW/Ink-Canvas/issues 。

### 特性
1. 支持主动式触控笔（支持压感）
2. 工具栏显示每个功能的文字描述
3. 增加了调色盘的颜色
4. 增加了荧光笔支持

### 提示
- 对于新功能的有效意见和合理建议，开发者会适时回复并进行开发。本软件并非商业软件，请不要催促开发者，耐心等待能让功能少些Bug，更加稳定。
- 此软件仅用于个人使用，请勿商用。更新速度不会很快，如果有能力请通过PR贡献代码，而不是在Issue里提问题。
- 欢迎使用InkCanvas家族的其他成员，包括ICC和ICA的创造者IC，以及和ICC类似的ICA。您的大力宣传能让更多用户发现我们的软件。
- 建议使用PowerPoint支持更好！！！

### FAQ

#### 点击放映后一翻页就闪退？
考虑是由于`Microsoft Office`未启用导致的，请自行启用。

#### 放映后画板程序不会切换到PPT模式？
如果您曾经安装过`WPS`，卸载后出现此问题，是由于暂未确定的问题导致的，可以尝试重新安装WPS。
> “您好，关于您反馈的情况我们已经反馈给技术同学进一步分析，请留意后续WPS版本更新~” --WPS客服回复

另外，处于保护（只读）模式的PPT不会被识别。

若因安装了最新版本的WPS而导致无法在WPS软件内进入PPT模式，可以尝试卸载WPS后，清除电脑垃圾、注册表垃圾，删除电脑上所有带 "kingsoft" 名称的文件夹，重新安装WPS（以上步骤可能存在多余步骤），经测试在WPS内可以正常进入PPT模式。

ICC可以支持WPS，但目前无法同时支持MSOffice和WPS。若要启用WPS支持，请确保WPS在 “配置工具” 中开启了 “WPS Office 兼容第三方系统和软件” 选项，勾选并应用该选项后，将无法检测到MS Office的COM接口。

如果您安装了“赣教通”、“畅言智慧课堂”等应用程序，可能会安装“畅言备课精灵”，这可能会导致丢失64位Office COM组件的注册，且目前似乎无法修复（可以切换到新用户正常使用）。但WPS Office可以正常使用。

若要将ICC配合WPS使用，可打开“WPS 演示”后，前往“文件” - “选项” ，取消勾选“单屏幕幻灯片放映时，显示放映工具栏”，以获得更好的体验。若要将ICC配合MS Office使用，可以打开PowerPoint，前往“选项” - “高级”，取消勾选“显示快捷工具栏”，以获得更好的体验。

#### 安装后程序无法正常启动？
请检查您的电脑上是否安装了 `.Net Framework 4.7.2` 或更高版本。若没有，请前往官网下载。

> 遇到各种奇葩问题请重启应用程序，如果不行请反馈给开发者解决！

### 特别鸣谢

<table>
    <tbody>
        <tr>
            <td align="center" valign="top" width="14.28%"><a href="https://github.com/ChangSakura"><img
                        src="https://avatars.githubusercontent.com/u/90511645?v=4" width="100px;"
                        alt="HelloWRC" /><br /><sub><b>ChangSakura</b></sub></a></td>
            <td align="center" valign="top" width="14.28%"><a href="https://github.com/WXRIW"><img
                        src="https://avatars.githubusercontent.com/u/62491584?v=4" width="100px;"
                        alt="Doctor-yoi" /><br /><sub><b>WXRIW</b></sub></a></td>
            <td align="center" valign="top" width="14.28%"><a href="https://github.com/Alan-CRL"><img
                        src="https://avatars.githubusercontent.com/u/92425617?v=4" width="100px;"
                        alt="姜胤" /><br /><sub><b>Alan-CRL</b></sub></a></td>
            <td align="center" valign="top" width="14.28%"><a href="https://github.com/dubi906w"><img
                        src="https://avatars.githubusercontent.com/u/185512682?v=4" width="100px;"
                        alt="逗比" /><br /><sub><b>Alan-CRL</b></sub></a></td>            
        </tr>
    </tbody>
</table>
