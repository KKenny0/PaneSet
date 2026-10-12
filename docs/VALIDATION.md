# Windows 多窗原型验收

## ui-many：测试宿主应用名假设修复

已定位前轮 `ui-many` 的“Expected 8, got 0”：用例搜索硬编码 `PaneSet.Checks`，而 `Fixture.Start` 通过 `Environment.ProcessPath` 启动测试窗口。用 `dotnet PaneSet.Checks.dll` 运行时，fixture 的应用名为 `dotnet`；通过 `PaneSet.Checks.exe` 运行时才为 `PaneSet.Checks`。主面板按真实应用名过滤，前一种启动方式匹配到 0 项，因此渲染出的已选复选框为 0；不是 B 选择规则重构导致窗口丢失。

复现与对照：原完整用例在 dotnet 宿主下连续 **2 次 FAIL**（各约 2.6 秒）。临时缩减至“创建 8 窗、全选、搜索、检查”，搜索前 8 项通过，搜索后仍 FAIL。记录宿主/候选应用名/筛选数据数量后，仅切换启动方式：dotnet 为 `apps=dotnet, filteredItems=0` 且 FAIL；apphost 为 `apps=PaneSet.Checks, filteredItems=8` 且 PASS。这一区别发生在筛选数据层，无需延长布局等待。

修复仅修改 `InteractionChecks.CheckManyUi`：以 `Path.GetFileNameWithoutExtension(Environment.ProcessPath!)` 作为应用搜索词，与 fixture 实际启动宿主一致；保留原完整流程及 8 项断言。临时缩减用例和 `[DEBUG-ui-many-host]` 输出均已移除，生产代码不变。最终 Release 构建 **0 警告 / 0 错误**；原完整 `ui-many` 在 **dotnet 与 apphost 两种方式均 PASS，退出码均为 0**，各约 3 秒，覆盖排布、恢复、搜索后选组、不同尺寸与操作可达。未重跑无关全套，历史 48/6/3 结果原样保留；本项根因已解决，其他稳定性/配置失败仍独立开放。

复现入口：`powershell.exe -NoProfile -File C:/Users/Administrator/AppData/Local/Temp/paneset-ui-many-diagnosis/run.ps1 -HostMode dotnet`（另一方式为 `-HostMode apphost`）。该目录保存独立 build、截图、`baseline-1.log`、`baseline-2.log`、`minimal.log`、`host-dotnet.log`、`host-apphost.log`、`fixed-dotnet.log`、`fixed-apphost.log` 及 `final-build.log`。所有生成物位于此临时目录，没有覆盖仓库历史截图。

同会话自查：测试搜索输入修正，与既有 fixture 启动机制一致，未改生产筛选、测试等待、计数断言或原生输入保护。现有 ui-many 即回归用例，双宿主入口分别验证；没有新增框架或全局配置。当前没有后台测试在运行。

## B 选择规则重构：桌面恢复后的补验

用户确认桌面可用后，基于 `c1df2d9` 的生产源码重建并补验；本轮生产代码未变。Release 构建仍为 **0 警告 / 0 错误**。测试侧仅增强 OverviewChecks 的失败证据：卡片实际鼠标事件及勾选变化、会话完成原因和窗口状态；尺寸用例在打开总览前检查 fixture 几何未被准备点击改变，并在读取已关闭宿主前报告退出原因。未放宽所有权/按键保护、延长等待或加入自动重试。

完整 **57 项**运行结果：**48 PASS / 6 FAIL / 3 unavailable**，约 80.90 秒，无超时/锁屏中止，运行期间源码哈希未变化。计数来自进程输出的 `RESULT`；外层 PowerShell 未取得进程退出码（记录为 null），不将它写成已观测的 0 或 1。完整运行的测试版本包含卡片点击诊断；后续仅补充会话/尺寸失败证据，没有再次跑全套。

| 全套失败 | 本次事实 |
| --- | --- |
| overview-size-ui | 取消后 fixture 几何比较失败：原普通窗口变为最大化；当时没有准备阶段的几何证据，不能归因于总览或用户操作。 |
| overview | 已越过真实卡片点击、按钮 Enter、顺序和搜索断言；后续读取宿主客户区失败，未走完排布/Undo。 |
| quick-invalid | 准备主面板时检测到鼠标按钮已按下，保护拒绝注入。 |
| global-quick-input | 首次选组、Alt+1、Enter 排布已越过，第二次进入前等待 fixture 真实前台超时；未到取消与 Undo 断言。 |
| configured-input | 临时配置保存报“无法删除要被替换的文件”，与历史原子替换失败一致；未改保存算法或宣称根因已确认。 |
| ui-many | 主面板大列表可见项期望 8，实际 0；本轮没有定位原因。 |

3 项 unavailable 是 `single-instance`（用户已有 PaneSet）及 `shortcut-routing`、`shortcuts`（入口被占用）；没有结束用户实例或释放其快捷键。全套通过包括新增选择规则、DWM `thumbnail`、`quick-flow`、真实 `quick-input`、独立 `quick-cancel-undo`、本屏/跨屏排布与恢复及最近组路径。`shortcut-settings` 本次通过不抵消 `configured-input` 的失败。

