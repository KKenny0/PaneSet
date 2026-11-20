# PaneSet 当前任务索引

更新：2026-10-30。产品源码基线：`fd09097`（v0.3.0-preview.1，已发布）。规格与历史记录仍保存在各功能目录，本页用于查看当前状态。

## 当前跟进

| 事项 | 状态 | 下一步与依据 |
| --- | --- | --- |
| 本机常用流程人工验收 | 已通过 | 已完成安排的清单，不重复要求用户操作。被测版本、用户自测与机器证据的区别见[验收记录](../docs/VALIDATION.md)。 |
| Windows 11 与混合 DPI | 等待环境 | [任务 10](current-screen-selection/issues/10-stability-validation.md)保持 needs-info / in-progress。中文输入法、第三方应用尺寸约束及管理员窗口、显示配置变化按支持范围补验。 |
| 真实任务试用与竞品操作比较 | 方案与记录表已准备，实际操作待执行 | 采用同应用两窗、浏览器/PDF/编辑器三窗、排布后临时使用新窗三项任务；比较现行办法、PaneSet 和 Windows Snap。当前仍无完整耗时或竞品实测，见[试用方案](real-task-trial/trial-plan.md)与[结果记录](real-task-trial/results.md)。 |
| 当前版本试用包 | `eadec9b` 本地包已核验，未切换用户实例 | 从明确提交的独立源码构建；解压哈希、元数据及 WPF 探针通过，只证明包完整性与启动。`54ed165` 旧包保留；见[试用说明](../docs/TRIAL.md)。 |
| 单窗放置（拖动投放） | 已实现并发版（fd09097）；人工验收进行中（16 项过 4） | 按住辅助键（用户现为 Ctrl+Shift）拖动标题栏投放，取代已移除的指针参照放置与位置图选择器。见[规格](single-window-placement/spec.md)、[人工清单](single-window-placement/issues/01-manual-acceptance.md)与[验收记录](../docs/VALIDATION.md)。 |
| 历史 quick-input 间歇问题 | 暂停调查 | 本轮未复现，根因未确认。输入保护保留，不以之后的通过记录认定修复；见任务 10。 |
| 启动前快捷键已占用 | 可选补验，未单独报告 | 与已通过的“修改绑定时发生冲突”分开记录，不列为用户必须重复的既有清单。 |

最新完整自动回归（v0.3.0-preview.1 最终树，2026-10-27）：三次全量出现轮换的输入类失败（仓库已记录、暂停追查的间歇类，证据指向用户会话活动与检查进程的前台竞争），全部失败项同树逐个单跑通过；当日早树另有 71/71 全量通过，不来自最终树。0.2.0 时代（eadec9b）的 62 PASS / 1 FAIL / 3 unavailable、cross-monitor 后续单项与跨屏 Undo 人工通过均保留为历史，人工补充不修改自动不可用结果。详见[验收记录](../docs/VALIDATION.md)末节。

## 已实现或已被替代的任务

| 任务 | 当前结论 |
| --- | --- |
| [两窗原型](windows-two-window/issues/01-arrange-and-restore.md) | 已完成。 |
| [跨屏、多窗与快捷入口](multi-window-controls/issues/01-displays-layouts-shortcuts.md) | 实现完成；剩余环境与稳定性覆盖统一在任务 10 跟踪。 |
| [大列表与局部布局键](multi-window-controls/issues/02-picker-and-layout-keys.md)、[最近组](multi-window-controls/issues/03-recent-window-group.md) | 已实现；实际收益仍待真实任务试用。 |
| [网页选窗原型 01](current-screen-selection/issues/01-quick-selection-prototype.md) | 被后续原生方案替代，按 wontfix / superseded 归档；旧逐项人工体验未完成，不补勾。 |
| [设置 02](current-screen-selection/issues/02-shortcut-settings.md)、[A 与本屏保护 03](current-screen-selection/issues/03-arrange-current-screen.md) | 已实现。 |
| [原生标记选窗 04](current-screen-selection/issues/04-native-quick-selection.md) | 前轮已实现，呈现由统一总览替代。 |
| [旧漏标记问题 05](current-screen-selection/issues/05-missing-selection-marker.md) | 随标记呈现替代而归档，原根因未确认；当前候选问题另按当前实现记录。 |
| [总览方向 06](current-screen-selection/issues/06-overview-selection-direction.md)、[原生总览 07](current-screen-selection/issues/07-native-window-overview.md) | 方向已确认、实现已完成。 |
| [配置替换失败 08](current-screen-selection/issues/08-settings-replace-failure.md) | `a0b068d` 已修复保存路径并完成相关回归；旧包没有该修复。 |
| [面板初始尺寸 09](current-screen-selection/issues/09-overview-fit-size.md) | 已实现，后续短窗口选择空间修复为 `037e216`。 |
| [指针借位 01–02](pointer-window-placement/issues/01-pointer-placement.md) | 曾实现未发布，随 fd09097 移除，由拖动投放取代；按 wontfix/superseded 归档。 |
| [参照借位与位置预览 01–02](reference-window-placement/issues/01-borrow-current-window.md) | 同随 fd09097 移除归档；旧人工验收结论失效，不再作为产品行为证据。 |

Status 使用项目五个分诊标签；Progress 单独记录进度。superseded 表示原目标或呈现已被替代，不表示旧验收全部通过或旧缺陷根因已修复。

## 架构评审的后续状态

2026-10-12 的评审基于 `54ed165`，建议按当前实现重新判断：

| 候选 | 当前状态 | 后续处理 |
| --- | --- | --- |
| B 总览选择规则独立 | `c1df2d9` 已完成 | [OverviewSelection](../src/PaneSet/OverviewSelection.cs)集中成员、顺序、失效状态与确认资格。 |
| 排布请求与最终结果协调 | 尚未提取，可选维护 | [主面板](../src/PaneSet/MainWindow.xaml.cs)仍协调等待、忙碌状态和最近组更新。修改相关逻辑，或出现旧请求覆盖新状态、取消后错误更新最近组、忙碌状态不释放等故障时再评估；这些是重新评估条件，不是当前已发现的缺陷。 |
| 真实输入测试前置条件集中 | 关键保护已落实，完整模块化可选 | `d71f487` 及前序修复已补齐目标归属、前台、焦点、按键状态与严格命中保护。准备、等待和诊断仍分散在测试文件中，维护需要出现时再整理。 |

后两项继续作为建议保留，本次没有新增架构实施任务或产品行为变更。
