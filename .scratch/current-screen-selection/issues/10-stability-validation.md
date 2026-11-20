# 稳定性收尾：本屏选择与恢复

Status: needs-info
Progress: in-progress

## 当前结论（2026-10-15）

本机已安排的常用流程人工验收已通过，当前没有等待用户重复执行的本机清单。任务继续开放的主要原因是 Windows 11 支持基线和混合 DPI 缺少实机记录。当前代码基线为 `eadec9b`，各项证据按实际被测版本保留。

| 范围 | 当前状态 |
| --- | --- |
| 本机人工验收 | 标题栏第一组来自 `d71f487`；标题修复、主题及受控 A/B 来自 `eadec9b`。真实全局入口、单实例、跨屏 Undo 和快捷键改绑冲突有用户通过报告；用户自测未独立采集的二进制和操作日志不补造。 |
| 最新完整自动回归 | `eadec9b`：62 PASS / 1 FAIL / 3 unavailable / 0 未运行，退出码 1。原 cross-monitor FAIL 保留；后续原 DLL 单项 PASS 和跨屏 Undo 人工 PASS 分别记录，根因仍未确认。 |
| 自动不可用项 | single-instance、shortcut-routing、shortcuts 受既有用户实例或快捷键占用影响；人工补充不修改原统计，也不要求重复已报告通过的自测。 |
| 待补环境 | Windows 11、混合 DPI。中文输入法、第三方应用最小尺寸及管理员窗口、显示配置变化按拟承诺的支持范围补验。 |
| 暂停与可选项 | 历史 quick-input：本轮未复现，暂停追查。启动前已占用快捷键是独立可选路径，未单独报告通过。 |

多窗基础任务的实现已经收尾，其剩余环境覆盖在本任务集中跟踪。真实任务收益与竞品操作比较按用户安排在另一 session 跟进，当前仓库尚无可回收结论，不属于本机验收重跑条件。当前任务与架构候选见[任务索引](../../README.md)。

以下为早期结果及逐次记录；其中的“待反馈”“宿主运行中”等描述只适用于对应记录时刻。已完成的自建人工宿主均已清理。

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

### 2026-10-15：快捷键改绑冲突人工PASS

用户“我测试通过了”，对应刚给出的四步：占用组合注册保存拒绝/原绑定与入口保留，释放后保存和实际唤起成功，恢复原设置。按用户报告人工PASS；未独立配置读回。只读未发现命令行含hold-shortcut.ps1的PowerShell/pwsh进程，没有自动关用户进程或改配置。shortcut-conflict-manual/manual-verdict.json保留范围；另附启动前冲突可选路径未单独报告，不补记通过。

本机已安排人工常用流程完成，受支持Win11/mixed DPI仍缺环境；原自动62/1/3/0与unavailable照存，Status/Progress继续保留环境缺口。无src/tests修改或新增提交。

### 2026-10-15：真实全局入口/单实例补记用户人工PASS，冲突流程工具准备

用户说“真实全局入口”和“单实例”都测过了，补记用户自测人工PASS，无另采二进制/操作日志，不称agent独立运行通过。原自动三个unavailable不修改，不要求重复已自测项。快捷键占用当前待人工：外部hold-shortcut.ps1只注册own线程Ctrl+Alt+Shift+F11，独立进程冲突1409及释放后可注册自检PASS，当前无残留；步骤检查OpenPanel改绑失败保留旧键、按钮入口、释放后保存/新键及原样恢复，另列启动时冲突。工具自检不算产品人工PASS。详见shortcut-conflict-manual/README.md。Win11/mixed DPI仍缺环境，task保持open。

### 2026-10-15：本机受控人工清单通过，清理完成，保留环境缺口

用户对最后A/B三步反馈“都正常”。A三窗排布恢复PASS；B两窗排布/报价清单不动/恢复PASS；改选择预览后Esc几何和Undo不变PASS。日志核验实际A/B列几何、两次用户保存restored=true/undo=false，取消后34个采样保持原几何/Undo=false，未见host错误。manual-ab-20261015T022333Z/manual-verdict.json及geometry-verification.json保留用户原话/机器边界。

关闭仅own45788/fixture82388并确认均退出，user135772仍活、配置SHA不变、system app light1。本轮所有own人工宿主已收尾；不再重复已通过清单。本机受控人工已完成，任务10整体尚有：自动single-instance/shortcut-routing/shortcuts不可用，真实生产全局A/B/桌面候选与Win11/mixed DPI环境缺证。原full62/1/3/0及cross FAIL保留；跨屏Undo人工PASS、clean单项PASS分开，不拼接全绿。Status/Progress仍needs-info/in-progress。HEAD eadec9b不变、两诊断文档未提交、无新src/tests改动或发布。

