# PaneSet

临时选出两扇窗口，一次排好。

PaneSet（窗口组）是当前工作名。Windows 两窗原型已可运行，支持选窗、搜索、交换左右、并排预览、应用和恢复。三窗、全局快捷键和 macOS 留在后续阶段。

## 启动与使用

双击 [run.cmd](run.cmd)。本机已经准备好局部 SDK；新机器首次启动会下载并校验 .NET 10 SDK，后续使用项目内的工具链构建和运行。

1. 在同一显示器打开需要对照的窗口，再打开 PaneSet。
2. 选择两扇窗口，列表序号对应预览中的左右位置。可以搜索应用名或窗口标题，点击“交换左右”调整顺序。
3. 点击“排布这两扇窗口”。完全成功后面板最小化，让目标窗口显示出来。
4. 从任务栏重新打开 PaneSet，点击“恢复上次排布”。恢复记录保留到应用退出。

Ctrl+F 聚焦搜索，Tab 和空格可选择窗口，Enter 确认排布，Esc 关闭面板。确认前关闭或取消不会移动目标窗口。

窗口仍在响应调窗请求时，面板继续显示等待状态，暂停排布和恢复，直到这次操作完成。此时选择退出会提示恢复记录丢失、目标仍可能稍后被移动，需要明确确认。部分失败保留受影响窗口的恢复记录。

## 验证与实现

双击 [verify.cmd](verify.cmd)，运行布局计算、真实受控窗口操作和面板操作验证。单项检查可执行 `verify.cmd --case ui`。

实现使用 C#、WPF 和 Win32，代码位于 [src/PaneSet](src/PaneSet/PaneSet.csproj)，验证程序位于 [tests/PaneSet.Checks](tests/PaneSet.Checks/PaneSet.Checks.csproj)。没有额外第三方运行依赖，局部 SDK 位于忽略跟踪的 `.tools/`。

本轮在 Windows 10 Pro 22H2 x64 上实测构建、启动及 16 项检查通过，构建无警告或错误。[验收记录](docs/VALIDATION.md)包含截图和边界。该系统未列入 .NET 10 官方支持范围，实测运行结果不代表官方支持或所有应用兼容。

## 文档入口

| 文档 | 用途 |
| --- | --- |
| [产品定义](docs/PRODUCT.md) | 核心痛点、交互方向、后续范围和产品价值验证。 |
| [Windows 方案](docs/WINDOWS_PLAN.md) | 窗口筛选、坐标、排布、恢复和工具链。 |
| [研究依据](docs/REFERENCES.md) | 竞品、Loop 与 Windows 官方资料。 |
| [已确认配置](docs/SETUP_REVIEW.md) | 本轮批准的工程配置和测试边界。 |
| [验收记录](docs/VALIDATION.md) | 本轮可运行成果、测试和截图。 |
| [本地任务](.scratch/windows-two-window/issues/01-arrange-and-restore.md) | 两窗交付的实施与验收状态。 |
