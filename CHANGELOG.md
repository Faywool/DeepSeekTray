# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 与
[语义化版本](https://semver.org/lang/zh-CN/)。

## [Unreleased]

## [2.1.0] - 2026-10-27

### Added
- 中英文双语界面，默认跟随系统语言（非中文一律英文），可在「外观 → 语言」手动锁定。
- 关于页新增免责声明（非官方、数据仅供参考、无担保、密钥本地存储、商标归属）。
- 仓库配套：MIT LICENSE、第三方组件声明、GitHub Actions（CI + 打 tag 自动发布）、
  Issue / PR 模板、Dependabot、安全策略与贡献指南。

### Changed
- 浮层布局重排：加大内边距，余额 21px、今日消耗右对齐成一列；单账号隐藏重复的「今日合计」与「默认」徽标，
  赠送余额为 0 时不再显示无信息量的构成行。
- 全应用英文与数字改用 Consolas，等宽数字对齐更稳定。
- 输入框文字垂直居中（`VerticalContentAlignment` + 模板内 TextPresenter 居中）。
- 关于页 ✓/✗/≈ 恢复语义配色，并改为两列对齐。
- 右键菜单加宽以容纳英文文案，危险项（退出）前增加分隔线。

### Fixed
- 主题/语言在「取消」时回滚到已保存值，而不是本次窗口的草稿值。

## [2.0.0] - 2026-10-26

### Changed
- **UI 框架从 WPF 迁移到 Avalonia 12**（.NET 10），界面全部重写：
  Fluent 主题、`ThemeDictionaries` 深浅色、跟随系统的 `ThemeVariant`。
- 托盘改为直接调用 `Shell_NotifyIcon`（v4）：Avalonia 自带 `TrayIcon` 没有悬停事件，
  也无法自绘菜单；改用 Win32 后同时拿到了图标矩形，浮层定位与"是否还在图标上"的判定更精确。
- 托盘图标改用 Skia 绘制，按 DPI 生成多尺寸 ICO。

### Added
- 空闲工作集回收（`MemoryJanitor`）：静置后关闭闲置浮层、GC、`SetProcessWorkingSetSize`，
  实测工作集 84 MB → 13 MB。
- `--preview`（离屏渲染全部窗口，用于 UI 自检）与 `--memtest`（反复建窗采样内存/句柄）。

### Removed
- 删除 WPF 版本代码（`src/DeepSeekTray.App`）。

## [1.1.0] - 2026-10-26

### Added
- 浅色 / 深色主题，可跟随系统。
- 今日消耗（按本机余额采样差值估算，跨天清零）。
- 自绘右键菜单、关于页能力边界说明。

### Fixed
- 悬停浮层闪烁：改用「光标是否还在图标附近」的几何判定，不再依赖鼠标移动事件。
- 去掉系统原生 tooltip（`NotifyIcon.Text` 置空）。

## [1.0.0] - 2026-10-26

### Added
- 首个版本：托盘常驻、悬停显示余额、三段式配色、余额预警、5 分钟轮询、多账号 / 多 Key 自动切换。
- API Key 使用 Windows DPAPI 加密后保存在本机。
