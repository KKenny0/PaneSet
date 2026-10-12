# 稳定性收尾：本屏选择与恢复

Status: needs-info
Progress: in-progress

## 授权与范围

用户同意先完成受阻的相关验证，以及配置保存失败定位。仅使用自建 fixture 窗口与临时配置，不结束用户 PaneSet、不改用户布局、不提交发布。配置问题独立见 08。

## 本轮结果

构建路径：`C:/Users/Administrator/AppData/Local/Temp/paneset-stability-build`。Release 0 警告/错误，53 项入口中定向执行 9 项，4 PASS / 5 FAIL / 0 unavailable。没有改动生产或测试源码。记录 `artifacts/stability-<case>.log`，汇总 `artifacts/stability-results.json`。

通过：

- shortcut-settings：配置事务与错误回滚；不证明间歇替换问题消失。
- overview-size：尺寸数学检查。
- overview-size-ui：2/3/8 窗真实面板外框、搜索保留、按钮可达、取消不改源几何和 Undo，真实合成截图。
- quick-activation-failure：现有独立负向 guard 检查；不替代 Main 初次被系统拒绝激活的端到端证据。

失败：

- overview：排序断言已越过，真实点击第二张卡片后 `IsChecked == true` 等待失败（OverviewChecks.cs:134），尚未执行后续真实按钮 Enter/顺序/搜索 Enter/排布恢复断言。不能只凭同轮其他前台失败判定本项根因。
- quick-flow：测试目标被同轮 WPF 窗口覆盖，所有权保护拒绝注入。
- quick-input：执行到第二次进入的准备阶段，物理鼠标键已按下，拒绝注入；之前的 Enter 排布/Undo 断言已越过，但整项仍 FAIL。
- global-quick-input：副屏点击命中 Zed::Window，拒绝注入，未进入后续验证。
- quick-invalid：点击命中 Chrome 子窗口，拒绝注入。

运行开始前台为 Chrome，非此前 LockApp。本轮不能归因于锁屏。没有放宽 guard、释放用户按键或反复抢焦点。运行结束后停止桌面输入。

## 剩余

下述最新验证已通过真实卡片点击、按钮 Enter、全局 B 和取消保留 Undo 的相关路径。历史间歇失败根因仍未确认；下次出现时根据新增诊断区分显示就绪、原生层级、焦点及按键送达，不反复要求用户暂停输入来追求全绿。原失败记录保留。

## 本轮空闲时段补验

用户回复“继续”，允许暂停操作后补验。开始时前台 Zed、左右物理鼠标键均未按下。原构建补跑 5 项：2 PASS / 3 FAIL。

- overview PASS：此前等待失败的真实卡片点击，以及完整初始排序、按钮 Enter/前移顺序、搜索 Enter 不提交、低高度/滚动、失效成员取消、最终布局顺序和 Undo 均通过。
- global-quick-input PASS：原断言版本的真实全局 B、选择/Alt+1/Enter、Undo、再次进入和 Esc 通过。
- quick-flow、quick-input、quick-invalid：全部在 PreparePickerHost 点击主面板标题时命中同一 Chrome 子窗口（PID 64792、root 20325302、point 1117,449），保护拒绝注入。用户暂停输入后仍出现，不能简单归咎用户操作，也不能证明目标功能失败。

之后加强 global-quick-input：将 Undo 移到第二次进入并 Esc 之后，先断言取消不改变已排布几何和 HasUndo，再验证恢复原状态。Release 构建 0 警告/错误；此新版本运行在前置 Alt+1 布局切换等待处失败（ScreenSelectionChecks.cs:279），未到新增断言。保留 FAIL，原版本 PASS 不覆盖新增取消保留 Undo 验收。没有改生产激活策略、准备步骤或输入保护。

日志 `artifacts/stability-idle-<case>.log`、汇总 `stability-idle-results.json`；增强后的独立结果 `artifacts/stability-cancel-undo.log`。测试源码仅 ScreenSelectionChecks.cs 变化，生产代码未改。已停止桌面输入，不要求用户不断重复暂停操作。

## 诊断与独立检查结果

用户再次调用 ask-matt 后，继续既定诊断工作。测试侧新增 InputStateChecks.cs：WindowEvidence、SelectColumnsByInput，以及独立 quick-cancel-undo。主面板准备失败记录 WPF/native 状态；布局键记录前后焦点、实际 KeyDown/Up 和会话退出原因，finally 解除监听。日志不采集外部窗口标题或输入内容。

