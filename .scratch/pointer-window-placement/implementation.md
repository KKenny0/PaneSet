Status: ready-for-human
Progress: implemented; native-acceptance-pending

# 指向辅窗直接放置：实施记录

代码已实现于 `integrate/pointer-window-placement`。实现任务 [01](issues/01-pointer-placement.md) 移交，未完成实机条目由 [02](issues/02-native-acceptance.md) 保持开放。没有发布或更新运行实例。

## 实现与提交

- 前置快照 `7ab18f3`：保留原工作区已有的参考列表、位置图和 P2 修复；只在隔离集成分支提交，原分支/暂存区没有迁移。
- 功能提交 `696a540`：既有单窗动作直接指向借位；保留动作身份、默认/自定义/禁用/冲突与 MOD_NOREPEAT；主面板“选择其他位置…”沿用显示源打开手动列表。
- 检查清理提交 `d906a37`：移除从未赋值/使用的 `PointerPosition` 测试字段及对应死分支；不改变生产行为。
- 读取物理指针先于候选枚举，反馈出现前固定源身份、指针、参照身份/完整几何/状态/桌面、参照和来源显示环境。工作线程只校验原指向点的遮挡，不重新读取鼠标来改选。源恢复快照仍读取首次修改前的最新状态。
- 原生命中以 WindowFromPoint 与 GA_ROOT 归属为依据，保持 popup 与 owner 的区别；有界 Z-order 检查拒绝不确定的覆盖面，不按矩形猜目标。透明/异形覆盖面的包围矩形可导致保守拒绝，手动备用入口仍可达。
- 直接请求复用共享 PlaceCore、700 ms pending、读回与 Undo。来源环境在触发时固定；修改前检查源仍在原屏及前台，合法跨屏后允许目标屏完成。手动路径仍使用提交时参照及执行时来源环境，正常激活源一次。
- 几何完成后前台再丢失单独报告，不再变成虚假的几何失败，不激活旧源。未知/部分结果保持原恢复语义；最后守卫后才标记已开始修改，两项 P2 保留。
- 不激活的 layered/tool/transparent 临时 HWND 显示目标描边或轻提示；采用主题色与反差边缘，目标约 950 ms，提示约 4.5 s。结束、过期和退出均释放。pending 的终局仍显示结果。不可显示目标反馈时不启动修改。
- 修正 `ui-many` 的等待条件：原用例只等后台 Undo 清空，可能在列表仍禁用时操作；现在等待列表重新启用。

## TDD 与相关检查

证据目录：`D:/VibeProject/diagnostics/paneset/pointer-placement-impl/`。构建/测试均在仓库外的独立快照与产物目录运行，保留源码哈希、逐项 stdout/stderr 和结果 JSON。

已观察的 red → green 包括旧默认选择器路径、缺少原生命中屏障、缺少反馈、备用按钮名称、候选枚举期间鼠标改选、来源屏幕上下文变化仍被执行、几何成功后焦点变化误报失败。最后三项在真实受控窗口几何/恢复上复现后修复。注入的是系统观察，不是位移执行器。

29 个不同相关检查通过；不是完整测试套件通过：

- 新增 7 项：`pointer-placement-flow`、`pointer-placement-hit`、`pointer-placement-guards`、`pointer-placement-fallback`、`pointer-feedback`、`pointer-placement-snapshots`、`pointer-placement-source-environment`。
- 原生/共享 15 项：`placement`、`placement-guards`、`placement-undo`、`placement-failure-undo`、`placement-restore-readback`、`reference-placement`、`reference-placement-guards`、`screen-arrange`、`screen-guard`、`recent-group`、`recent-group-noop`、`recent-group-failures`、`maximized`、`local-layout-routing`、`reference-preview-geometry`。
- UI/配置 7 项： `reference-placement-flow`、`placement-flow`、`placement-settings`、`shortcut-settings`、`ui-many`、`settings-ui`、`reference-position-preview`。

计数为 7 + 15 + 7 = 29。新增 guards 覆盖 14 个场景，source-environment 覆盖 5 个场景；它们是用例内部场景，不额外累加测试数。真实两屏的普通/最大化源放置与恢复通过；DPI/配置/桌面失效有注入，不能算物理混合 DPI 或切屏验收。列表回归仍为 `PICKER visible=6, viewport=332, row=54`。

`committed-check` 从 `696a540` 的纯 Git 归档构建并通过 10 项，但构建日志实际有 1 条未使用测试字段警告、0 错误，先前的“0 警告”表述已纠正。删除该死字段后，`committed-final` 从 `d906a37` 的纯归档重新构建，没有覆盖工作区文件；构建日志明确为 0 警告、0 错误，7 个新增检查与 3 个 UI/位置预览回归再次全部通过，各子进程 exit 0、unavailable 0。其余 19 项相关检查见 `shared-regressions`、`final-related`，不宣称都在最终头重跑或逐字哈希完全相同。

