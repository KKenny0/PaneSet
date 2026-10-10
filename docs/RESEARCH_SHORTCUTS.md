# 多窗布局与快捷键交互调研

调研日期：2026-10-10。问题是如何减少已打开窗口的临时选组、排布和重复调整成本，不是寻找完整工作区管理器。

本次直接读取官方产品页、帮助文档及开发者维护的仓库和回复。网页从源站获取，在本地提取；没有第三方阅读代理。来源摘要与建议分开记录，没有安装或操作竞品，没有完成任务耗时比较。帮助页面和开发分支说明不能覆盖所有正式版本；历史开发者回复另行标注。

## 已确认的界面方向

用户已接受 Q1：窗口列表为主要区域，宽窗口左侧大列表与搜索、右侧紧凑布局和预览；窄窗口折叠预览。正常尺寸争取同时看到 6 至 8 扇窗口。当前已实施，正常短标题 fixture 测得 6 个完整行，实际边界见验收记录。

Q2 随后由用户确认采用收敛范围：明确选组、面板内布局键、主动载入最近窗口组。两个增量已实现并完成范围审查；行为以[产品定义](PRODUCT.md)和[本地规格](../.scratch/multi-window-controls/spec.md)为准。下文的竞品资料仍是研究依据，不是操作实测。

## 现有产品怎样确定目标窗口

