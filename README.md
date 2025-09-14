# VIEE Is a tExt Extractor

VIEE是一款和[BizHawk](https://github.com/TASEmulators/BizHawk)配合使用的，用于提取游戏内文字的插件。以及一些配套的工具。

## 支持的游戏

目前支持的游戏有

* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan) (Fukyuuban)
* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan)
* Not Treasure Hunter
* Summon Night

## 目前的已知问题

* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan) （包括Fukyuuban）在向洋子下达指令时会错误的识别到选项，选项中会包含不相关的内容。该问题应该不会修复。
* Not Treasure Hunter 不支持选项的提取

## 组件

### VieeExtractor

BizHawk的插件，用于提取文字。

### VieeSubtitleGenerator

将VieeExtractor提取出来的文字按照当前OBS录像时的时间戳转换成SRT文件的工具。

### LunaTranslator For VIEE

基于[HIllya51/LunaTranslator](https://github.com/HIllya51/LunaTranslator/pulse)的Fork，增加了VIEE支持。

目前是自己构建了一个支持VIEE的版本，同时禁用了程序的自动更新，以防止主分支有更新覆盖了对VIEE支持部分的代码。

等VIEE支持的游戏稍微多一些，会向主分支提交PR。

使用方式：设置界面的核心设置里，文本输入选择VIEE即可。

同时可以在同界面下的“其他”Tab页的`等待时间 (ms)`调整VIEE接收到数据后等待多久再将数据传递给翻译器去翻译。  
因为部分游戏会有打字机效果，若直接将模拟器传回的文本拿去翻译会导致部分还未显示完全的文本也被翻译。通过调整等待时间，我们可以做到在收到**最新**的文本之后一定时间内没有更新的文本（或者收到的新文本并不是当前文本的延续）才将文本送去翻译。

目前的默认60ms是比较合适的数据。

LunaTranslator For VIEE 的下载地址：
https://github.com/ZumiKua/LunaTranslator/releases

## 使用说明

见[wiki](https://github.com/ZumiKua/VIEE/wiki/%E5%A6%82%E4%BD%95%E4%BD%BF%E7%94%A8-VieeSubtitleGenerator)

## 其他

好奇要如何提取出文字？请参考这篇博客文章：  
[https://zumikua.in/2025/08/24/ps1-game-analytics/](https://zumikua.in/2025/08/24/ps1-game-analytics/)