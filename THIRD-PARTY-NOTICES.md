# 第三方组件与声明

本项目的**自有代码**采用 [MIT](LICENSE) 协议。发布的可执行文件（单文件 exe）中打包了下列第三方组件，
它们各自使用宽松许可（MIT / BSD / 类似 MIT），与 MIT 协议兼容，但**分发时必须保留其版权与许可声明**。

| 组件 | 版本 | 许可证 | 说明 |
|---|---|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | 12.1.4 | MIT | UI 框架 |
| Avalonia.Desktop / Themes.Fluent / Skia / Win32 / HarfBuzz | 12.1.4 | MIT | Avalonia 官方包 |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | 3.119.4 | MIT | 2D 渲染绑定 |
| [Skia](https://skia.org/) | 随 SkiaSharp | BSD-3-Clause | 原生渲染库（`libSkiaSharp.dll`） |
| [HarfBuzz](https://github.com/harfbuzz/harfbuzz) | 随 HarfBuzzSharp 8.3.1.3 | “Old MIT” / HarfBuzz License | 文字排版（`libHarfBuzzSharp.dll`） |
| [ANGLE](https://chromium.googlesource.com/angle/angle) | 随 Avalonia | BSD-3-Clause | 仅在 GPU 渲染路径使用；本应用默认软件渲染 |
| [MicroCom.Runtime](https://github.com/kekekeks/MicroCom) | 0.11.6 | MIT | Avalonia 依赖 |
| [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) | 0.94.1 | MIT | Avalonia 依赖 |
| [.NET Runtime](https://github.com/dotnet/runtime) | 10.x | MIT | 框架依赖发布时**不**随包分发，由用户机器提供 |

完整的许可证文本请参见各项目仓库中的 LICENSE 文件。若你要二次分发本项目的二进制，
建议在发布页附上本文件（或同等效力的第三方声明）。

## 商标声明

- `DeepSeek` 名称及相关标识是 DeepSeek 的权利人所有的商标。
- 本项目**不是** DeepSeek 官方产品，未获授权或背书，仅在描述用途时提及该名称。
- 仓库中 `Assets/app.ico` 与 `Assets/app-icon.png` 的应用图标，其鲸鱼图形取自 DeepSeek 的品牌标识
  （矢量为 Simple Icons 收录版本）。**该图形不在 MIT 授权范围内**，商标权也不因开源协议而转移。
  如果你要把本项目公开发布，建议替换为自己的图标（例如项目自绘的「¥」圆角方块托盘图标），
  或保留该文件但明确标注为商标用途、并在权利人要求时移除。
- 托盘图标（`IconFactory` 动态绘制）为本项目原创，不受上述限制。

## 免责

本项目按“现状”提供，不附带任何明示或暗示的担保。因使用本软件产生的 API 费用、数据丢失
或其它损失由使用者自行承担。
