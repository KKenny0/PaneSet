Status: ready-for-human
Progress: implemented; native-acceptance-pending

# 单窗放置：指向可见辅窗，快捷键直接借位

## Problem Statement

用户已有用于对照的主窗口和辅窗，临时打开一扇新窗口，希望让它占用辅窗的位置和大小。拖动及手工缩放需要修补；当前单窗入口又要求打开选择器、找标题或编号、对照位置图、再选择。用户已经能在真实桌面上指认目标，却仍要在另一套界面中重新辨认，降低了快捷入口的价值。

该场景不应要求先有 PaneSet 排布记录。被部分遮挡但仍可指认的辅窗适合直接操作；完全遮挡的窗口仍需手动选择入口。

## Solution

默认单窗放置改为：保持临时窗口激活，将鼠标移到另一扇合格窗口的可见部分，不点击它，按一次现有单窗快捷键，立即借用其完整位置和大小。只修改源窗口，参照窗口留在后面。短暂描边标明本次目标，正常完成后反馈自动消失，源窗口保持可继续输入；实际改变可通过现有恢复动作还原。

指向无效目标时轻提示并保持不动，不猜目标、不自动打开选择器。主面板提供“选择其他位置…”作为备用入口，保留现有列表、搜索、位置预览及显式历史位置模式。

本规格中的“源窗口”是待放置的那一扇窗口，“参照窗口”是提供当前可见边界的那一扇窗口；同一应用的两扇窗口仍为不同目标。“布局位置”和“最近窗口组”保留各自既有含义，不与参照窗口或恢复快照混用。

## User Stories

1. As a PaneSet user, I want to keep my temporary window active while pointing at an auxiliary window, so that I can choose a destination without switching applications.
2. As a PaneSet user, I want one press of my placement shortcut to perform the move, so that I do not need to open a picker or confirm a second time.
3. As a PaneSet user, I want placement to work before any PaneSet arrangement, so that I can borrow a position I arranged manually.
4. As a PaneSet user, I want the window under the pointer to determine the reference, so that I do not need to translate the desktop into titles or numbers.
5. As a PaneSet user, I want to point at a partially exposed window, so that overlapping windows remain useful references.
6. As a PaneSet user, I want placement to copy the reference's whole visible-frame rectangle, so that exposing one corner does not produce a corner-sized destination.
7. As a PaneSet user, I want a reference's content, title bar and border to identify the same window, so that pointing does not require a special hotspot.
8. As a PaneSet user, I want separate windows of the same application to remain distinct, so that the intended document supplies the destination.
9. As a PaneSet user, I want the reference and unrelated windows to receive no placement requests, so that I can preserve my existing arrangement.
10. As a PaneSet user, I want my source to remain active after normal completion, so that I can continue working immediately.
11. As a PaneSet user, I want a brief outline of the selected destination, so that I can see which position was chosen.
12. As a PaneSet user, I want feedback to leave focus and pointer input alone, so that feedback does not interrupt my work.
13. As a PaneSet user, I want failures to be distinguished from the target outline, so that seeing an outline does not falsely imply success.
14. As a PaneSet user, I want pointing at my source, the desktop or the taskbar to leave windows unchanged, so that an invalid invocation is easy to recover from.
15. As a PaneSet user, I want an ineligible covering surface to block selection beneath it, so that a menu or popup does not silently select another window.
16. As a PaneSet user, I want an invalid invocation to give a light hint without opening a picker, so that I stay in my current task.
17. As a PaneSet user, I want the chosen source and reference to stay fixed after triggering, so that later pointer movement cannot redirect an operation.
18. As a PaneSet user, I want a changed or disappearing reference to stop placement before the first change, so that I do not land at an unexpected destination.
19. As a PaneSet user, I want switching applications before placement to stop the direct request, so that PaneSet does not bring an old source back over my new task.
20. As a PaneSet user, I want stale window identities to be rejected, so that closing and reopening an application cannot redirect a request.
21. As a PaneSet user, I want to borrow a reference on another connected display, so that the shortcut also works across my current desktop.
22. As a PaneSet user, I want unsafe desktop, display or scaling changes to stop an operation, so that PaneSet does not guess new coordinates.
23. As a PaneSet user, I want a maximized reference to supply its visible bounds without imposing its maximized state, so that the source remains an ordinary window at that destination.
24. As a PaneSet user, I want normal and maximized sources to be recoverable, so that borrowing a position does not lose their previous state.
25. As a PaneSet user, I want invalid sizes and out-of-work-area targets to be explained, so that PaneSet does not silently clamp or distort the destination.
26. As a PaneSet user, I want a waiting operation to block conflicting requests, so that repeated shortcuts cannot launch overlapping native changes.
27. As a PaneSet user, I want holding a shortcut to avoid repeated placement, so that one intentional invocation remains one request.
28. As a PaneSet user, I want no-op and pre-change failures to preserve my previous recovery record, so that an unsuccessful shortcut does not erase useful recovery.
29. As a PaneSet user, I want partial or uncertain changes to retain source recovery and an honest result, so that I can respond even when an application rejects the requested geometry.
30. As a PaneSet user, I want recovery to affect only the source changed by this placement, so that the reference stays where I left it.
31. As a PaneSet user, I want my existing custom or disabled shortcut setting to remain effective, so that this interaction change respects my configuration.
32. As a PaneSet user, I want the main panel to offer a manual destination picker, so that I can reach completely covered or inconveniently placed references.
33. As a PaneSet user, I want the manual picker to use the source displayed by the panel, so that clicking a PaneSet button cannot replace my intended source.
34. As a PaneSet user, I want keyboard navigation, search and cancellation in the manual picker to remain available, so that the fallback is usable without precise pointing.
35. As a PaneSet user, I want the explicit historical-position mode to retain its saved coordinates, so that later movement of a former occupant does not redefine a saved position.
36. As a PaneSet user, I want borrowing and source recovery to leave layout-position history and the recent window group unchanged, so that these separate operations do not overwrite each other's records.

