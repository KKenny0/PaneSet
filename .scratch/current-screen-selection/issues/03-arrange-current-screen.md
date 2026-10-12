# 03：A 本屏全部排布与共享本屏保护

Status: ready-for-agent
Progress: done

## What to build

A 使用独立快捷入口排布触发时本屏全部合格窗口，提供本屏复验、失败反馈和最新恢复；共享保护供 B 使用。依据[规格](../spec.md)。

Blocked by: 02

## Acceptance criteria

- [x] 捕获目标屏幕，确定顺序；零/一窗及过小布局修改前拒绝。
- [x] 整组快照前、逐窗修改前复验身份、来源及目标区域。
- [x] 环境改变与单窗失效报告实际影响，保留有效恢复记录。
- [x] 主副屏和普通/最大化 fixture 检查，未参与窗口不变。

## Comments

用户已明确授权该增量。依赖 02 的配置入口，不自动修改用户现有应用窗口。

实现 ApplyOnScreenAsync 显式入口，共用原操作/Undo；A 前台优先，其余 Y/X/枚举顺序。screen-guard、screen-arrange、screen-partial、screen-eligibility、screen-member-move 在完整运行中通过，覆盖双向屏幕、最大化、旧 Undo 保留、首窗间接关闭/最小化/移屏第二窗、恢复失配与返回后重试。收尾补充零/一窗反馈和几何/Undo 不变断言，screen-arrange 再次通过。真实配置 A 键验证回调，排布协调使用 fixture 候选；没有用 A 全排用户桌面。工作区变化仅元数据拒绝，物理拔屏/混合 DPI 仍待实机；详见[验收记录](../../../docs/VALIDATION.md)。
