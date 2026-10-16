Status: ready-for-agent
Progress: implemented; visual-runtime-checked; real-input-validation-pending

# 02 · 补齐单窗放置的实际位置预览

## 用户报告与根因

用户截图显示借用位置列表只有标题、屏幕名及尺寸，无法判断实际位置。同样大小、分别处于屏幕左右两侧的窗口在列表中几乎没有区别。

`PlacementWindow.BuildReferences` 只创建标题和元数据；`RefreshReferences` 只展示 `Bounds.Width/Height`，没有把 `Bounds.X/Y` 映射为图形。只有旧的历史模式 `BuildDiagram` 有空间图。此次属于参照模式缺少呈现，不是 DWM 内容缩略图失效。

## 修改

- 借用模式使用左侧窗口列表、右侧“目标位置预览”。默认预览首个有效匹配项，鼠标进入或键盘聚焦行时切换并高亮对应行。预览不代表已提交；点击/编号仍按原身份执行。
- `ReferencePositionPreview` 根据当前物理工作区和当前参照可见边界绘制屏幕框及绿色目标区域。先减工作区原点，再以同一比例缩放 X/Y，保留居中留白；无效/越界目标清空，不裁切成一个看似有效的位置。
- 每次预览使用与列表元数据相同的本次观测。筛选后保留仍可见的原项，否则预览首个有效匹配项；无匹配或参照失效清空图。候选身份及编号固定，不使用过滤后索引匹配另一窗口。
- 期望宽度为 900 DIP，继续受实际工作区限制；小于 780 DIP 将位置图放到列表上方并保留滚动。旧历史坐标图及 history-only 宿主规则保留。
- 执行器、配置、快捷键、700 ms pending、源快照及 Undo 均不修改。图表达请求目标，执行仍重新读取和校验；不保证目标应用一定接受尺寸。

## 验证

证据：`D:/VibeProject/diagnostics/paneset/placement-position-preview-03008b3-20261015/`。

- `red-reference-position-preview.stderr.log`：未修复代码成功构建，用例因 `Reference placement has no screen-position preview.` 失败。
- 永久测试：`tests/PaneSet.Checks/ReferencePreviewChecks.cs`。两扇真实受控窗口均为 1274 × 1400，分别处于左/右半屏，预览区域位置不同；悬停、聚焦、筛选和缩放后核对原生几何及位置历史没有改变。聚焦使用路由事件，不等同于实际 Tab 输入。
- 几何检查覆盖负原点、顶部任务栏的非零工作区 Y、同尺寸左右位置、竖屏、缩放重绘、越界与清空；包含有效图作为清空断言的正对照。
- 中途测试持有筛选前已移除的按钮，`TransformToAncestor` 失败；改为查询当前可视行，不修改产品代码绕过断言。
- 最终隔离构建 0 警告/0 错误，7 个选定用例均 exit 0 / 非空 PASS / unavailable 0：`reference-position-preview`、`reference-preview-geometry`、`reference-placement-flow`、`placement-flow`、`reference-placement`、`placement`、`local-layout-routing`。
- 在同一轮核对了亮色左侧、暗色右侧、窄窗口上下排列及历史入口的真实 WPF 渲染图。最终确认截图在 `final-source/artifacts/paneset-position-*.png`。
- Impeccable 检测结果单独保存；无 WPF 专用机械保证，以实际渲染和几何断言为据。

## 同类检查与保全

检查了参照列表、历史 `BuildDiagram`、主面板 `UpdatePreview` 和 B 总览 `RenderPreview` 四处：参照列表缺失位置图，本次修正；其余三处已有坐标映射。B 的窗口内容预览承担辨认任务，不套用本次单窗空间预览规则。

本轮开始冻结全部 27 个已有工作区文件、settings.json、运行 EXE 和用户进程；本轮只修改 `PlacementWindow.cs`、测试注册，新增预览组件、永久测试及本任务记录。其余已有增量保留。不提交、不推送、不打包、不切换用户实例；检查结果见 `verification.json`。

仍未完成真实按键/前台交接及物理混合 DPI、显示配置、桌面切换验收。没有效率测量或完整套件通过结论。

## Comments

- 用户明确要求能看到实际窗口位置；新增图显示工作区内的目标位置，标题及应用名继续用于辨认窗口。