定向过程保留全部证据：最初 `overview` 在真实点击后等待勾选超时；加入鼠标诊断后的运行在更早的按钮已按下保护处失败。全套后补充会话证据，再运行 `overview` **PASS**，完整覆盖最终顺序、低高度/滚动、失效取消、实际排布及 Undo。`overview-size-ui` 先在读取已关闭宿主时出现 1400 错误；将会话诊断提前至此读取之前后，定向 **PASS**，覆盖 2/3/8 窗尺寸、搜索/选择不改变外框、按钮可达与取消不改源几何/Undo。两次通过均没有生产修复，不能认为间歇输入/失焦或 fixture 最大化根因已解决。全套仍按 48/6/3 保留，不能改写成全套通过。

构建仍在 `C:/Users/Administrator/AppData/Local/Temp/paneset-selection-build`。全套证据：`artifacts/selection-module-full.stdout.log`、`.stderr.log`、`-status.json`、`-source.json`；定向证据：`selection-module-overview-desktop.log`、`selection-module-overview-click-diagnosis.log`、`selection-module-diagnosis-overview.log`、`selection-module-diagnosis-overview-size-ui.log`、`selection-module-size-session-diagnosis.log`（均在 artifacts）。失败与定向成功分开记录，当前没有后台验证在运行。本次生成的 23 张截图保存在 `C:/Users/Administrator/AppData/Local/Temp/paneset-selection-desktop-screenshots-c1df2d9/`，受版本控制的历史截图已恢复，避免覆盖旧验证证据；已查看本次 450 高总览合成图，搜索、已选区及主要操作可见。

Standards / Spec 同会话自查：改动限定于受控检查、诊断监听在 finally 释放、原断言与注入保护保留；不将合成路由/控件自动化当作真实输入。无独立双代理审查。本轮提交测试与验收记录，不推送或发布；历史稳定性问题继续开放，见[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)。

## B 总览选择规则集中（架构重构）

新增 `OverviewSelection`，集中本次固定候选排序、有序选组、身份刷新、搜索匹配、编号、布局预览与确认资格。`QuickSelectWindow` 仅转交选择/取消/移位及原生观察结果，读取同一份状态绘制；不再维护自己的 selected/invalid 集合、可写编号或确认资格缓存，也不再依赖主面板的 `WindowRow`。原生执行、焦点保护、DWM 生命周期和 Undo 路径保持原职责。

用户确认的规则已同步到规格：失效期间保留成员及顺序并禁确认；同一身份恢复合格后自动恢复有效性，其他条件满足时恢复确认资格，仍需用户最终确认。主动取消的成员不自动选回，再选加入末尾；不同身份不能替代。此项澄清既有刷新行为，没有新增“曾失效待确认”状态。

新规则按 TDD 分三轮建立：排序/选择顺序、身份刷新/搜索、布局确认资格，每轮先记录缺失接口的编译失败，再实现并通过。最终独立 Release 构建 **0 警告 / 0 错误**；不依赖桌面的 **8 PASS / 0 FAIL / 0 unavailable**：`selection-order`、`overview-selection-refresh`、`selection-eligibility`、`layout`、`layout-many`、`layout-invalid`、`layout-negative`、`overview-size`。覆盖初始 Y/X/稳定同位排序、前台优先、移位/取消再加入、标题与应用名搜索、完整身份各字段失配、同身份恢复、失效后主动取消及尺寸边界。总览集成检查改为读取显示标题、编号和卡片位置，不再读取 `WindowRow` 或新 module 的内部状态。

桌面定向 `overview` **1 FAIL**：首次 fixture 所有权检查命中外部窗口，`actualPid=19500`、`actualClass=Windows.UI.Core.CoreWindow`，拒绝点击；随后只读查询确认该进程为 `LockApp`。按约束停止所有桌面检查，没有解锁、释放用户按键、结束用户 PaneSet 或放宽保护。该失败发生在最终审查调整之前且尚未进入总览，不能作为最终产物的 UI 验证；最终源码只重建和运行上述 8 项无桌面检查。完整入口现为 **57 项，本轮完整套件未运行**，真实卡片/Enter、DWM、取消/Undo 和最终排布回归仍待桌面可用后补验。历史输入与配置失败仍开放。

构建目录：`C:/Users/Administrator/AppData/Local/Temp/paneset-selection-build`。日志：`artifacts/selection-module-build.log`、`artifacts/selection-module-pure-checks.log`、`artifacts/selection-module-overview-blocked.log`。

审查基线为 `54ed16572909c132690103d1a186c9efd9ffe317`，依据 AGENTS、PRODUCT、WINDOWS_PLAN 和 current-screen-selection 规格及本次确认。Standards：同会话自查无未解决代码发现；Spec：同会话自查无未解决实现偏差，桌面验证未完成。无可用子代理，未执行独立双代理审查；Advisor 因与执行模型相同禁用，不能计作外部复核。本轮仅本地提交，不推送或发布。

## 输入诊断与取消恢复独立验证