### 2026-10-15：系统主题与跨屏Undo人工PASS，A/B下一组待反馈

用户“跨屏Undo可以标注验收通过。我人工验收完成，都正常”，记录跨屏Undo人工PASS及刚才系统跟随/固定不随系统变化组PASS。原full66的跨屏FAIL及后续原DLL单项PASS独立保留；不认定根因或修改自动统计62/1/3/0。机器有System亮/暗、Dark在系统亮/暗样本，固定Light和各surface视觉依人工报告。没有A/B确认排布，B两次均未确认，故不扩大PASS。退出产品面板后最后保存失败是宿主身份读取不可用，不当作产品主题缺陷。

own宿主125208/fixture25164已正常清理，两PID均退出；user135772仍在、配置SHA不变、系统模式恢复light1。新manual-ab-20261015T022333Z/PID45788已启动，仅own三窗待A三窗排布Undo、B选两窗第三不动Undo、改预览后Esc不改几何/Undo。未新增源/测试改动。生产全局/单实例受用户实例占用和Win11/mixed DPI环境缺口仍保留，任务不关闭。

### 2026-10-15：完整66结束及系统/A/B人工待进行

本轮eadec9b原冻结DLL full66实际62 PASS/1 FAIL/3 unavailable/0未运行（93.96s/exit1），没有input保护ABORT。跨屏首组合Undo Success=false，原日志不含详细报告；现有用户135772及快捷键占用使single-instance/shortcut-routing/shortcuts不可用。全部source/binary/PDB/result/inventory/current源核对通过，未改产品/测试。

外部Undo观测clone先因IntPtr JSON序列化工具失败，修正primitive后单项PASS；同一正式冻结DLL另条cross-monitor也PASS。不改写full失败、不拼成全套绿。用户说刚刚关闭显示器可能相关，原full初始Y0/height1440、诊断Y40/height1400，未记录Undo时环境，故只作线索，本次未复现/未解释，不改源码。quick-input历史调查仍暂停。详见[独立证据与结果](D:/VibeProject/diagnostics/paneset/preview-heading-green-20261014T095725Z/CONTINUATION-20261015.md)。

新受控人工宿主manual-continuation-20261015T021501Z/PID125208已启动等待系统跟随/固定对系统变化及A/B完整布局恢复取消。仅own fixture/无注入和全局键，用户配置SHA相同、系统初始light1、用户PaneSet135772保留。此时own fixture有意运行，尚未人工PASS或清理。任务保持needs-info/in-progress；真实全局/受支持系统环境仍缺证据。

### 2026-10-14：eadec9b标题修复人工复验通过及own清理

用户明确“验收通过”，对应本组暗色标题可读、固定亮/暗切换、新宿主原临时主题保持；不扩大为任务10或生产App启动PASS。机器panel-opened/saved mode=Dark、paletteDark=true、系统app light=1，几何restored=true/undo=false，未采样到亮色及无A/B操作，亮色人工结论来自用户报告。

正常关闭自建194988/fixture87568，精确两PID均退出，用户配置SHA未变，系统应用主题保持初始light=1；无键鼠注入。详见manual-preview-fix-20261014/manual-verdict.json、stop.json、cleanup-verification.json。全66仍24/1保护FAIL/0 unavailable/41未跑，系统跟随/固定独立、A/B完整恢复取消及其他环境/入口待验，任务状态不关闭。

### 2026-10-14：授权暗色预览标题修复，机器绿/人工待反馈

用户授权当前session修复并复验。eadec9b（local-only）只含Expander动态Ink一行绑定与既有appearance实际模板标题断言，未改输入保护/清单/Undo/取消。正式断言先红精确捕获黑字，再绿；逐字节工作区副本强制Release0警告错误及PDB声明校验通过。实际修复DLL无运行时补色的截图对比度12.31、标题亮色。详见[修复复验记录](D:/VibeProject/diagnostics/paneset/preview-heading-green-20261014T095725Z/README.md)。

最后该版本全66是24 PASS/1保护FAIL/0 unavailable/41未运行，overview-size-ui有按键/按钮按下而拒绝点击，停止后无自动输入重试；先前另一次预检Backspace阻挡是0启动的独立记录。不拼接单项appearance或旧版通过。两份诊断文档继续未提交。

新人工宿主194988已启动、读取原临时主题设置，用户仍需点显示主面板并反馈当前修复/新进程暗色保持；保留owned fixture并非清理完成。系统主题/A/B完整恢复取消及真实全局/Win11混合DPI依然未完成，Status/Progress保持。历史间歇quick-input不重开。

