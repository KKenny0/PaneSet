# 配置检查首次原子替换失败

Status: ready-for-agent
Progress: done

当前修复与验证结果见文末“第 5 阶段修复与回归”及验收记录；下文按时间保留各次未修复阶段的证据。

## 现象

总览增量的完整回归中，`shortcut-settings` 在 `ScreenSelectionChecks.cs:458` 的首次 Change 返回：

```text
未保存，原绑定保留。无法删除要被替换的文件。
```

预期是空错误字符串；尚未执行后续主动锁文件的负向场景。该项保持 FAIL，不能归因为 LockApp 的前台阻挡。

## 当前证据

- 日志：`artifacts/overview-final-checks.log`，完整结果 38 PASS / 13 FAIL。
- 每次用例创建唯一临时配置路径；发生于 `ShortcutSettings.Save` 的 `File.Replace` 路径。异常经绑定事务转成失败反馈。
- 本次总览改动没有修改 ShortcutSettings 或 PanelShortcuts；这仅界定变更范围，不能证明与新代码或环境无关。
- 尚无失败时文件持有者、Win32/HResult 或可重复最小复现；不能猜测杀毒、锁屏或读写时序为根因。

## 后续

在独立临时文件上采集首次替换的错误码与文件状态，区分持续占用与瞬时失败。先证明原因，再决定是否修生产逻辑；不采用自动重试或非原子写入来掩盖失败。保留实际原绑定/磁盘不变的断言。

## Comments

作为独立问题记录，不阻止总览代码交付为待试用版本，也不将完整回归标为通过。

## 本轮定位证据

用户授权稳定性收尾后，独立 `shortcut-settings` 一次 PASS，涵盖保存/注册失败保留原值、清理暂存注册及重载。不能注销原失败。

已建立秒级失败探针，全部使用唯一 Temp 目录：

1. 直接链接生产 `ShortcutSettings.cs` 的独立控制台，预设 20 目录×50 次保存和读回，第一个目录第 19 次替换出现相同异常，HRESULT `0x80070497`。
2. 去掉每次读回，第一次目录第 7 次仍同错误，排除配置 Load 是必要触发条件。目标属性 Archive，非只读。Restart Manager 失败后查询返回 `none-at-query`；不能据此认定失败瞬间没有持有者。
3. 仅 `File.WriteAllText` + `File.Replace`，第 7 次同错误，排除 JSON 序列化、Flush(true)、快捷键、WPF、总览是必要触发条件。
4. Python ctypes 直接调用 Win32 `ReplaceFileW`，第 28 次返回 1175，排除 .NET 封装是必要触发条件。

可独立重现的命令：`python C:/Users/Administrator/AppData/Local/Temp/paneset-native-replace.py`。代码及 C# 探针位于 Temp 的 `paneset-save-probe/`，失败即停止，不重试原操作。日志：`artifacts/stability-save-probe.log`、`stability-save-no-read.log`、`stability-save-native.log`、`stability-save-win32.log`。上述数字为该次失败位置，不是固定阈值；探针高频保存不代表正常使用故障概率。

当前结论：错误来自系统 ReplaceFileW 拒绝替换，具体触发者尚未识别，不能猜测杀毒/索引器为根因。`fltmc filters` 返回 Access denied；Procmon/handle 不在 PATH，未提权、安装或停止服务。更深定位需要失败瞬间的文件 I/O/过滤器证据。未改生产保存算法，未增加重试或改成非原子写入。

## 2026-10-12：configured-input 保存路径继续诊断

基线 `7032654`，开始时工作区干净。按 diagnosing-bugs 延续原生复现，不重跑无关桌面路径。`configured-input` 的前三次 Change 位于真实前台/按键阶段之前，本轮聚焦其相同保存失败，未执行完整 configured-input 或原快捷键输入验收。生产源码未改，状态继续 needs-info / in-progress。

### 红色反馈循环

历史探针仍存在，先读后运行：`python C:/Users/Administrator/AppData/Local/Temp/paneset-native-replace.py`，第 0 次替换 Win32 1175 / `0x80070497`，退出码 1，约 0.3 秒。由于原脚本会在 finally 删除现场，另建保留现场的独立原生探针。

当前更短入口：

