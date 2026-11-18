# PaneSet 定位与技术评审（2026-11-17）

> 三路并行分析汇总：竞品格局调研（联网核实）× 代码库技术审计 × 产品 UX 评审。
> 基线：main @ `de9182a`（含性能提交 `e553a0c`/`96180ee`）。本文是决策文档，只保留结论与可执行项；论证细节见各节引用。

---

## 0. 一句话结论

**差异化成立，但成立得很窄：Windows 上没有任何单一竞品提供「跨屏勾选任意窗口 → 平铺到指定屏 → 一键全部还原」这条完整链路；而产品最大的风险是——唯一的护城河（"敢排布，因为能复原"）恰恰是当前体验做得最弱的一环（信任链感知层）。**

战略上：不要以「窗口管理工具」大类与 FancyZones / DisplayFusion 正面竞争，以「临时对照 / 会前会后复原」具体场景切入做口碑。
战术上：小时级修复三个健壮性硬伤 + 用现有浮条组件把"退路写在脸上"，是当前投入产出比最高的一批动作。

---

## 1. 竞品格局与差异化判断

### 1.1 能力矩阵（逐项核实过）

| 能力 | PaneSet | Win11 Snap | FancyZones | DisplayFusion Pro | WindowLayout(2026新) | 其他 |
|---|---|---|---|---|---|---|
| 选任意多窗（跨屏挑选） | ✅ 面板勾选+搜索+缩略图 | ⚠️ Snap Assist 仅同屏 ≤4 窗 | ❌ 无选窗概念 | ❌ 无选窗 | ❌ 全量保存 | PowerToys Workspaces 面向"启动应用"；AquaSnap/MaxTo ❌ |
| 平铺到**指定屏** | ✅ 显式选目标屏 | ❌ 仅当前屏 | ⚠️ 单窗移 zone；跨屏 zone 需同 DPI | ⚠️ 单窗移屏有热键；多窗平铺到指定屏 **2013 年请求至今无内置**（开发者本人确认，靠社区脚本） | ⚠️ 恢复到保存时的屏 | — |
| 一键还原到**排布前原位** | ✅ 排布即自动快照，Ctrl+Alt+Z 撤销语义 | ❌ Snap Group 只回"吸附后布局"，最大化脱离即随机落位（微软版主确认无解） | ❌ 仅单窗 unsnap 恢复尺寸；恢复全部的请求 #27311 被关 duplicate 多年未做 | ⚠️ Save/Restore Window Positions 按句柄可绑热键，但**需先手动保存**（快照非撤销）；匹配靠 进程+标题+类 三条件，浏览器改标题即失效（开发者确认） | ⚠️ 有 Undo last restore，但基于主动保存的 profile | — |
| 持久布局保存（Profile） | ❌ 仅进程内"最近组" | ⚠️ Snap Group | ✅ | ✅（Pro 付费） | ✅ 含 z-order/自动触发 | Workspaces ✅ |
| 拖动投放模板 | ✅ Shift+拖动 6 模板 | ⚠️ 拖边缘/角落 | ✅ 拖入 zone + 滚轮换布局 + 任意自定义 zone | ✅ | ⚠️ | AquaSnap 磁吸 |

### 1.2 真正独有 vs 不独有

- ✅ **完整链路独有**（Windows 范围内）：勾选任意跨屏窗口 → 平铺到指定屏 → 一键全部还原。每个环节都有人做过，**串起来的会话式体验无人提供**。这是交互范式差异，不是技术护城河（Win32 API 人人可调）——但恰好是巨头最难转向的地方（FancyZones 架构与用户预期都锚定在"预设 zone"上）。
- ✅ **需求存在性有历史证据**：XP/7 任务栏"Tile Windows"任意数量平铺被微软砍掉后，MS Q&A 持续有人问，官方答疑只能推荐 FancyZones，而 FancyZones 是逐窗操作没接住这批用户。PowerToys #35550（Windows Groups "Snap Groups on Steroids"）至今 Open——这些 issue 就是免费的需求验证。
- ❌ **Shift 拖动投放不独有**：FancyZones 拖动 + 滚轮 + 完全自定义 zone（Grid/Canvas 编辑器）覆盖同一场景且自由度碾压，PaneSet 的 3+1 布局是其子集。拖动投放的价值定位应降级为"入口多样性"，不是卖点。
- ⚠️ **"还原"侧正在被夹击**：DisplayFusion（手动保存+句柄）覆盖了"会前保存/会后复原"的大部分价值；2026 年新入场的 WindowLayout（免费 3MB、自动恢复、SEO 进攻"免费 DF 替代品"）证明恢复侧不再空旷。PaneSet 的真实增量只剩"**免手动保存的撤销语义**"。窗口期在收窄。