### 2026-10-14：暗色预览标题人工FAIL，诊断确认未修复

用户报告暗色“布局预览”文字黑且不清楚，其他本组无问题。同一产品DLL的零目标/无全局键/无注入非激活窗口测量确认：PreviewExpander及模板标题Foreground黑，背景#1A2D3D、对比度1.49；IsDark=true/Ink亮色，编号正常。MainWindow.xaml:134缺主题Foreground绑定；临时诊断窗口Ink资源绑定使标题亮色、对比度12.31，只证实根因，不是产品修复或回归。拟议一行补丁未应用；现session限回归/人工，修复范围待用户决定。详见[诊断和可审阅提案](D:/VibeProject/diagnostics/paneset/dark-preview-d71f487-20261014/README.md)。

首次人工宿主隐藏A使用错误Automation名称，旧防护没有隐藏原面板A。为避免误触已正常关闭仅自建194688/fixture，记录保留；工具改为四个x:Name显示前隐藏且禁命中/焦点/Tab，探针实测控件状态与重建通过，修正宿主未重开。未改产品/正式测试源码，不关闭任务，不将其余未验步骤视为PASS。

### 2026-10-14：人工第一组通过

用户对标题栏拖动、窗口边缘缩放、最大化/还原、最小化、关闭收起并再显示的第一组要求反馈“都正常，目前没看到异常”，这些受控MainWindow操作记人工PASS。native-chrome/尺寸/状态日志只做支持；边界仍是最终产品DLL的独立宿主与fixture，不是生产App/全局快捷键验收。详见[人工判定](D:/VibeProject/diagnostics/paneset/manual-d71f487-20261014/human-verdict.json)及docs/VALIDATION顶部节。日志另有A调用/B确认不扩大为全部几何与恢复/取消人工PASS，主题/重启/A-B独立恢复仍待后续，最终66余36项不补记通过。未改源码、未重试受阻注入、未发布，任务保持needs-info/in-progress。

### 2026-10-14：d71f487最终回归续跑与人工准备

用户明确当前session只留回归/人工验收，后续产品规划交新session；再次授权开始本轮。工作区只有两份文档改动，C#与冻结最终源相符。08:48Z预检可用后跑全66，**29PASS/1保护拒绝FAIL/0 unavailable/36未运行**，退出1、41.08秒。quick-entry在准备点击的allVK检查拒绝，ABORT unrun36，停止后续桌面注入。overview-size-ui/overview-viewport/overview/thumbnail/mouse-routing本轮PASS。08:51只读预检按键为空，未自动重试或释放用户按键。实际source/DLL/PDB/hash核验及细项见[本轮汇总](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T080102Z/regression-summary.json)，新label final-d71f487-20261014T0848-full66。验证脚本按实际command分类全套，不再依赖固定label；只读无输入。

已准备并于09:01启动[人工验收控制台](D:/VibeProject/diagnostics/paneset/manual-d71f487-20261014/README.md)，原生标题栏/主题及fixture A/B供用户手动操作，仍PENDING反馈。宿主PID194688/fixture是有意存活，产品DLL与最终冻结一致，构建0警告错误，用户配置哈希未变，未结束用户PaneSet。无键鼠注入/全局注册，三扇fixture及临时配置隔离。A/B helper使用生产显式候选入口，不能当真实全局A/B或全桌面A验收；主题重启是新宿主进程的MainWindow构造，不是生产App单实例入口。机器观察不得写人工PASS。详细限制、人工顺序及原始系统应用主题=亮色1的还原要求见上述README与docs/VALIDATION最新节。

保护停止后的36项与人工/Win11/mixedDPI仍待验，任务needs-info/in-progress不变。没有新产品/检查源码改动、提交、推送或发布。

### 2026-10-14：正式检查输入保护与66项回归

用户决定quick-input未复现就先暂停，并授权先将逐次输入保护/应用位置断言迁入正式测试，再审计所有真实输入入口和跑全66。只改6个测试文件，产品/原60ms/原Undo及取消断言保持，时序和DEBUG不迁入；已本地提交d71f487（test: guard owned input and isolate shortcut checks），两份诊断文档继续未提交。键盘绑定检查WPF或活fixture目标并逐次校验foreground/visible/enabled/focus/Ready/allVK；鼠标保留严格命中并追加活fixture、allVK及移光标后二次命中。quick-input增加两窗应用位置、第三窗保持和4个不注入的真实入口拒绝检查。数量仍66。