```text
python C:/Users/Administrator/AppData/Local/Temp/paneset-replace08-diagnosis/capture-probe.py
```

已运行，第 2 次替换 Win32 1175，退出码 1，约 0.3 秒。每次仅创建唯一 Temp 目录；同一目标连续替换，源写完后等待 10 ms；失败立即读取 last-error，记录 PID、UTC、QPC、目标/源内容与属性，停止该试次，不重试失败操作。每次源内容带不同序号，断言失败目标等于上一次成功内容，源仍等于本次新内容。最多 500 次，没有复现时退出 0，但单次绿色不代表修复。

原生声明明确参数/返回类型，使用 `WinDLL(..., use_last_error=True)`；错误只在失败后立即读取，成功时不解释残留错误码。

### 可证伪假设与单变量结果

先展示三个假设：短暂占用、目录/卷因素、目标移除阶段受阻；独立文件对大量通过后增加同一目标反复替换的时序假设。各条件交错运行，随机种子固定 7032654，每试次第一次失败即停止、保留文件对。

| 同一目标连续替换（最多 100 次/试次） | 失败试次/总数 | 失败前累计成功替换 | 错误 |
| --- | --- | --- | --- |
| C 卷，0 ms | 4/10 | 825 | 全为 1175 |
| C 卷，10 ms | 10/10 | 215 | 全为 1175 |
| D 卷，0 ms | 0/10 | 1000 | 无 |
| D 卷，10 ms | 0/10 | 1000 | 无 |

这建立了高复现率的红色循环，也不支持简单增加延迟的修复。两卷通过 Get-Volume 确认为不同 NTFS 卷，但目录根也不同；不能把结果当成卷本身的因果证明，更不能推算正常用户的故障概率。

其他对照：

- 每试次新文件对：C 卷 immediate 20/20 PASS，source-age100ms 18/20 PASS、target-age100ms 19/20 PASS；3 次失败均 1175。D 卷三条件共 60/60 PASS。100 ms 等待也未消除失败。
- 受控目标读句柄不给删除共享：0/20 PASS，全为 32；允许 read/write/delete 共享则 20/20 PASS。源文件的受控读句柄无论 read-only 共享或完整共享，均为 32，与 ReplaceFileW 官方文档中源以无共享方式打开一致。普通共享冲突不能直接解释 1175。
- 独立新文件对的 ReplaceFileW、MoveFileExW 替换、DeleteFileW、带备份 ReplaceFileW、ignore-merge ReplaceFileW，各 100/100 PASS。这些绿色对照没有复现连续目标的故障，不能证明改算法或标志可解决它。未对失败现场先删除/重命名后再推断状态。
- 全部已捕获的 1175 保留原目标内容及本次源文件。目标属性为 Archive，未观察到只读；Microsoft 官方 ReplaceFileW 文档明确 1175 表示目标无法删除，两者保留原名。此证据定位失败阶段，不能识别阻挡者。

### 关联进程查询与工具限制

失败返回后，在文件检查之前调用 NtQueryInformationFile 的 FileProcessIdsUsingFileInformation（47）。数量按 ULONG、PID 按 ULONG_PTR 和指针对齐解析。用主动持有完整共享读句柄做正向校验，能观察到本探针 PID 160868。随后 20/20 试次均重现 1175，源/目标查询均成功但 PID 列表为空。

两个查询合计耗时 **104–123 ms**，并非失败瞬间快照；不能排除瞬时持有者、映射或过滤驱动。首次未做校验的采样另保留，不用它替代已校验结果。

`fltmc filters` 仍 Access denied；Procmon、handle、CDB/WinDbg 未在 PATH，常见 Windows SDK Debuggers 目录也不存在。只读 registry/CIM 查询确认多种 FSFilter 服务运行，但不是实际卷挂载或失败调用栈证据，不能指认具体厂商。未安装工具、提权、停服务或改安全策略。

### 生产失败契约

Temp `contract/` 直接链接现有 ShortcutSettings.cs、PanelShortcuts.cs、Native.cs、LayoutEngine.cs；仅用小型 WindowManager adapter 提供与保存无关的前台属性。宿主是两个不显示的 message-only HwndSource，使用真实且检查返回值的 RegisterHotKey；绑定冲突单列 unavailable，只释放本探针持有的 ID，不注入输入或改变桌面。