新增测试侧诊断：主面板准备失败记录 IsLoaded、可见/最小化状态、WPF/native Topmost、外框、前台句柄/类和焦点；Alt+1 失败记录前后状态、实际收到的 PreviewKeyDown/Up、修饰键、handled 及会话结束原因。Alt+1 注入前检查真实前台、会话就绪、键盘焦点及物理按键是否已有按下；未放宽原点击保护、延长等待或修改生产激活策略。

新增 `quick-cancel-undo`：通过控件自动化选择布局并执行排布，再进入 B 改选成员和布局、取消，验证排布后几何和 HasUndo 不变，最后完整恢复最初几何。这是独立行为验证，不代替真实键盘或全局入口验收。完整入口现为 54 项。

独立 Release 构建 0 警告/错误；本次 **5 PASS / 0 FAIL / 0 unavailable**：`quick-flow`、增强后的 `global-quick-input`、`quick-cancel-undo`、`quick-input`、`quick-invalid`。增强后的全局检查此次已走完取消保留已有 Undo 的新增断言。日志 `artifacts/input-diagnosis-host.log`、`input-diagnosis-alt.log`、`input-diagnosis-cancel.log`、`input-diagnosis-quick-input.log`、`input-diagnosis-quick-invalid.log`。

本次只改测试和记录，生产代码不变。加入诊断后这次没有再次捕获遮挡或 Alt+1 失败，因此不能认为历史偶发问题根因已定位或被修复；原失败日志继续保留。未重跑完整 54 项。审查为同会话 Standards/Spec 自查，无独立双代理审查。配置 ReplaceFileW 的系统错误仍按问题 08 保持开放。

## 空闲时段补验

用户允许暂停输入后，前轮 5 个失败用例定向复查为 **2 PASS / 3 FAIL**：`overview` 完整通过，`global-quick-input` 原版本通过；`quick-flow`、`quick-input`、`quick-invalid` 均在主面板准备阶段命中 Chrome 而拒绝点击。不能把这些失败简单归咎人工干扰。

随后加强 `global-quick-input`，把 Undo 放到第二次进入并取消之后，验证取消保留已有恢复记录。构建通过，但新版本在前置 Alt+1 布局等待处失败，尚未执行新增断言。原通过证据不覆盖增强版本；生产源码未改。见[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)，日志为 `artifacts/stability-idle-<case>.log` 和 `artifacts/stability-cancel-undo.log`。配置的系统替换问题仍维持下述调查结论。

## 稳定性收尾首轮定向检查

用户认可尺寸效果并授权稳定性验证及配置失败定位后，独立 Release 构建 0 警告/错误；定向 9 项为 **4 PASS / 5 FAIL / 0 unavailable**，未修改生产或测试源码。通过 `shortcut-settings`、`overview-size`、`overview-size-ui`、`quick-activation-failure`。失败 `overview`（真实卡片点击后选择状态等待）、`quick-flow`（WPF 窗口遮挡）、`quick-input`（已有物理按键按下）、`global-quick-input`（Zed 遮挡）、`quick-invalid`（Chrome 遮挡）。不是此前锁屏失败的重复结论。

本次补齐 2/3/8 窗自适应面板真实尺寸、搜索保留、按钮可达、取消不改几何/Undo；真实合成截图为 `artifacts/paneset-overview-fit-2.png`、`-3.png`、`-8.png`。全局入口、顺序调整及取消保留已有 Undo 的完整流程仍未通过。证据见 `artifacts/stability-results.json` 与[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)。

配置失败已在独立探针复现 `0x80070497`，并缩小到 Python 直接调用 Windows `ReplaceFileW` 返回 1175；去掉 PaneSet、JSON 和 .NET 仍能出现。失败后 Restart Manager 未找到占用者，过滤器查询被权限拒绝，具体触发者仍未知。未以一次事务检查通过关闭问题，未改保存算法或添加重试，见[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)。

## 总览初始尺寸调整

用户反馈全工作区面板过大，现按本屏捕获候选数计算初始大小并居中，搜索或勾选不改变外框。Release 构建 0 警告、0 错误；`overview-size` 通过候选数、负坐标、多种 DPI 比例的几何检查。新完整入口为 53 项，本次只做相关定向检查，未重跑前轮全套。

`overview-size-ui` 首次通过初始外框和搜索后外框不变断言，但在截图前面板已关闭，读取客户区失败，原因未捕获；补退出原因采集后再次运行，在 fixture 真实前台等待失败，未进入面板。保持失败，未放宽保护或反复注入；新尺寸的完整界面/截图与全局键验收未完成。具体范围见[任务 09](../.scratch/current-screen-selection/issues/09-overview-fit-size.md)。下面的总览截图和完整结果均早于此调整。

## B 统一网格总览：当前交付

用户确认 Q1–Q6 并授权实施后，B 已改为普通不透明 WPF 总览，加入 DWM 内容预览、稳定卡片、搜索、右侧前移/后移与低高度预览折叠。原共享本屏执行/Undo 与生产激活请求不变。源码和检查没有新增第三方依赖，未提交、推送或发布。

### 运行结果与版本边界

