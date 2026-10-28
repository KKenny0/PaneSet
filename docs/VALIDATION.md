# Windows 多窗原型验收

## 当前验收状态（2026-10-15）

当前产品源码基线为 `eadec9b`。本机已安排的常用流程人工验收已通过；标题栏第一组来自 `d71f487`，后续标题修复、主题及受控 A/B 来自 `eadec9b`。用户自测真实全局入口、单实例、跨屏 Undo 与改绑冲突的范围和采集限制仍按各条记录保留。

最新完整自动回归是 **62 PASS / 1 FAIL / 3 unavailable / 0 未运行，退出码 1**。原 cross-monitor FAIL、后续正式单项 PASS 和人工 PASS 分别保留，根因未确认；不拼接成全套通过。single-instance、shortcut-routing、shortcuts 三项自动不可用结果不因人工报告改写。

[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)继续开放，主要等待 Windows 11 与混合 DPI 实机记录；其他环境按支持范围补验。历史 quick-input 调查暂停，启动前快捷键已占用路径尚未单独验收。当前任务、已替代项与可选架构维护见[任务索引](../.scratch/README.md)。

以下为按次保存的历史证据；其中“待反馈”“宿主运行中”等状态只适用于对应时刻。本次文档整理没有运行新检查或生成新包。

## 快捷键改绑冲突人工通过（2026-10-15）

用户对上一条四步流程反馈“我测试通过了”。记录人工PASS：被外部占用的测试组合保存失败并保留原绑定/启用状态及可用入口；占用器释放后同组合保存与实际唤起成功；最后恢复原设置。恢复按用户完成流程报告，未独立读回其配置。只读进程核对未发现命令行含hold-shortcut.ps1的PowerShell/pwsh进程；没有自动退出进程或修改用户设置。详见[人工判定](D:/VibeProject/diagnostics/paneset/shortcut-conflict-manual/manual-verdict.json)。

本次通过范围为修改绑定时的外部冲突；README另附的“启动前已占用”路径未单独报告，不扩展为该路径本次人工PASS。连同用户此前自测真实全局入口/单实例及本机受控人工清单，本机已安排常用流程人工验收完成；受支持Windows11/mixed DPI仍缺环境记录，任务10继续保留环境覆盖缺口。原full66的62/1/3/0和三个unavailable不因人工结果而改写。无产品/正式检查修改、无新提交/发布。

## 用户补充真实全局入口与单实例已自测通过（2026-10-15）

用户明确“真实全局入口”和“单实例”都测过了，按当前对话语义补记两项用户人工验收PASS；未另行采集其被测进程/二进制/逐步操作日志，不表述为agent独立运行通过。原full66 single-instance/shortcut-routing/shortcuts三个unavailable记录保持。用户报告已覆盖的路径不再列为待其重复人工验收；Windows11/mixed DPI仍缺环境。

用户询问快捷键占用如何测，已按当前SettingsWindow/PanelShortcuts行为准备[只占用F11组合的临时工具与步骤](D:/VibeProject/diagnostics/paneset/shortcut-conflict-manual/README.md)。Ctrl+Alt+Shift+F11占用器自检成功：独立进程同组合被拒绝1409，释放后独立进程可注册并释放；无输入注入/窗口操作/配置写入、无持续占用留下。这只是工具自检，PaneSet本次人工冲突流程仍PENDING。步骤只改OpenPanel，检查注册失败保留原值/原键、按钮可用、释放后新键保存/实际唤起、最后原样恢复；另附启动时冲突路径，需由用户自行退出/重启。未自动操作用户程序或改配置。无产品/正式检查变更。

## 本机受控人工清单全部通过及收尾（2026-10-15，eadec9b）

用户对刚发出的A恢复、B两窗排布/第三窗不动/恢复、B改选择预览后Esc取消三步反馈“都正常”，三项人工PASS。本机受控人工清单完成：标题栏/系统按钮（先前d71f487）、固定主题/标题修复/新MainWindow主题保持、系统跟随与固定模式对系统变化、跨屏Undo用户明确接受，以及本组受控A/B。按版本与实际路径保留，不把历史标题栏版本结果当新版本全流程实测。

本组机器支持：02:25:54 A三窗列布局实际宽845/845/846、gap12、工作区Y40/height1400；02:26:06用户保存恢复三窗原始位置大小，restored=true/undo=false。02:26:23 B确认后两列实际1274/1274、gap12，第三窗仍原状态；02:26:29保存恢复完整初始状态/Undo=false。02:26:44 B未确认取消后34个被动样本几何保持原样、Undo/pending=false；用户确认改变选择和预览后Esc正常，机器只证明采样时刻状态，不声称连续全时刻未变。无action/cleanup/observation错误。详见[A/B人工反馈与几何核验](D:/VibeProject/diagnostics/paneset/manual-ab-20261015T022333Z/manual-verdict.json)。

按准确控制台PID/HWND/标题正常关闭自建45788，fixture82388也退出；cleanup-verification确认两PID无剩余、用户PaneSet135772仍活、用户settings SHA不变、系统应用主题仍初始light1。全部本轮自建人工宿主已清理（194688、194988、125208、45788各自收尾记录）；没有自动退出用户进程或键鼠注入。

自动全套最新仍62 PASS/1 FAIL/3 unavailable/0未运行，exit1，原cross-monitor FAIL保留；用户跨屏Undo人工PASS及后续正式单项PASS独立，不重写为全66绿。三个不可用为single-instance/shortcut-routing/shortcuts（用户实例/全局键占用）。受控宿主不证明生产App/真实全局A/B/桌面候选范围，受支持Win11、mixed DPI仍未验。task10保持needs-info/in-progress，仅待这些剩余覆盖，不继续重复已通过人工清单或追查暂停的间歇quick-input。

修复local commit eadec9b不变，本轮仅追加两诊断文档、外部证据及handoff，未新增产品/正式测试修改、未push/发布。

## 人工系统主题与跨屏Undo通过（2026-10-15，eadec9b）

用户明确“跨屏Undo可以标注验收通过。我人工验收完成，都正常”。跨屏Undo验收结论记人工PASS，同时保留原full66 cross-monitor FAIL和随后正式单项PASS为独立记录，不宣称根因修复或把full改记绿。本次“都正常”对应上一轮发出的系统跟随/固定不随系统变化五步清单；主面板、设置、B外观人工PASS，不扩展成未发出的A/B排布恢复取消组。

宿主机器实际采样System/light1→palette亮、System/light0→palette暗，以及Dark/light0及light1→palette暗；固定亮色及各surface视觉结论依据人工报告，非所有机器样本。两次B均未确认，不能推断两窗排布或Undo；退出主面板后最后保存因manager已关闭身份不可读而报观察错误，不是产品主题FAIL，也没有有效最终保存几何结论。

只关闭自建控制台125208，其fixture25164也退出；cleanup-verification确认两PID无剩余、用户PaneSet135772仍在、用户配置SHA不变、系统应用模式已恢复light1。[系统组人工结果/清理](D:/VibeProject/diagnostics/paneset/manual-continuation-20261015T021501Z/manual-verdict.json)保留用户原话与证据范围。

已打开新[受控A/B验收宿主](D:/VibeProject/diagnostics/paneset/manual-ab-20261015T022333Z/README.md)，02:23Z/PID45788，仍用同一正式Product84f18b0e和own fixture，Release0警告错误/validate-only通过，user config SHA相同/system light1。等待A三窗排布恢复、B两窗排布第三窗不变及恢复、B改选择预览后Esc几何/Undo不变的逐项人工反馈；有意保留own宿主，尚未完成此组。用户真实全局实例/热键不自动关闭，Win11/mixed DPI仍缺环境，task10保持open。无新产品/正式测试修改或提交。

## 继续最终验收（2026-10-15，eadec9b，全66结束62/1/3/0）

用户授权继续后，同一冻结修复版本次完整66项实际运行02:08:55Z–02:10:29Z/93.963156s，62 PASS/1 FAIL/3 unavailable/0未运行，exit1，没有输入保护ABORT。旧label含20261014T1020而非执行日期；source834dee89/Checks23383d4c/Product84f18b0e/PDB2970accf与此前修复版一致，全部冻结source/binary/PDB/result/inventory及当前工作区源已核实。源码和二进制分别对各自快照比较。此前41未运行只属于旧一轮，本次没有未运行项。

FAIL仅cross-monitor：首个正向普通窗组合UndoAsync().Success断言false，原日志没有Pending/具体窗口结果，原因未确定。unavailable分别是single-instance（用户现有实例）、shortcut-routing/shortcuts（全局键占用）；现用户PaneSet135772保留，不自动退出/解除热键。其他62通过，包括quick-input/global-quick-input及标题appearance，历史间歇问题继续暂停。

随后仅外部test clone加Undo报告/几何观测，首次IntPtr JSON序列化失败单列工具错误；修正primitive日志后一条诊断cross-monitor全部原断言通过。用户补充“跨屏的问题可能和我刚刚关闭了显示器有关”。原始full初始workarea Y=0,height1440，诊断Y=40,height1400；关屏时间/Undo时拓扑未采样，环境变化是线索，不认定根因。随后同一未经观测修改的正式冻结DLL独立cross-monitor也1/0/0、exit0，四方向/最大化组合及副屏三窗原检查通过；与full FAIL分开，不拼接为全66通过。没有生产/正式测试改动或放宽断言。

[原始全66、单项及跨屏跟进](D:/VibeProject/diagnostics/paneset/preview-heading-green-20261014T095725Z/CONTINUATION-20261015.md)保留，Advisor支持“诊断单项未复现、原始失败仍未解释”。任务10不关闭。

新[系统主题与A/B人工宿主](D:/VibeProject/diagnostics/paneset/manual-continuation-20261015T021501Z/README.md)02:15Z/PID125208启动，build0警告错误/validate-only通过，读取原临时设置，user config SHA相同、系统应用light1。四个桌面范围控件在显示前关闭，只用own三fixture/无SendInput或全局注册；用户PaneSet135772仍保留。现在等待人工系统跟随/固定不跟随、A/B布局/恢复/取消反馈，own宿主有意保留不称已清理。真实生产入口及Win11/mixed DPI仍不可用/未验。

## 暗色预览标题修复（2026-10-14，eadec9b，机器及本组人工PASS）

用户完成本组后明确反馈“验收通过”。仅记录此前给出的三步范围：暗色预览标题可读、固定亮/暗切换正常、新宿主读取原临时配置保持暗色；此新MainWindow构造不等同生产App/单实例启动。10:08:43 panel-opened机器快照实际mode=Dark/paletteDark=true/Windows app light=1，两次用户保存仍Dark、fixture原几何restored=true/undo=false。机器未采样到亮色，亮色人工结论依据用户报告；没有A/B操作，不推断A/B恢复或系统主题切换PASS。

验收后按准确宿主PID/HWND/控制台标题发WM_CLOSE，宿主194988与其fixture87568均退出；未发送键鼠输入。cleanup-verification.json确认两PID无剩余、用户配置SHA未变、系统应用主题仍light=1。两份原诊断文档仍未提交。此缺陷复验完成；任务10仍保留全66余41、系统跟随及固定模式对系统变化、A/B完整布局/恢复/取消、生产入口/Win11混合DPI等未验项。

用户明确授权“在当前session完成并复验”。MainWindow.xaml:134的PreviewExpander新增Foreground动态Ink绑定；既有appearance检查读取实际模板“布局预览”TextBlock，在初始暗色、运行时亮→暗及设置切到亮色后断言主题文字色。没有改变66项清单或输入/Undo/取消规则。本地提交eadec9b只含产品XAML及检查Program.cs，12增1删；两个诊断文档继续未提交，未推送/发布。

新增断言在未修产品上先红（Expected #FFE9F1F0, got #FF000000），前台准备/真实输入前失败；修复机器appearance复验1/0/0、exit0，主/隔离Release构建0警告错误。最终全声明文件逐字节工作区副本source834dee89，Checks23383d4c/Product84f18b0e/PDB proof2970accf。实际修复DLL的零输入/零目标自有WPF截图标题亮色、对比度12.31289，无临时SetResourceReference干预。第一次隔离绿副本的部分未改文件由Git检出换行为CRLF，原通过记录保留而不称工作区全字节相同；当前另建精确副本通过。