Release 构建 0 警告/错误。20/20 试次出现同一“无法删除要被替换的文件”保存失败，0 unavailable，失败前累计 391 次 Change 成功。每次失败均断言磁盘旧字节、内存绑定、旧 ID、旧键持有状态保留；暂存键已释放，临时文件清理，重载仍为旧配置。退出 1 用于表明保存失败仍存在，安全断言全部通过。Change 的返回文案不暴露 HRESULT，因此该探针不声称直接观测其错误码；1175 来自原生探针。未把成功的回滚断言称为保存故障修复。

### 证据与下一步

脚本/证据稳定归档：[诊断 README](D:/VibeProject/diagnostics/paneset/issue08-20261012/README.md)，包含探针源码、八份原始 JSON 和三份合成失败现场，不含构建二进制。原始 Temp 位置另列在索引中；D 卷试次已移出工作树至 `D:/Temp/PaneSet.Replace08-evidence-44e4651e16e7441684de5d4554dac7ad`，旧 JSON 路径通过 `d-evidence-location.json` 的前缀映射解析。

已准备 `capture-kernel.ps1` 和自定义 `replace08.wprp`，用系统 WPR 10.0.19041 采集文件操作、minifilter 失败与相关栈、进程/模块元数据；不启用 registry/network/input provider。`-ValidateOnly` 已执行、退出 0，配置被 WPR 接受；短探针也已独立红色验证。尚未实际启动/停止内核录制，没有 ETL 证据。

当前令牌已确认为非管理员；真正捕获需用户明确授权管理员权限。稳定目录中的 [capture-kernel.ps1](D:/VibeProject/diagnostics/paneset/issue08-20261012/capture-kernel.ps1) 无自动 UAC，拒绝已有 WPR 录制，用唯一 instance，finally 只停止自己的实例。探针进程最多运行 10 秒，超时仅终止自己创建的进程，再保存并停止自己的录制；即使脚本位于 D 卷，fixture 仍显式限定 C 卷 Temp，避免改变复现条件。录制覆盖短探针期间的系统文件 I/O，不能预先按路径过滤；ETL 可能包含其他文件名和进程元数据，本地保留，分析时以 fixture 路径、PID、失败 QPC 对齐，同时保留其他进程对该 fixture 的活动。

根因仍缺少失败操作的底层状态和调用栈，不进入生产修复阶段。禁止以自动重试或非原子覆盖掩盖结果；不关闭任务、不改写历史全套统计、不推送或发布。

## 2026-10-12：用户批准内核捕获后的机制证据

用户随后明确回复“批准”，授权一次管理员短时捕获。已通过 RunAs 执行，未停止用户应用或安全服务、未注入桌面输入、未修改生产源码。ETL 会话实际约 3.038 秒；含合并保存的外层耗时约 18.4 秒。finally 保存 ETL 后 `wpr -status` 确认未录制。

### 本次 1175 的具体失败请求

探针 PID 60980 / TID 96244，在第 14 次调用捕获 1175。失败前目标内容 `new13`、源 `new14`；失败后两者保留。原始 ETL 位于诊断目录 `kernel-capture-d9ed44217a3d4872b21c082ed5c7f73d/replace08.etl`，约 440 MB，仅本地保留。

用任务目录内的 Microsoft.Diagnostics.Tracing.TraceEvent 3.2.8 流式读取，不引入产品依赖。ETL `PerfFreq` 与系统 QueryPerformanceFrequency 均为 10,000,000；Python perf_counter_ns 以 `ns × frequency / 1e9` 换算，按原始 QPC 对齐，不把纳秒直接当 ETW ticks。

| 事件 | QPC ticks | 证据 |
| --- | --- | --- |
| ReplaceFileW 进入 | 10302583277889 | 探针外部计时 |
| PID 185240 打开内部 `settings.json~RF3d687e78.TMP` | 10302583304411 | FileIO/Create，FileObject 18446667689626983888，ShareAccess=7 |
| 该打开成功 | 10302583304587 | 同一 IRP 的 OperationEnd，NtStatus=0 |
| 旧 settings.json 覆盖重命名到该 RF 文件 | 10302583305091 | FileIO/Rename，InfoClass=65；RenamePath 给出 RF 目标名 |
| 重命名失败 | 10302583305419 | 同一请求 IRP 18446667690933095848 的 OperationEnd，`0xC0000022 STATUS_ACCESS_DENIED` |
| PID 185240 的该文件 Cleanup | 10302583308015 | 相同 FileObject，晚于失败完成 |
| ReplaceFileW 返回 1175 | 10302583319358 | 探针外部计时 |