### 1.3 竞品更强之处（不要正面硬刚的领域）

- FancyZones：布局自由度碾压 + 微软官方维护 + 免费 + 媒体背书，用户心智默认答案。
- DisplayFusion：20 年多屏全家桶（多屏任务栏/触发器/脚本/显示器配置文件）+ 商业支持 + Steam 渠道。
- PowerToys Workspaces：能"启动并定位"应用，对每天固定开场的用户是更对的范式（PaneSet 只管已开窗口）。
- AquaSnap：排布后联动微调（Ctrl+缩放同步、Ctrl+拖动整组移动）恰好补 PaneSet 短板——PaneSet 排完后只有 Alt+S 轮换，缺连续微调。
- 竞品的稳定性口碑坑 = 我们的机会：FancyZones 多屏 bug（唤醒丢位置 #38581、三屏错位 #40532/#40629）、DF Profiles 匹配失效、同应用多窗吸进同一 zone（#19122）。**但 PaneSet 自己的第三方应用兼容性也尚未实测验证，做不稳会踩进同一个坑。**

### 1.4 风险

1. 微软若响应 #35550 把 Snap Groups 做强，核心卖点瞬间贬值。
2. WindowLayout 类免费新工具继续向"恢复"侧渗透。
3. 第三方应用（最小尺寸/提权/Electron）下的排布与还原可靠性未实测——竞品用户的重灾区。

**结论**：空位真实、有据可查、但狭小。守住位置的唯一方式：在巨头补齐 undo 之前，把「选组→平铺→复原」对真实应用做到 100% 可靠 + 把"能复原"的感知做到位。

---

## 2. UX：信任链断点（产品生死命题）

「用户敢不敢让一个工具一次搬多扇窗」——机制层 80 分（快照在前、读回在后、逐窗报告、pending 保护），感知层 40 分。按危险度排序：

| # | 问题 | 场景 |
|---|---|---|
| R1 | **恢复点被拖动投放静默覆盖** | 刚排好三窗（恢复点 A），随手 Shift 拖一扇（B 覆盖 A），按 Ctrl+Alt+Z 只有那扇回去，三窗布局永失。用户踩一次就学会"用之前先想想"——工具开始收认知税 |
| R2 | **排布成功的恢复提示不可见** | ShowReport 写完"可用 Ctrl+Alt+Z 恢复"紧接着 CollapseToTray()，提示被收起动作吞掉。反馈待遇倒挂：拖动单窗有 950ms 高亮+4.5s 浮条，动窗更多的整组排布反而什么都没有 |
| R3 | **退出/重启/崩溃 = 恢复记录静默清空** | 无警告；恢复记录只在进程内 |
| R4 | Ctrl+Alt+Z 与 Photoshop 多步撤销同键位，全局直达、按下即搬窗，无预告 | 设计软件用户按撤销 → PaneSet 开始搬窗口。试用者已自发把 Ctrl+Alt+1 改绑 Ctrl+Shift 系 |
| R5 | 恢复失败文案无出口 | "可稍后重试"——重试动作是什么没说，且写在已隐藏的面板状态栏里 |
| R6 | 最小化窗口静默消失 | UI 零解释，用户第一反应是"工具坏了" |
| R7 | 部分成功靠用户拼图 | 需要自己推理"读状态栏→只回滚已动的→再按恢复" |

其他 UX 要点：
- **首次运行零引导**，4 全局键 + 5 组面板键 + 1 拖动辅助键的发现路径只有一行 11px 小字（窗口矮时还折叠）。
- **"A · 本屏全部 / B · 本屏选窗"是内部代号泄漏**，新用户无从知道 A/B 何意。
- **主面板纯文字列表无图标无缩略图**——产品头号任务 T1（同应用两份文档辨认）走在能力最弱的界面上；主面板只有轮换、B 总览却有前移/后移，顺序编辑能力不对称。
- **边缘打架（人工验收 #11 实锤）**：PaneSet 分区与 Windows Snap 预览在屏幕边缘格竞争。且拖动 16 项人工验收只过 4 项，手感结论一半来自推测。
- **FancyZones 共存无任何说明**：两者 Shift 拖动同源同键，双装会分区叠加甚至语义对撞；FancyZones 用户恰是最可能的早期采用者。
- 竞品迁移成本：Snap 用户最好走拖动投放（心智同构）；预设 zone 心智用户会问"布局存哪了"——答案是产品主张（"不存，排完即弃，可复原"），应在 onboarding 主动说出来。

---

## 3. 技术审计要点

### 3.1 架构评价

