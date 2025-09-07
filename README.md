# VIEE Is a tExt Extractor

VIEE是一款和[BizHawk](https://github.com/TASEmulators/BizHawk)配合使用的，用于提取游戏内文字的插件。以及一些配套的工具。

## 支持的游戏

目前支持的游戏有

* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan) (Fukyuuban)
* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan)

## 目前的已知问题

* Tantei Jinguji Saburo - Tomoshibi ga Kienu Ma ni (Japan) （包括Fukyuuban）在向洋子下达指令时会错误的识别到选项，选项中会包含不相关的内容。该问题应该不会修复。

## 组件

### VieeExtractor

BizHawk的插件，用于提取文字。

### VieeSubtitleGenerator

将VieeExtractor提取出来的文字按照当前OBS录像时的时间戳转换成SRT文件的工具。

## 使用说明

见[wiki](https://github.com/ZumiKua/VIEE/wiki/%E5%A6%82%E4%BD%95%E4%BD%BF%E7%94%A8-VieeSubtitleGenerator)

## 其他

好奇要如何提取出文字？请参考这篇博客文章：  
[https://zumikua.in/2025/08/24/ps1-game-analytics/](https://zumikua.in/2025/08/24/ps1-game-analytics/)