冻结源码的完整 51 项运行：**38 PASS / 13 FAIL / 0 unavailable**，退出码 1，94.38 秒，无超时，`source_changed=[]`。日志 `artifacts/overview-final-checks.log`；临时状态 `paneset-overview-status.json` 和清单 `paneset-overview-source.json`。不得报告全套通过。

| 结果 | 范围 |
| --- | --- |
| 9 项命中保护失败 | overview、thumbnail、mouse-routing、quick-entry、quick-flow、quick-invalid、quick-input、global-quick-input、quick-activation-failure。前置点击或目标像素读取命中外部窗口，未继续注入或读取外部内容。主屏 PID 19500 的 CoreWindow 经只读进程查询确认为 LockApp；副屏命中 LockScreenInputOcclusionFrame。 |
| 3 项前台/输入等待失败 | configured-input、shortcuts、local-layout-keys。实际前台条件未达成；保持失败，不延长等待、不改为 unavailable。 |
| 1 项配置失败 | shortcut-settings 首次 Change 报告原子替换文件无法删除，见[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)。独立于输入失败，根因未证明。 |
| 38 项通过 | 包含布局、跨屏几何、恢复、本屏保护、部分影响、设置 UI、路由、最近组与原面板 UI。single-instance 当时仍硬编码原输出目录，实际运行旧构建，不能作为新总览版本的单实例证据；该路径随后修正为同构建目录，重建后单独运行 single-instance 为 1 PASS / 0 FAIL，补上当前产物证据。 |

在桌面尚可交互时，新增两项分别有定向通过记录：

- `thumbnail`：自建源窗口完全被不透明目标宿主覆盖，目标原生像素从红更新为绿；裁切区域外恢复宿主白底，注销后缩略图消失；源几何不变，无效 HWND 注册失败。验证实时合成，不是 WPF 单独截图。
- `overview`：12 个受控窗口，默认前台第一；右侧移动顺序、筛选保留选组、标题变化不跳位、搜索 Enter 不提交、失效阻止确认、取消失效项后真实排布与 Undo。1440×900、1000×650、700×450 下实际读取按钮边界，低高度已选列表至少 70 DIP。定向通过后，又增加完整初始排序断言、真实卡片点击和按钮 Enter 操作，以及失效恢复后的缩略图处理/顺序按钮焦点保持；这些最终新增断言在后续运行中因 LockApp 前置阻挡而未执行，不能用先前通过替代。

Release 构建 0 警告、0 错误；总览应用 `--probe` 退出码 0。完整回归之后仅修正单实例检查的产物路径，未改变生产总览源码；最终 Release 重建仍为 0 警告、0 错误，单实例定向检查通过。未解锁桌面、关闭用户进程或放宽所有权保护。

### 截图与审查

- 实际桌面合成：[900 高](../artifacts/paneset-overview-900-composed.png)、[650 高](../artifacts/paneset-overview-650-composed.png)、[450 高](../artifacts/paneset-overview-450-composed.png)。只截取受控总览宿主客户区，捕获前检查多个位置的窗口所有权；图中候选均来自 fixture。
- 同名 `-wpf-only.png` 是 WPF 单独渲染，空白预览槽不代表 DWM 失败；它们不作为真实内容证据。
- Standards / Spec 由同会话按 AGENTS、词汇表和规格自查；没有可用独立子代理，未完成并行双代理 code-review。Advisor 本次因与执行模型相同而禁用，不能声称有外部复核。
- 修正低高度预览挤掉已选列表；失效成员释放缩略图，恢复有效后重新注册；顺序调整后保留仍可用按钮的键盘焦点。单实例测试启动旧产物的问题已修正，并完成当前产物的定向验证。

仍待验证：最终真实全局 B/键盘与取消保留 Undo 回归、第三方与受保护内容、混合 DPI、物理输入法、Windows 11 官方支持基线和完整任务效率。DWM 返回成功也可能提供黑色或受保护画面，当前不能可靠区分合法黑色内容；应用名和标题始终保留。原漏标记的具体根因未定位；替换呈现不等于证明旧缺陷原因。

## 前轮：本屏设置、A/B 增量

设置、A 本屏全部排布、B 原生快速选窗已实现，共用原来的操作 guard、Undo 与最近成功记录。当时新增 15 项，完整入口为 49 项。这里分别记录全套运行、收尾定向检查及尚未完成的输入验收。

冻结源码在独立构建目录完整运行：**45 PASS / 1 FAIL / 3 unavailable**，退出码 1，用时 82.33 秒，无超时，运行期间源码哈希未变化。日志为 `artifacts/current-screen-final-checks.log`；源码清单和退出状态保存在临时目录 `paneset-screen-source.json`、`paneset-screen-final-status.json`。Release 构建 0 警告、0 错误。用户的旧 PaneSet 进程占用原输出目录，因此用 SDK 的 `--artifacts-path` 构建到独立目录，未关闭旧实例或清除其恢复记录。