两个对应 StackWalk 通过原始 QPC 和 TID 精确配对；失败完成栈包含 Ntfs.sys。其他过滤驱动也出现于栈和挂载列表，但其存在不等于由它主动拒绝，不能据此归责杀毒或加密组件。较早的某些非零状态出现在成功替换中，也不当成目标错误的原因。

PID 185240 在捕获之后仍存在；只读进程元数据确认它是 Raycast 2.7.3.0 安装目录下的 `Raycast/backend/node.exe`，父进程 PID 54584 为 Raycast，启动时间 2026-10-10 早于捕获，避免仅凭复用 PID 认错进程。原始命令行未输出或保存。这里证明它在该时刻打开内部 RF 文件，尚未识别 Raycast 内部发起该操作的模块，也不推断它是历史所有 1175 的唯一原因。

### 受控因果模式复现

把变量从“持有原 settings.json”改为“持有覆盖重命名的内部目标”，仍只操作自建 fixture。

1. MoveFileExW 覆盖一个独立目标：不持有 20/20 PASS；持有源且允许完整共享 20/20 PASS；持有覆盖目标且允许完整共享 0/20 PASS，全为 Win32 5；重命名前释放该目标句柄 20/20 PASS。允许删除共享不保证可以覆盖已有打开句柄的目标。
2. D 卷目录通知探针直接调用原 ReplaceFileW，不添加备份：notify-only 20/20 PASS；20 次 hold 条件中，实际及时打开 RF 文件的 19 次全部返回 **1175**，未及时截获的 1 次 PASS。每次失败旧目标和新源都保留；所有自建句柄与目录通知均已结束。

该实验在此前原始无备份循环全部通过的 D 卷，单独引入内部 RF 文件句柄就重现相同 1175，确认了受控持有内部覆盖目标可以触发这一故障；无需停止真实 Raycast 来获取可证伪信号。实际捕获中 Raycast 打开与失败时间相邻且后续有相同 FileObject 的 Cleanup，但大量丢失事件意味着不能把缺少中间 Close 当成持续持有的排他证明，不能完全排除地址复用或其他参与因素。可执行入口：`python D:/VibeProject/diagnostics/paneset/issue08-20261012/rf-control.py`。

### 隔离的修复候选，尚未应用

给 ReplaceFileW 提供同目录、唯一且未使用的显式备份名，沿用该 API：

- C 卷交错 10 试次 × 最多 100 次替换：无备份 10/10 试次失败，失败前 327 次成功；唯一备份 0/10 失败、1,000 次成功。逐次断言新目标正确，备份等于替换前旧字节。
- D 卷目录通知 `rf-control.py --backup`：notify-only 20/20 PASS；实际持有备份句柄 20/20 PASS。40 次首个备份通知均为 RenameNewName（action 5），未观察到先创建 RF 占位目标的模式；40 份备份内容均为旧字节。

这些结果支持显式唯一备份名可以避开本次内部占位目标竞争窗口，仍是候选验证，**不是生产修复或完整输入验收**。探针保留备份用于证据，尚未验证生产中的备份清理、清理失败后磁盘/绑定发布一致性，以及 1176/1177 等不同失败状态。不得直接加 finally 删除备份后把“写入成功、清理失败”误报为未保存。

### 取证工具的收尾与证据限制

原外层 Start-Process.ExitCode 得到 null，故 `approved-capture-result.json` 保留 Success=false；它不表示没录制到 1175。实际 ETL 已保存、WPR 已停止。之后改用直接 .NET Process 的 invoke-owned-probe.ps1，probe-only 实际验证得到整数退出码 1 / Win32 1175；没有第二次内核录制。修正后的完整捕获入口只做配置校验，未重新执行。

