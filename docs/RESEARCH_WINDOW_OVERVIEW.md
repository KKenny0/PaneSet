# B 窗口总览：原生内容预览可行性记录

用户已确认本屏窗口总览，以及内容预览为主要辨认信息。本文记录官方能力和待验证范围，不是原生功能验收结论。

## 官方能力

| 结论 | 依据 |
| --- | --- |
| DWM 缩略图是源窗口到目标窗口的持续动态联系，非单次截图；以二维方式绘制在目标窗口。 | [DWM Thumbnail Overview](https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw) |
| 源和目标 HWND 都必须是顶层窗口；目标必须由调用进程拥有，或为桌面窗口。创建关系返回 HRESULT 与进程内缩略图句柄。 | [DwmRegisterThumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail) |
| 注册关系不会直接显示缩略图；需调用 Update 设置属性。更新目标绘制矩形与属性，不要求移动源窗口。 | [DwmUpdateThumbnailProperties](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmupdatethumbnailproperties)、[Overview](https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw) |
| 可设置目标区域、源裁切、透明度、可见性及是否仅显示客户区；默认使用整个源窗口。 | [DWM_THUMBNAIL_PROPERTIES](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_thumbnail_properties) |
| 关闭会话时在创建关系的进程内解除注册；重复或无效句柄返回错误，不能忽略所有生命周期状态。 | [DwmUnregisterThumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmunregisterthumbnail) |

注意 Update 文档示例含与 Register 正式声明不一致的额外 size 参数；实现应采用 Register 的三个参数正式签名，不逐字复制该示例。

## 对 PaneSet 的意义

优先验证系统实时缩略图，应用名与标题独立存在；缩略图注册/绘制不可用时只降级内容预览，窗口仍可选择。窗口身份失效则是不同状态：保留已选成员的失效说明并禁止提交，必须明确取消该成员。

总览仅在捕获的目标屏幕呈现现有合格窗口，保持原资格规则。选择与预览不改源窗口几何、不替换已有 Undo；真正排布仍交给 WindowManager。源窗口原有位置线索与选中编号含义必须区分。

## 原生验证进展与剩余边界

已运行受控 `thumbnail` 探针：普通不透明 WPF 目标完全覆盖源窗口时，目标像素仍随源背景从红变绿；裁切与注销符合预期，源几何不变。总览集成也有实际桌面合成截图及定向流程通过。后续完整回归遇到 LockApp 前台，相关命中保护失败保留，不覆盖先前记录。详见[验收记录](VALIDATION.md)。

以下第三方、显示环境和宿主组合尚未完成全面验收：

- 被其他窗口完全遮挡的来源是否持续提供可辨认内容；官方动态关系说明不足以证明所有应用的遮挡行为。
- WPF 顶层宿主，尤其现有 AllowsTransparency 分层窗口，与 DWM 缩略图的实际合成和命中行为；必要时验证非分层宿主，不承诺直接沿用当前透明选窗层。
- 第三方应用、受保护内容及不同权限窗口；预览降级与失效窗口的处理必须分开。
- 物理坐标/DIP 换算、源纵横比、客户区与标题栏裁切、混合 DPI、缩略图注册释放和失效刷新。
- 真实全局 B、焦点/键盘、单实例、pending 和 Undo 的既有保护。

先读取上述五份官方页面，再运行受控原生探针；证据只覆盖被实际检查的 WPF 窗口与本机环境。HTML 原型使用模拟内容，只用于比较信息组织与交互，不证明这些原生能力。