首次全66预检因Backspace/VK8阻挡，0检查启动，归档独立。最后精确副本的全66实际启动后24 PASS/1保护FAIL/0 unavailable/41未运行，exit1：overview-size-ui注入前有键/按钮按下，拒绝点击并ABORT，不自动重试。独立appearance通过不填入41项，不拼接旧版结果。

[修复来源、红/绿与全66及截图](D:/VibeProject/diagnostics/paneset/preview-heading-green-20261014T095725Z/README.md)已验证。执行助手静态自审确认局部主题绑定及模板断言符合本次范围，非独立/CodeReview双轴审查。

新[人工宿主](D:/VibeProject/diagnostics/paneset/manual-preview-fix-20261014/README.md)10:03Z/PID194988启动，原临时设置路径继续使用；用户10:08点击“显示主面板”后构造新MainWindow，主题保持/可读性已按本组反馈通过。宿主与own fixture随后已正常退出；四个桌面范围控件在显示前按x:Name封闭。用户配置SHA与初次相同、系统应用light=1，未注入键鼠。此宿主不证明生产App/单实例/全局入口；修复后系统跟随、A/B完整恢复/取消、Win11/混合DPI仍待验收。任务保持open。

## 人工主题验收缺陷（2026-10-14，d71f487，历史发现时未修复）

用户反馈暗色主题下“布局预览”字体黑、看不清，其他本组无问题。标题可读性记录人工FAIL，其余固定主题检查记录用户未发现异常；系统主题切换、宿主新进程主题保持、A/B完整恢复/取消不补记通过。

根因已用同一冻结产品DLL696dfac6确认：MainWindow.xaml:134的PreviewExpander未绑定Foreground，模板生成“布局预览”TextBlock保持#FF000000；实际IsDark=true、Ink=#FFE9F1F0，Panel背景#FF1A2D3D，对比度1.48666。预览编号已经亮色。零目标/零全局热键/无键鼠注入/ShowActivated=false探针复现退出1，自己的WPF截图读回一致。仅在诊断窗口SetResourceReference(Foreground,"Ink")后标题#FFE9F1F0、对比度12.31289、退出0。该干预是根因对照，不是修复构建或正式回归。全src Expander搜索只命中本处。

[诊断、原始/提案截图及一行未应用补丁](D:/VibeProject/diagnostics/paneset/dark-preview-d71f487-20261014/README.md)已保存，产品/正式检查源码未改、没有新提交。当前session原定回归/人工验收，产品修复范围待用户决定，任务不关闭。

另纠正本机验收宿主防护：首次隐藏A所用AutomationProperties.Name写错，真实名为“排布本屏全部窗口”，旧宿主实际未隐藏A。发现后按PID/HWND/精确控制台名向自建194688发WM_CLOSE，确认正常退出；原日志保留，不改写为旧防护成功，不从用户无异常反馈推断未操作其他窗口。仓库外宿主已改用四个x:Name、在显示前隐藏并取消命中/焦点/Tab，缺控件则拒绝创建；0警告错误重建。探针实际确认这些最终控件全部隐藏/不命中/不焦点。尚未重新启动修正宿主。关闭/诊断没有键鼠注入或终止用户PaneSet的工具调用；末次只读查找PaneSet进程结果为空（原117480已不在），未观察谁关闭它，不把此前“仍保留”当作当前存活证据。用户配置文件SHA与本轮启动前一致。原初始化黑标题截图、修正外部工具的来源和停止记录分开存档。

## 人工验收第一组反馈（2026-10-14，d71f487）

用户按第一组要求操作后反馈“都正常，目前没看到异常”。据此记录受控MainWindow的标题栏拖动、边缘缩放、最大化/还原、最小化、关闭收起及再次显示为人工PASS。现场日志有WM_SYSCOMMAND/WM_EXITSIZEMOVE与尺寸/状态变化，支持有实际交互，不代替人工体验结论。宿主源/产品和范围不变；[人工判定记录](D:/VibeProject/diagnostics/paneset/manual-d71f487-20261014/human-verdict.json)与observations-20261014T090101Z-194688.json保留。

日志还出现受控A调用和B确认，但此次明确要求/反馈对应第一组标题栏及按钮；不把这些事件扩大为A/B排布-恢复/取消全部人工通过。保存观察时palette仍System/Light；固定主题、系统跟随、主题重启、A/B独立几何恢复/取消仍待下一组操作。机器restored字段在最后保存时false，不自行归因为产品恢复缺陷或记录恢复PASS。最终回归剩余36项、App全入口、Windows11/mixedDPI仍待验收。未重跑桌面注入/修改源码/发布。

## 最终版本续验与人工入口（2026-10-14，d71f487）

用户将后续产品事项交给新session，当前session只执行最终回归和人工验收。本轮核对HEAD d71f487、仅两份旧文档未提交，工作区C#与冻结最终源fb785351相同。08:48Z默认桌面预检通过后启动受保护全66，冻结Checks34df2dbd、产品DLL696dfac6、DLL/PDB证明核验通过，源码无漂移。

本轮41.08秒，**29PASS/1输入保护FAIL/0 unavailable/36未运行**，退出1。第30项quick-entry在PreparePickerHost→ClickOwned→ClickNativeOwned之前的GetAsyncKeyState检查检测到按下状态，拒绝发送点击，ABORT unrun36并停止后续桌面注入。overview-size-ui/overview-viewport/overview/thumbnail/mouse-routing本轮均PASS；后续36未运行，不以此前版本或定向PASS代替。停止后08:51Z只读预检为空按键/Default桌面，未据此自动重试。本轮不能判断拒绝时哪个键或来源（错误仅报告有键/按钮按下），不当作产品根因证据。

受保护runner命令不变；只读verify-regression.py由实际command是否含--case识别全套，而非固定label，支持此次新label；核对所有冻结文件、证明哈希和case枚举，未启动检查。记录：[本轮JSON](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T080102Z/regression-summary.json)、evidence/final-d71f487-20261014T0848-full66。

当前harness未提供ComputerUse/node_repl可调用入口，已读取其规则而没有运行ComputerUse动作。为人工操作准备了仓库外[受控验收宿主与步骤](D:/VibeProject/diagnostics/paneset/manual-d71f487-20261014/README.md)，Release0警告错误，--validate-only核对产品DLL通过且不启动UI/input。09:01Z人工宿主启动预检通过，宿主PID194688及独立fixture存在，尚无人工通过结论。宿主使用同一冻结产品DLL/临时配置，键盘鼠标不自动注入、全局键不注册，隐藏生产面板桌面范围A/B及刷新/最近组入口；helper调用生产A/B的显式fixture候选方法，仅影响三扇自建窗口。它不是PaneSet.exe/App单实例端到端启动，不证明全局热键或全桌面A。重启主题的观察范围必须按此注明。Windows系统主题由人操作并恢复初始AppsUseLightTheme=1，宿主只读；机器native-chrome/palette/几何观察不是人工PASS。

09:01后核对宿主控制台确实存在、stderr为空、用户配置哈希未变，用户PaneSet仍保留；人工宿主有意保持运行等待操作，不能写owned0。其launch.json、observations-UTC-PID.json保留独立启动记录。当前原生回归仍未完成，人工步骤待用户操作反馈；Win11/混合DPI未运行。未改生产/测试源码、未提交/推送/发布本轮记录。

## 正式输入检查保护与66项回归（2026-10-14）

用户暂停quick-input间歇根因追查，并授权先补正式测试保护、再跑整体回归。生产源码未改，时序参数和DEBUG探针均留在诊断目录。测试改动已本地提交d71f487（test: guard owned input and isolate shortcut checks），诊断文档保持未提交。正式测试对每次键盘注入校验检查窗口/活fixture身份、前台、可见/启用/焦点/Ready及全部VK按下状态；鼠标保留布局就绪/严格命中/物理保护，增加活fixture身份、全部VK检查与移动光标后再次命中。quick-input追加确认后两窗布局Bounds、第三窗保持，保留原60ms和Undo/取消断言。原66入口数量不变，同一quick-input补入未显示WPF/无效fixture目标的4个真实发送入口拒绝检查（不执行SendInput）。

审阅还发现固定全局按键不可读用户配置：shortcuts/shortcut-routing现使用进程唯一临时配置，明确OpenPanel/Restore键，A/B禁用；旧配置键失效检查临时注册id90确认独占，再发送，finally只释放自身注册。B入口先读取真实设置UI的注册状态。两处日志包装保留Unavailable传播。

| 构建与本轮 | 通过 | 失败 | 环境不可用 | 未运行 | 退出码 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 初版保护b0b75135/a7884972：quick-input单项 | 1 | 0 | 0 | 0 | 0 |
| 初版保护b0b75135/a7884972：全66，108.82s | 61 | 2 | 3 | 0 | 1 |
| 最终配置隔离版fb785351：全66，28.90s | 24 | 1（输入保护拒绝） | 0 | 41 | 1 |

初版2FAIL为mouse-routing在首次点击后的前台等待超时、quick-entry在PreparePickerHost前台等待超时，未据此归因产品。3 unavailable为single-instance/shortcut-routing/shortcuts（现有用户实例/快捷键占用）。最终版第25项overview-size-ui检测到GetAsyncKeyState按下状态，在ClickNativeOwned发送前拒绝，ABORT unrun41；停止本轮后续输入。首轮结果不覆盖最终版41项未运行，不拼成全绿。最终版仍未完成全部桌面检查，人工/平台验收未启动。quick-input历史故障保留为未复现、暂停追查。

两次工作区与独立Release均0警告/错误；冻结源/二进制SHA与DLL/PDB/声明C#证明核验通过，最终测试与工作区字节一致。独立只读审阅44调用找出配置P1，修复后17调用复核无新可行动问题、正常结束且审阅源SHA未变。审阅不是实机PASS，也不是/CodeReview双轴报告。08:03:45Z自建诊断进程0、用户PaneSet117480保留；未推送/发布。

证据：[初版完整记录](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T075159Z/regression-summary.json)、[最终版记录](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T080102Z/regression-summary.json)、[配置修复只读复核](D:/VibeProject/diagnostics/paneset/formal-input-guards-20261014T080102Z/review/report.md)。来源核验限DLL/PDB配对与声明C#，不扩大为资源/生成输入/依赖/编译配置完整可复现性证明。Windows10/96+96DPI下记录；Windows11/混合DPI保持未验收。

## quick-input 解锁后续验（2026-10-14，157ec7d）

原版source212fec9e/Checks21e33f三组9轮完整PASS，覆盖实际Space/Alt1、两个Enter、Undo和二次Esc；未复现历史确认超时。68归档67来源匹配/1旧失配明确排除，不是68PASS。独立策略审阅核实原SendKeys60ms、直接Space/Enter/Esc没有逐次输入保护，原版未断言Undo前应用位置；之前关于每次保护的描述范围过宽，原9PASS不得补足这些边界。

[有界时序诊断副本](D:/VibeProject/diagnostics/paneset/quick-input-timing-157ec7d/README.md)产品源码保持，测试补逐次保护与应用位置断言；同一新副本sourcef3ec8362/Checks1385d418控制60ms与first-Enter settling 1/8/20ms全部PASS。新旧构建分别记，条件仅每值一次，不能证明不存在竞态/高复现率或修复。仍Phase1，生产/仓库testC#不改，不关闭任务10。独立Advisor重试因同模型被禁用，策略审阅为独立只读Pi，区别于Advisor调用。

## 任务10续验与来源校验（2026-10-14，157ec7d）

本版11项纯规则/临时文件/message-only HWND检查全部PASS/退出0，覆盖overview-size、选择刷新/资格/顺序和7项保存事务；这些不创建可见窗口、不发送输入。预检起初Ctrl按下，完成无输入工作后按键已清空，才尝试quick-input。原版source212fec9e/Checks21e33f在第一次第二Enter确认等待失败，历史第二次进入准备超时未重现。窄Enter日志sourceef151/Checks60117一轮全PASS，含实际确认、Undo和第二次Esc，不证明根因消失。

随后点击准备被Explorer ToolbarWindow32所有权保护拒绝，停止后续实际输入，未启动global/viewport/完整66。该次同时发现源码/产物来源失配：copy2恢复源文件旧mtime，增量构建仍用带日志Checks60117。原task10-157ec7d-clean-quick-input记录保留，provenance-exclusions.json明确不计clean源码验收；不能把其中的保护拒绝当作产品激活缺陷。被动空目标面板8采样WPF/native topmost一致、前台保持原窗口，只有观察意义。