tracerpt 首次参数组合错误，纠正后转换 40 秒超时，只完成约 21%；854 MB 的截断衍生 XML 已删除，原始 ETL 保留。TraceEvent 完整读取报告 **1,174,675 丢失事件**，不能根据某事件缺席排除原因。最终 extract-v2 按明确 fixture 路径及对象/IRP 生命周期关联，Close 后移除对象，排除仅把 fixture 名包含在其他路径中的事件；原始、初版衍生和最终衍生分开保留。完成解析不等于无丢失录制。

[聚焦证据与断言结果](D:/VibeProject/diagnostics/paneset/issue08-20261012/evidence/root-mechanism.json)包含上述时间线、栈、读者生命周期、原始外层结果与受控对照；summarize-mechanism.py 已执行，所有关联和字节断言通过。新原始 JSON、合成失败文件也已复制到稳定诊断目录。官方 ReplaceFileW 及 FILE_RENAME_INFORMATION 语义补充到 docs/REFERENCES.md。

当前分诊改为 **ready-for-agent / in-progress**：失败请求已定位，受控竞争机制已复现，并有隔离通过的修复候选，可继续实现与验证。保存故障仍未在生产修复，完整 configured-input、Windows 11 基线和历史全套未重跑；问题不关闭、统计不改写，不提交、推送或发布。

## 2026-10-12：第 5 阶段修复与回归

用户追加授权按 diagnosing-bugs → TDD → 验收 → 规范/规格审查 → 提交 → 复盘完成本路线。本次生产 Save 继续使用同目录临时文件、Flush(true) 与 File.Replace，提供唯一未使用的显式备份名。1177 按官方磁盘状态尝试一次无覆盖恢复旧备份；恢复失败时保留备份并提供路径。提交后的清理异常返回提示，Change 发布新配置与新注册；提交失败仍保留旧绑定、释放暂存注册。组合恢复/临时清理故障保留原 HRESULT 与全部错误，SettingsWindow 显示清理提示。

TDD 包含真实 Save 的 1175 红/绿、真实替换后持有备份导致清理失败的红/绿，以及注入 1177 同时构造真实移动后的磁盘状态、组合清理异常的红/绿。1176/1177 是控制流故障注入，不声称实际 Windows 返回这些状态；锁目标、锁备份与锁临时文件使用真实句柄。

独立 checkout 基线 7032654 仅加同一 settings-replace：第 7 次 Save 返回 Win32 1175，观察到累计 7 次 RF 打开、退出 1；此计数不证明失败瞬间仅由该监听器持有。应用本次代码后，Release 0 警告/错误，7 项定向检查全部 PASS、退出 0，其中真实 Save/Load 连续 200 次、Change 与注册验证往返 100 次。

完整 64 项结果为 **57 PASS / 4 FAIL / 3 unavailable**、退出 1；新增 7 项、shortcut-settings 和最终未加临时日志的完整 configured-input 均 PASS。4 个失败分别为 overview-size-ui、quick-cancel-undo（物理鼠标键已按下，拒绝注入）、quick-flow、local-layout-keys；其他三项在 UI 状态/前台等待超时。3 项因现有用户 PaneSet 或快捷键占用 unavailable。此前 configured-input 单独运行曾越过三次保存、在前台等待超时；其后一次辅助日志版本 PASS，保留失败，未更改激活、等待、断言或输入保护。最终全套之后仅增加 Save 返回值契约注释和测试探针退出清理，最新版在独立 checkout 复跑上述 7 项；未再次重复桌面全套。

规范轴检查项目说明、词汇表、无新产品依赖、窄存储 seam 和资源释放；规格轴核对问题 08 与快捷键事务、磁盘重载和真实注册状态，没有未处理范围内发现。当前工具未提供并行子代理，审查为本会话两个轴的人工核对；Advisor 仅提供边界建议，不宣称独立完整源码审查。问题 08 实现完成；任务 10 继续 needs-info / in-progress。历史 48 PASS / 6 FAIL / 3 unavailable 保留，Windows 11 支持基线未验收，未推送、发布或重建既有 preview。用户实例未重启。

实际命令、原始 stdout/stderr、整数退出状态及冻结源码校验在 [phase5 证据](D:/VibeProject/diagnostics/paneset/issue08-20261012/phase5-evidence/)；详见 [验收记录](../../../docs/VALIDATION.md)。
