# 配置检查首次原子替换失败

Status: needs-info
Progress: in-progress

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