初版b0b75135 quick-input PASS退出0（2.78s），全66 61PASS/2FAIL/3 unavailable/0未运行、退出1（108.82s）。2FAIL分别mouse-routing前台等待与quick-entry宿主准备超时，3 unavailable为用户实例与快捷键占用。独立只读44调用发现shortcuts读取用户配置却发固定按键的P1；本轮该项SKIP没有发危险按键。修复shortcuts/shortcut-routing使用进程唯一临时绑定、A/Bnull；旧绑定失效检查先临时独占按键，B发送前读设置UI状态。用户配置不变，日志Unavailable继续传播。17调用目标复核无新可行动问题，均正常结束和只读，源码SHA未变。

最终fb785351全66在第25项overview-size-ui发现按键/鼠标键按下状态，在注入前拒绝：24PASS/1保护拒绝FAIL/0 unavailable/41未运行、退出1（28.90s），ABORT并停止后续实际输入。首轮全66不覆盖最终41项，也不认定间歇生产故障修复。最终工作区/隔离Release0警告错误，冻结源/二进制与DLL/PDB/C#核验通过。08:03:45ZOwned0/User117480保留。任务整体保持needs-info/in-progress，人工及Win11/mixedDPI待验；本次没有发布/推送。

记录见[最终回归JSON](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T080102Z/regression-summary.json)、[初版回归JSON](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T075159Z/regression-summary.json)与docs/VALIDATION最新节。

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

### 2026-10-12：configured-input 保存失败继续调查

按 diagnosing-bugs 继续问题 08，不占用桌面。C 卷连续替换同一目标、源写完后等待 10 ms 的原生探针在 10/10 个试次捕获 1175，D 卷同条件 0/10 失败；目录根也不同，尚无卷或过滤驱动的因果结论。关联进程查询已通过受控句柄正向校验，但耗时 104–123 ms，空列表不能排除瞬时占用。

直接链接生产保存/绑定事务的 message-only 宿主构建 0 警告/错误，20/20 个试次重现相同保存失败，所有磁盘/内存/旧注册保留、暂存注册释放和临时文件清理断言通过；不是保存故障修复，也没有运行完整 configured-input。根因仍待失败时的 I/O/调用栈。系统 WPR 的自定义捕获脚本已准备并完成配置校验，实际管理员捕获尚未授权或执行。完整证据和下一步入口见[问题 08](08-settings-replace-failure.md)及 docs/VALIDATION.md 顶部；状态、历史全套统计保持不变。

### 用户批准内核取证后的问题 08 机制确认

一次管理员 WPR 捕获已保存并停止。第 14 次 ReplaceFileW 返回 1175 的现场，内部 RF 临时覆盖目标有 Raycast 后台 node.exe 成功打开的记录，早于旧配置覆盖重命名失败，相同 FileObject 的 Cleanup 晚于失败；同一重命名 IRP 返回 0xC0000022。受控 D 卷仅引入该 RF 句柄，19 次实际持有均产生同样 1175，notify-only 20/20 PASS，确认受控竞争机制。实际 ETL 不能据未观察到中间 Close 排他证明持续持有或排除地址复用。录制有大量丢失事件，不根据缺席排除其他因素或归因全部历史失败。

显式唯一备份名在隔离 C 卷 1,000 次通过，受控持有备份 20/20 通过，仍未改生产 Save，也未验证生产备份清理和其他失败码事务。问题 08 变为 ready-for-agent / in-progress，可继续实现验证；任务 10 的 needs-info / in-progress 和历史全套结果不变。完整 configured-input 与桌面全套未跑，原外层 null 退出码记录保留，诊断 runner 另修并仅 probe-only 验证。详见问题 08、docs/VALIDATION.md 顶部及稳定诊断索引。

### 2026-10-12：问题 08 修复后的完整检查

用户追加授权第 5 阶段修复、TDD 回归、审查、提交和复盘；覆盖早期仅诊断且不提交的范围。问题 08 的生产保存与清理/恢复事务已修复，独立基线同一 Save 回归仍红（迭代 7、1175、累计 7 次 RF 打开），修复版 7 个定向检查全部绿。完整 configured-input 与 shortcut-settings 均 PASS。

完整 64 项 **57 PASS / 4 FAIL / 3 unavailable**，退出码 1，约 103.48 秒。失败为 overview-size-ui、quick-cancel-undo（物理按键保护拒绝注入）、quick-flow、local-layout-keys；3 unavailable 仍与用户实例或快捷键占用有关。单独 configured-input 曾在保存成功后的前台等待超时，后续辅助日志及最终无临时日志版本通过，不据此认定激活稳定性修复。原 48/6/3 统计保留；全套后的一条契约注释和测试探针退出清理已在最新独立构建中复跑 7 项保存检查，未重复全套。

