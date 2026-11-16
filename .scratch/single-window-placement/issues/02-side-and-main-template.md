Status: ready-for-agent
Labels: ready-for-agent

# 辅主模板（左 1/3 + 右 2/3）：翻案收录

## 背景

首版规格将"反向模板变体（如 1/3+2/3）"列入 Out of Scope。2026-11-13 用户以"布局方案对称闭环"为由推翻该取舍：目录中左右两栏、三等分、上下两栏、2×2 均为对称模板，唯主辅缺镜像，左置聊天/参考窗、主工作区靠右的用户无法一步投放。

## 范围

仅拖动投放的 `ZoneTemplate` 目录新增 `SideAndMain`；多窗排布 `ArrangementKind`（一主多辅）的对称性问题不属本任务。

## 实施（已完成，2026-11-13）

- `LayoutEngine`：枚举插入 `SideAndMain`（值 3，原 3–4 顺移；无持久化，无兼容影响）；新增 `Mirror`——翻转 X 坐标并反转分区顺序，保持空间从左到右枚举（与既有约定一致）。
- `ZoneTemplateCatalog`：目录第 4 项，标签"辅主（1/3 + 2/3）"，紧邻主辅。
- `DragChecks`：数量 5→6；镜像断言（宽度不变、顺序互换、宽度守恒，含奇数宽与负原点屏）；`ZoneTemplateCount` 增补。
- 文档同步：PRODUCT.md、spec.md（含 Out of Scope 翻案注记）、implementation.md。

## 验证

- `drag-placement-zones` PASS（Debug 构建，2026-11-13）。
- `drag-placement` 环境性 FAIL：本机前台被系统 `Windows.UI.Core.CoreWindow` 占据（quick-input 间歇问题同源），与本改动无关；实机验收时受控环境复跑。
- 缩略图条 6 槽逻辑宽约 674 px @96dpi，2560 宽屏可容纳；窄屏（<730 px 工作区）居中计算会左移贴边，视觉验证留人工验收。

## Comments

- 2026-11-13 用户确认翻案，当场实施。