- 15 项新增检查在这次完整运行中全部通过。
- `local-layout-keys` 在最后普通数字输入检查前，等待主面板成为真实前台失败，位置为 `InteractionChecks.cs:411`；该次检查保持 FAIL，根因及其与本轮改动的关系尚未证明。它未完成最后一段普通数字断言，不能据新 B 通过宣布历史前台不稳定已解决。
- `single-instance`、`shortcut-routing`、`shortcuts` 因已有用户实例或已注册的旧面板/恢复键不可运行，保持 unavailable。未停用户进程或用合成消息替换真实输入。这三项本轮没有新通过证据，前一轮记录保留在下文。

完整运行后自查补充了 A 零/一窗与入口异常的可见反馈，统一用不激活方式显示主面板；增强恢复默认冲突/成功验证，以及候选标题的有限宽度换行。随后重建仍为 0 警告/错误，`screen-arrange`、`shortcut-settings`、`quick-activation-failure`、`settings-ui`、`quick-invalid` 五项各 1 PASS / 0 FAIL，日志分别为 `artifacts/current-screen-final-<case>.log`。原生 `--probe` 退出码 0。49 项全套早于这些收尾改动，未再次重复全套。最后有限宽度修正后，另一次 `global-quick-input` 定向检查也通过（1 PASS / 0 FAIL），日志为 `artifacts/current-screen-final-global-quick-input.log`，覆盖最终选窗源码的真实全局 B、排布/恢复及非 Topmost 再进入/取消。

| 新检查 | 结果与证据范围 |
| --- | --- |
| shortcut-settings / settings-ui / configured-input | 临时配置、缺失与 null、损坏配置不覆盖、动态 ID、占用/重复、保存失败回滚、禁用、重启和释放；恢复 B 默认时占用失败保留旧键，释放占用后保存默认成功。真实新面板键及 A/B 回调触发、旧键停止触发；A 回调不执行用户桌面的全部排布。设置 700 高与 450 高、滚动到底部可触及 B 默认按钮通过。 |
| screen-guard / screen-arrange | 严格本屏入口、两个显示器双向操作、普通/最大化、外屏 fixture 不变、零/一窗不改变已有布局和 Undo、工作区元数据改变拒绝。A 协调检查注入 fixture 候选，未直接执行用户已有桌面的 A 全排。 |
| screen-partial / screen-eligibility / screen-member-move | 执行中首窗间接关闭、最小化或移屏第二窗；失败窗不被主动还原/拉回，其他有效项继续，直接/间接影响保留 Undo。移屏后的恢复失配保留记录，受控返回原屏后可重试恢复。 |
| quick-entry / quick-flow / quick-invalid | 真实面板按钮进入；每次新成员、默认前台、标题候选、布局预览不移窗、取消保留已应用几何与 Undo、确认仅排选中 fixture、失效成员保留说明、零/一窗不可确认、内部弹出控件、外部失焦、完整长标题换行。流程内合成键路由与原生几何分别断言。 |
| quick-input / global-quick-input | 真实 Space、Alt+1、Enter、Esc；布局下拉打开时第一次 Enter 仅接受选项、第二次才排布。真实自定义全局 B 从受控前台触发，在副屏排所选两窗并恢复；同一次用例又验证非 Topmost 前台进入与取消。其余 fixture 不变。 |
| mouse-routing | 实际主按钮点击进入 fixture 客户区并改变测试标题，验证下层点击指示器有效。覆盖层防穿透检查同时要求收到逻辑主按钮 down/up，再确认下层标题未改变；不以 SendInput 返回数单独认定送达。 |
| quick-activation-failure | 受控 ShowActivated=false 会话未取得前台，失败关闭且保持已有布局/Undo。该项验证选窗层 guard；Main 的激活失败反馈分支由源码审阅覆盖，没有稳定制造 OS 拒绝的端到端证据。 |

### 本轮输入诊断与保留的失败

早期直接调用选窗组件未建立原生前台前置，曾在 Ready 阶段失败；普通主面板激活请求也有返回 false 的日志。受控点击建立真实前台后组件可进入，但不能用这点解释所有外部干扰。真正注册的 B 从 fixture 前台收到 WM_HOTKEY 后，原 Show-only 路径曾因前台未转入而关闭；增加一次普通激活请求的对照记录为窗口可见、Activate=true、实际 foreground 等于 picker，并完成排布/Undo。最终保留普通请求及实际前台/键盘 guard，没有反复抢焦点或修改历史面板激活策略。

鼠标早期注入未触发面板按钮。本机读取 `SM_SWAPBUTTON=1`，修正受控注入以物理右按钮代表逻辑主按钮后，mouse-routing 与按钮入口在完整回归中通过。注入前检查物理按钮均松开，不替用户释放预先按住的按钮；命中必须属于测试拥有的窗口。此前多次因命中 Desktop、pebrel 或 Chrome 子窗口而拒绝注入，其中一次点在可见/可用、DPI=96 的测试主窗矩形内却命中另一进程；没有据此统一归因为 WXWork、坐标错误或系统权限，也没有放宽命中断言。左右键映射是此次鼠标协议的具体问题，不用于解释历史键盘/前台失败。

曾尝试 fixture 前台锁制造 Main 激活失败，但未稳定令 picker 关闭，超时保持失败；该测试设置最终移除，finally 已解锁，不将它算为生产回归修复或负向验收通过。最终没有临时激活诊断输出、前台锁策略或新的环境 skip。