**好**（有证据，非客套）：纯逻辑与系统观察分离扎实（DragPlacementStateMachine 从不调系统、可用合成事件流驱动）；身份模型深思熟虑（pid+thread+class+创建时间+代际五元组，失败语义=永不异常永不放行，1601 纪元坑有行内注释+回归锚点）；危险边界注释质量高；配置保存是真事务（临时文件→File.Replace 带备份→错误 1177 专门恢复分支）；多 DPI 探针手法正确（PerMonitorV2 + 1×1 探针 HWND）；低权限自律（asInvoker + LIMITED_INFORMATION）；测试覆盖失败路径密度罕见（不是 happy-path-only）。

**差**：MainWindow.xaml.cs 560 行上帝类、7 个布尔标志耦合；PointerWindow 命中测试策略泄漏进 Native 层；UiPalette 全局可变静态状态；无全局异常兜底；少量死代码未标注。

### 3.2 健壮性风险（按严重度）

| 级别 | 问题 | 位置 |
|---|---|---|
| H1 高 | 无 `DispatcherUnhandledException`，UI 线程任何逃逸异常 = 进程消失、恢复记录丢、用户只见"托盘没了" | App.xaml.cs |
| H2 高 | `known` 字典只靠 EVENT_OBJECT_DESTROY 清理，事件丢失即无界慢泄漏 | WindowManager.cs:47-79 |
| H3 中高 | 排布路径（ApplyCore）无跨 DPI 二次重试，拖动路径有——不对称；混合 DPI 下最容易产生"排不动" | WindowPlacement.cs:137-143 vs WindowManager.cs ApplyCore |
| H4 中 | 低级鼠标 hook 无条件接受 injected 事件（注释给了驱动重映射理由），自动化注入可触发非意图提交 | DragPlacementMonitor.cs:107-112 |
| H5 中 | 提权窗口移动被 UIPI 拒绝 → 用户只见"排不动"不知道因为是管理员窗口 | WindowPlacement.cs:164-174 |
| H6 中低 | Mirror/Rows 的 `checked` 溢出抛 OverflowException，调用方只 catch ArgumentOutOfRange，叠 H1 即崩溃 | LayoutEngine.cs:44-47 等 4 处 |
| H7-H11 低 | GetWindowLongPtrW 32 位无导出；拖动 Tick 冗余身份校验；2s 注册表轮询；SetProp 依赖销毁隐式回收；fixture 与进程名耦合 | 详见审计 |

反向发现：MainWindow 200ms 收集 DispatcherTimer "只 new 未 Start" **不是 bug**——四参构造自动启动（.NET 参考源+实测确认），建议加注释固化，防未来误删。

### 3.3 性能

- **最大热点：QuickSelect 200ms 全量重枚举 ≈ 5k pinvoke/s**（选窗开着时持续烧）。改 WinEvent 事件驱动（SHOW/HIDE/DESTROY/NAMECHANGE 订阅 + tick 降级兜底）是性能+架构双赢。
- ProcessIdentity 按 pid 缓存（destroy/代际失效）可再砍枚举热路径 ~40% pinvoke，有 enumeration 钉死测试护航。
- 拖动 Tick 内 IdentityAlive + ReadState 双重全套身份校验可合并，每次拖动省几百次 OpenProcess。
- 无任何性能回归测试——刚做完性能改造，正是加 Stopwatch 级 smoke 阈值的时机。

### 3.4 测试盲区

混合 DPI/多屏异缩放零自动化覆盖（cross-monitor <2 屏即 SKIP，PlaceCore 跨 DPI 分支从未被自动执行）；真实拖动管线未端到端自动化（现检查绕过 native move loop）；无 cloaked 窗口 fixture（挂起 UWP 过滤从未被触发）；Explorer 重启托盘重挂无检查；并发双入口只有代码推理。

### 3.5 macOS 准备度 ≈ 30%

纯逻辑核心可带走（LayoutEngine/DragPlacementStateMachine/OverviewSelection/测试方法论）；但**领域类型本身绑死 Win32**（`WindowTarget.Handle: nint`、`DragSessionStart.Placement: Native.Placement`），窗口系统一半移植即重写。建议：仅当 macOS 方向确认后，拆 `PaneSet.Core`(netstandard)/`PaneSet.Windows` + `WindowId` 不透明类型 + `IWindowSystem`/`IInputEnvironment` 接口，用构建期引用检查强制 Core 无 Win32 引用。现在不做过早抽象。

---

## 4. 行动清单（合并三份报告，按投入产出比）

### 第一梯队：小时级，消灭确认存在的崩溃/泄漏/信任断点