外部诊断build/runner改为强制Rebuild、旧状态失效后原子发布成功状态、配对DLL/portable PDB并核对25个项目C#文档、冻结来源核对完成后才启动。失配红（ScreenSelectionChecks.cs checksum）与完整来源绿均已记录。独立工具复审指出Python-O移除assert和遗漏普通C#误接受，已用隔离无输入红绿修为显式拒绝和双向集合核对；6种普通/-O无效状态实际case进程启动0，最终-O安全pipeline PASS。59份归档58来源匹配/1明确排除，不是PASS计数，也不证明资源/生成输入/依赖/编译配置来源。

任务10继续needs-info/in-progress，剩余输入/根因、完整66和Windows11/mixed-DPI等实机验收未完成。仓库生产/测试C#不改，原两份文档及追加未提交；只使用自建窗口/临时配置，保留用户117480，无push或release。证据见[本轮诊断目录](D:/VibeProject/diagnostics/paneset/issue10-1a32825-current/README.md)、[来源排除清单](D:/VibeProject/diagnostics/paneset/issue10-1a32825-current/provenance-exclusions.json)。最终[独立工具双轴复审](D:/VibeProject/diagnostics/paneset/task10-tooling-review-20261014T035407Z/REVIEW.md)Standards/Spec无新增问题，快照SHA一致；只验证工具范围，不关闭真实输入根因。

## 审查P2修复（2026-10-14，157ec7d）

独立审查1a32825...53a11e4确认新增局部输入诊断包装会把UnavailableException变为普通Exception，令runner将不可用输入误计FAIL/退出1。157ec7d仅在CheckLocalLayoutInput通用catch增加`when (e is not UnavailableException)`，清理正常完成时保留原类型交给runner计SKIP/unavailable；普通断言/前台等待/保护拒绝仍按原路径包装并FAIL，生产及输入保护未改。

主区和隔离树Release均0警告/错误；7份迁回源码一致，43轮历史冻结证据重新核对通过，旧14项PASS未在新构建重跑。独立只读双轴复审53a11e4...157ec7d未发现新增Standards/Spec问题。未发送桌面输入，实际SendInput不可用负向路径未验证，现有入口缺少无需真实输入覆盖该catch的自然测试边界；没有用人工runner异常或私有实现结构检查替代端到端回归，也不声称TDD红绿或完整验收。

原Standards两项维护建议保留：错误文案变化时升级专用保护异常，视口结构/阈值共同变动时考虑共享断言。任务10仍needs-info/in-progress，完整66与实机验收待完成；只有一行测试源码提交，原文档及追加未提交，无push/release。复审见[REVIEW.md](D:/VibeProject/diagnostics/paneset/review-53a11e4-157ec7d-20261014T023613Z/REVIEW.md)，取舍见[P2复盘](D:/VibeProject/diagnostics/paneset/review-fix-unavailable-53a11e4/RETRO.md)。

## 解锁续验与测试点击修复（2026-10-13，任务10仍开放）

用户确认解锁后，037e216对应冻结产物的`overview-size-ui`、`appearance`、`local-layout-keys`完整PASS，退出0；局部键走完三种Alt布局、Enter排布/恢复与末尾普通1，文本实际变为1。旧日志中全量SendInput成功同时error=5是残留错误码，不能解释为拒绝；新测试日志成功打印error=0，失败仍保留原码。

原overview真实卡片click仍失败。最小时间线两轮捕获：计算点时卡片measure无效、原点(680,741)，鼠标消息时原点已变(1266,461)，旧点落在另一张卡片。down/up都早于光标恢复，这两轮不能归因恢复过早。测试ClickOwned在读坐标前UpdateLayout并等待measure/arrange有效；保留实际点击、命中/按钮保护、原等待和勾选断言，不改生产。最小点击绿、带时间线的完整overview绿；移除临时插桩后同一构建的原`overview`、`mouse-routing`、`quick-flow`均完整PASS，包括700×450边界/70 DIP、搜索/顺序/失效/排布/Undo。

随后最终`quick-input`在第二次进入准备的前台等待失败；`global-quick-input`因已有鼠标按钮按下拒绝注入，立即停止后续桌面输入，没有释放用户按钮。新增可选PANESET_CHECKS_STOP_ON_INPUT_GUARD=1供诊断runner，在保护拒绝时停止并报告unrun；默认入口行为保持。无注入合成对照证明启用时后一哨兵不执行、unrun=1，默认则执行；两子进程退出1是预期负向证据，不计作产品失败或完整套件统计。字符串识别保护依赖现有错误文案，限制已注释。

本地提交53a11e4仅三个测试文件（布局同步、错误日志和可选停止机制），产品DLL与037e216冻结产物逐字节相同。临时鼠标/哨兵探针清出源码；当前7份迁回源逐字节一致，Release0警告/错误。verify核对43轮源/程序集/补丁与退出码，并检查哨兵结果；不同构建结果分别保留，不混为同一全套。12:51:40Z只读确认本诊断进程0、用户PaneSet117480保留。

完整66项尚未启动；quick-input/global-quick-input剩余链、首次viewport前台准备间歇失败、真实Windows主题切换/重启、标题栏鼠标、Windows11仍待验。任务保持needs-info/in-progress，不关闭历史前台不稳定。原两份未提交文档及后续追加保留未提交，未推送/发布/动用户配置布局。证据与复盘见[诊断索引](D:/VibeProject/diagnostics/paneset/issue10-1a32825-current/README.md)。

## 1a32825 当前基线：低高度布局与输入诊断（2026-10-13）

现场HEAD为1a32825，原两份未提交文档的SHA与交接manifest完全一致，无产品源码漂移。独立checkout/输出与a0b068d历史诊断分开；未移植旧被动显示候选。

700×450的最小真实窗口检查捕获已选列表视口 **56.60 DIP <70**。实际96 DPI，外框与客户区均为700×450，确认、取消按钮当次均在边界内；原全套的按钮边界失败尚未本轮复现。产品在低于600 DIP时缩减头部留白18 DIP，恢复正常高度时还原，保留全部文字、按钮与原断言。相同250ms前三档流程取得修复后 **74.60 DIP**，但后续缩放失焦使整项仍失败；另以Dispatcher ContextIdle/UpdateLayout及原生尺寸读回完成`overview-viewport` **1 PASS / 0 FAIL**、退出0，两主题三尺寸、599→600、回450/900及取消后的fixture几何/Undo均通过。截图属于WPF，不是DWM合成；同一idle测试的未修复版本复跑后来被LockApp阻挡，不能称其红测。

当前`quick-flow`完整PASS、退出0；`local-layout-keys`在首次前台等待超时，未发送任何键。新增测试侧局部输入保护/诊断：注入前检查owned前台/焦点、修饰键/目标键/按钮异步状态，记录自己窗口的文本/选区、HWND/PID、键/字符及激活消息和SendInput返回数量，finally解除hook。该诊断键路径未越过本轮前台等待，不宣称实际送达或历史输入故障修复；生产激活不改。

最终冻结构建 **14项无注入定向全部PASS**、每项退出0：`ui`、`settings-ui`、`overview-size`、三项选择规则、7项保存契约、`shortcut-settings`（含主题/快捷键互相保留及保存失败）。Release0警告/错误；迁回主仓库的六份源码/测试与隔离树逐字节一致，主区独立重建也0警告/错误，未额外重跑主区桌面检查。

最新真实输入命中PID19500，07:56:11Z只读确认LockApp后停止全部桌面注入。未运行修复后原`overview`全流程、`appearance`、`overview-size-ui`、完整66项（原65加新增viewport）；真实Windows主题切换/手动覆盖/重启、标题栏鼠标和Windows11仍待验。历史61/1/3与更早统计保留，任务10维持needs-info/in-progress。

[完整证据与限制](D:/VibeProject/diagnostics/paneset/issue10-1a32825-current/README.md)、`summary.json`、`verify.py`核对27轮冻结源/程序集/补丁；初期tracked diff不含未跟踪probe，须看逐文件source SHA。新runner验证源码集合与构建、启动程序集对应且运行中无源漂移；哈希不代替因果验证。Standards/Spec为同会话人工审查，Advisor仅摘要建议，无独立双代理审查。本地提交037e216只含本轮六份源码/测试；原文档及本轮追加保留未提交。无用户配置/布局变更、推送、发布或新增提权捕获。

## A/C 外观审查修复（2026-10-13）

暗色主面板窗口标题现在明确使用 `Ink`，避免复用控件默认黑字；快速选窗卡片的悬停、键盘焦点描边改用动态资源，运行中切换调色板会立即更新。`appearance` 检查两种标题颜色、卡片焦点描边的明暗往返、设置保存及主窗口最小化、最大化与还原。暗色总览截图等到 `IsReady` 后生成，不再把初始化瞬间当作稳定界面。

本机 Windows 10 Build 19045 对 `DwmSetWindowAttribute` 属性 20 返回成功，但在系统亮色时对测试窗口设置暗色后，标题栏仍显示白色。主面板和设置改用 WPF `WindowChrome` 绘制应用主题标题栏，保留拖动、缩放和系统按钮。独立输出目录的 Release 构建为 0 警告、0 错误；定向 `appearance`、`ui`、`settings-ui`、`overview-size-ui`、`quick-flow` 已通过。完整检查为 **61 PASS / 1 FAIL / 3 unavailable**；唯一失败仍是 `overview` 在 700 × 450 时的确认按钮边界断言，定向 `overview-size-ui` 通过。3 项 unavailable 仍因现有用户 PaneSet 实例或全局键占用。此结果不构成全量通过。系统主题真实切换、标题栏鼠标拖动及 Windows 11 实机仍待验收。

## 解锁后的固定基线复核（2026-10-13，任务 10 仍开放）

用户回复已解锁后，前台只读查询为 pebrel；固定 `a0b068d` 的插桩 `overview-size-ui` 两轮均完整 PASS，新建未插桩独立 checkout 再跑一轮也 PASS，覆盖 2/3/8 窗、过滤前后视口、取消及源窗口几何。真实标题点击记录送达 WM_NCLBUTTONDOWN，无双击或最大化消息；不据三轮通过解释历史前台超时或会话取消。

同一原版独立构建的 `quick-flow` 在 PreparePickerHost 点击后实际前台等待超时；`local-layout-keys` 在发送任何布局键之前等待前台超时。最小化不稳定：不含 picker 的主面板准备可通过，保留 120ms 后另捕获被外部窗口覆盖的命中保护失败，不能混同点击后超时。普通自建 Window 正反两序对照，ShowActivated=false 时原生 topmost 位为 true，默认 true 时为 false，Dispatcher idle 和直接原生请求后仍保持区别；实际前台均为 Zed。此为当前环境的路径相关性，未查明底层机制或具体外部因素。

仅在独立诊断树试验首次被动显示，`quick-flow` 保留原全部流程/断言完整 PASS 一轮。`local-layout-keys` 的候选使用原受保护点击准备，首轮越过三种 Alt 布局键、确认排布和恢复，在末尾普通字符 1 断言超时；仅添加末尾日志后的另一轮又在更早的前台等待失败，末尾日志没有执行。候选未移植主工作区，不称为完成修复。

最后最小普通字符 probe 成功取得前台，在任何合成键盘输入之前收到 Shift key-down（WM_KEYDOWN/16），按键检查拒绝注入，随即停止实际输入。异步按键状态不能识别人为或其他合成来源；未释放该按键。LockApp 的先前阻挡已解除，本轮新的拒绝不能归为锁屏，也不覆盖此前字符失败。

[21 轮复核索引与限制](D:/VibeProject/diagnostics/paneset/issue10-overview-a0b068d/README.md)及 `evidence/unlocked-summary.json` 保存每轮分类；其中最后 7 轮启动前冻结本地程序集（含 PaneSet.dll）并绑定完整产品/测试源码，验证脚本重算哈希通过。更早记录只有 Checks.dll 哈希和部分源码记录，不能补造完整产物；tracked git diff 哈希不含未跟踪 probe，另由每文件记录覆盖。本轮未重跑全套、未完成 Windows 11 验收，不改写其他工作的全套结果或历史 57/4/3、48/6/3。本调查在主工作区只追加文档；所有候选留在隔离目录，未提交、推送或发布。

