# 单窗放置（拖动投放）：实施记录

Status: ready-for-human
Progress: implemented, checks passed

基线：`47cb48bff7c830c467c3268568a08ab2e13e7756`。本文件记录拖动投放的实施；旧"位置图/指针参照放置"的实施记录保留在 git 历史（提交 03008b3、47cb48b 的版本），对应代码已随本轮移除。

## 交付内容

按 [spec.md](spec.md) 实施，交互决定见 [interaction-notes.md](interaction-notes.md)（Q1–Q16）。

- `LayoutEngine.Zones`：五种布局模板（左右两栏、三等分、主辅、上下两栏、2×2 网格）在任一工作区的整数分区；上下两栏为新增的行分区。
- `DragPlacementStateMachine`（DragPlacement.cs）：纯交互状态机——进入投放（按住辅助键 + 左键拖动 + 非缩放）、指针命中分区、缩略图划过换模板（粘滞）、取消（Esc/右键永久取消）、提交判定（必须观察到左键释放且结束时左键已松开、辅助键仍按住、命中分区、无取消、无缩放、起手为鼠标拖动）。不确定一律不投放。
- `DragPlacementMonitor`（DragPlacementMonitor.cs）：原生观察层。WinEvent（移动/缩放循环开始/结束）界定会话；会话内安装低级键盘/鼠标钩子记录 Esc、右键、左键释放（忽略注入事件）；15ms 轮询辅助键（与配置掩码精确相等，应用自身的组合不受影响）、指针、所在屏；跨屏换屏时按新工作区现算分区与缩略图。
- `DragPlacementOverlay`：置顶、分层、点击穿透的纯视觉层（沿用放置反馈层的窗口样式）：分区描边、悬停高亮、顶部居中五模板缩略图；不取焦点、不消费输入。
- `WindowManager.TryBeginDragSession / PlaceDraggedAsync / IdentityAlive`：会话起点捕获拖动前 `WINDOWPLACEMENT` 与状态；执行复用 PlaceCore（可见边框换算、读回验证、最大化先还原、pending 互斥），恢复快照注入拖动开始时的数据——恢复回到拖动前，而非松手前的中间位置。
- 设置：新增"拖动投放辅助键"（默认 Shift，可选任意修饰键组合，按住检测不经全局注册）；`ShortcutAction.PlaceWindow` 移除；旧配置中的 `placeWindow` 键逐字段解析跳过，其余绑定、外观、拖动键全部保留。
- 移除：PlacementWindow（位置图选择器）、PointerPlacement（指针参照放置）、ReferencePositionPreview、最近成功布局位置记录（recentPositions）及 PublishPositions/PlaceAsync/PlaceAtReferenceAsync 全链路；主面板"选择其他位置…"按钮与源窗口跟踪移除。

## 与规格的偏差

- 投放由任意移动/缩放拖动 + 按住辅助键触发，不区分标题栏：缩放拖动因窗口尺寸变化被状态机排除（最大化窗口拖离标题栏的尺寸变化除外，属预期投放路径）。
- Esc 取消依赖低级键盘钩子观察，而非仅结束时按钮电平：顾问复核指出 WinEvent 回调延迟会造成“Esc 后松开”被误判为提交，已按“全程记录取消原因、不确定不投放”实现。
- 主键按 SM_SWAPBUTTON 映射到物理按键读取（GetAsyncKeyState 与低级钩子看到物理层，交换在其上）；鼠标事件不按注入标记过滤（驱动级按键重映射会注入真实输入），副键取消由“结束时主键仍按住”覆盖。
- 监视器内置 PANESET_DRAG_DEBUG=1 环境变量诊断日志（临时目录 paneset-drag-debug.log），默认关闭。

## 人工验收首轮修复（2026-10-27）

用户交换主次键环境实测发现四处问题并修复：主键交换映射（从不进入投放）、覆盖层缺少 HWND_TOPMOST+SWP_SHOWWINDOW 定位（分区不可见）、鼠标注入事件被过滤器丢弃（松手检测不到）、模板缩略图未填满（改 Viewbox）。详见 docs/VALIDATION.md 与人工清单。

## 检查

已注册 `drag-placement-zones`、`drag-placement-state`、`drag-placement`、`drag-placement-settings`、`tray-presence`（与托盘规格共用）；旧 pointer/reference/placement 系列检查随功能移除，共享回归保留。最终隔离全量 71 通过 / 0 失败 / 0 跳过，记录见 docs/VALIDATION.md；运行中还修复了 OpenForForeground 对隐藏窗口不显示、StateChanged 同步改状态导致原生窗口卡 iconic、全局开关键仍用旧最小化三处产品问题。原生拖动环路的手感、贴边竞争、覆盖层视觉属人工验收，未运行。