## Implementation Decisions

1. **入口与模块职责。** MainWindow 的既有单窗快捷动作改为直接指向请求；主面板按钮显示“选择其他位置…”并打开 PlacementWindow。PanelShortcuts 保留同一动作身份、默认 Ctrl+Alt+3、MOD_NOREPEAT、自定义/禁用/冲突及退出释放语义；不新增第六个动作。SettingsWindow 与入口提示说明快捷键现在直接借位。源捕获与状态展示继续由主面板协调，原生修改、pending、结果和恢复由 WindowManager 统一持有。
2. **触发契约。** 直接请求在同次触发、任何自身反馈出现之前固定源身份、物理指针位置、实际命中的参照身份、参照完整物理可见矩形/最大化状态及桌面和目标显示环境。它不是只保存两个句柄后再由工作线程重新决定目标矩形的请求。捕获与复验不能视为 Windows 原子事务；捕获不可靠或状态不一致时拒绝，不默默选用后来读到的新位置。实现需兼顾现有后台操作和响应期限，不把可能阻塞的原生执行搬到界面线程。
3. **直接命中。** 命中内容、标题栏和边框须识别正确的顶层参照；仅有矩形包含关系、窗口标题、应用名、枚举顺序或最近距离不构成命中证明。不能穿过源自身、桌面、任务栏、菜单或其他不合格覆盖面去猜下方窗口，不能将附属弹窗错误归并为它遮住的普通窗口。不确定时拒绝。只要求触发时指认目标，之后不持续跟随指针，也不因参照后来被源遮住而改选。
4. **资格与坐标。** 源与参照不能相同；沿用现有完整 WindowTarget 身份、资格与当前虚拟桌面校验，允许同桌面跨连接屏幕。参照须为合格普通或最大化窗口；排除最小化、隐藏、工具/附属、不合格全屏及身份不可确认的窗口。参照矩形至少 240 × 160 物理像素且完整位于对应工作区；不裁切、不迁移、不切桌面。沿用可见边框与原生外框补偿、工作区原点和 DPI 校验；不把 WPF DIP 当作物理像素。
5. **固定目标与复验。** 接受请求后在关键阶段与每次源修改前复验：源/参照完整身份、资格、桌面，参照物理矩形和最大化状态，以及显示器、工作区、DPI 和配置版本。参照状态与已捕获值不一致时停止，不按新状态继续借位。源的恢复快照仍读取首次修改前的最新状态，不用打开面板时的旧几何代替。
6. **前台约束按入口区分。** 直接路径首次修改前须确认源仍是预期前台；用户已换到别的应用则停止，不通过激活旧源继续操作。正常完成保持源前台；后续焦点流失与实际几何结果分别报告，不反复抢焦点。手动选择器会取得焦点，因此保留其执行前一次普通源激活，不把直接路径的前台前提全局施加给手动路径。该边界沿用上一轮设计记录，原生时序仍须验证。
7. **共享执行。** WindowManager 的直接指向请求复用既有参照放置与 PlaceCore 的原生执行能力，扩展其请求验证以承载触发快照及直接入口前台约束；不另建一套位移、读回、锁或 Undo。源用普通窗口状态匹配参照完整可见矩形，不复制参照最大化标志。仅向源发出修改请求；不承诺应用或系统绝无连带变化。
8. **互斥与生命周期。** 沿用共享操作 guard、700 ms pending 和最终完成收集；等待不是取消。操作未完成时，排布、放置、恢复及重复触发不能启动另一项修改。设置及已有选择会话维持既有互斥/取消语义，不被转换成一次意外的指向请求。收起保留进程内记录，退出沿用既有待完成风险与清理规则；回调不得在关闭后重新显示反馈。
9. **反馈。** 在固定目标后绘制目标区域短暂描边，描边仅表示选中区域，不提前宣布成功。反馈层不激活、不取键盘焦点、不截获鼠标或快捷键、不进入候选；不得使后续请求命中自身。成功自动消失；无效目标及失败使用不激活、不自动打开面板的轻提示。部分变化和未知结果必须明确说明，可通过既有恢复入口处理。具体显示时长、颜色、控件和原生 API 由实现验证确定，不增加确认步骤；pending 的后续终局结果仍需可见且不能被早期描边替代。
10. **结果与恢复。** 沿用 OperationReport、实际变化判断与一次恢复。可读最终状态优先于中间观察；确认无变化或首次原生修改请求前拒绝，保留旧恢复记录。发生变化或已发请求后无法确认结果时，按既有安全边界保留源快照并说明结果。仅在对应最后守卫通过、即将发出原生修改请求时记录已开始修改；若此前退出最大化已改变源，则仍计入源恢复。恢复仅处理本次实际影响的源，不链式恢复更早操作；身份失效跳过并说明，不能修改同应用的替代窗口。
11. **备用选择器。** 按钮使用主面板此前捕获且显示的源，不能把鼠标下的 PaneSet 按钮作为参照或改用旧源。源失效/缺失时提示先从源窗口唤起面板。保留参考列表、稳定身份/编号、应用与标题搜索、位置图、Tab/Enter/Space、Ctrl+F、取消和输入就绪保护。无历史也可借用；有历史时保留显式“上次布局位置”。筛选/预览/取消不修改几何和恢复记录，提交后才执行。手选模式继续按提交时的当前参照状态建立请求，而非复用过期预览。
12. **独立记录与配置。** 指向借位及单窗恢复不发布、改写或清除布局位置，不更改最近窗口组；历史模式保持固定保存坐标。保留现有快捷键配置结构、动作身份及注册事务，不启动时改写设置，不添加持久化布局或新配置项。
13. **系统边界。** 优先扩展现有 PlacementEnvironment 这一系统观察边界，承接必要的触发/命中观察及故障注入。协调层消费经过身份和环境验证的请求，不持有一套独立身份模型或自行按矩形找窗口。原生命中、透明/禁用区域、顶层与附属关系、不激活且不阻挡输入的反馈机制须查证官方接口并以真实受控窗口验证，不提前认定单一接口满足所有规则。