## overview-size-ui：单项诊断与桌面访问阻挡（2026-10-13，未修复）

以 a0b068d 独立构建，Release 0 警告/错误；主分支新 logo 与其他进行中的 UI 修改未纳入。原版单项两次 FAIL 均为已打开 picker 后的会话取消，不是原前台超时。阶段插桩两次分别在 count2/count3 复现原始 fixture 前台等待超时；第二次 count2 完整通过。事后外部 Chrome 类快照不确定取消与前台切换的先后，不将窗口类当作具体应用或根因。

去掉 picker 的最小版本两次 PASS；仅保留其生命周期时，第二次标题栏点击造成独立最大化/几何失败。自建 fixture 的直接消息对照 PASS：客户区双击不改几何，标题栏双击产生系统最大化，再次产生还原。此控制不调用 PaneSet 面板、不注入物理鼠标/键盘，只支持独立标题栏机制，不替代真实输入或证明前台超时原因。

随后一次真实输入在命中所有权保护处拒绝注入；阻挡 PID19500 已只读确认为 LockApp，桌面输入随即停止。锁屏仅解释这一轮，不覆盖前面的失败。当前需解锁交互桌面才可继续真实点击/前台记录；未改变激活、等待、断言或保护。原始 9 轮状态分别保存，并有插桩补丁、程序集/源码哈希，详见 [诊断索引](D:/VibeProject/diagnostics/paneset/issue10-overview-a0b068d/README.md)与 [任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)。

本次调查未改生产源码，当前主工作区其他进行中的 UI 修改保留。尚未形成稳定的最小前台超时复现或产品修复；任务 10 保持 needs-info / in-progress。未重跑全套，57 PASS / 4 FAIL / 3 unavailable 与历史 48/6/3 保留。

## 配置替换：第 5 阶段修复及最终回归（2026-10-12）

基线 `7032654`。生产 Save 以唯一未使用的同目录备份名调用 File.Replace，临时 JSON 仍先 Flush(true)；1177 尝试一次无覆盖恢复旧备份，恢复失败则保留旧字节并报路径。提交后清理异常作为成功提示返回，Change 发布新绑定；失败时保留旧绑定并释放暂存注册。恢复与临时清理同时失败保留原 HRESULT、恢复路径和各层异常。

| 验证 | 结果及边界 |
| --- | --- |
| 独立旧基线 + settings-replace | 迭代 7 返回 Win32 1175，累计观测 7 次 RF 打开，退出 1。计数不证明失败瞬间的排他持有；更早原工作区红阶段迭代 0 / observedRFopens=0。 |
| 独立修复版 Release | 0 警告 / 0 错误；只应用 3 个生产文件、检查入口和新测试文件。 |
| 最新版 7 项保存回归 | 各 1 PASS / 0 FAIL / 0 unavailable，均退出 0。真实 Save/Load 200 次；真实 Change、磁盘/内存绑定和注册持有/释放逐次验证 100 次。 |
| 清理、恢复、竞争失败 | 真实成功替换后锁备份验证清理提示与新绑定；真实锁目标验证失败保留旧值。1176/1177 注入控制流，1177 同时真实移动旧目标；锁备份、外部重建目标、锁临时文件分别验证恢复失败、无覆盖、组合错误与备份保留。不是实际 Windows 返回 1176/1177 的记录。 |
| 最终完整 64 项 | **57 PASS / 4 FAIL / 3 unavailable**，整数退出码 **1**，约 103.48 秒；新增 7 项及 shortcut-settings、configured-input 全部 PASS。 |

4 个失败为 overview-size-ui（OverviewChecks.cs:90）、quick-cancel-undo（物理鼠标按钮已按下，严格保护拒绝注入）、quick-flow（覆盖层 UI 等待超时）、local-layout-keys（前台等待超时）。3 unavailable 为 single-instance、shortcut-routing、shortcuts，存在用户实例或快捷键占用。上述分类只记录失败位置；未建立四项的同环境旧基线差异对照，UI 超时根因仍未确认，不将四项统一归因于既有问题或环境。未关闭用户进程或放宽 guard。全套后的最终改动只有 Save 返回值契约注释及测试探针退出清理，随后最新独立构建复跑 7 项保存回归；未再次重复桌面全套。

完整 configured-input 之前的单独运行已越过三次保存，但在原生前台等待处失败；加两行临时状态输出的另一次通过，激活请求初始均 false、随后达到严格前台断言。最终全套代码移除了临时输出且完整通过；不据重跑 PASS 推断前台稳定性已修复。global-quick-input 本轮全套 PASS，同样不关闭其历史间歇问题。原 **48 PASS / 6 FAIL / 3 unavailable** 记录继续保留在下文。

定向入口为 `--case settings-replace`、`settings-cleanup`、`settings-recovery`、`settings-recovery-blocked`、`settings-save-failures`、`settings-binding-replace`、`settings-temporary-cleanup`。构建/执行命令：

```text
.tools/dotnet/dotnet.exe build tests/PaneSet.Checks -c Release --nologo --artifacts-path D:/VibeProject/diagnostics/paneset/issue08-20261012/phase5-build
.tools/dotnet/dotnet.exe D:/VibeProject/diagnostics/paneset/issue08-20261012/phase5-build/bin/PaneSet.Checks/release/PaneSet.Checks.dll [--case <case>]
```

实际执行脚本设置子进程 DOTNET_ROOT / DOTNET_ROOT_X64 指向本仓库 .tools/dotnet，保存整数退出码及 stdout/stderr。[phase5 证据目录](D:/VibeProject/diagnostics/paneset/issue08-20261012/phase5-evidence/)保存 baseline-red、temporary-cleanup-red/green、final-settings-*、full-suite 原始日志与状态；独立源码位于 phase5-isolated，最新版定向产物位于 phase5-final-build。原常规构建曾被用户运行中的 PaneSet.exe 锁定，转隔离输出，未关闭用户进程。

规范/规格两轴按本仓库说明、GLOSSARY、问题 08 和快捷键事务规格人工核对；当前工具无并行子代理，Advisor 提供边界建议但未独立读取完整补丁。没有范围内未处理发现。问题 08 为 ready-for-agent / done，稳定性任务 10 仍 needs-info / in-progress。当前主机 Windows 10 的受控结果不替代 Windows 11 x64 支持基线验收；未推送、发布或重建既有 v0.2.0-preview.1，现有用户进程继续运行旧构建。后文是各阶段历史记录，其中未修复、未验收表述对应当时状态。

## configured-input：内部 RF 失败请求定位与竞争机制复现（生产未修复）

用户批准一次管理员取证后，WPR 实际录制约 3.038 秒，原生探针第 14 次 ReplaceFileW 捕获 1175；旧目标 `new13`、新源 `new14` 保留。ETL 已保存，`wpr -status` 确认停止，无桌面输入、停用户应用/服务或生产源码修改。

已按 ETL PerfFreq=10,000,000 与系统 QPC 校验、换算 Python 纳秒计时。失败调用中，旧 settings.json 到内部 `settings.json~RF3d687e78.TMP` 的覆盖重命名（InfoClass=65）及其同 IRP 完成事件明确返回 **0xC0000022 STATUS_ACCESS_DENIED**。同一内部 RF 文件可见 PID 185240 成功打开（ShareAccess=7），该打开早于失败、相同 FileObject 的 Cleanup 晚于失败完成；只读进程路径、启动时间及父进程确认这是 Raycast 后台 node.exe。由于事件丢失，不能把未观察到中间 Close 当成持续持有的排他证明，或完全排除地址复用和其他参与因素。失败完成栈含 Ntfs.sys；其他驱动的存在不作主动拒绝证据，Raycast 内部发起模块仍未知。

受控对照直接验证机制：D 卷原 ReplaceFileW + 目录通知，notify-only **20/20 PASS**；20 次持有条件中，实际及时持有内部 RF 文件的 **19/19 返回同样 1175**，未截获的 1 次 PASS，失败均保留旧目标和新源。独立 MoveFileExW 对照也显示完整共享的覆盖目标句柄会导致 5，而持有源或先释放目标均通过。故阻挡点是本次内部覆盖目标，不是必须持有原配置文件。

显式唯一备份名的候选仍仅在隔离探针：C 卷无备份 10/10 试次失败、失败前 327 次成功；唯一备份 **1,000 次成功、0/10 试次失败**。D 卷显式备份通知均在重命名后出现，实际持有备份时 **20/20 PASS**，40 份备份旧字节均正确。未改生产 Save，未验证备份清理与其他失败码的绑定/磁盘一致性，不称修复或完整 configured-input PASS。

证据：[稳定诊断索引](D:/VibeProject/diagnostics/paneset/issue08-20261012/README.md)、[聚焦时间线与断言](D:/VibeProject/diagnostics/paneset/issue08-20261012/evidence/root-mechanism.json)，详细命令见[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)。原始 ETL 本地约 440 MB，TraceEvent 3.2.8 为任务目录临时解析依赖，不进入产品。录制报告 **1,174,675 丢失事件**，不据缺失事件排除原因，也不将本次读者当成历史所有 1175 的唯一触发者。

外层取证脚本原退出码读取为 null，原记录 Success=false 保留；ETL 保存和 WPR 停止分别有实际证据。已修正诊断 runner，probe-only 验证整数退出码 1 / 1175，无第二次内核录制。tracerpt 截断 XML 已删除，仅保留原始 ETL 和分开的流式衍生。问题 08 改为 ready-for-agent / in-progress，稳定性总体仍开放；完整 configured-input、Windows 11 与全套均未重跑，历史统计原样保留。

## configured-input：配置原子替换继续诊断（2026-10-12，未修复）

基线 `7032654`，仅继续[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)的保存失败调查。生产源码未改；本轮没有桌面输入、提权、安装工具或内核录制，未重跑完整 configured-input 或全套。

原 Python ReplaceFileW 探针仍重现 Win32 **1175 / 0x80070497**，第 0 次失败、退出 1、约 0.3 秒。新增保留现场的循环交错比较两个不同 NTFS 卷，每试次最多连续替换同一目标 100 次：

| 条件 | 失败试次/总数 | 失败前累计成功替换 |
| --- | --- | --- |
| C 卷，0 ms 间隔 | 4/10 | 825 |
| C 卷，10 ms 间隔 | 10/10 | 215 |
| D 卷，0 ms 间隔 | 0/10 | 1000 |
| D 卷，10 ms 间隔 | 0/10 | 1000 |

失败均为 1175，旧目标/新源内容保留。固定种子 7032654；两卷使用不同目录根，不能把关联当成卷本身的因果证明。独立新文件对多数通过，100 ms 等待仍有 1175；受控普通共享冲突稳定返回 32，未产生相同错误。没有依据采用延迟重试或替换算法。

关联进程查询通过主动持有读句柄的正向校验；随后 20/20 试次出现 1175，查询成功但源/目标 PID 列表为空。两个查询合计需 **104–123 ms**，不是失败瞬间证据。只读驱动服务列表不能指认阻挡者；fltmc filters 仍 Access denied，缺少实际 I/O/调用栈。

生产链接的独立 message-only 宿主 Release 构建 **0 警告/错误**，不显示窗口、不注入输入，使用真实 checked RegisterHotKey。20/20 个试次出现同样保存失败，0 unavailable，失败前 391 次 Change 成功；每次均验证磁盘/内存/旧 ID/旧快捷键保留，暂存注册释放、临时文件清理及重载一致。退出码 1 表示保存故障继续存在，安全断言全过，不能称修复或完整输入验收通过。

短入口：`python C:/Users/Administrator/AppData/Local/Temp/paneset-replace08-diagnosis/capture-probe.py`，已在第 2 次替换重现 1175、退出 1、约 0.3 秒，保留不同序号字节、PID/UTC/QPC。脚本、八份原始 JSON 和三份合成失败现场稳定归档到 [D:/VibeProject/diagnostics/paneset/issue08-20261012/README.md](D:/VibeProject/diagnostics/paneset/issue08-20261012/README.md)；D 卷全部 fixture 另归档到 D:/Temp，路径映射另存，无生成物留在仓库。