问题 08 记为 ready-for-agent / done，本任务继续 needs-info / in-progress；Windows 11 基线和完整发布验收仍待完成。没有重启用户 PaneSet、改变用户配置/布局、推送或发布。命令与证据见问题 08 和 docs/VALIDATION.md 最新节。

### 2026-10-13：overview-size-ui 单项反馈循环与当前阻挡

用户授权按 ask-matt 路线继续任务 10，先诊断 overview-size-ui 的原生前台等待超时。固定独立 checkout a0b068d；主分支开始时已是 06a5631（logo），调查中又出现其他进行中的 UI 源码改动，均未纳入诊断或修改。所有临时插桩/最小 probe 位于独立诊断目录，本次调查未改生产源码；主工作区的其他 UI 修改继续保留。

原版单项两次 FAIL，但均越过前台等待与 Ready，在 count3 过滤中、count2 恢复列表前取消会话；事后快照为外部 Chrome_WidgetWin_1，不证明外部窗口先抢前台导致取消。加入阶段记录后的两次运行，分别在 count2、count3 **复现原始 fixture 前台等待超时**，后者 count2 已完整通过。保留原等待/激活/命中/物理按钮保护及几何断言，无全套重跑。

最小化去掉 picker 后两次 PASS；只保留 picker 生命周期后第二次标题栏点击令 fixture 从普通变为最大化，捕获独立几何失败，尚未获得稳定的最小前台超时循环。无需桌面输入的消息对照不创建 PaneSet 面板：客户区双击消息不改几何，标题栏 WM_NCLBUTTONDBLCLK 产生 SC_MAXIMIZE，下一次产生 SC_RESTORE；只证明标题栏双击可以导致独立最大化模式，不解释原前台超时或等同真实输入验收。

最新真实输入运行在所有权保护处拒绝注入，阻挡 HWND 对应 PID19500；只读进程查询确认为 **LockApp**。已停止桌面注入，随后只做自建 fixture 消息对照。该证据仅解释此一轮阻挡，不归因此前超时/取消。2026-10-13T04:03:28Z 只读前台查询再次得到同一 LockApp/PID19500；继续实际鼠标与前台验证需要解锁交互桌面。

Release 均 0 警告/错误。诊断 runner 保存单项 stdout/stderr、整数退出码、补丁与程序集/源码哈希，初始未插桩两次记录另保留。原始记录、每轮失败分类及可执行命令见 [诊断索引](D:/VibeProject/diagnostics/paneset/issue10-overview-a0b068d/README.md)。任务保持 needs-info / in-progress，未进入产品修复或宣称完成；历史 57/4/3、48/6/3 不改写，quick-flow/local-layout-keys 后续诊断尚未执行，未推送或发布。

### 2026-10-13：解锁后复核及输入准备对照

用户回复已解锁后，前台为 pebrel；固定 a0b068d 的插桩 overview-size-ui 两轮及全新未插桩构建一轮均完整 PASS，2/3/8 窗、过滤前后尺寸、取消和源几何断言保留。无标题双击/最大化消息。历史前台等待/取消原因仍未确定。

原版 quick-flow 在主面板准备点击后的实际前台等待超时，local-layout-keys 在任何布局键发送前等待前台超时。普通自建 Window 的 ShowActivated 正反两序对照中，false 与 true 分支分别有/无原生 topmost 位；前台保持 Zed，无鼠标注入。此为窄路径相关性，不证明系统、WPF 或特定外部程序的底层原因；观测 probe 退出 0 仅表示观测完成。

隔离候选首次被动显示后 quick-flow 完整通过一轮；local-layout-keys 候选首轮通过三组 Alt 布局键、确认与恢复，在末尾普通字符 1 断言超时；增加末尾日志后的下一轮又在更早的前台准备处失败，未执行该日志。保留候选和失败快照，未移植主树或认定修复。

最后最小普通字符用例取得测试前台，在未合成任何键盘事件之前收到 Shift 按下消息，异步按键状态触发保护拒绝注入，因此停止真实输入；该信号不能区别人为与外部合成来源。没有释放按键、强抢前台、放宽等待或丢弃取消/几何断言。

[诊断索引](D:/VibeProject/diagnostics/paneset/issue10-overview-a0b068d/README.md)、`evidence/unlocked-summary.json` 与 `verify-unlocked.py` 保存 21 轮记录并核对退出码和 tracked 补丁哈希；最后 7 轮完整本地程序集/产品及测试源码冻结且逐文件哈希验证。此前仅有 Checks.dll 哈希、tracked diff 和部分源码记录，明确保留完整产物缺口，未补造旧记录；tracked diff 不含未跟踪 probe，由单独源码记录覆盖。

