Status: ready-for-agent
Progress: implemented; native-input-validation-pending

# 01 · 冷启动借用窗口位置

## 实现

- 主面板及现有单窗动作默认打开参照窗口列表，冻结源身份；不再要求 `RecentPositions`。
- `PlacementWindow` 沿用原主题、窗口式样和输入保护，增加搜索、稳定编号、带标题的可访问名称、空状态及可选历史入口。搜索框继承主面板亮暗样式。共享按钮模板保留默认居中，允许参照列表显式铺满内容宽度。
- `WindowManager.PlaceAtReferenceAsync` 在共享工作线程读取当前参照几何和环境，通过验证回调复用 `PlaceCore`。源身份/桌面验证容许本次合法的移动与退出最大化；参照和目标环境在关键阶段保持不变。
- 旧坐标放置继续使用原历史验证回调。此前 P2 的最后守卫后标记与恢复读回修复保留，两项正式回归仍通过。
- 只修改源窗口，共享源快照、pending、结果不明及 Undo；借用不发布位置历史或最近窗口组。

## TDD 与最终证据

证据根目录：`D:/VibeProject/diagnostics/paneset/reference-placement-03008b3-20261015/`。

- `red-reference-placement.stderr.log`：旧实现可编译，新用例因缺少冷启动能力失败。永久用例随后改为直接调用类型化 API。
- `ui-red-reference-placement-flow.stderr.log`：新列表在原按钮模板中未铺满内容区域；修正模板的对齐绑定后用例通过。默认按钮布局仍居中。
- 最终三份独立工作区快照：`validation-ui-source`、`validation-native-source`、`validation-shared-source`。各自成功构建，0 警告、0 错误；输入哈希与逐项结果见相应 `*-inputs.json` 和 `*-results.json`。
- 最终合计 20 个不同检查通过，每项子进程 exit 0、非空 PASS 和 `unavailable=0`：
  - UI：`reference-placement-flow`、`placement-flow`、`ui-many`、`settings-ui`。
  - 原生与故障：`reference-placement`、`reference-placement-guards`、`placement`、`placement-guards`、`placement-undo`、`placement-failure-undo`、`placement-restore-readback`。
  - 共享路径：`placement-settings`、`screen-arrange`、`screen-guard`、`recent-group`、`recent-group-noop`、`recent-group-failures`、`maximized`、`local-layout-routing`、`shortcut-settings`。
- 新原生检查验证首次使用、参照/第三窗不被修改、当前几何区别于历史坐标、普通与最大化源跨本机两屏放置并还原，以及最大化参照。故障覆盖同窗、身份失配、越出工作区、激活失败、桌面/DPI 变化、参照移动/关闭、最大化恢复后和普通移动后参照再变、未知读回、pending 与 no-op。前台及故障观测有注入，几何操作使用真实受控 HWND。
- 新 UI 检查验证冷启动入口、固定源、取消、筛选及编号保持、11 项候选可滚动、空状态和显式历史切换。通过主面板发起整组准备操作，避免测试自身与面板争用完成结果。小尺寸、亮色、暗色与历史截图在 `validation-ui-source/artifacts/paneset-reference-*.png`。
- 主面板正常列表回归：`PICKER visible=6, viewport=332, row=54`。
- 两次长批次触及 60 秒工具预算；最终原生批次的 7 项子进程结果均已完整记录并通过，不把外层超时当作测试通过。最终证据以逐项结果及日志为准，没有重跑整套真实输入。
- Impeccable 机械检测返回 `[]`；该工具没有提供 WPF 专用结论，界面核对依据实际 WPF 渲染截图。

## 保全与剩余事项

不提交、不推送、不打包、不切换用户实例。既有文档与设置按哈希保全；测试与构建产物在仓库外。此前两项 P2 仍是当前未提交增量的一部分，不能将本次改动描述为独立干净提交。

最终 `verification.json` 中，源码/快照、20 项结果、范围、HEAD、空暂存区、既有文档与设置检查均为 true；历史 PID 135772 的存活断言为 false，因此总 `all_passed` 保留为 false。只读查询观察到当前用户实例 PID 233168，路径仍为仓库的 Release 程序。没有本轮开始时的进程基线，不能证明连续保活；不因此重启或切换实例。新观测单独保存在 `runtime-observation.json`，不覆盖失败断言。完成前 Advisor 咨询未获得补丁正文，属于范围与报告边界建议，不是独立代码正确性审查。

真实快捷键、编号选择及最终前台交接未完成验收；`placement-input` 只更新了显式历史入口步骤，未执行。历史 quick-input 调查保持暂停，不放宽任何归属或命中保护。Windows 11、物理混合 DPI、屏幕与桌面配置切换，以及实际任务效率仍待验证。

现有 `docs/PRODUCT.md`、`GLOSSARY.md` 和其他 14 份已有文档 WIP 保留原字节；本功能行为以本目录规格为新增范围依据，产品文档统一归并仍是后续文档工作。不是发布或完整验收结论。

## Comments

- 用户选择先做“借用已有辅窗的位置”，本轮未加入固定屏幕分区或历史持久化。