quick-cancel-undo 使用 fixture 前台和严格命中建立会话，以控件自动化选择布局并排布；再次进入改变选组和布局后取消，断言几何/Undo 保留并恢复原状态。它与真实键盘检查分开报告。

构建 0 警告/错误；5 个定向入口全过：quick-flow、global-quick-input（含增强的取消后 Undo）、quick-cancel-undo、quick-input、quick-invalid。证据在 artifacts/input-diagnosis-*.log，汇总 input-diagnosis-results.json、源码清单 input-diagnosis-source.json；54 项全套未重跑。只改测试和文档，生产不变。

无证据表明诊断代码修复了间歇故障，本轮未捕获新失败现场，故根因调查保持开放，不将通过等同于修复。自查 Standards：仅测试辅助与断言、无新依赖、监听释放、严格保护保留；Spec：取消/Undo 独立验证与真实键盘均通过，历史原因仍未知。无独立双代理审查。

复盘建议：保留失败点状态采集与分层用例，比反复占用桌面重跑整条路径更能定位问题；不新增全局 agent 规则或架构改造。

## Comments

### B 选择规则重构后的桌面补验

用户确认桌面可用后，基于 `c1df2d9` 的生产源码运行完整 57 项：48 PASS / 6 FAIL / 3 unavailable，约 80.90 秒，无源码运行中变更。外层退出码捕获为 null，统计使用进程输出 RESULT。全套失败分别为 overview-size-ui（fixture 意外最大化）、overview（客户区读取失败）、quick-invalid（已有鼠标键按下）、global-quick-input（第二次进入前等待前台超时）、configured-input（原子替换失败）和 ui-many（可见项期望 8、实际 0）。3 个 unavailable 为用户实例与占用快捷入口，未清理用户进程。

测试侧为 overview 加入点击事件/勾选变化及会话退出诊断，尺寸用例增加打开总览前 fixture 几何检查及宿主已关闭的提前报告。仅增强证据，不更改等待、断言或输入保护；生产代码不变。全套后的定向 overview 与 overview-size-ui 均有完整 PASS，覆盖真实点击、顺序、搜索、低高度、排布/Undo 和 2/3/8 窗尺寸。期间的失败仍保留，不能把后续通过当作根因修复。完整失败表、日志及产物版本边界见 docs/VALIDATION.md 的“桌面恢复后的补验”。本次停止桌面测试，保持 needs-info / in-progress，不宣称稳定性全通过。

这次总览尺寸已获得用户正向反馈且自动界面检查通过。稳定性收尾仍未全部完成，暂不声称可发布。

### global-quick-input 前台超时调查

用户指定 diagnosing-bugs global-quick-input。单独运行原版 6 次（4 PASS / 2 FAIL），加入阶段状态诊断后 10 次（9 PASS / 1 FAIL），未复现目标“第二次进入前等待前台超时”。两次失败为最终最大化恢复断言，一次为首次总览点击事件等待；其中带诊断的几何失败已证实准备点击后、排布前目标就变为普通窗口，不能归因于 Undo，但准备点击改变状态的机制仍未知。

临时同进程串联 quick-input → global-quick-input 在前置用例被 Chrome 命中所有权保护拦下，未到目标，随即停止桌面输入。保留测试侧边界状态采集，删除临时串联入口与 DEBUG 标记；生产逻辑、等待、断言和输入保护不变。最终构建 0 警告/错误，最终整理后未再桌面验收。证据位于 Temp/paneset-global-input-diagnosis，完整结果见 docs/VALIDATION.md。目标故障仍处于建立复现阶段，不能声称修复或关闭任务；需要下次带阶段状态的超时现场或更具体的触发顺序。

### ui-many 根因与修复

用户指定 diagnosing-bugs ui-many 后，原完整用例在 dotnet 宿主下两次稳定复现 Expected 8, got 0。最小用例确认全选后的 8 项在搜索时被筛掉；只切换启动方式的对照确认 fixture 的应用名分别为 dotnet / PaneSet.Checks，硬编码搜索 PaneSet.Checks 仅适用于后者。这是测试启动方式假设错误，不是生产选组丢失或布局时序故障。

仅将 CheckManyUi 的搜索词改为宿主实际进程名，恢复全部原流程并移除临时诊断。Release 构建 0 警告/错误；完整 ui-many 在 dotnet 和 apphost 下均 PASS、退出码 0。日志与产物独立保存到 Temp/paneset-ui-many-diagnosis，详情见 docs/VALIDATION.md 顶部。本项已解决；任务整体继续保持 needs-info / in-progress，其余配置和间歇输入失败未因此关闭。