本轮主工作区只追加本文和 docs/VALIDATION.md；并行 UI/测试工作保留。临时产品定位裁剪已还原，当前诊断目录产品 diff 为空。未重跑全套或完成 Windows 11 实机验收，不改写并行工作的全套统计与历史 57/4/3、48/6/3。任务继续 needs-info / in-progress；无提交、推送或发布。

### 2026-10-13：以1a32825续接，小尺寸布局修复及当前阻挡

用户授权先处理当前UI的700×450再续输入诊断，保留已有保护/未提交记录/发布边界。现场HEAD与两文档SHA均与交接manifest一致；创建新的1a32825隔离checkout和构建，不复用a0b068d候选。

原overview在卡片click后失焦取消，未到尺寸断言。最小真实尺寸用例保留fixture受保护准备点击，使用控件自动化选择3窗；700×450、96 DPI、客户区与外框均700×450，两主按钮当次在边界内，但已选列表视口56.60 DIP低于原70要求。缩减低于600 DIP的头部padding/margin，释放18 DIP；正常高度还原，文字/按钮/原断言及生产激活、选择/取消/排布/Undo不变。

相同250ms前三档路径修复后700×450为74.60 DIP，后续缩放失焦整项仍FAIL。新增viewport布局检查改为ContextIdle/UpdateLayout并读回原生尺寸，完整1 PASS、退出0，亮/暗两主题、三档尺寸、599→600、回450/900及取消几何/Undo通过；截图为WPF。其未修复版本同一idle复跑被LockApp阻挡，不算布局红测。原历史按钮失败尚未本轮复现或关闭。

当前quick-flow完整PASS；local-layout-keys在首次真实前台等待超时，未发送任何键。测试侧新增每拍局部键注入的foreground/focus/已按键按钮保护，记录自建主窗文本/选区、前台HWND/PID/类、修饰键/async状态、自己的键/char/激活消息及SendInput返回数，finally解除hook。诊断键路径本轮未执行，不能称输入根因修复；未移植旧ShowActivated候选。

最终冻结Release0警告/错误，14项无需键鼠注入的相关回归全部PASS，每项退出0：ui/settings-ui/overview-size、三项选择规则、7项保存契约及shortcut-settings（主题/快捷键互相保留）。迁回主区六个文件逐字节等于隔离树，主区独立重建0警告/错误；最终产品与已通过viewport/quick-flow的产品一致，测试末次只增强诊断。

07:55:34Z真实准备命中PID19500，07:56:11Z只读确认为LockApp，随后停止所有桌面注入。原overview最终全流程、appearance、overview-size-ui、local-layout剩余链与完整66项未补完；Windows系统真实主题切换、标题栏鼠标、Windows11仍待验。不改历史61/1/3、57/4/3、48/6/3，保持needs-info/in-progress。

[本轮27轮证据与完整限制](D:/VibeProject/diagnostics/paneset/issue10-1a32825-current/README.md)、summary.json、verify.py及RETRO.md保存来源、整数退出码和复盘。源/程序集/补丁逐文件SHA核对通过；初期tracked diff不含未跟踪probe，完整来源需看sources；新runner增加source集合/构建/启动产物匹配及运行后漂移检查。Standards/Spec同会话人工核对，无独立双代理审查，Advisor未获完整差异/日志。

本地提交037e216（fix: preserve overview selection space in short windows）只含六个本轮源码/测试。原两份未提交文档及本轮追加保留未提交；未动用户实例、设置或布局，未推送、发布或新增提权捕获。

### 2026-10-13：用户解锁后的续验与点击准备修复

解锁后037e216对应冻结产物的overview-size-ui、appearance和local-layout-keys完整PASS，退出0；三种Alt布局/Enter排布恢复与末尾普通1都执行，搜索文本为1。overview-viewport本轮一次fixture前台等待超时；原overview在前台保持时真实卡片click失败。

最小时间线明确计算坐标时目标卡片measure无效、原点680,741，事件时移至1266,461，旧点击落在别的卡片；原生down/up早于恢复光标，排除这两轮的恢复过早解释。仅修测试ClickOwned：UpdateLayout及measure/arrange有效前置，再取屏幕点。保持原命中/已有按钮保护、实际鼠标、等待/选择/尺寸/Undo断言，不改生产点击或激活。最小红绿、带时间线overview以及清理后原overview完整通过；清理后mouse-routing与quick-flow亦PASS。

