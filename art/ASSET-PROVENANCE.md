# 原创建筑美术来源

2026-10-02。chamber-housing.png 由内置 image_gen 生成，透明 RGBA；原始输出保留于 C:/Users/54492/.codex/generated_images/01a0f85f-4b61-7751-a27b-7bcfcbb3b17a/exec-eae8ace3-8563-422d-816c-d250a3bd5d37.png。工作区源图为 art/source/chamber-housing.png。

提示词：

> Use case: stylized-concept. Asset type: original 2D game building sprite for an Oxygen Not Included mod, 4 tiles wide and 4 tiles tall. Create a single front-view medical life reconstruction chamber, isolated on a genuinely transparent background. Thick dark hand-drawn outlines, warm off-white panels, slate teal machinery, restrained amber fittings, cartoon industrial flat shading, crisp readable silhouette at small scale. Vertically elongated CENTRAL medical capsule fills the middle: a large clear teal glass door, empty interior, visible dark reclining support bed, no person or body painted inside. Left and right compact supply canisters with hoses. Sturdy flat horizontal base with two feet aligned along bottom. A small console at lower right, no readable letters. Mechanical frame should occupy a roughly square canvas 1024x1024 with 5% transparent margin. Strict straight-on orthographic side-scroller elevation, no perspective, no ground scene, no backdrop, no shadow outside silhouette, no floating icons, no logos, no text, no watermark. Make it an original believable medical apparatus with rich but organized details. Keep central glass free of opaque clutter so a separate animated patient silhouette can be overlaid. This is final production sprite art, not a mockup.

0.1.1：tools/build-anims.py 保留原始主体，仅按游戏尺寸缩放；另外制作原创能量环、扫描光、汇聚粒子、供给流光、气泡、心电与提示灯，导出 SCML 和原生 KAnim。透明度变化烘焙到 PNG 帧，不依赖转换器的 SCML alpha 支持。主动画与前景光效分层，暂停/缺料状态没有运动治疗图层。原中性病人剪影已删除，透明 patient_anchor 仅作运行时位置定位。

病人外观通过游戏原生 Accessorizer、WearableAccessorizer 和 SymbolOverrideController 在运行时读取，采用原版休眠姿势；不复制或分发游戏图集。art/preview 是建筑光效的源图预览，不包含运行时复制人，也不是游戏截图。医生继续使用游戏已有控制台操作动画。

0.1.2：内置 image_gen 基于已查看的 chamber-housing.png 编辑出匹配的开门/无液体仓体，另存 art/source/chamber-housing-open.png，保留原图。输出原件：C:/Users/54492/.codex/generated_images/01a0f85f-4b61-7751-a27b-7bcfcbb3b17a/exec-6fecf31d-2d6d-49ee-afc4-e8475e3b6c2e.png。检查透明背景和空仓布局后复制；未用程序涂改原图。

编辑提示词：

> Edit this existing ONI-style sci-fi resurrection pod game sprite for an open-door state. Preserve the exact original silhouette, layout, white metal frame, amber clamps, pipes, side tanks, control panel, thick ink outlines, scale and straight-on orthographic perspective. ONLY change the large central patient bay: remove the front glass panel completely (including the cyan glass reflections), make the bay clearly open with a dark muted desaturated teal rear wall and the empty small reclining medical support chair visible. The cyan central liquid must be absent: empty chamber, no liquid, no person, no silhouette. Preserve the central bay boundaries and the original outside structures exactly. Transparent outside background. No text, no new objects. This is a matched game animation housing sprite; the original front glass and treatment liquid will be separate animated layers later.

tools/build-anims.py 新绘制透明玻璃门和青绿治疗液两个独立覆盖层，51 档从隐藏到全量；与主体、病人和科幻光效分层导出。本版医疗姿势读取 anim_healing_bed_kanim 的 working_loop，用原生符号/衣着边界旋转居中；仅隐藏医疗动作中的 cast 道具。tools/verify-game-pose.py 只读本机 Unity TextAsset 动作元数据核对名称、帧数与头部位置，不导出或分发原版游戏美术。

0.1.3：用户实机截图仍显示病人与椅子不匹配，确认改为无椅悬浮仓。内置 image_gen 基于已查看的 chamber-housing-open.png 移除中央椅子、补齐背板，保留导轨和外部设备；新源图为 art/source/chamber-housing-chairless.png，旧源图均保留。原始输出：C:/Users/54492/.codex/generated_images/01a0f85f-4b61-7751-a27b-7bcfcbb3b17a/exec-0e02493c-d0bc-47fc-b246-50d8a34debda.png。人工检查无座椅、透明背景及框架布局后复制，未用程序涂改原图。

编辑提示词：

> Use case: precise-edit. Edit this existing transparent ONI-style sci-fi medical chamber game sprite. Remove ONLY the reclining medical chair in the lower middle of the central bay: remove headrest, seat back, arm rests, cushions, foot rest, chair legs and all chair support shapes. Replace that occupied area with the same clean dark desaturated teal rear wall, extending naturally to the bay floor. Preserve both vertical guide rails and their clamps, the top medical fixture, all surrounding door frame edges, pipes, side tanks, control panel, bottom amber latch, base feet, outer silhouette, exact proportions, thick ink outlines and all outside machinery. Central bay stays empty and open: no front glass, no liquid, no person, no silhouette, no chair or bed or pedestal, no new decorations. Straight-on orthographic game sprite, original transparent background. Match the source dimensions and alignment as precisely as possible; this asset must remain compatible with independently animated door and liquid overlays.

仓内显示改用原生 anim_idles_default_kanim 的 idle_default 首帧，角度为零，按未旋转的可见身体/衣着边界居中。搬运显示单独使用横卧医疗动作。verify-game-pose.py 核对直立首帧头在脚上方、横卧搬运首帧主轴水平；不把资源元数据核对视为游戏显示验收。