| # | 动作 | 来源 | 成本 |
|---|---|---|---|
| ~~A1~~ ✅ 2026-11-17 | 全局异常兜底：DispatcherUnhandledException + DiagnosticsLog（%LOCALAPPDATA%\PaneSet\error.log，256KB 轮转）+ 托盘气泡（60s 限频），探针注入验证通过 | H1 | 0.5h |
| ~~A2~~ ✅ 2026-11-17 | `known` 字典惰性清理（ListWindows 剔除 `!IsWindow`），enumeration 检查护航通过 | H2 | 0.5h |
| ~~A3~~ ✅ 2026-11-17 | 溢出 catch 扩到 OverflowException（4 处） | H6 | 1h |
| ~~A4~~ ✅ 2026-11-17 | **排布成功浮条**：“已排布 N 扇 · <恢复键> 一键复原”（PlacementFeedback 文本模式复用，CollapseToTray 之后创建）；隐藏态恢复成功同弹“已恢复 N 扇原位” | R2/R5 | 小 |
| ~~A5~~ ✅ 2026-11-17 | **恢复点预告**：UndoButton 动态文案（“恢复上次排布（N 扇）”/“恢复上次投放（标题）”）+ ToolTip 覆盖关系说明；UIA Name 不变保检查兼容 | R1 | 小 |
| ~~A6~~ ✅ 2026-11-17 | 退出时若有未消费恢复记录弹确认（含恢复键名与窗数） | R3 | 小 |
| ~~A7~~ ✅ 2026-11-18 | "A · 本屏全部 / B · 本屏选窗"改为"本屏全部排布/本屏选窗总览" | UX | 文案 |

### 第二梯队：1-2 天级，修已知功能风险 + 验证

| # | 动作 | 来源 |
|---|---|---|
| ~~B1~~ ✅ 2026-11-18 | ApplyCore 补跨 DPI 二次重试（镜像 PlaceCore；DPI 探测惰性，同 DPI 零开销） | H3 |
| B2 | 跑完拖动 16 项人工验收中未执行的 12 项（尤其边缘打架 #11、划过换模板 #3、右键取消 #6）——当前手感结论一半是推测 | 验收债 |
| ~~B3~~ ✅ 2026-11-18 | 提权窗口"排不动"明确文案（UIPI/NativeErrorCode=5 分支，排布+恢复两路径） | H5 |
| ~~B4~~ ✅ 2026-11-18 | 最小化窗口存在性解释（"另有 N 扇最小化窗口未列出"） | R6 |
| ~~A7~~ ✅ 2026-11-18 | "A · 本屏全部 / B · 本屏选窗"改为"本屏全部排布/本屏选窗总览" | UX |

### 第三梯队：投资项

| # | 动作 | 条件 |
|---|---|---|
| C1 | QuickSelect 改 WinEvent 事件驱动 + ProcessIdentity pid 缓存 | 性能主战场，两者配合 |
| C2 | 补 cloaked fixture + 真实拖动端到端自动化（复用现有 SendInput/AttachThreadInput 注入机器） | 测试债 |
| C3 | 混合 DPI 实机验证 + 恢复前"拓扑已变化可能失准"提示 | 正式版前必须 |
| C4 | 首次运行教学卡（三键位+拖动提示，不做向导）+ 主面板应用图标 + 前移/后移对齐 + 键位冲突引导（Ctrl+Alt+Z/Photoshop） | 正式版前 |
| C5 | FancyZones 共存文档章节 | 文档 |
| C6 | 平台缝重构（Core/Windows 拆分 + WindowId） | **仅当 macOS 方向确认后** |

### 战略层（非代码）

- 定位叙事统一为「临时对照 / 会前会后复原」场景，README/试用页/文案围绕它讲，不打"窗口管理工具"大类。
- 对外所有文案的验收标准定为：**每一步之后用户都知道退路在哪**（"发生了什么 / 下一步做什么"两段式）。
- 观察 PowerToys #35550 与 WindowLayout 动向，作为差异化存续的预警指标。

---

## 5. 来源

- 竞品：Microsoft Learn FancyZones 文档（2026-08 版）；PowerToys issues #27311/#35550/#166/#9601/#38581/#40532/#40629/#19122；MS Q&A（Win11 移除任务栏平铺、Snap Group 最大化随机落位 5740663）；DisplayFusion 官方论坛/Steam（开发者 Keith Lammers 回复）及 2013 平铺功能请求；windowlayout.com（2026-10）；nurgo AquaSnap 官网；docs/RESEARCH_SHORTCUTS.md（macOS 侧）。
- 技术：静态审计 src/PaneSet（24 文件 ~5600 行）+ tests/PaneSet.Checks（13 检查）+ 关键行为实测（DispatcherTimer 构造语义探针）。
- UX：README_zh/TRIAL/PRODUCT/WINDOWS_PLAN/GLOSSARY/plans/post-release-followup/.scratch 人工验收记录 + 源码走查。
- 三份完整原始报告见 2026-11-17 分析会话（竞品调研 / 技术审计 / UX 评审）。
