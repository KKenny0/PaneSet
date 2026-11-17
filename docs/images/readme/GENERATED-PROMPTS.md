# GPT 图像生成 README showcase 提示词（2026-11-16）

配套参考图（生成时一并上传）：
- `artifacts/paneset-normal.png` — 主面板真实截图（浅色主题、布局、控件密度）
- `artifacts/paneset-overview-dark.png` — B 本屏选窗卡片网格（深色）
- `designs/logo-exploration/c1-refinement/A1.png` — logo

品牌色（提示词内已嵌，改主题时同步）：
浅色：面板底 `#F7F8F7` · 卡片白 `#FFFFFF` · 薄荷绿点缀 `#CAE9D8` · 焦点绿 `#276F62` · 深青主按钮 `#25464C` · 正文 `#233039`
深色：底 `#132332` · 面板 `#1A2D3D` · 薄荷 `#AEDFC1` · 正文 `#E9F1F0`

**文字渲染护栏（三份提示词共用，已写入各提示词）**：只要求精确渲染少数关键中文短语（引号内原文），其余 UI 文本一律用灰色抽象线条代替——这是避免中文鬼画符的关键，别删。

生成后每个 GIF 下方注明：`概念演示（Generated concept demo，非真实界面截图）`。

---

## 1. hero.gif — 核心旅程（唤出面板 → 勾选 → 排布 → 还原）

**提示词：**

> A short looping animated GIF demo of a Windows desktop utility called "PaneSet". 1280x640, 6 seconds, smooth UI motion, clean flat design.
>
> Scene: a light desktop with two overlapping app windows (a document editor and a spreadsheet). A small floating panel appears center-right — light theme (#F7F8F7 background, white cards, mint-green accents #CAE9D8, deep teal primary button #25464C), matching the attached reference screenshot exactly in layout style: search bar on the left column, a window list with checkboxes, a layout preview showing two rectangles on the right, a big teal button at the bottom.
>
> Animation beats: (1) the panel pops in; (2) a checkbox in the list ticks with a mint-green highlight and the number badge "1" appears; (3) the teal button presses; (4) the two messy windows glide and snap into a clean side-by-side half-screen arrangement with a soft mint glow on landing; (5) everything shuffles back to the original overlapping positions, suggesting one-key restore.
>
> Text rules: render ONLY these Chinese strings exactly — "PaneSet", "窗口列表", "排布目标", "排布所选窗口", "恢复上次排布". All other text must be abstract light-gray placeholder lines, NOT readable characters. A small keyboard hint chip reading "Ctrl+Alt+Space" floats near the panel.
>
> Style: crisp 2x-resolution UI mock, subtle shadows, motion easing like a polished product demo, no watermark, no photo elements.

## 2. drag-placement.gif — 拖动投放（分区 → 模板切换 → 入格）

**提示词：**

> A short looping animated GIF demo of a drag-to-place feature in a Windows desktop utility "PaneSet". 1280x640, 6 seconds.
>
> Scene: a light desktop with one large arranged window and one small loose window being dragged by a cursor (cursor visible, holding the title bar). As the drag starts, the screen overlays a translucent zone grid: a large left zone (2/3 width) and a stacked right column (1/3) — mint-green outlined rounded rectangles (#276F62 borders, mint fill at low opacity). At the top center floats a horizontal strip of 6 miniature layout thumbnails; the hovered one gets a mint highlight ring.
>
> Animation beats: (1) drag begins, zones fade in; (2) cursor sweeps across the thumbnail strip — the zone grid morphs from "big-left" to "big-right" (mirrored); (3) the dragged window glows and snaps into the right 2/3 zone, fitting edge to edge; (4) brief mint pulse on landing.
>
> Text rules: render ONLY these strings exactly — "主辅（2/3 + 1/3）" and "辅主（1/3 + 2/3）" as labels on the two adjacent thumbnails, plus a hint chip "Shift + 拖动". All other text: abstract gray placeholder lines only, no readable characters.
>
> Style: flat clean UI animation, mint/teal accent palette from attached reference, smooth geometric morphs, loop seamlessly.

## 3. quick-pick.gif — 本屏缩略图快选

**提示词：**

> A short looping animated GIF demo of a visual window picker in a Windows desktop utility "PaneSet". 1280x640, 5 seconds.
>
> Scene: a cluttered dark desktop (#132332 background) with several overlapping windows. An overlay panel appears — dark theme (#1A2D3D panel, text #E9F1F0, mint accent #AEDFC1), matching the attached dark reference: a grid of window thumbnail cards with live content previews, each card has a small checkbox; a header and a confirm button in mint.
>
> Animation beats: (1) overlay fades in showing 6 thumbnail cards of overlapping windows; (2) two cards get selected — mint checkmarks tick, cards lift with a subtle glow; (3) the confirm button presses; (4) the overlay collapses and the two chosen windows snap into a clean side-by-side arrangement on the now-tidy desktop.
>
> Text rules: render ONLY these strings exactly — "选择要一起看的窗口" as the overlay heading, "确认本屏选窗" on the confirm button, and a hint chip "Ctrl+Alt+2". Everything else: abstract gray placeholder lines, no readable characters.
>
> Style: dark-mode product demo, crisp card grid, mint highlights, smooth fade and snap motion, seamless loop.

---

## 生成参数建议

- 尺寸 1280 宽（GIF 版）；如果工具输出 MP4 再转 GIF：`ffmpeg -i in.mp4 -vf "fps=12,scale=1280:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer" out.gif`
- 每条独立生成，别让一张图讲两个功能
- 生成后核对：中文短语是否逐字正确（尤其 "主辅（2/3 + 1/3）" 的全角括号）；不对就重抽该条，别将就

## 入库位置（生成后）

`docs/images/readme/hero.gif`、`drag-placement.gif`、`quick-pick.gif`，README 引用时每图下方加"概念演示"图注。