最终quick-input在第二次进入前准备的前台等待失败；global-quick-input已有鼠标按钮按下，保护拒绝注入，停止后续真实输入。未启动完整66项。诊断runner启用可选保护停止策略、报告未运行数量；无注入两项合成对照证明启用时停止于首个保护失败、后一哨兵未执行且unrun=1，默认策略继续执行哨兵。预期退出1不当产品FAIL。全量插入成功的残留error5不解释为失败，新日志成功为0，实际失败码处理不变。

本地提交53a11e4只含三个测试文件。最终生产DLL逐字节等于037e216；临时鼠标及合成哨兵源码已清理，7份迁回源码与隔离树一致，Release0警告/错误。43轮证据重算SHA/退出码通过，原历史结果和不同源码/产物边界保留；12:51:40Z只读确认本诊断进程0、用户PaneSet117480保留。

Standards/Spec同会话人工核对，无独立双代理审查；Advisor仅摘要/可见差异建议。完整66、其余输入链、真实Windows主题/标题栏鼠标和Windows11待验，任务保持needs-info/in-progress，不认定间歇前台根因修复。原两份诊断文档及本轮追加不提交；没有push/release、用户配置/布局变更或新增提权。详见诊断README、summary.json及RETRO.md。

### 2026-10-14：独立审查P2修复

1a32825...53a11e4独立双轴审查确认局部输入的通用catch将UnavailableException包装为普通Exception，改变runner的SKIP/unavailable分类。157ec7d仅增加catch filter排除UnavailableException；清理正常完成时原类型继续传播，普通断言/前台等待/保护失败仍附原诊断并FAIL，输入保护和生产不改。

主区和隔离Release0警告/错误，七份迁回源码逐字节一致，43轮历史证据哈希核对通过；大多数旧退出码只校验整数类型，两组guard对照另断言预期退出1。旧14项PASS不是新构建重跑。53a11e4...157ec7d两独立只读进程复审Standards/Spec均无新增问题。现有真实local入口没有安全的无输入负向测试边界，本次不新增只为测试服务的hook/helper；仅构建和静态传播复审，未发送键鼠、未验证实际SendInput不可用路径、不声称TDD红绿。

两项非阻断维护建议保留，详见[P2处理复盘](D:/VibeProject/diagnostics/paneset/review-fix-unavailable-53a11e4/RETRO.md)及[复审](D:/VibeProject/diagnostics/paneset/review-53a11e4-157ec7d-20261014T023613Z/REVIEW.md)。任务继续needs-info/in-progress；完整66、剩余输入及系统主题/标题栏/Windows11待验。仅一行测试代码本地提交，原两份诊断文档及追加未提交，不push/release、改用户配置布局或新增提权。

### 2026-10-14：开始任务10续验，输入边界与诊断来源修复

用户授权开始实施任务10；HEAD157ec7d及原两份未提交记录保留。初始只读预检Default桌面发现Ctrl/左Ctrl按下，先不发送输入。执行本版11项不创建可见窗口/不注入检查（总览尺寸、刷新/资格/顺序及7项配置事务），全部PASS/退出0、源212fec9e及Checks21e33f固定。后续按键清空时仅执行quick-input：失败在第一次第二个Enter确认等待（246），未到历史第二次进入准备失败；计数完整不等于按键交付。

隔离窄Enter原生/WPF/Completed记录未改等待、断言或保护，sourceef151/Checks60117一轮完整PASS，确认两个Enter及第二次进入Esc；不能据一次通过归因或修复。临时记录已归档/从活动源移除。再试时所有权保护发现Explorer ToolbarWindow32，拒绝任何键鼠注入，随后停止全部真实输入；global、viewport及完整66未启动。被动空目标面板的8个采样见WPF/native topmost一致、原前台不变，仅无输入观察，未重现失配/解释历史前台。

发现copy2还原旧mtime后增量构建仍用诊断Checks60117，却记录clean源212fec9e；该次task10-157ec7d-clean-quick-input保留原始记录，单独provenance-exclusions.json从clean源行为验收排除。DLL/portable PDB匹配及源码checksum真实红证实ScreenSelectionChecks.cs失配；强制Rebuild后25个项目C#文档均匹配、Checks恢复21e33f。修外部build/runner：先失效旧状态、成功重建与来源核对后原子发布、实际冻结DLL/PDB与源码校验后才运行。PDB范围不包括资源、生成输入、依赖或编译配置。