下一步的系统 WPR 自定义文件/minifilter 配置与 capture-kernel.ps1 已准备，`-ValidateOnly` 校验通过。当前令牌为非管理员；脚本给自建探针设置 10 秒运行上限，fixture 显式保持 C 卷 Temp，只停止自己的录制。实际管理员内核捕获尚未获授权或执行；配置在短时间窗口录制系统文件 I/O 与进程/模块信息，无法预先按 fixture 路径过滤，ETL 可能含其他文件名/进程元数据，仅本地保留。根因未识别，问题 08 和稳定性任务仍开放，历史全套统计保持原样。

## global-quick-input：第二次进入前台超时调查（未复现目标故障）

基于 `fea6de2` 开始单独诊断。入口为 `powershell.exe -NoProfile -File C:/Users/Administrator/AppData/Local/Temp/paneset-global-input-diagnosis/run.ps1`。原用例执行 6 次：4 PASS / 2 FAIL；增加阶段状态采集后执行 10 次：9 PASS / 1 FAIL。16 次合计 13 PASS / 3 FAIL，**没有一次复现原先第二次进入 B 前的前台等待超时**；不能进入该故障的根因修复阶段，也不能用通过次数宣称已修复。

三个失败中，一次在首次总览 `OverlayClick` 等待鼠标事件时超时，另外两次为最终恢复期望最大化、实际普通窗口。其中带诊断的一次明确记录：初始准备点击之前目标最大化，点击之后即变为普通窗口，当时 `pending=False, undo=False`；后续第二次进入的前台检查通过。因此这一场景的几何差异已在排布前发生，不能据此认定 Undo 保存/恢复错误。为何准备点击改变最大化状态仍未定位，且不等于原前台超时的根因。

还尝试临时同进程串联原全套相邻的 `quick-input → global-quick-input`。第一次执行即在前置用例准备主面板时，因命中 Chrome 子窗口而拒绝输入，未到目标用例；随后停止桌面输入，没有清理用户窗口或放宽所有权保护。临时串联入口已移除，未运行完整套件。

保留 `CheckGlobalQuickInput` 的失败上下文：初始准备点击前后、第一次排布后、取消置顶后、请求前台后、第二次进入前及失败时的自建窗口状态、前台句柄/类、pending/Undo 与主面板状态；不记录外部窗口标题或输入内容。仅失败时输出，异常保留原始 InnerException，未改等待、断言、输入路径或生产逻辑。临时 `[DEBUG-global-entry]` 标记已清除。最终 Release 构建 0 警告/错误；最终整理移除临时入口、调整异常标签并让同条目标状态记录使用同一次前台采样，此后未再次进行桌面验收。

上述目录保存 `baseline-1.log` 至 `baseline-6.log`、`probe-1.log` 至 `probe-10.log`、`sequence-1.log`，以及明确标记为 debug 的采样源码副本。`sequence.ps1` 是本轮临时串联入口记录，最终构建不再包含该用例；常规 `run.ps1` 仍可执行。历史全套失败不改写，稳定性任务继续开放。下一步需要再次出现目标超时时的阶段状态，或能提高其复现率的具体操作顺序；现有证据不足以修改产品激活策略。

## ui-many：测试宿主应用名假设修复

已定位前轮 `ui-many` 的“Expected 8, got 0”：用例搜索硬编码 `PaneSet.Checks`，而 `Fixture.Start` 通过 `Environment.ProcessPath` 启动测试窗口。用 `dotnet PaneSet.Checks.dll` 运行时，fixture 的应用名为 `dotnet`；通过 `PaneSet.Checks.exe` 运行时才为 `PaneSet.Checks`。主面板按真实应用名过滤，前一种启动方式匹配到 0 项，因此渲染出的已选复选框为 0；不是 B 选择规则重构导致窗口丢失。

复现与对照：原完整用例在 dotnet 宿主下连续 **2 次 FAIL**（各约 2.6 秒）。临时缩减至“创建 8 窗、全选、搜索、检查”，搜索前 8 项通过，搜索后仍 FAIL。记录宿主/候选应用名/筛选数据数量后，仅切换启动方式：dotnet 为 `apps=dotnet, filteredItems=0` 且 FAIL；apphost 为 `apps=PaneSet.Checks, filteredItems=8` 且 PASS。这一区别发生在筛选数据层，无需延长布局等待。

修复仅修改 `InteractionChecks.CheckManyUi`：以 `Path.GetFileNameWithoutExtension(Environment.ProcessPath!)` 作为应用搜索词，与 fixture 实际启动宿主一致；保留原完整流程及 8 项断言。临时缩减用例和 `[DEBUG-ui-many-host]` 输出均已移除，生产代码不变。最终 Release 构建 **0 警告 / 0 错误**；原完整 `ui-many` 在 **dotnet 与 apphost 两种方式均 PASS，退出码均为 0**，各约 3 秒，覆盖排布、恢复、搜索后选组、不同尺寸与操作可达。未重跑无关全套，历史 48/6/3 结果原样保留；本项根因已解决，其他稳定性/配置失败仍独立开放。

复现入口：`powershell.exe -NoProfile -File C:/Users/Administrator/AppData/Local/Temp/paneset-ui-many-diagnosis/run.ps1 -HostMode dotnet`（另一方式为 `-HostMode apphost`）。该目录保存独立 build、截图、`baseline-1.log`、`baseline-2.log`、`minimal.log`、`host-dotnet.log`、`host-apphost.log`、`fixed-dotnet.log`、`fixed-apphost.log` 及 `final-build.log`。所有生成物位于此临时目录，没有覆盖仓库历史截图。

同会话自查：测试搜索输入修正，与既有 fixture 启动机制一致，未改生产筛选、测试等待、计数断言或原生输入保护。现有 ui-many 即回归用例，双宿主入口分别验证；没有新增框架或全局配置。当前没有后台测试在运行。

## B 选择规则重构：桌面恢复后的补验

用户确认桌面可用后，基于 `c1df2d9` 的生产源码重建并补验；本轮生产代码未变。Release 构建仍为 **0 警告 / 0 错误**。测试侧仅增强 OverviewChecks 的失败证据：卡片实际鼠标事件及勾选变化、会话完成原因和窗口状态；尺寸用例在打开总览前检查 fixture 几何未被准备点击改变，并在读取已关闭宿主前报告退出原因。未放宽所有权/按键保护、延长等待或加入自动重试。

完整 **57 项**运行结果：**48 PASS / 6 FAIL / 3 unavailable**，约 80.90 秒，无超时/锁屏中止，运行期间源码哈希未变化。计数来自进程输出的 `RESULT`；外层 PowerShell 未取得进程退出码（记录为 null），不将它写成已观测的 0 或 1。完整运行的测试版本包含卡片点击诊断；后续仅补充会话/尺寸失败证据，没有再次跑全套。

| 全套失败 | 本次事实 |
| --- | --- |
| overview-size-ui | 取消后 fixture 几何比较失败：原普通窗口变为最大化；当时没有准备阶段的几何证据，不能归因于总览或用户操作。 |
| overview | 已越过真实卡片点击、按钮 Enter、顺序和搜索断言；后续读取宿主客户区失败，未走完排布/Undo。 |
| quick-invalid | 准备主面板时检测到鼠标按钮已按下，保护拒绝注入。 |
| global-quick-input | 首次选组、Alt+1、Enter 排布已越过，第二次进入前等待 fixture 真实前台超时；未到取消与 Undo 断言。 |
| configured-input | 临时配置保存报“无法删除要被替换的文件”，与历史原子替换失败一致；未改保存算法或宣称根因已确认。 |
| ui-many | 主面板大列表可见项期望 8，实际 0；本轮没有定位原因。 |

3 项 unavailable 是 `single-instance`（用户已有 PaneSet）及 `shortcut-routing`、`shortcuts`（入口被占用）；没有结束用户实例或释放其快捷键。全套通过包括新增选择规则、DWM `thumbnail`、`quick-flow`、真实 `quick-input`、独立 `quick-cancel-undo`、本屏/跨屏排布与恢复及最近组路径。`shortcut-settings` 本次通过不抵消 `configured-input` 的失败。

定向过程保留全部证据：最初 `overview` 在真实点击后等待勾选超时；加入鼠标诊断后的运行在更早的按钮已按下保护处失败。全套后补充会话证据，再运行 `overview` **PASS**，完整覆盖最终顺序、低高度/滚动、失效取消、实际排布及 Undo。`overview-size-ui` 先在读取已关闭宿主时出现 1400 错误；将会话诊断提前至此读取之前后，定向 **PASS**，覆盖 2/3/8 窗尺寸、搜索/选择不改变外框、按钮可达与取消不改源几何/Undo。两次通过均没有生产修复，不能认为间歇输入/失焦或 fixture 最大化根因已解决。全套仍按 48/6/3 保留，不能改写成全套通过。

构建仍在 `C:/Users/Administrator/AppData/Local/Temp/paneset-selection-build`。全套证据：`artifacts/selection-module-full.stdout.log`、`.stderr.log`、`-status.json`、`-source.json`；定向证据：`selection-module-overview-desktop.log`、`selection-module-overview-click-diagnosis.log`、`selection-module-diagnosis-overview.log`、`selection-module-diagnosis-overview-size-ui.log`、`selection-module-size-session-diagnosis.log`（均在 artifacts）。失败与定向成功分开记录，当前没有后台验证在运行。本次生成的 23 张截图保存在 `C:/Users/Administrator/AppData/Local/Temp/paneset-selection-desktop-screenshots-c1df2d9/`，受版本控制的历史截图已恢复，避免覆盖旧验证证据；已查看本次 450 高总览合成图，搜索、已选区及主要操作可见。

Standards / Spec 同会话自查：改动限定于受控检查、诊断监听在 finally 释放、原断言与注入保护保留；不将合成路由/控件自动化当作真实输入。无独立双代理审查。本轮提交测试与验收记录，不推送或发布；历史稳定性问题继续开放，见[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)。

## B 总览选择规则集中（架构重构）

新增 `OverviewSelection`，集中本次固定候选排序、有序选组、身份刷新、搜索匹配、编号、布局预览与确认资格。`QuickSelectWindow` 仅转交选择/取消/移位及原生观察结果，读取同一份状态绘制；不再维护自己的 selected/invalid 集合、可写编号或确认资格缓存，也不再依赖主面板的 `WindowRow`。原生执行、焦点保护、DWM 生命周期和 Undo 路径保持原职责。

用户确认的规则已同步到规格：失效期间保留成员及顺序并禁确认；同一身份恢复合格后自动恢复有效性，其他条件满足时恢复确认资格，仍需用户最终确认。主动取消的成员不自动选回，再选加入末尾；不同身份不能替代。此项澄清既有刷新行为，没有新增“曾失效待确认”状态。

新规则按 TDD 分三轮建立：排序/选择顺序、身份刷新/搜索、布局确认资格，每轮先记录缺失接口的编译失败，再实现并通过。最终独立 Release 构建 **0 警告 / 0 错误**；不依赖桌面的 **8 PASS / 0 FAIL / 0 unavailable**：`selection-order`、`overview-selection-refresh`、`selection-eligibility`、`layout`、`layout-many`、`layout-invalid`、`layout-negative`、`overview-size`。覆盖初始 Y/X/稳定同位排序、前台优先、移位/取消再加入、标题与应用名搜索、完整身份各字段失配、同身份恢复、失效后主动取消及尺寸边界。总览集成检查改为读取显示标题、编号和卡片位置，不再读取 `WindowRow` 或新 module 的内部状态。

桌面定向 `overview` **1 FAIL**：首次 fixture 所有权检查命中外部窗口，`actualPid=19500`、`actualClass=Windows.UI.Core.CoreWindow`，拒绝点击；随后只读查询确认该进程为 `LockApp`。按约束停止所有桌面检查，没有解锁、释放用户按键、结束用户 PaneSet 或放宽保护。该失败发生在最终审查调整之前且尚未进入总览，不能作为最终产物的 UI 验证；最终源码只重建和运行上述 8 项无桌面检查。完整入口现为 **57 项，本轮完整套件未运行**，真实卡片/Enter、DWM、取消/Undo 和最终排布回归仍待桌面可用后补验。历史输入与配置失败仍开放。

构建目录：`C:/Users/Administrator/AppData/Local/Temp/paneset-selection-build`。日志：`artifacts/selection-module-build.log`、`artifacts/selection-module-pure-checks.log`、`artifacts/selection-module-overview-blocked.log`。