### 截图与实机边界

设置截图 `paneset-settings-700.png`、`paneset-settings-450.png`、`paneset-settings-450-bottom.png`，以及 `paneset-native-selection.png`、`paneset-native-selection-invalid.png`、`paneset-native-selection-long-title.png` 已生成并读取。选窗图为 WPF 透明层单独渲染，未包含完整桌面合成；只能证明受控界面状态，不能当作真实桌面遮挡、输入或效率测量。

本机仍是 Windows 10、两个显示器 96 DPI。受支持环境的验收基线为 .NET 10 官方支持的 Windows 11 x64 版本，尚未在该基线运行完整检查；当前 Windows 10 只有受控运行证据，不承诺正式支持或普遍兼容。物理键盘/中文输入法、第三方应用、混合 DPI、真实拔屏与工作区改变、完整任务成本仍待实机验收。网页原型的视觉认可和 28 项浏览器检查不替代这些证据。本轮未提交、推送或发布。

## 前一轮：大列表与最近组

前一轮在跨显示器选组、多窗布局和快捷入口上，完成大列表、局部布局键及主动载入最近窗口组。两个增量均完成 /check 范围审查与修复；当时隔离完整回归通过。期间真实输入检查仍出现过焦点失败，不能据随后一次通过认定稳定性问题已解决。

### 当时的完整结果

最终未加诊断的源码在独立工作树中完整运行 34 项：**34 通过、0 失败、0 跳过**，退出码 0，用时 59.23 秒。隔离输入为 HEAD `444a16a` 加冻结的完整前置状态与本轮 5 个源码/检查文件，19 个已有源码、配置和工具文件逐字节匹配预期；最终 5 个文件与主工作区一致。Release 重建 0 警告、0 错误；随后 `tools/run.ps1 --probe` 原生探针退出码 0。日志及结构化退出状态保存在忽略跟踪的 `artifacts/q2-final-isolated.log`、`q2-final-isolated.json`、`q2-final-probe.log` 和 `q2-final-probe.json`。

此前主工作区最终源码的完整运行是 **33 通过、1 失败、0 跳过**，退出码 1，用时 67.7 秒，日志为 `artifacts/q2-final-checks.log`。失败在 local-layout-keys 的焦点前置条件，尚未发送 Alt 数字；该次 shortcuts 通过。相同隔离环境中，替换冻结 A 文件与当前 B 文件的串行诊断均又在原生前台等待失败。后续测得已有 Activate 与 SetForegroundWindow 均返回 false，WPF IsActive 为 true，原生前台不属于面板且不为 0；这不能确定系统拒绝的根因，也不支持放宽焦点断言或修改生产激活策略。临时诊断只在隔离代码中，已从最终验证源码删除。较早的 A 诊断完整 28 项通过属于加了诊断输出的运行，不能混同上述最终结果。

完整入口为 `verify.cmd`；合成消息、路由与真实系统输入各自报告，人工物理键盘与中文输入法仍未验收。

收尾审查在完整回归后修正了 WindowManager 的一条既有失败提示：原“原位置已保留”容易让人误以为未移动，现提示未确认达到预期布局、已改变时可尝试恢复。这是单条文案修改，未改窗口操作逻辑；修改后主区和隔离工作树重建均为 0 警告/错误，partial-failure 与 recent-group-failures 两项定向检查分别通过。上述 34 项结果来自修正文案前的完整运行，未再次重复完整回归。

| 检查组 | 最新结果及证据范围 |
| --- | --- |
| 布局 | 两窗、任意多窗、各布局、奇数尺寸、负坐标、无效区域、包含区域和互不重叠通过；12 窗列布局过小时移动前拒绝。 |
| 双向跨屏 | 主屏到副屏、副屏到主屏的普通及最大化排布和恢复通过；第三扇未选窗口保持原状态。 |
| 多窗 | 副屏三窗、三窗主辅、八窗网格排布与恢复通过。 |
| 记录及失败 | 整组快照、间接改变第二窗、部分排布与恢复、关闭、身份失效、恢复重试、无效新请求保留旧记录通过。 |
| 响应延迟 | 延迟响应时阻止冲突操作；永久挂起 fixture 终止后结束等待通过。 |
| 单实例 | 启动实际应用、关闭仅收起、重复启动恢复已有面板、第二进程正常退出、明确退出后可重新启动通过。 |
| shortcut-routing | 合成 WM_HOTKEY 到注册的原生 HWND，验证 hook 路由、面板解除最小化、副屏定位及默认选择、pending 时恢复保护、恢复、关闭仅收起、注册冲突和退出释放通过。该项不证明实际按键送达或获得前台。 |
| shortcuts | 最新隔离运行通过真实 SendInput 全局打开、恢复、副屏及等待保护；历史存在未送达和激活失败，本轮也有通过与失败，稳定性根因未确认。 |
| local-layout-keys | 最新隔离运行通过真实 Alt+1/2/3，在搜索框及布局控件内切预览不移窗，Enter 原生排布、恢复及未选窗口不变，普通数字仍输入搜索。此前焦点前置条件曾失败，不能称为持续稳定。 |
| local-layout-routing / selection-refresh | 合成 WPF 按键和线程键盘状态覆盖精确修饰键、中文与数字搜索内容、等待保护；选择模型与渲染同步、前台新组及重复唤起保留选择通过。合成路由不替代真实键盘或输入法组合。 |
| 最近组 | 6 项检查通过：成功载入顺序、目标、布局、当前标题；载入不移动或覆盖 Undo；无变化成功保留旧 Undo；连续布局与手动移窗恢复使用最新状态；缺失成员不替换；失效目标不回退；部分及延迟失败保留旧组；终局成功才发布；关闭收起、明确退出及迟到回调保护。窗口关闭期间枚举异常修复前失败、修复后可重新选择。 |
| 界面 | 多窗选择、布局切换、顺序轮换、刷新和切屏保持顺序、搜索、排布与恢复、最小尺寸按钮边界通过。 |