## Testing Decisions

### 测试边界

主要从**生产的单窗动作入口 → WindowManager → 真实受控窗口及恢复结果**验证。将一个直接请求完整走过捕获、固定目标、校验、执行与结果反馈，而不只测试一个已构造好的目标矩形。手动备用入口通过真实主面板按钮和现有选择器验证。

优先复用 Fixture、WindowManager、PlacementEnvironment 及 PlacementObservations；如果必须增加注入能力，只在现有系统观察边界增加触发/命中观察。替身不能跳过命中归属规则、身份校验、冻结时机或最后守卫，不能替代原生位移与恢复。无需为每个 Win32 调用增加接口或另建测试专用执行器。

少量真实系统边界检查验证命中与反馈的事实；它们是对主要集成边界的补充，不能由注入一个“正确参照”取代。真实快捷键、前台和跨 DPI 验收单独报告。

### 好测试的标准与先例

- 断言用户可观察结果：哪扇窗口改变、完整可见边界/状态、源是否仍可输入、参照及第三窗是否保持、结果提示、可恢复目标及旧恢复记录是否保留。不要锁定私有调用顺序、控件树层数、类名、描边像素样式或某个 Win32 API 选择。
- 新行为应先有能在旧默认选择器路径上失败的回归，再实现；保留现有故障、几何及恢复回归，避免测试只镜像新实现。
- `reference-placement`、`reference-placement-guards` 作为真实几何、跨屏、最大化、环境失效、partial/unknown/no-op/pending 的先例；`placement-failure-undo` 和 `placement-restore-readback` 覆盖不能倒退的两项既有 P2 边界。
- `reference-placement-flow`、`reference-position-preview`、`reference-preview-geometry`、`placement-flow` 作为备用入口、固定源、筛选、取消、历史模式和空间图的先例；旧测试须明确其检验的是手动入口，不能继续要求新快捷入口打开选择器。
- `shortcut-routing` 的合成 WM_HOTKEY 只证明既有 hook 路由及相应面板行为；它还直接调用普通面板入口，不能当作新单窗动作、物理按键或指针捕获的证明。新集成检查必须进入实际单窗分派路径。`placement-settings` 与 `shortcut-settings` 作为自定义/禁用/冲突及迁移保护先例。