独立工具复审原Spec发现Python优化移除assert门槛和遗漏普通C#文档误接受；已在隔离复制环境用无输入命令证明并修为显式require/双向源码集合、明确生成目录名称排除和根外普通C#拒绝。正常/-O六种坏状态/缺proof/校验器失败均拒绝，OS进程审计实际case启动0；完整来源以及最终-O安全pipeline PASS。59轮归档重查58个DLL/PDB/C#来源匹配、1个已排除，不是59个PASS；旧记录未改写。

没有新的生产或仓库测试源码修改/提交；仅原两文档追加未提交，外部诊断工具和复盘保留。任务仍needs-info/in-progress；真实SendInput不可用负向、间歇Enter/准备失败根因、完整66、系统主题/标题栏/Windows11/mixed-DPI待验。用户PaneSet117480保留，无push/release/用户配置布局修改/提权。当前入口与来源说明见诊断README、provenance-exclusions.json和task10-pdb-audit.json。最终独立工具双轴复审正常结束、快照SHA一致，Standards/Spec均无新增问题；报告见[REVIEW.md](D:/VibeProject/diagnostics/paneset/task10-tooling-review-20261014T035407Z/REVIEW.md)，本轮复盘见RETRO-task10-20261014.md。复审只覆盖工具边界，不关闭产品输入根因。

### 2026-10-14：用户指定diagnosing-bugs quick-input

只推进Phase1反馈循环，不依据旧日志猜原因。新增外部quick-input-loop.py：预检每轮Default/LockApp/按键，最多3轮顺序，只在保护允许时沿用冻结来源run.py；首非PASS停。PASS核对完整单case终局，真实源码/PDB/二进制映射与冻结方法边界定位CONFIRM_TIMEOUT，准备/所有权拒绝独立归类。04:21:57Z实际命令trials3被LockApp阻塞：requested3、started0、confirm-stage0、runner/child_started=false、exit2；未发送输入，没有新真实case归档。

归档只读重验/分类：当前原版确认超时=>CONFIRM_TIMEOUT，旧版第二进入失败=>PREPARE_FAILURE，窄日志版=>PASS；均独立源/产物并标archive_only/new_bug_run=false，不能作新复现率。此前全文件grep发现相同confirm语句在global用例出现，现分类仅冻结CheckQuickInput方法范围，历史确认分类已实际验证。无生产/test源码修改、不自动解锁/抢焦点/释放用户键。Phase1完成条件尚不满足，需要用户提供解锁交互桌面后继续受保护命令；不进行根因假设。入口见外部quick-input-loops/README.md。

用户解锁后，2026-10-14T06:48:19Z起实际受保护原版quick-input三组各3轮共9轮完整PASS/退出0，均当前source212fec9e/Checks21e33f，完整Enter/Undo/二次Esc保持原断言，无来源漂移。第四组工具被取消、未创建额外组或记录，07:01:24Z自建诊断进程0、用户117480保留。68份归档重新核验67来源匹配/1原明确排除，不是68PASS。当前数据见quick-input-unlocked-20261014.json；9/9green未捕获历史confirm失败，Phase1尚未满足红循环/高复现率，不认定根因或修复。

用户要求重试Advisor：工具返回executor/advisor相同已禁用，没有新的Advisor指导；另起独立只读策略审阅（无执行/输入权限）评估Phase1触发变化，保持保护与生产/test代码不变。

独立只读策略审阅正常结束（51只读调用、快照三C#一致），指出之前描述过宽：原SendKeys等待60ms（不是40），Space/Enter/Esc无逐次前台或按键保护；原quick-input只有排布前不动与Undo后几何，未断言Undo前应用位置。原9轮通过不补足这些检查。未据此推断生产根因。

新建明确诊断副本quick-input-timing-157ec7d：产品字节保持，测试Space/Enter/Esc各次增加ready/foreground/focus/allVK保护，Alt前allVK保护，追加应用后两窗位置和第三窗保持；与控制相同条件保留所有原断言。sourcef3ec8362/Checks1385d418，原9轮与新副本分别计算。只第一次Enter发送后的等待参数化，seed157固定60(control)/1/8/20ms，其余默认60、原Until和Dispatcher/Undo/二次Esc不改；每轮预检、首非PASS停。07:13:22Z实际4条件完整PASS，逐冻源/二进制/PDB证明及结果SHA核验通过，活动观察前缀DEBUG-quick-timing只在诊断目录，未迁入仓库。仍无本轮confirm red，Phase1未完成，不进假设/生产修复。07:16:13ZOwned0、用户117480保留。脚本入口与边界见[诊断README](D:/VibeProject/diagnostics/paneset/quick-input-timing-157ec7d/README.md)。