检查使用独立 fixture 进程创建的真实 WPF 顶层窗口；界面通过可访问性接口驱动真实控件。只移动 fixture 自建窗口，不操作已有用户窗口。布局和截图不能替代原生移动验证。

## 前置版本的快捷键定位记录

2026-10-09 的前置版本完整检查为 24 通过、1 shortcuts 失败、0 跳过，退出码 1，原生探针通过。历史日志为 `artifacts/multi-window-checks.log` 和 `multi-window-probe.log`。以下记录保留当时的具体失败，不将新一次通过当作根因修复。

最初 Ctrl+Alt+P 在本机注册失败，返回冲突错误 1409；当前绑定为 Ctrl+Alt+Space 和 Ctrl+Alt+Z，两项分别注册。此前定向实际按键检查曾通过，但它早于当前副屏和 pending 断言，不能作为最新完整验收。

当前首次等待同时包含恢复和获得前台。临时记录显示两项 RegisterHotKey 成功，面板在注入前原生 IsIconic 为 true，Ctrl、Alt、Space 均未按下；一次失败未收到生产 hook 的 WM_HOTKEY，也未执行打开分支。此证据不能证明生产激活逻辑有问题。

旧 keybd_event 没有插入计数反馈，检查已改用明确布局的 x64 INPUT 和 SendInput，并立即读取返回数量及失败错误。一次诊断返回 0/3、错误 5；独立线程探测随后返回 6/6，却未在限定等待内观察到 WM_HOTKEY。成功调用后的错误值不能用于解释失败，也没有据此断言 UIPI、输入桌面或第三方输入拦截是根因。会话为 Active，当前和输入桌面均为 Default。

尝试在注入前让测试面板获得前台没有成功。该新增前置条件已移除：全局快捷键本身不要求面板已有前台，不能把应用激活失败转换为跳过。最新完整运行中的 SendInput 没有触发“完全未插入、错误 5”的跳过分支，随后仍在首次等待失败。没有延长等待或发送合成消息来替代实际按键验证。

后续隔离跟踪进一步确认：SendInput 返回 6/6，面板持续最小化、前台句柄为 0，生产 hook 未收到 WM_HOTKEY。临时 WH_KEYBOARD_LL 使用唯一标记，只记录该批输入；虚拟键及扫描码两种方式均观察到完整的 Ctrl、Alt、Space 按下与松开事件。注入前 Shift、Ctrl、Alt、Space 和两侧 Windows 键均未按下。逐键诊断中，Ctrl 按下事件可见，但五秒内通用 Ctrl 及左 Ctrl 的异步按键状态仍为 0；尚未发送 Alt 或 Space，释放请求又被系统拒绝。观察到 hook 事件不能证明后续输入链已接受，也不能据此确认第三方拦截或桌面权限的根因。后续独立 Ctrl 探测的 CallNextHookEx 返回 0；只读检查确认窗口站为 WinSta0，线程和输入桌面均为 Default 且报告正在接收输入。仍未定位导致状态不更新的组件。

临时 hook、逐键探测和生产诊断输出均已移除，检查保留常规的已检查返回值的 SendInput 组合键。清理后的完整回归再次为 24 通过、1 失败。生产恢复和激活流程没有因未证明的猜测修改；合成消息检查独立覆盖后续处理。余下需定位输入状态为何不更新，或通过运行中的 PaneSet 完成真实键盘验证。

## 实测环境

Windows 10 Pro 22H2、Build 19045、x64，交互会话 1。局部 .NET SDK 10.0.401 已校验官方 SHA-512；应用和检查声明 PerMonitorV2。

| 显示器 | 工作区域，物理像素 | 实测 DPI |
| --- | --- | --- |
| 主屏 | X=0，Y=40，宽=2560，高=1400 | 96，100% 缩放。 |
| 副屏 | X=2560，Y=40，宽=2560，高=1400 | 96，100% 缩放。 |

任务栏在上方。四组双向普通和最大化跨屏测试的 DPI 均为 96/96，不构成混合 DPI 验收。

## 已实现行为