审查基线为 `54ed16572909c132690103d1a186c9efd9ffe317`，依据 AGENTS、PRODUCT、WINDOWS_PLAN 和 current-screen-selection 规格及本次确认。Standards：同会话自查无未解决代码发现；Spec：同会话自查无未解决实现偏差，桌面验证未完成。无可用子代理，未执行独立双代理审查；Advisor 因与执行模型相同禁用，不能计作外部复核。本轮仅本地提交，不推送或发布。

## 输入诊断与取消恢复独立验证

新增测试侧诊断：主面板准备失败记录 IsLoaded、可见/最小化状态、WPF/native Topmost、外框、前台句柄/类和焦点；Alt+1 失败记录前后状态、实际收到的 PreviewKeyDown/Up、修饰键、handled 及会话结束原因。Alt+1 注入前检查真实前台、会话就绪、键盘焦点及物理按键是否已有按下；未放宽原点击保护、延长等待或修改生产激活策略。

新增 `quick-cancel-undo`：通过控件自动化选择布局并执行排布，再进入 B 改选成员和布局、取消，验证排布后几何和 HasUndo 不变，最后完整恢复最初几何。这是独立行为验证，不代替真实键盘或全局入口验收。完整入口现为 54 项。

独立 Release 构建 0 警告/错误；本次 **5 PASS / 0 FAIL / 0 unavailable**：`quick-flow`、增强后的 `global-quick-input`、`quick-cancel-undo`、`quick-input`、`quick-invalid`。增强后的全局检查此次已走完取消保留已有 Undo 的新增断言。日志 `artifacts/input-diagnosis-host.log`、`input-diagnosis-alt.log`、`input-diagnosis-cancel.log`、`input-diagnosis-quick-input.log`、`input-diagnosis-quick-invalid.log`。

本次只改测试和记录，生产代码不变。加入诊断后这次没有再次捕获遮挡或 Alt+1 失败，因此不能认为历史偶发问题根因已定位或被修复；原失败日志继续保留。未重跑完整 54 项。审查为同会话 Standards/Spec 自查，无独立双代理审查。配置 ReplaceFileW 的系统错误仍按问题 08 保持开放。

## 空闲时段补验

用户允许暂停输入后，前轮 5 个失败用例定向复查为 **2 PASS / 3 FAIL**：`overview` 完整通过，`global-quick-input` 原版本通过；`quick-flow`、`quick-input`、`quick-invalid` 均在主面板准备阶段命中 Chrome 而拒绝点击。不能把这些失败简单归咎人工干扰。

随后加强 `global-quick-input`，把 Undo 放到第二次进入并取消之后，验证取消保留已有恢复记录。构建通过，但新版本在前置 Alt+1 布局等待处失败，尚未执行新增断言。原通过证据不覆盖增强版本；生产源码未改。见[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)，日志为 `artifacts/stability-idle-<case>.log` 和 `artifacts/stability-cancel-undo.log`。配置的系统替换问题仍维持下述调查结论。

## 稳定性收尾首轮定向检查

用户认可尺寸效果并授权稳定性验证及配置失败定位后，独立 Release 构建 0 警告/错误；定向 9 项为 **4 PASS / 5 FAIL / 0 unavailable**，未修改生产或测试源码。通过 `shortcut-settings`、`overview-size`、`overview-size-ui`、`quick-activation-failure`。失败 `overview`（真实卡片点击后选择状态等待）、`quick-flow`（WPF 窗口遮挡）、`quick-input`（已有物理按键按下）、`global-quick-input`（Zed 遮挡）、`quick-invalid`（Chrome 遮挡）。不是此前锁屏失败的重复结论。

本次补齐 2/3/8 窗自适应面板真实尺寸、搜索保留、按钮可达、取消不改几何/Undo；真实合成截图为 `artifacts/paneset-overview-fit-2.png`、`-3.png`、`-8.png`。全局入口、顺序调整及取消保留已有 Undo 的完整流程仍未通过。证据见 `artifacts/stability-results.json` 与[任务 10](../.scratch/current-screen-selection/issues/10-stability-validation.md)。

配置失败已在独立探针复现 `0x80070497`，并缩小到 Python 直接调用 Windows `ReplaceFileW` 返回 1175；去掉 PaneSet、JSON 和 .NET 仍能出现。失败后 Restart Manager 未找到占用者，过滤器查询被权限拒绝，具体触发者仍未知。未以一次事务检查通过关闭问题，未改保存算法或添加重试，见[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)。

## 总览初始尺寸调整

用户反馈全工作区面板过大，现按本屏捕获候选数计算初始大小并居中，搜索或勾选不改变外框。Release 构建 0 警告、0 错误；`overview-size` 通过候选数、负坐标、多种 DPI 比例的几何检查。新完整入口为 53 项，本次只做相关定向检查，未重跑前轮全套。

`overview-size-ui` 首次通过初始外框和搜索后外框不变断言，但在截图前面板已关闭，读取客户区失败，原因未捕获；补退出原因采集后再次运行，在 fixture 真实前台等待失败，未进入面板。保持失败，未放宽保护或反复注入；新尺寸的完整界面/截图与全局键验收未完成。具体范围见[任务 09](../.scratch/current-screen-selection/issues/09-overview-fit-size.md)。下面的总览截图和完整结果均早于此调整。

## B 统一网格总览：当前交付

用户确认 Q1–Q6 并授权实施后，B 已改为普通不透明 WPF 总览，加入 DWM 内容预览、稳定卡片、搜索、右侧前移/后移与低高度预览折叠。原共享本屏执行/Undo 与生产激活请求不变。源码和检查没有新增第三方依赖，未提交、推送或发布。

### 运行结果与版本边界

冻结源码的完整 51 项运行：**38 PASS / 13 FAIL / 0 unavailable**，退出码 1，94.38 秒，无超时，`source_changed=[]`。日志 `artifacts/overview-final-checks.log`；临时状态 `paneset-overview-status.json` 和清单 `paneset-overview-source.json`。不得报告全套通过。

| 结果 | 范围 |
| --- | --- |
| 9 项命中保护失败 | overview、thumbnail、mouse-routing、quick-entry、quick-flow、quick-invalid、quick-input、global-quick-input、quick-activation-failure。前置点击或目标像素读取命中外部窗口，未继续注入或读取外部内容。主屏 PID 19500 的 CoreWindow 经只读进程查询确认为 LockApp；副屏命中 LockScreenInputOcclusionFrame。 |
| 3 项前台/输入等待失败 | configured-input、shortcuts、local-layout-keys。实际前台条件未达成；保持失败，不延长等待、不改为 unavailable。 |
| 1 项配置失败 | shortcut-settings 首次 Change 报告原子替换文件无法删除，见[问题 08](../.scratch/current-screen-selection/issues/08-settings-replace-failure.md)。独立于输入失败，根因未证明。 |
| 38 项通过 | 包含布局、跨屏几何、恢复、本屏保护、部分影响、设置 UI、路由、最近组与原面板 UI。single-instance 当时仍硬编码原输出目录，实际运行旧构建，不能作为新总览版本的单实例证据；该路径随后修正为同构建目录，重建后单独运行 single-instance 为 1 PASS / 0 FAIL，补上当前产物证据。 |

在桌面尚可交互时，新增两项分别有定向通过记录：

- `thumbnail`：自建源窗口完全被不透明目标宿主覆盖，目标原生像素从红更新为绿；裁切区域外恢复宿主白底，注销后缩略图消失；源几何不变，无效 HWND 注册失败。验证实时合成，不是 WPF 单独截图。
- `overview`：12 个受控窗口，默认前台第一；右侧移动顺序、筛选保留选组、标题变化不跳位、搜索 Enter 不提交、失效阻止确认、取消失效项后真实排布与 Undo。1440×900、1000×650、700×450 下实际读取按钮边界，低高度已选列表至少 70 DIP。定向通过后，又增加完整初始排序断言、真实卡片点击和按钮 Enter 操作，以及失效恢复后的缩略图处理/顺序按钮焦点保持；这些最终新增断言在后续运行中因 LockApp 前置阻挡而未执行，不能用先前通过替代。

Release 构建 0 警告、0 错误；总览应用 `--probe` 退出码 0。完整回归之后仅修正单实例检查的产物路径，未改变生产总览源码；最终 Release 重建仍为 0 警告、0 错误，单实例定向检查通过。未解锁桌面、关闭用户进程或放宽所有权保护。

### 截图与审查

- 实际桌面合成：[900 高](../artifacts/paneset-overview-900-composed.png)、[650 高](../artifacts/paneset-overview-650-composed.png)、[450 高](../artifacts/paneset-overview-450-composed.png)。只截取受控总览宿主客户区，捕获前检查多个位置的窗口所有权；图中候选均来自 fixture。
- 同名 `-wpf-only.png` 是 WPF 单独渲染，空白预览槽不代表 DWM 失败；它们不作为真实内容证据。
- Standards / Spec 由同会话按 AGENTS、词汇表和规格自查；没有可用独立子代理，未完成并行双代理 code-review。Advisor 本次因与执行模型相同而禁用，不能声称有外部复核。
- 修正低高度预览挤掉已选列表；失效成员释放缩略图，恢复有效后重新注册；顺序调整后保留仍可用按钮的键盘焦点。单实例测试启动旧产物的问题已修正，并完成当前产物的定向验证。

仍待验证：最终真实全局 B/键盘与取消保留 Undo 回归、第三方与受保护内容、混合 DPI、物理输入法、Windows 11 官方支持基线和完整任务效率。DWM 返回成功也可能提供黑色或受保护画面，当前不能可靠区分合法黑色内容；应用名和标题始终保留。原漏标记的具体根因未定位；替换呈现不等于证明旧缺陷原因。

## 前轮：本屏设置、A/B 增量

设置、A 本屏全部排布、B 原生快速选窗已实现，共用原来的操作 guard、Undo 与最近成功记录。当时新增 15 项，完整入口为 49 项。这里分别记录全套运行、收尾定向检查及尚未完成的输入验收。

冻结源码在独立构建目录完整运行：**45 PASS / 1 FAIL / 3 unavailable**，退出码 1，用时 82.33 秒，无超时，运行期间源码哈希未变化。日志为 `artifacts/current-screen-final-checks.log`；源码清单和退出状态保存在临时目录 `paneset-screen-source.json`、`paneset-screen-final-status.json`。Release 构建 0 警告、0 错误。用户的旧 PaneSet 进程占用原输出目录，因此用 SDK 的 `--artifacts-path` 构建到独立目录，未关闭旧实例或清除其恢复记录。

- 15 项新增检查在这次完整运行中全部通过。
- `local-layout-keys` 在最后普通数字输入检查前，等待主面板成为真实前台失败，位置为 `InteractionChecks.cs:411`；该次检查保持 FAIL，根因及其与本轮改动的关系尚未证明。它未完成最后一段普通数字断言，不能据新 B 通过宣布历史前台不稳定已解决。
- `single-instance`、`shortcut-routing`、`shortcuts` 因已有用户实例或已注册的旧面板/恢复键不可运行，保持 unavailable。未停用户进程或用合成消息替换真实输入。这三项本轮没有新通过证据，前一轮记录保留在下文。

完整运行后自查补充了 A 零/一窗与入口异常的可见反馈，统一用不激活方式显示主面板；增强恢复默认冲突/成功验证，以及候选标题的有限宽度换行。随后重建仍为 0 警告/错误，`screen-arrange`、`shortcut-settings`、`quick-activation-failure`、`settings-ui`、`quick-invalid` 五项各 1 PASS / 0 FAIL，日志分别为 `artifacts/current-screen-final-<case>.log`。原生 `--probe` 退出码 0。49 项全套早于这些收尾改动，未再次重复全套。最后有限宽度修正后，另一次 `global-quick-input` 定向检查也通过（1 PASS / 0 FAIL），日志为 `artifacts/current-screen-final-global-quick-input.log`，覆盖最终选窗源码的真实全局 B、排布/恢复及非 Topmost 再进入/取消。

