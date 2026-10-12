# 02：设置入口与全局快捷键配置

Status: ready-for-agent
Progress: done

## What to build

统一设置入口，显示全局/局部作用范围、保存键位与实际状态，支持全局绑定修改、禁用、持久保存、冲突提示和恢复默认。依据[规格](../spec.md)与[实施拆分](../implementation-plan.md)。

Blocked by: None

## Acceptance criteria

- [x] 注册与配置保存采用保留旧绑定的修改事务，失败与清理异常有真实反馈。
- [x] 默认恢复、重启读取、损坏配置、重复/占用、退出释放均验证。
- [x] 设置界面及原面板在正常和最小尺寸可用。
- [x] 真实输入和合成路由分别记录，不改用户配置或移动用户窗口。

## Comments

用户明确授权按设置、A、B 三个增量推进。开始实施，先用现有 UI 动作边界验证设置入口缺失；红阶段 settings-ui 报 Sequence contains no matching element。

独立构建无警告/错误。settings-ui、shortcut-settings、configured-input 各 1 PASS / 0 FAIL；系统注入新 F19 绑定触发、旧 F17 不再触发，消息路由另验证动态注册 ID。测试使用独立临时配置；原目录旧 PaneSet 正在运行，未关闭其进程或覆盖其恢复记录。后续 A/B 添加后的完整界面与全套检查仍待完成。

当前实现四个动作的编辑/禁用/保存/默认恢复，已有绑定与磁盘状态在注册或保存失败时保留。完整 49 项中新增设置三项通过；随后增强默认 B 占用失败与释放占用后成功恢复的断言，shortcut-settings 通过。设置正常/最小尺寸和最小尺寸滚动到底部触及 B 默认按钮检查通过，截图已读取。完整结果 45 PASS / 1 既有输入 FAIL / 3 已占用 unavailable，边界见[验收记录](../../../docs/VALIDATION.md)。配置仅在临时目录验证，未改用户配置。本任务实现完成，不将其记为物理键盘或完整发布验收。