- 宽窗分栏、窄窗折叠预览、精确 Alt+1/2/3 只切预览，确认后执行。
- “调整最近组”显式载入最后一次整组最终成功的成员、顺序、目标及布局；记录仅在当前进程中，与恢复快照独立。
- 当前虚拟桌面所有连接屏幕上的普通或最大化窗口独立列出，标明来源屏幕，支持应用名和完整标题搜索。
- 至少两窗多选、编号预览、明确目标显示器；自动、等宽、一主多辅和网格布局，按选择顺序轮换。
- 刷新按 HWND、generation、PID、线程、进程启动时间及类名保留选择和顺序，避免句柄复用造成错误保留。最近组的 generation 失配已有 UI 检查，真实 HWND 复用仍未实测。
- 首次修改前保存整组最新状态，排布后读回；受影响窗口分别恢复各自来源屏幕及正常或最大化状态。失败记录保留供重试。
- pending 时按钮与全局恢复共用操作保护；700 ms 是反馈阈值，不能当成取消。
- 收起、Esc、关闭保留运行和恢复记录；明确退出释放快捷键。重复启动通知已有进程，面板已有焦点时也保持打开。

## 界面检查

检查 820 × 730、600 × 730、820 × 600、600 × 600，以及长标题和完整快捷键说明。760 宽度是分栏断点，另检查 759/760；窄窗默认折叠预览，高度低于 680 时预览为 48，状态区为 24。正常短标题检查测得 6 个完整行，列表视口约 364、单行约 60；最小尺寸不承诺 6 行。按钮和预览在真实 GetClientRect 客户区内；局部键只改变预览。列表显示最多 64 个字符，搜索和 tooltip 保留完整标题。

截图来自受控 WPF 渲染，只含检查内容：

- [正常尺寸](../artifacts/paneset-normal.png)、[窄窗口](../artifacts/paneset-narrow.png)、[低窗口](../artifacts/paneset-short.png)、[最小尺寸](../artifacts/paneset-minimum.png)、[长标题](../artifacts/paneset-long-title.png)。
- [三窗主辅](../artifacts/paneset-three-main.png)、[八窗网格](../artifacts/paneset-grid.png)、[网格最小尺寸](../artifacts/paneset-grid-minimum.png)、[快捷说明最小尺寸](../artifacts/paneset-shortcuts-minimum.png)。
- [分栏前](../artifacts/paneset-breakpoint-759.png)、[分栏后](../artifacts/paneset-breakpoint-760.png)、[最近组](../artifacts/paneset-recent.png)、[最近组最小尺寸](../artifacts/paneset-recent-minimum.png)、[缺失成员](../artifacts/paneset-recent-missing.png)。

视觉采用浅色背景、墨绿色选择和主要按钮，Segoe UI 与微软雅黑回退，没有高频操作动画。

## 审查及剩余边界

按项目说明、词汇表和当前规格进行 Standards / Spec 人工核对：布局计算独立，系统调用集中于窗口管理和入口模块；ReadState、排布及恢复检查保存身份；跨屏记录来源和目标工作区域；正常界面、全局恢复及 pending 收集使用同一操作保护；单实例退出停止轮询并释放事件和互斥量。修正了重复启动在面板已有前台时误进入收起分支的源码问题，并增加独立消息处理检查。没有并行双代理审查；Advisor 提供定位和验证边界建议，没有独立读取完整仓库。

两个增量分别以冻结前置状态和增量 A 快照为审查边界，避免将已有未提交改动算作新功能。按 /check 做源码、架构、窗口身份安全、终局状态及视觉对抗核对；没有并行代理，Advisor 只收到摘要。修复了低高度预览裁切，增加原生边界断言；修复候选枚举后窗口关闭导致 updating 不释放，旧代码回归为 Expected False, got True，finally 修复后通过。扫过两个 updating 批量更新点并补齐释放，所有新截图已筛选 fixture。新增 9 项检查，6 项用于最近组及异常恢复，直接和延迟发布均走生产 Apply 路径。

最新隔离完整运行通过，但实际输入在本轮仍表现不稳定；任务 01 保持 in-progress，不宣称根因已修复。最近组失效身份检查通过修改保存的 generation 验证，不等于真实 HWND 复用；无目标与过期区域检查通过注入保存的失效句柄和旧区域验证，不等于物理拔屏、真实工作区改变或混合 DPI。其他桌面成员、人工物理键盘和中文输入法组合未实测。其余边界如下：

- 主要实测受控 WPF 窗口，尚未系统验收浏览器、PDF、管理员应用等第三方程序；不自动提权。
- 混合 DPI、125% / 150%、左侧任务栏、负原点的真实显示拓扑和断屏未实机验收。显示环境变化后不会自动迁移恢复坐标。
- 逐窗恢复不是原子事务；身份检查与使用之间存在系统固有竞态，不能保证捕获快照比较结束后的全部延迟变化。
- 恢复记录仅在进程中保留。退出清除记录，已发出的未响应请求可能稍后生效，不能称为取消成功。
- macOS、长期窗口组、联动缩放、跨虚拟桌面和最小化窗口唤醒未实现。
- 当前 Windows 10 Pro 22H2 不在 .NET 10 官方支持列表中，实测可运行不等于正式兼容性承诺。
- 尚未完成竞品和完整任务耗时比较，不能根据功能实现宣称更快或独创。