| 新检查 | 结果与证据范围 |
| --- | --- |
| shortcut-settings / settings-ui / configured-input | 临时配置、缺失与 null、损坏配置不覆盖、动态 ID、占用/重复、保存失败回滚、禁用、重启和释放；恢复 B 默认时占用失败保留旧键，释放占用后保存默认成功。真实新面板键及 A/B 回调触发、旧键停止触发；A 回调不执行用户桌面的全部排布。设置 700 高与 450 高、滚动到底部可触及 B 默认按钮通过。 |
| screen-guard / screen-arrange | 严格本屏入口、两个显示器双向操作、普通/最大化、外屏 fixture 不变、零/一窗不改变已有布局和 Undo、工作区元数据改变拒绝。A 协调检查注入 fixture 候选，未直接执行用户已有桌面的 A 全排。 |
| screen-partial / screen-eligibility / screen-member-move | 执行中首窗间接关闭、最小化或移屏第二窗；失败窗不被主动还原/拉回，其他有效项继续，直接/间接影响保留 Undo。移屏后的恢复失配保留记录，受控返回原屏后可重试恢复。 |
| quick-entry / quick-flow / quick-invalid | 真实面板按钮进入；每次新成员、默认前台、标题候选、布局预览不移窗、取消保留已应用几何与 Undo、确认仅排选中 fixture、失效成员保留说明、零/一窗不可确认、内部弹出控件、外部失焦、完整长标题换行。流程内合成键路由与原生几何分别断言。 |
| quick-input / global-quick-input | 真实 Space、Alt+1、Enter、Esc；布局下拉打开时第一次 Enter 仅接受选项、第二次才排布。真实自定义全局 B 从受控前台触发，在副屏排所选两窗并恢复；同一次用例又验证非 Topmost 前台进入与取消。其余 fixture 不变。 |
| mouse-routing | 实际主按钮点击进入 fixture 客户区并改变测试标题，验证下层点击指示器有效。覆盖层防穿透检查同时要求收到逻辑主按钮 down/up，再确认下层标题未改变；不以 SendInput 返回数单独认定送达。 |
| quick-activation-failure | 受控 ShowActivated=false 会话未取得前台，失败关闭且保持已有布局/Undo。该项验证选窗层 guard；Main 的激活失败反馈分支由源码审阅覆盖，没有稳定制造 OS 拒绝的端到端证据。 |

### 本轮输入诊断与保留的失败

早期直接调用选窗组件未建立原生前台前置，曾在 Ready 阶段失败；普通主面板激活请求也有返回 false 的日志。受控点击建立真实前台后组件可进入，但不能用这点解释所有外部干扰。真正注册的 B 从 fixture 前台收到 WM_HOTKEY 后，原 Show-only 路径曾因前台未转入而关闭；增加一次普通激活请求的对照记录为窗口可见、Activate=true、实际 foreground 等于 picker，并完成排布/Undo。最终保留普通请求及实际前台/键盘 guard，没有反复抢焦点或修改历史面板激活策略。

鼠标早期注入未触发面板按钮。本机读取 `SM_SWAPBUTTON=1`，修正受控注入以物理右按钮代表逻辑主按钮后，mouse-routing 与按钮入口在完整回归中通过。注入前检查物理按钮均松开，不替用户释放预先按住的按钮；命中必须属于测试拥有的窗口。此前多次因命中 Desktop、pebrel 或 Chrome 子窗口而拒绝注入，其中一次点在可见/可用、DPI=96 的测试主窗矩形内却命中另一进程；没有据此统一归因为 WXWork、坐标错误或系统权限，也没有放宽命中断言。左右键映射是此次鼠标协议的具体问题，不用于解释历史键盘/前台失败。

曾尝试 fixture 前台锁制造 Main 激活失败，但未稳定令 picker 关闭，超时保持失败；该测试设置最终移除，finally 已解锁，不将它算为生产回归修复或负向验收通过。最终没有临时激活诊断输出、前台锁策略或新的环境 skip。

### 截图与实机边界

设置截图 `paneset-settings-700.png`、`paneset-settings-450.png`、`paneset-settings-450-bottom.png`，以及 `paneset-native-selection.png`、`paneset-native-selection-invalid.png`、`paneset-native-selection-long-title.png` 已生成并读取。选窗图为 WPF 透明层单独渲染，未包含完整桌面合成；只能证明受控界面状态，不能当作真实桌面遮挡、输入或效率测量。

本机仍是 Windows 10、两个显示器 96 DPI。受支持环境的验收基线为 .NET 10 官方支持的 Windows 11 x64 版本，尚未在该基线运行完整检查；当前 Windows 10 只有受控运行证据，不承诺正式支持或普遍兼容。物理键盘/中文输入法、第三方应用、混合 DPI、真实拔屏与工作区改变、完整任务成本仍待实机验收。网页原型的视觉认可和 28 项浏览器检查不替代这些证据。本轮未提交、推送或发布。

## 前一轮：大列表与最近组

前一轮在跨显示器选组、多窗布局和快捷入口上，完成大列表、局部布局键及主动载入最近窗口组。两个增量均完成 /check 范围审查与修复；当时隔离完整回归通过。期间真实输入检查仍出现过焦点失败，不能据随后一次通过认定稳定性问题已解决。

### 当时的完整结果

最终未加诊断的源码在独立工作树中完整运行 34 项：**34 通过、0 失败、0 跳过**，退出码 0，用时 59.23 秒。隔离输入为 HEAD `444a16a` 加冻结的完整前置状态与本轮 5 个源码/检查文件，19 个已有源码、配置和工具文件逐字节匹配预期；最终 5 个文件与主工作区一致。Release 重建 0 警告、0 错误；随后 `tools/run.ps1 --probe` 原生探针退出码 0。日志及结构化退出状态保存在忽略跟踪的 `artifacts/q2-final-isolated.log`、`q2-final-isolated.json`、`q2-final-probe.log` 和 `q2-final-probe.json`。

此前主工作区最终源码的完整运行是 **33 通过、1 失败、0 跳过**，退出码 1，用时 67.7 秒，日志为 `artifacts/q2-final-checks.log`。失败在 local-layout-keys 的焦点前置条件，尚未发送 Alt 数字；该次 shortcuts 通过。相同隔离环境中，替换冻结 A 文件与当前 B 文件的串行诊断均又在原生前台等待失败。后续测得已有 Activate 与 SetForegroundWindow 均返回 false，WPF IsActive 为 true，原生前台不属于面板且不为 0；这不能确定系统拒绝的根因，也不支持放宽焦点断言或修改生产激活策略。临时诊断只在隔离代码中，已从最终验证源码删除。较早的 A 诊断完整 28 项通过属于加了诊断输出的运行，不能混同上述最终结果。

完整入口为 `verify.cmd`；合成消息、路由与真实系统输入各自报告，人工物理键盘与中文输入法仍未验收。

收尾审查在完整回归后修正了 WindowManager 的一条既有失败提示：原“原位置已保留”容易让人误以为未移动，现提示未确认达到预期布局、已改变时可尝试恢复。这是单条文案修改，未改窗口操作逻辑；修改后主区和隔离工作树重建均为 0 警告/错误，partial-failure 与 recent-group-failures 两项定向检查分别通过。上述 34 项结果来自修正文案前的完整运行，未再次重复完整回归。

| 检查组 | 最新结果及证据范围 |
| --- | --- |
| 布局 | 两窗、任意多窗、各布局、奇数尺寸、负坐标、无效区域、包含区域和互不重叠通过；12 窗列布局过小时移动前拒绝。 |
| 双向跨屏 | 主屏到副屏、副屏到主屏的普通及最大化排布和恢复通过；第三扇未选窗口保持原状态。 |
| 多窗 | 副屏三窗、三窗主辅、八窗网格排布与恢复通过。 |
| 记录及失败 | 整组快照、间接改变第二窗、部分排布与恢复、关闭、身份失效、恢复重试、无效新请求保留旧记录通过。 |
| 响应延迟 | 延迟响应时阻止冲突操作；永久挂起 fixture 终止后结束等待通过。 |
| 单实例 | 启动实际应用、关闭仅收起、重复启动恢复已有面板、第二进程正常退出、明确退出后可重新启动通过。 |
| shortcut-routing | 合成 WM_HOTKEY 到注册的原生 HWND，验证 hook 路由、面板解除最小化、副屏定位及默认选择、pending 时恢复保护、恢复、关闭仅收起、注册冲突和退出释放通过。该项不证明实际按键送达或获得前台。 |
| shortcuts | 最新隔离运行通过真实 SendInput 全局打开、恢复、副屏及等待保护；历史存在未送达和激活失败，本轮也有通过与失败，稳定性根因未确认。 |
| local-layout-keys | 最新隔离运行通过真实 Alt+1/2/3，在搜索框及布局控件内切预览不移窗，Enter 原生排布、恢复及未选窗口不变，普通数字仍输入搜索。此前焦点前置条件曾失败，不能称为持续稳定。 |
| local-layout-routing / selection-refresh | 合成 WPF 按键和线程键盘状态覆盖精确修饰键、中文与数字搜索内容、等待保护；选择模型与渲染同步、前台新组及重复唤起保留选择通过。合成路由不替代真实键盘或输入法组合。 |
| 最近组 | 6 项检查通过：成功载入顺序、目标、布局、当前标题；载入不移动或覆盖 Undo；无变化成功保留旧 Undo；连续布局与手动移窗恢复使用最新状态；缺失成员不替换；失效目标不回退；部分及延迟失败保留旧组；终局成功才发布；关闭收起、明确退出及迟到回调保护。窗口关闭期间枚举异常修复前失败、修复后可重新选择。 |
| 界面 | 多窗选择、布局切换、顺序轮换、刷新和切屏保持顺序、搜索、排布与恢复、最小尺寸按钮边界通过。 |

检查使用独立 fixture 进程创建的真实 WPF 顶层窗口；界面通过可访问性接口驱动真实控件。只移动 fixture 自建窗口，不操作已有用户窗口。布局和截图不能替代原生移动验证。

## 前置版本的快捷键定位记录

2026-10-09 的前置版本完整检查为 24 通过、1 shortcuts 失败、0 跳过，退出码 1，原生探针通过。历史日志为 `artifacts/multi-window-checks.log` 和 `multi-window-probe.log`。以下记录保留当时的具体失败，不将新一次通过当作根因修复。

最初 Ctrl+Alt+P 在本机注册失败，返回冲突错误 1409；当前绑定为 Ctrl+Alt+Space 和 Ctrl+Alt+Z，两项分别注册。此前定向实际按键检查曾通过，但它早于当前副屏和 pending 断言，不能作为最新完整验收。

当前首次等待同时包含恢复和获得前台。临时记录显示两项 RegisterHotKey 成功，面板在注入前原生 IsIconic 为 true，Ctrl、Alt、Space 均未按下；一次失败未收到生产 hook 的 WM_HOTKEY，也未执行打开分支。此证据不能证明生产激活逻辑有问题。

旧 keybd_event 没有插入计数反馈，检查已改用明确布局的 x64 INPUT 和 SendInput，并立即读取返回数量及失败错误。一次诊断返回 0/3、错误 5；独立线程探测随后返回 6/6，却未在限定等待内观察到 WM_HOTKEY。成功调用后的错误值不能用于解释失败，也没有据此断言 UIPI、输入桌面或第三方输入拦截是根因。会话为 Active，当前和输入桌面均为 Default。

尝试在注入前让测试面板获得前台没有成功。该新增前置条件已移除：全局快捷键本身不要求面板已有前台，不能把应用激活失败转换为跳过。最新完整运行中的 SendInput 没有触发“完全未插入、错误 5”的跳过分支，随后仍在首次等待失败。没有延长等待或发送合成消息来替代实际按键验证。

后续隔离跟踪进一步确认：SendInput 返回 6/6，面板持续最小化、前台句柄为 0，生产 hook 未收到 WM_HOTKEY。临时 WH_KEYBOARD_LL 使用唯一标记，只记录该批输入；虚拟键及扫描码两种方式均观察到完整的 Ctrl、Alt、Space 按下与松开事件。注入前 Shift、Ctrl、Alt、Space 和两侧 Windows 键均未按下。逐键诊断中，Ctrl 按下事件可见，但五秒内通用 Ctrl 及左 Ctrl 的异步按键状态仍为 0；尚未发送 Alt 或 Space，释放请求又被系统拒绝。观察到 hook 事件不能证明后续输入链已接受，也不能据此确认第三方拦截或桌面权限的根因。后续独立 Ctrl 探测的 CallNextHookEx 返回 0；只读检查确认窗口站为 WinSta0，线程和输入桌面均为 Default 且报告正在接收输入。仍未定位导致状态不更新的组件。

