# 研究依据

本轮最初直接读取了公开仓库、产品官网和 Microsoft 官方文档。以下公开研究记录源码、用户报告和官方功能说明，没有进行竞品操作实测；后续 Windows 受控窗口测试结果见[验收记录](VALIDATION.md)。

## 产品方向的证据

| 来源 | 本轮观察 | 可以支持的判断 |
| --- | --- | --- |
| [Loop 仓库快照](https://github.com/mrkai77/loop/tree/1ce7e72e2e20e7ebeda70869fe4cafbb6cf8ad96) | 查看了默认 `develop` 分支的 README、演示、输入处理、设置、布局和窗口执行代码。 | 径向方向选择和提交前预览值得参考。开发分支不能直接代表正式版行为。 |
| [Loop #988](https://github.com/mrkai77/Loop/issues/988) | 用户报告多窗组合需要重复执行动作，并提出先选择窗口再一起排布。 | 有其他用户报告类似摩擦，可以研究临时多窗选择，不能据此证明市场需求。 |
| [Loop #324](https://github.com/mrkai77/Loop/issues/324) | 请求多窗口命令，并引用 Rectangle Pro 的已有能力。 | 批量调窗本身已有替代方案。 |
| [Moom](https://manytricks.com/moom/)、[Moom 帮助](https://manytricks.com/moom/help/) | 官方说明支持保存布局，按应用匹配，或按最前窗口、窗口与区域的空间距离填入；控制器支持局部命令。 | macOS 已有多窗布局能力，需实测任意临时窗口组的选择成本。最前窗口不直接等同于按使用时间排序。 |
| [Rectangle Pro](https://rectangleapp.com/pro) | 官方明确列出多窗口快捷键、工作区安排和显示器连接变化触发。 | 不能把它仅描述成固定布局工具，也不能宣称批量排布是新功能。 |
| [Swish](https://highlyopinionated.co/swish/) | 官方展示标题栏手势和拖动分隔线同时调整多个窗口。 | 手势和相邻窗口缩放都已有成熟产品。 |
| [FancyZones](https://learn.microsoft.com/en-us/windows/powertoys/fancyzones) | 官方说明自定义分区、相对布局、快捷键、显示变化适配和命令行。 | Windows 首版需要与原生排列和现有分区工具比较。 |
| [PowerToys Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces) | 官方说明保存工作区、启动并安排应用，以及移动已有窗口。 | 保存桌面场景和恢复工作区已有方案。它与临时选取窗口的操作差异仍需实测。 |
| [AquaSnap](https://www.nurgo-software.com/products/aquasnap) | 官方说明相邻窗口联动缩放、磁性对齐和组移动。 | Windows 上的窗口联动也不是未被覆盖的功能。 |

Loop 最初的评估针对上表中的固定提交，并对照了[正式版 1.4.2](https://github.com/mrkai77/loop/releases/tag/1.4.2)。那次评估是静态源码和演示分析，未运行 macOS 应用。继续开发 PaneSet 时，优先使用用户已明确的对照场景，不把 Loop 的一般设置改进建议自动加入产品范围。

## 2026-10-10 快捷交互补充调研

用户要求先调查现有产品，再讨论 Q2 的多窗与快捷键组合。新增读取 Windows Snap、DisplayFusion Window Position Profiles，以及 Moom 的 Layout、Keyboard 和 Hot Keys 详细章节；重新读取 Rectangle Pro、FancyZones、Workspaces、AquaSnap 及 Loop 开发分支说明。Rectangle Pro 的组绑定与三窗顺序另核对开发者回复，并标明历史日期。

具体来源、访问日期、适用平台及交互边界见[快捷交互调研](RESEARCH_SHORTCUTS.md)。本次仍未进行竞品操作实测，不能据文档说明宣称完整任务更快。Q1、Q2 收敛范围已确认并按两个增量实现；运行证据与未验证边界见[验收记录](VALIDATION.md)。

## Windows 技术资料

| 官方资料 | 用于验证的内容 |
| --- | --- |
| [WPF 概述](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) | WPF 的桌面界面能力及只运行于 Windows 的平台限制。 |
| [.NET 支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | 本轮页面列出 .NET 10 为 LTS，支持结束时间为 2028 年 11 月 14 日。实施时重新确认补丁版本。 |
| [.NET 10 支持的操作系统](https://raw.githubusercontent.com/dotnet/core/main/release-notes/10.0/supported-os.md) | 官方系统列表的范围，需与实际主机系统分别核对，不能仅用 LTS 期限判断系统支持。 |
| [.NET 10 发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) | Windows x64 SDK ZIP 的下载地址和 SHA-512 校验值，供局部工具链准备时使用。 |
| [EnumWindows](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows) | 枚举桌面应用的顶层窗口，不能独自判断完整的产品选择范围。 |
| [IVirtualDesktopManager](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager) | 判断窗口是否属于当前虚拟桌面。 |
| [DWMWINDOWATTRIBUTE](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute) | 系统隐藏状态及可见窗口边框等信息。 |
| [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) | 调整位置和大小、避免激活或改变层级、读取调用失败。 |
| [GetWindowPlacement](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement) | 获取正常、最小化和最大化的位置及显示状态，为恢复提供快照。 |
| [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook) | 在有消息循环的线程监测窗口关闭，回调需要保持有效，事件有异步交付的边界。 |
| [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) | 全局绑定、WM_HOTKEY、不重复触发、占用反馈和 F12 保留规则。 |
| [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) | 普通激活请求的系统限制；即使符合条件也可能被拒绝，需读取实际前台。 |
| [GetSystemMetrics](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics) / [GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate) | SM_SWAPBUTTON 与物理鼠标按钮状态：受控输入按系统映射选择主按钮，已按住按钮时不注入。 |
| [ShowWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showwindow) | 最大化到正常状态的转换，以及不激活窗口的显示方式。 |

这些资料支持实现方向的可行性，不能替代真实应用中的筛选、缩放、焦点和恢复验收。WPF 单位、系统坐标及位置恢复之间的转换还需要实现代理结合官方结构说明和实测核对。

## B 窗口总览的内容预览研究

已核对 Microsoft 的 DWM 实时缩略图接口及生命周期。资料与原生验证边界见[总览预览调研](RESEARCH_WINDOW_OVERVIEW.md)；三种组织方式见[可点击原型](../designs/window-overview/README.md)。官方接口说明和网页模拟均不能替代 WPF 宿主、遮挡窗口与第三方应用的实测。

## 仍然缺少的证据

- 用户在 Windows 上执行两窗和三窗对照任务的完整操作记录。
- 现有多窗口命令是否已经能以同样低的成本完成任意临时多选。
- PaneSet 的选窗面板是否减少整体操作负担，尤其是同一应用的多个窗口。
- 第三方应用的最小尺寸、位置恢复及其他显示配置的兼容性。受控窗口验证已有记录，仍需实际应用试用。

产品建议和验收标准分别保存在[产品定义](PRODUCT.md)与[Windows 实现建议](WINDOWS_PLAN.md)，本文件只维护来源和证据范围。