### 必须覆盖的行为

| 场景 | 可观察预期 |
| --- | --- |
| 从未排布，当前源指向合格辅窗 | 一次动作执行完整借位，不打开选择器、不建立布局位置历史。 |
| 同应用多窗、不同可见区域 | 准确识别参照；内容、标题栏、边框及部分露出不改变完整借用矩形。 |
| 几何重叠与不合格覆盖面 | 不按矩形、标题或应用猜目标，不穿过不合格覆盖面选下方窗口。 |
| 源自身、桌面、任务栏、无效源或参照 | 轻提示；没有源修改、不弹面板、旧恢复保持。 |
| 捕获后鼠标移开 | 本次源和目标不变，不重新命中。 |
| 捕获与执行之间参照移动/缩放/最大化变化/关闭 | 在首次修改前拒绝，旧恢复保持；不使用工作线程后来读到的新矩形。 |
| 参照句柄复用、源身份失效 | 不向替代窗口发出请求；提示拒绝或跳过。 |
| 首次修改前源失去前台 | 不激活旧源、不移动它；手动选择器的正常源激活路径仍可用。 |
| 反馈显示、消失与后续触发 | 捕获先于反馈；反馈不占前台、不挡指针/键盘、不成为参照；正常完成源保持前台。 |
| 失败及未知结果 | 不能只有看似成功的描边；实际变化与恢复提示一致。 |
| 普通/最大化源及最大化参照 | 源以普通状态借位，参照不被调整；恢复回到源原屏、几何及状态。 |
| 跨屏、负原点、非零任务栏与混合 DPI | 正确物理矩形与读回；注入环境变化和纯几何检查不能冒充物理混合 DPI 通过。 |
| 工作区外、小于阈值、桌面或显示配置变化 | 修改前拒绝且不裁切；后续失效按实际变化处理。 |
| 700 ms pending、快速重复及持续按键 | 单一操作占有 guard，不启动第二次放置、排布或恢复；终局结果仍被正确收集。 |
| no-op、最后守卫拒绝且读回失败 | 原恢复记录保持；不能因尚未发出的修改请求替换旧记录。 |
| 退出最大化后失败、partial、unknown | 恰当保留源恢复；读得最终状态时以它为准。 |
| 已有最近组及布局位置 | 借位/单窗恢复不改变两项记录；旧历史坐标不跟随原占用窗移动。 |
| 主面板备用入口、搜索、取消、历史模式 | 使用已显示源；不要求鼠标目标；确认前不动窗口；真实输入证据与路由分开。 |
| 设置和其他选择会话、退出清理 | 沿用互斥，不将会话内快捷键误当新指向请求，不遗留反馈或退出后回调。 |
| 自定义、禁用、冲突与其他快捷动作 | 不改用户设置；其他动作保留原行为，主面板备用入口可达。 |