临时 hook、逐键探测和生产诊断输出均已移除，检查保留常规的已检查返回值的 SendInput 组合键。清理后的完整回归再次为 24 通过、1 失败。生产恢复和激活流程没有因未证明的猜测修改；合成消息检查独立覆盖后续处理。余下需定位输入状态为何不更新，或通过运行中的 PaneSet 完成真实键盘验证。

## 实测环境

Windows 10 Pro 22H2、Build 19045、x64，交互会话 1。局部 .NET SDK 10.0.401 已校验官方 SHA-512；应用和检查声明 PerMonitorV2。

| 显示器 | 工作区域，物理像素 | 实测 DPI |
| --- | --- | --- |
| 主屏 | X=0，Y=40，宽=2560，高=1400 | 96，100% 缩放。 |
| 副屏 | X=2560，Y=40，宽=2560，高=1400 | 96，100% 缩放。 |

任务栏在上方。四组双向普通和最大化跨屏测试的 DPI 均为 96/96，不构成混合 DPI 验收。

## 已实现行为

- 宽窗分栏、窄窗折叠预览、精确 Alt+1/2/3 只切预览，确认后执行。
- “调整最近组”显式载入最后一次整组最终成功的成员、顺序、目标及布局；记录仅在当前进程中，与恢复快照独立。
- 当前虚拟桌面所有连接屏幕上的普通或最大化窗口独立列出，标明来源屏幕，支持应用名和完整标题搜索。
- 至少两窗多选、编号预览、明确目标显示器；自动、等宽、一主多辅和网格布局，按选择顺序轮换。
- 刷新按 HWND、generation、PID、线程、进程启动时间及类名保留选择和顺序，避免句柄复用造成错误保留。最近组的 generation 失配已有 UI 检查，真实 HWND 复用仍未实测。
- 首次修改前保存整组最新状态，排布后读回；受影响窗口分别恢复各自来源屏幕及正常或最大化状态。失败记录保留供重试。
- pending 时按钮与全局恢复共用操作保护；700 ms 是反馈阈值，不能当成取消。
- 收起、Esc、关闭保留运行和恢复记录；明确退出释放快捷键。重复启动通知已有进程，面板已有焦点时也保持打开。

## 界面检查

检查 820 × 730、600 × 730、820 × 600、600 × 600，以及长标题和完整快捷键说明。760 宽度是分栏断点，另检查 759/760；窄窗默认折叠预览，高度低于 680 时预览为 48，状态区为 24。正常短标题检查测得 6 个完整行，列表视口约 364、单行约 60；最小尺寸不承诺 6 行。按钮和预览在真实 GetClientRect 客户区内；局部键只改变预览。列表显示最多 64 个字符，搜索和 tooltip 保留完整标题。

截图来自受控 WPF 渲染，只含检查内容：

- [正常尺寸](../artifacts/paneset-normal.png)、[窄窗口](../artifacts/paneset-narrow.png)、[低窗口](../artifacts/paneset-short.png)、[最小尺寸](../artifacts/paneset-minimum.png)、[长标题](../artifacts/paneset-long-title.png)。
- [三窗主辅](../artifacts/paneset-three-main.png)、[八窗网格](../artifacts/paneset-grid.png)、[网格最小尺寸](../artifacts/paneset-grid-minimum.png)、[快捷说明最小尺寸](../artifacts/paneset-shortcuts-minimum.png)。
- [分栏前](../artifacts/paneset-breakpoint-759.png)、[分栏后](../artifacts/paneset-breakpoint-760.png)、[最近组](../artifacts/paneset-recent.png)、[最近组最小尺寸](../artifacts/paneset-recent-minimum.png)、[缺失成员](../artifacts/paneset-recent-missing.png)。

外观现支持 A 亮色与 C 暗色，默认跟随 Windows 应用模式，设置中可手动覆盖。主题值与快捷键保存在同一配置中；切换时更新主面板、设置和本屏总览的颜色。受控 WPF 检查保存了[亮色主面板](../artifacts/paneset-appearance-light.png)、[暗色主面板](../artifacts/paneset-appearance-dark.png)、[暗色设置](../artifacts/paneset-settings-dark.png)及[暗色本屏总览](../artifacts/paneset-overview-dark.png)。`appearance` 覆盖主题读取、界面切换和保存，`shortcut-settings` 覆盖快捷键修改与主题值互不覆盖；真实 Windows 亮暗切换仍需人工操作确认。Segoe UI 与微软雅黑回退保留，没有高频操作动画。

本次全量检查用时 99.6 秒，结果为 59 通过、3 失败、3 跳过。`overview` 在一次 700 × 450 的按钮边界断言失败，单独运行时又因 Chrome 抢到前台而中断；`quick-entry` 与 `quick-activation-failure` 也未在等待期得到预期前台。现有 PaneSet 占用实例及全局键，相关 3 项跳过。定向 `appearance`、`shortcut-settings`、`overview-size-ui` 分别通过，其中 `appearance` 还独立检查 700 × 450 的主要按钮边界。这些结果不足以把全量检查标记为通过。

## 审查及剩余边界

按项目说明、词汇表和当前规格进行 Standards / Spec 人工核对：布局计算独立，系统调用集中于窗口管理和入口模块；ReadState、排布及恢复检查保存身份；跨屏记录来源和目标工作区域；正常界面、全局恢复及 pending 收集使用同一操作保护；单实例退出停止轮询并释放事件和互斥量。修正了重复启动在面板已有前台时误进入收起分支的源码问题，并增加独立消息处理检查。没有并行双代理审查；Advisor 提供定位和验证边界建议，没有独立读取完整仓库。

两个增量分别以冻结前置状态和增量 A 快照为审查边界，避免将已有未提交改动算作新功能。按 /check 做源码、架构、窗口身份安全、终局状态及视觉对抗核对；没有并行代理，Advisor 只收到摘要。修复了低高度预览裁切，增加原生边界断言；修复候选枚举后窗口关闭导致 updating 不释放，旧代码回归为 Expected False, got True，finally 修复后通过。扫过两个 updating 批量更新点并补齐释放，所有新截图已筛选 fixture。新增 9 项检查，6 项用于最近组及异常恢复，直接和延迟发布均走生产 Apply 路径。

最新隔离完整运行通过，但实际输入在本轮仍表现不稳定；任务 01 保持 in-progress，不宣称根因已修复。最近组失效身份检查通过修改保存的 generation 验证，不等于真实 HWND 复用；无目标与过期区域检查通过注入保存的失效句柄和旧区域验证，不等于物理拔屏、真实工作区改变或混合 DPI。其他桌面成员、人工物理键盘和中文输入法组合未实测。其余边界如下：

- 主要实测受控 WPF 窗口，尚未系统验收浏览器、PDF、管理员应用等第三方程序；不自动提权。
- 混合 DPI、125% / 150%、左侧任务栏、负原点的真实显示拓扑和断屏未实机验收。显示环境变化后不会自动迁移恢复坐标。
- 逐窗恢复不是原子事务；身份检查与使用之间存在系统固有竞态，不能保证捕获快照比较结束后的全部延迟变化。
- 恢复记录仅在进程中保留。退出清除记录，已发出的未响应请求可能稍后生效，不能称为取消成功。
- macOS、长期窗口组、联动缩放、跨虚拟桌面和最小化窗口唤醒未实现。
- 当前 Windows 10 Pro 22H2 不在 .NET 10 官方支持列表中，实测可运行不等于正式兼容性承诺。
- 尚未完成竞品和完整任务耗时比较，不能根据功能实现宣称更快或独创。

## 拖动投放与托盘常驻（本轮）

本轮移除了指针参照放置与位置图选择器（提交 03008b3、47cb48b 交付、从未发布），交付拖动投放与托盘常驻；交互决定与规格见 .scratch/single-window-placement/ 与 .scratch/tray-presence/。检查集合同步重写：删除 21 项旧 placement/pointer/reference 检查，新增 drag-placement-zones、drag-placement-state、drag-placement、drag-placement-settings、tray-presence 五项；共享回归全部保留。

最终隔离全量运行：**71 通过 / 0 失败 / 0 跳过**（2026-10-27，命令 `powershell tools/run.ps1 -Check`，Windows 10 Pro 22H2，双屏 100% 缩放，工作树为本次交付的未提交状态；逐项输出未保留，以本记录汇总为准）。期间两次运行未计为有效验收结果：一次观察到浏览器持有前台并伴随真实键鼠活动，输入保护按设计拒绝注入；另一次运行中系统锁屏，此后输入类检查连锁失败。两次均未作为通过或失败依据。

本轮修复的产品问题：托盘可用时 OpenForForeground 无法重新显示隐藏窗口；在 StateChanged 内同步改回窗口状态会使原生窗口停留在 iconic 而 WPF 误认为常规（改为拦截标题栏 WM_SYSCOMMAND，程序化收起全部走隐藏路径）；全局开关键仍按旧方式最小化到任务栏。检查侧修复：fixture position 命令参数错误导致子进程停止响应；single-instance 对刚唤起窗口的 UIA 空代理缓存（改为逐次重新解析句柄）；drag-placement 的前台取得改为零位移合成鼠标事件（SendInput，属合成输入，无可见输入效果，仅作用于检查进程与 fixture）加线程输入附着。

未验证边界：原生拖动环路（真实鼠标拖动进入投放、松手提交、Esc/右键取消、屏幕边缘与 Windows 自身贴边的竞争、覆盖层与缩略图条的实际观感）属人工验收，本轮未运行；MOVESIZE 回调延迟对拖动起点快照精度的影响未实测；混合 DPI 跨屏分区与恢复沿用既有坐标合同、未新增实测；托盘图标的真实出现、左右键与 Explorer 重启重挂未自动化。旧入口移除后，此前其人工验收结论随之失效，不再作为本产品行为的证据。

### 人工验收首轮（2026-10-27，交换主次键环境）

用户以"鼠标左键功能设为右键"的环境实测拖动投放，功能端到端确认可用（分区出现、投放成功、缩略图正常）。过程中发现并修复四处问题，均属自动检查未覆盖的原生观察与渲染路径：

1. 主键读取不感知交换映射：GetAsyncKeyState 与低级鼠标钩子看到的是物理按键，交换发生在更上层的消息翻译；改为按 SM_SWAPBUTTON 将主键映射到物理右键读取。这是用户环境里"从不进入投放状态"的直接原因。
2. 覆盖层从未真正显示：分层弹出窗口创建后缺少 HWND_TOPMOST + SWP_SHOWWINDOW 的定位调用（放置反馈层有、新覆盖层遗漏），补齐后分区可见。
3. 鼠标钩子丢弃注入标记事件：按键由鼠标驱动软件重映射注入时带 LLMHF_INJECTED，被过滤器全部忽略；改为鼠标事件不按注入过滤、同时接受两种释放消息，副键按下取消改由"结束时主键仍按住"的既有判定覆盖。Esc 仍走低级键盘钩子并保留注入过滤。
4. 模板缩略图不填满卡片：缩微图固定 48×30 逻辑像素未缩放；改用 Viewbox 等比填满。

拖动监视器内置 PANESET_DRAG_DEBUG=1 环境变量诊断日志（写入临时目录 paneset-drag-debug.log），默认关闭。以上四项修复以用户实机验证为准；自动套件的 71/71 结论来自修复前的构建，修复仅触及套件不覆盖的监视器与覆盖层路径，编译零警告。人工清单其余各项（取消、最大化恢复、跨屏、贴边竞争、托盘真实交互等）仍未执行。

### 发版验证（2026-10-27，v0.3.0-preview.1 树）

B 入口收起改为与托盘一致的隐藏路径（检查进程无托盘时走逐语句相同的回退）、版本号升至 0.3.0-preview.1 后，三次全量运行出现轮换的输入类失败（overview-viewport、quick-cancel-undo、overview-size-ui、overview、mouse-routing、quick-flow、quick-input、global-quick-input、quick-activation-failure 中的不同子集），证据显示用户会话活动与检查进程自身窗口的前台竞争，属仓库已记录、暂停追查的间歇输入类。全部失败项在同一最终树上逐个单跑均通过。当日早些时候的 71/71 全量通过来自此前树，与本树的差异仅上述一行回退等价改动与版本字符串。间歇输入类不因本轮发版标记为已修复。