| 产品与平台 | 窗口如何进入目标集合 | 快捷入口及操作 | 证据范围 |
| --- | --- | --- | --- |
| Windows Snap，Windows 10 / 11 | 先排当前窗，再通过 Snap Assist 的窗口缩略图补齐其他窗。Windows 11 可先选择预设布局。 | Windows + 方向键排当前窗；Windows 11 的 Windows + Z 打开布局选择。排在一起的窗口形成 Snap Group，可从任务栏、任务视图或 Alt+Tab 返回。 | [官方说明 S1](#s1-windows-snap)。布局弹层与 Snap Group 是本文 Windows 11 部分，不把它们当成当前 Windows 10 主机的能力。 |
| FancyZones，Windows | 用户把窗口分配到预先定义的区域；常规方向键命令作用于当前焦点窗。 | Windows + Ctrl + Alt + 数字切换自定义区域布局。可开启设置，使已分配区域的窗口跟随新区域尺寸和位置。 | [官方说明 S2](#s2-fancyzones)。切区域模板不等于任意多选窗口组。 |
| Moom 4，macOS | 保存的布局可按应用匹配，也可按最前面的窗口、最前窗口中离区域最近者、全部窗口中离区域最近者填入。 | 自定义布局可绑定全局组合键；也可先唤起键盘控制器，再使用只在控制器内生效的单键命令。 | [布局 S3](#s3-moom-4)。这里的 frontmost 指窗口前后层次，不笼统等同于按使用时间排序。 |
| Rectangle Pro，macOS | 配置应用组和位置；开发者还展示了按窗口前后层次填入三窗自定义布局的办法。 | 官网说明一键排布整套应用布局；开发者说明应用组可绑定键盘快捷键或窗口投掷动作。 | [官网与开发者资料 S4](#s4-rectangle-pro)。具体组配置 UI 依据 2022 年回复，三窗示例依据 2024 年回复，不能当成最新版本 UI 操作截图。 |
| DisplayFusion，Windows | 先摆好窗口、保存 Window Position Profile；再次调用时按进程文件、窗口类和标题条件匹配已打开的窗口。 | 给配置绑定快捷键、标题栏按钮或命令行加载。 | [官方帮助 S5](#s5-displayfusion)。配置不会启动应用；匹配条件相同的两窗有歧义。 |
| PowerToys Workspaces，Windows | 先摆好桌面、捕获应用及窗口位置，再保存和编辑工作区。 | 通过编辑器或桌面快捷方式启动，应用会被打开并定位；可选择移动已存在的窗口。 | [官方说明 S6](#s6-powertoys-workspaces)。文档的全局键用于打开编辑器，桌面快捷方式是启动入口，不能直接等同于每种布局都有全局键。 |

这些产品中，区域模板、布局配置、应用启动和窗口组召回是不同操作。表中的“一键”只表示已完成相应准备工作后的调用，不表示初次组建所需操作相同。

## 值得借鉴的交互

### 1. 一个全局入口，进入后使用局部命令

Moom 区分全局组合键和控制器内单键，还能显示命令提示。它的 Folder 自定义动作可绑定一个键唤起动作菜单，支持方向键、输入搜索和 Return 执行。[S3](#s3-moom-4)

这给 PaneSet 的启发是：把少量全局键用于进入工具与恢复，在工具内展示布局命令及其键位。用户不必为每个布局背一套全局组合键，输入搜索时也不会误执行布局。

Loop 的开发分支 README 同样描述触发键、预览和重复按键循环动作。[S8](#s8-loop) 这些是交互参考，不证明某个已发布版本的多窗操作范围，也不能用 README 的竞品比较表代替各产品的官方说明。

### 2. 初次组合与再次使用分开

Windows 11 Snap Assist 负责补齐窗口，Snap Group 负责之后返回同一组。DisplayFusion 和 Rectangle Pro 则把较多准备放在保存配置阶段。[S1](#s1-windows-snap)、[S4](#s4-rectangle-pro)、[S5](#s5-displayfusion)

PaneSet 的已确认方向是保留临时多选，并记住最近一次整组排布成功的窗口组。再次调整这组时可载入成员和顺序，不必重新勾选所有窗口。窗口组仍是当前进程中的临时选择，不因此变成持久工作区。

### 3. 自动取最前几窗省去选择，也改变目标集合的确定方式

Moom 明确提供按最前窗口或最近空间位置填入布局的模式；Rectangle Pro 开发者也说明三窗示例按照 z-index 分配位置。[S3](#s3-moom-4)、[S4](#s4-rectangle-pro)

这些方式适合用户知道窗口层次的场景。对 PaneSet 的任意临时对照任务，显式选择可以让用户检查标题、顺序和目标屏幕。哪一种更快仍须实测，不能把自动选择一概说成不好用。建议将自动取前几窗作为以后可选操作，不作为第一版默认行为。

### 4. 持久布局会引入窗口匹配问题

DisplayFusion 官方帮助明确说，进程、类和标题条件相同的两个窗口无法用这些条件区分；标题会变的浏览器需要调整匹配条件。Rectangle Pro 的开发者也曾回应多 Chrome 窗口的标题匹配问题。[S4](#s4-rectangle-pro)、[S5](#s5-displayfusion)

因此，保存“应用组合”与记住“刚才选中的具体窗口”不能混为一谈。PaneSet 当前已保存具体窗口身份，近期复用可继续按身份校验；长期保存或跨进程重建则需要另行设计规则。

## 对 PaneSet 的已确认收敛范围

用户已确认主流程：**明确选组 → 选择整组布局 → 确认排布；之后可主动载入最近窗口组。**

首次使用沿用已确认的大列表，合格前台窗口默认选中；用户补选窗口、检查顺序并指定目标屏幕。在同一面板展示布局和局部键位，按键先更新预览，Enter 或排布按钮执行。首版采用 Alt+1 等宽、Alt+2 主辅、Alt+3 网格，已实现，真实输入与合成路由分别验收。

重复使用在同一面板点击“调整最近组”。普通唤起仍开始当前窗口的新选择，不隐式复用旧组。最近组只保存最后一次整组成功排布的成员、顺序、目标及布局，当前进程内保留一组，退出清除。载入只更新选择和预览，不移动窗口或覆盖恢复记录。

失效成员明确说明，不替换为同应用其他窗口；有效成员保留供用户检查或补选，少于两扇禁止排布。原目标屏幕不可用时，重新选择目标后才能执行。每次实际执行仍校验当前身份和环境并读取最新快照，恢复语义保持最近一次实际受影响的排布。部分失败不更新最近组；等待必须收到最终结果后再判断是否更新。

交付分两个可独立使用的增量：大列表与局部布局键，然后最近组入口。首版保留已有全局唤起和恢复，不新增全局布局键、独立弹窗、多组保存、自动选最前几窗或应用启动。详细规则和验收条件见[本地规格](../.scratch/multi-window-controls/spec.md)。

## 后续验证

用两份同应用文档，以及跨屏的浏览器、PDF、编辑器三窗，比较全过程的找窗、选组、布局切换、修补和恢复。记录最近组实际调用频率，验证重复调整同组这一价值假设；不能由竞品文档推断 PaneSet 已更快。

最新隔离完整回归通过，但本轮真实输入检查仍出现过焦点与前台失败；历史不稳定根因未确认。消息路由通过不能替代系统按键送达，详见[验收记录](VALIDATION.md)。两个增量的实现和检查不能替代物理键盘、中文输入法或完整任务收益试用。

## 原始来源

以下来源均在 2026-10-10 读取。

### S1 Windows Snap

[Microsoft Support：Snap Your Windows](https://support.microsoft.com/en-us/windows/snap-your-windows-885a9b1e-a983-a3b1-16cd-c531795e6241)。读取两个平台标签的正文：Windows 11 部分列出 Windows + Z、Snap Assist 与 Snap Group；Windows 10 部分列出方向键和 Snap Assist。调用组从任务栏、Task View 或 Alt+Tab 返回，不能等同于撤销到组建前的原位置。

### S2 FancyZones

[Microsoft Learn：FancyZones](https://learn.microsoft.com/en-us/windows/powertoys/fancyzones)，页面更新于 2026-08-25。重点读取 Shortcut keys、Quickly switch between custom layouts 和 Settings。设置名“During zone layout changes, windows assigned to a zone will match new size/position”是已分区窗口随布局重排的依据。

### S3 Moom 4

- [帮助概览](https://manytricks.com/moom/help/)，页面标注更新于 2026-09-10。
- [Custom Actions：Layout](https://manytricks.com/moom/help/customactions.html#_layout)：保存布局、Apply to 的应用匹配和三种 frontmost / nearest 模式，以及可选启动应用的限制。
- [Custom Actions：Folder](https://manytricks.com/moom/help/customactions.html#_folder)：一个快捷入口打开动作菜单，再用方向键、输入搜索和 Return 操作。
- [Keyboard](https://manytricks.com/moom/help/keyboard.html)：控制器、动作提示、菜单和局部命令。
- [About Custom Actions：Hot Keys](https://manytricks.com/moom/help/aboutcustom.html#_hotkeys)：组合键全局生效，单键只在键盘控制器中生效。

### S4 Rectangle Pro

- [官方产品页](https://rectangleapp.com/pro)：页面提供 v3 下载，明确说明一个快捷键排布整套应用工作区，以及重复执行的自定义动作。
- [开发者维护的仓库 README](https://github.com/rxhanson/RectanglePro-Community/blob/main/README.md)：说明命名布局的 URL 调用接口，项目为闭源产品的支持仓库。
- [开发者对应用组绑定快捷键的回复](https://github.com/rxhanson/RectanglePro-Community/issues/54#issuecomment-1106832264)，2022-04-22：组行弹出快捷键配置，组可绑定投掷动作。这是历史操作说明，不声称当前 UI 完全相同。
- [开发者对三窗自定义布局的回复](https://github.com/rxhanson/RectanglePro-Community/issues/507#issuecomment-2113992801)，2024-05-16：按窗口前后层次分别放入左、中、右。
- [开发者对多 Chrome 窗口标题匹配的回复](https://github.com/rxhanson/RectanglePro-Community/issues/480#issuecomment-2027940781)，2024-03-30：说明应考虑标题，并表示需要调查报告中的边界情况。这不能证明当前版本仍有该问题。

### S5 DisplayFusion

[官方帮助：Window Position Profiles](https://www.displayfusion.com/HelpGuide/WindowPositionProfiles/)。重点读取 Save、Load、HotKey 及 Tips。原文明确窗口配置只移动已有窗口，三个相同匹配条件会产生歧义。

### S6 PowerToys Workspaces

[Microsoft Learn：Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces)，页面更新于 2026-09-14。重点读取创建、启动、现有窗口与新窗口行为。全局键打开编辑器；保存的桌面快捷方式可固定到任务栏。

### S7 AquaSnap

[官方产品页](https://www.nurgo-software.com/products/aquasnap)：相邻窗口可联动缩放和整体移动，功能支持可配置键盘操作。该页没有给出足够细节来证明“任意临时多选后，一键应用整组模板”的完整流程，因此只作为联动交互参考，不推断具体组选择机制或键位。

### S8 Loop

[开发分支 README](https://github.com/mrkai77/Loop/blob/develop/README.md)：读取 Radial Menu、Preview、Keyboard Shortcuts、Cycles 与 Triggering。它描述触发键、动作前预览和重复触发循环。来源是可变开发分支，不等同于已发布版本操作实测；本次没有重新执行 macOS 源码或产品。