## 原生与 UI 证据边界

- 原生命中检查观察了透明覆盖层下原始 API 返回后窗的情况，Z-order 屏障仍拒绝；也有禁用根窗口拒绝及无遮挡正对照。使用受控 HWND 查询，没有注入输入。
- 同进程受控 HWND 上，实际前台在显示/销毁反馈前后不变；描边不透明边缘与内部的原始系统点查询穿过反馈，计时释放通过。不能由此推断跨进程鼠标/键盘穿透。
- 跨进程前置对照被遮挡，`red-late-focus` 中 `pointer-feedback` 为 unavailable（0 failed、1 unavailable），尚未创建反馈、未发送输入。最终拆为单独的 `pointer-feedback-cross-process`，未重复顶层/输入尝试；该新命名入口未再运行。
- `placement-input` 的旧选择器期望已改为直接借位，保护增强，未运行；真实快捷键/连续按键/Tab 与焦点交接均未证明。历史输入根因没有修复结论。
- 两轮有限 WPF 渲染核对后收敛，目标描边增加反差边缘。最终 `visual-round-2.png` 与各阶段 artifacts 包含亮/暗目标、提示、600 × 600 主面板及设置。接触图使用审阅底色，不能当作原生桌面截图或输入证据。
- Windows 11、物理混合 DPI/拓扑/工作区/桌面变化及效率试用留在任务 02。

## 两轴审阅

固定点 `7ab18f3`，核心功能头 `696a540`，检查清理头 `d906a37`；范围为 17 个源码/检查文件。标准依据为 AGENTS、产品/Windows 计划及现有代码约定，未找到额外 CONTRIBUTING/CODING_STANDARDS。没有可用的子代理接口，按已说明的降级方式自行做 Standards 与 Spec 两轴审阅；Advisor 没有集成分支补丁正文，其意见只用于阶段约束和证据边界。

- Standards：最终限定增量中未观察到尚未修复的明确标准违例。暂存阶段发现 CRLF 引起全文差异，已按仓库现有换行规则重新暂存，补丁检查通过。最终日志核对另发现未使用测试字段警告，已在 `d906a37` 删除死字段，并以纯归档构建证明零警告。
- Spec：捕获顺序、来源上下文、完成后的焦点语义已在 red/green 回归中修正。直接与手动路径的激活、目标时点及恢复仍分开；未发现需要继续修改的已复现代码缺陷。任务 02 的实机缺口保持开放，不视为独立审查或完整验收通过。

## 保全与交付

构建前的 205 个受跟踪/未忽略文件、原分支 HEAD/索引、设置、运行 EXE 和进程身份在回填前逐项比较。回填限定为本功能 17 个源码/检查文件及本功能跟踪文档；其他原有 WIP 按基线保全，最终结果写入外部 `final-verification.json`。

集成分支保留前置快照和功能提交，原工作区只回填可审阅增量，不切分支或创建原分支提交。没有推送、PR、打包、安装或运行替换。没有创建实现子代理工作树；隔离集成工作树清理前先确认所有分支内容已提交并可由归档重建，原目录 WIP 仍在。

## 后续审查、修复与提交

- `7ab18f3..6d4c320` 的 deep 审查发现一项 P2：`placement-input` 两条新命中保护异常未包含运行器识别的 `injection refused`，设置 `PANESET_CHECKS_STOP_ON_INPUT_GUARD=1` 时仍会继续后续用例。修复仅统一这两条消息，保留原诊断原因及原有输入保护。
- 仓库外红绿诊断将目标源码中的准确异常消息交给未改动的运行器捕获逻辑，后接无输入哨兵。修复前两条消息都继续执行，修复后都中止；既有保护消息和普通异常对照符合预期。该检查证明消息分类和停机，不证明真实命中或输入路径已运行。证据位于 `D:/VibeProject/diagnostics/paneset/fix-pointer-input-guard-6d4c320/`。
- `6d4c320` 纯归档叠加上述两行修复后独立构建为 0 警告、0 错误，`pointer-placement-flow` 和 `placement-settings` 通过；没有重放实际 `placement-input` 或完整套件。
- 用户随后反馈“目前效果还可以”，并授权先提交。在 `feat/windows-two-window` 纳入完整的借位、位置预览、恢复保护、指向放置及相关规格记录；其他产品、选窗及试用文档 WIP 保留。此前“原分支未提交”的描述仅对应前一交付阶段。本次授权不包含推送、打包、安装或运行实例替换，任务 02 仍开放。
