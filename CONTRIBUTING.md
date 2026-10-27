# 贡献指南

欢迎 issue 与 PR。为了少走弯路，动手前请先看这几条。

## 环境

- Windows 10 / 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- 可选：Visual Studio 2022+ / Rider（能直接打开 `DeepSeekTray.slnx`）

## 本地构建

```powershell
dotnet restore DeepSeekTray.slnx
dotnet build DeepSeekTray.slnx -c Release
```

期望结果：**0 警告 0 错误**（PR 会检查这一点）。

## 自检

```powershell
dotnet run --project src\DeepSeekTray.Avalonia -- --selftest
```

CLI 不方便看输出时，结果同时写到 `%TEMP%\deepseek-tray-selftest.log`，退出码 0 表示通过。
自检覆盖：三段分级、余额 JSON 解析（含多币种）、DPAPI 往返、Key 脱敏、金额格式化、
托盘 ICO 生成、中英文案。

## 改代码前请知道

- **托盘是原生的**：Avalonia 自带的 `TrayIcon` 没有悬停事件也不能自绘菜单，
  所以 `Services/TrayIconHost.cs` 直接 P/Invoke `Shell_NotifyIcon`。改动这部分请先读注释。
- **主题**：颜色全在 `Theme/Palette.axaml` 的 `ThemeDictionaries` 里，样式在 `Theme/Styles.axaml`，
  不要在窗口里写死颜色。
- **文案必须双语**：所有面向用户的字符串都进 `Localization/Loc.cs`，
  XAML 用 `{loc:Tr Key}`，代码用 `Loc.T` / `Loc.F`。只加中文会被 review 打回。
- **不要引入重依赖**：这个项目刻意保持零第三方 UI 库，Avalonia 之外只有框架自带能力。

## 提交 PR

1. Fork → 建分支（`feat/xxx`、`fix/xxx`）；
2. 改动尽量聚焦，一个 PR 一件事；
3. 跑通上面的 build 与 selftest；
4. 涉及 UI 的贴一张改动前后的截图（`--preview` 可以批量出图，见 README）；
5. 按 PR 模板勾选自查项。

## 提交信息

用 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/)：

```
feat: 支持按账号设置不同的刷新间隔
fix: 修复深色主题下浮层阴影过重
docs: 补充 DPAPI 的安全边界说明
refactor: 抽出余额格式化工具
```

## 发布流程（维护者）

1. 更新 `CHANGELOG.md`，把 `[Unreleased]` 的内容移到新版本号下；
2. 改 `src/DeepSeekTray.Avalonia/DeepSeekTray.Avalonia.csproj` 里的 `<Version>`；
3. 提交后打 tag 并推送：

   ```powershell
   git tag v2.1.0
   git push origin v2.1.0
   ```

4. `.github/workflows/release.yml` 会自动：构建 → 自检 → 打包（框架依赖 + 自包含）→
   创建 GitHub Release 并上传 zip。tag 里带 `-`（如 `v2.2.0-beta.1`）会自动标为预发布。

## 行为准则

参与本项目即表示同意遵守 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)。
