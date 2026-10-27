# 安全策略

## 支持范围

只有**最新 Release** 会收到安全修复。旧版本请先升级再报告。

## 报告漏洞

**请不要公开提 issue。** 请通过 GitHub 的
[私密漏洞报告](https://docs.github.com/zh/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability)
（仓库 `Security` 标签页 → `Report a vulnerability`）提交，并包含：

- 受影响的版本与构建类型；
- 复现步骤或 PoC；
- 影响评估（信息泄露 / 本地提权 / 远程代码执行……）。

我会在 7 天内确认收到，并在修复发布后再公开细节。

## 本项目的安全边界（请先确认是否属于漏洞）

这个工具是**纯本地**的托盘程序，设计上：

- 只与 `https://api.deepseek.com` 通信，没有其它网络出口；
- API Key 用 Windows DPAPI（CurrentUser）加密后保存在
  `%APPDATA%\DeepSeekTray\settings.json`；
- 日志不记录 Key 明文。

因此以下**不算**漏洞，属于已知设计取舍：

| 情形 | 说明 |
|---|---|
| 同一 Windows 用户下的其他进程能解密 Key | DPAPI CurrentUser 的固有限制；需要更强隔离请用凭据管理器或主密码（本项目未实现） |
| 拿到 `settings.json` 就能读出余额等非敏感配置 | 余额不是凭据 |
| 明文 Key 出现在 `%APPDATA%` 之外的临时文件 | 不会发生；若发现请报告 |
| 程序以管理员身份运行时托盘行为异常 | 已知问题，请用普通权限运行 |

## 依赖安全

Dependabot 每周检查 NuGet 与 GitHub Actions 依赖。运行时依赖均为宽松许可的开源组件，
详见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