### 证据与执行限制

- 几何/恢复集成检查可使用注入的前台、桌面或命中观察，必须标明哪些观察是模拟，哪些窗口读回和修改是真实的。注入测试不证明系统命中或实际键盘送达。
- 真实命中要覆盖普通内容区、非客户区、同应用多窗、部分遮挡、工具/附属/菜单、禁用及透明区域、置顶覆盖和自身反馈；确认既不误选后窗，也不误把子控件当独立目标。无法可靠确认的命中须安全拒绝。
- 物理快捷键至源窗口可继续输入、反馈无焦点/鼠标干扰、混合 DPI、Windows 11 和物理拓扑/虚拟桌面变化需要对应环境与独立结果。缺少环境记 unavailable/未运行，不能用另一层测试结果抵扣。
- 保持 Fixture 窗口归属与命中保护；不恢复历史 quick-input 的等价危险尝试，不因新功能放宽保护。当前 `placement-input` 的旧路径与历史阻断不记为已通过或根因已修复。
- 规格发布阶段只检验文档；本次实施及分层验证另见 [实施记录](implementation.md)。上表不能因代码完成或状态更新而视为全部实机通过。

## Out of Scope

- 固定屏幕分区、自动排布最近几扇窗口、通过矩形距离/历史记录猜目标。
- 向下穿透源窗或不合格覆盖面选择窗口、让完全遮挡的目标直接命中。
- 窗口交换、移动参照、联动拖动/缩放、持续跟随指针、整组重排。
- 长按、松手提交、双击或额外全局动作；不从 Loop 类比推定其接口或手势。
- 长期保存窗口组/布局、多级 Undo、跨进程重建窗口、跨虚拟桌面切换、显示变化后的自动迁移。
- 自动提权、任意第三方应用一定接受几何、Windows 原子事务或绝无应用连带变化的承诺。
- 解决历史输入调查、推广性产品文档改写、macOS 实现及以自动检查代替真实任务效率测量。
- 本轮规格编写不执行程序实现、提交、推送、打包、发布或运行实例替换；后续实施按单独指令开展。

## Further Notes

- Q1 接受直接指向方向；Q2 接受短暂描边/立即执行/源保持激活；Q3 接受无效目标轻提示且不动、不自动选择；Q4 接受主面板备用入口。来源见 [讨论记录](notes.md)。捕获快照、拒绝过期目标、分入口前台约束及证据分层是对既有设计和执行保护的具体化，不新增用户手势。
- 本规格取代 [借用窗口位置规格](../reference-window-placement/spec.md) 中“快捷键默认打开列表”的入口规则，并提前固定直接路径的目标状态；手动选择器仍保留提交时读取当前参照的语义。该规格中的源放置、恢复、历史独立性继续适用。[旧单窗规格](../single-window-placement/spec.md) 的固定保存坐标语义继续适用于显式历史模式。
- 根词汇表的“单窗放置”仍只描述早期历史位置入口；本规格沿用该术语并明确扩展为借用当前参照边界。本次不改写已保全的词汇表、产品/Windows 计划和其他文档 WIP。相关 ADR 目录当前不存在，没有现有 ADR 需要覆盖。
- 借用列表、空间预览及 P2 修复已作为隔离集成分支的前置快照保留。原工作区仍有未提交工作；实施记录列出提交、检查和保全范围，运行实例没有替换。
- 实施任务见 [任务 01](issues/01-pointer-placement.md)，代码已实现并提交到 `integrate/pointer-window-placement`。实机缺口移交 [任务 02](issues/02-native-acceptance.md)，保持未验收；`ready-for-human` 不表示发布就绪。
- 测试边界按现有集成测试先例拟定，交付时供用户核对；不把内部测试边界的反馈升级为新产品访谈或额外发布审批。
