# B：原生统一网格总览

Status: ready-for-agent
Progress: done

## 授权与范围

用户已确认 Q1–Q6，并在设计收口时回复「认可」，授权原生验证与实现。依据 [规格](../spec.md) 的总览章节和 [方向记录](06-overview-selection-direction.md)。

替换 B 的透明桌面标记呈现：不透明 WPF 宿主、DWM 内容缩略图、稳定卡片、搜索/滚动、右侧前移/后移。保持完整身份、本屏固定、焦点 guard、共享排布和 Undo。不改 A、跨屏主面板或生产激活策略。

## 验收

- [x] 受控遮挡源窗口的实时内容、等比缩放、裁切、释放与无效注册探针。
- [x] 默认前台第一；卡片稳定，选择编号与候选空间顺序独立。
- [x] 右侧前移/后移，提交后真实几何对应所选顺序，Undo 恢复。
- [x] 搜索保留隐藏的已选项，失效成员保留说明并禁止提交。
- [x] 低高度折叠预览，已选列表与排布入口可达。
- [x] 最终完整回归、输入证据与源码审查记录完成；不代表全部检查通过。

## Comments

DWM 原生探针在本机通过：源窗口被目标宿主完全遮挡时，红色到绿色变化可在目标屏幕像素读到；裁切区域外为宿主白底，注销后绘制消失，源几何不变。普通 WPF 不透明宿主可用，不继续使用透明覆盖层。

总览组件/共享执行定向检查通过；截图另存 `artifacts/paneset-overview-<900|650|450>-composed.png`（实际桌面合成）与 `-wpf-only.png`（仅 WPF 渲染）。后者不含 DWM，不作为缩略图证据。

保留早期失败：quick-flow 在 Main 测试前置点击命中外部 Chrome，拒绝注入；overview 初版未建立受控前台，Ready 失败。建立已拥有 fixture 的实际点击前置后可进入；早期两次 Automation Invoke 未等待异步投递而读到旧编号，补正常 Until 后通过。后一次 overview 启动前命中系统 CoreWindow，同样拒绝输入；不归因为总览功能，也不删除该失败。

冻结完整 51 项为 38 PASS / 13 FAIL / 0 unavailable，exit 1，94.38 秒，无超时和源码变化。9 项命中检查遇到 LockApp/CoreWindow 或 LockScreenInputOcclusionFrame，3 项前台等待失败，另 1 项配置原子替换失败，见[问题 08](08-settings-replace-failure.md)。最终添加的真实卡片点击/按钮 Enter 与完整排序断言被前置保护阻断，先前定向 PASS 不替代这些最终断言。

发现 single-instance 原检查启动旧输出目录，修为同构建目录后，Release 重建 0 警告/错误，单独运行 1 PASS / 0 FAIL。应用 probe exit 0。生产源码与完整运行冻结版本相同。Standards/Spec 同会话自查已完成；无独立双代理审查，Advisor 因同模型禁用。

真实输入保护、交换鼠标映射、原等待上限均保留；未终止用户实例，未移动用户窗口。尚无第三方/受保护内容、混合 DPI、Windows 11 支持基线或物理键盘/IME 验收。无提交、推送或发布。
