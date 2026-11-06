# QuickFill — 人物跟随补光

面向 VaM 1.x 的单文件 C# 插件。将 `Custom/Scripts/Lai/QuickFill/QuickFill.cs` 加到 **Person 人物的 Plugins** 中即可自动布光，支持经典四灯和模拟柔光箱切换。

## 已安装位置

`F:\Virt A Mate\Virt A Mate\Custom\Scripts\Lai\QuickFill\QuickFill.cs`

## 使用

1. 在 VaM 中打开场景，进入 Edit Mode，选中要补光的人物。
2. 人物的 Plugins → Add Plugin → Select File，选择上面的 `QuickFill.cs`。
3. 打开插件的 Custom UI。初次加载会依据当前视角布光，并自动跟随人物。
4. `Align lighting to current view`：从现在的观察方向重新布光。切换到合适的正面或四分之三侧面视角后点击。
5. 场景已经很亮时，先把 `Overall brightness` 调到 0.4–0.7；较暗场景从默认 1.0 开始。

## 0.3 更新：经典 / 柔光箱 A/B 对比

0.3.1 修复单文件加载时报 `must inherit from MVRScript` 的入口类型顺序问题。已加载旧版时，点击插件 Reload 重新加载修复文件。

重新加载插件后，点击 **A/B: Classic / Softbox**，或使用 **Lighting layout** 下拉框切换。默认 Classic，旧场景加载后不会自动启用更耗性能的柔光箱。

- **Classic (4 lights)**：0.2 的四灯布局，包含上一轮新增的阴影和垂直角度调整；不是回退到最早的 0.1。
- **Softbox (sampled key)**：只替换主光，保持补光、轮廓光、眼神光及现有参数相同。切换不重新校准朝向、不重置跟随位置，不同时叠加旧主光。
- **Softbox quality**：Balanced 使用 2×2 主光采样，总共 7 盏活动灯；High 使用 3×3 采样，总共 12 盏活动灯。
- **Softbox width / height (m)**：默认 0.8 × 1.0 米，按人物尺寸缩放。灯面始终朝向主光瞄准点。调整 Rig distance 改变距离，不会同比放大灯面，因此距离也会影响柔和程度。
- 主光总强度均分给采样灯，避免灯数增加直接使总强度翻倍。但由于角度、距离、遮挡和材质不同，画面亮度不会保证完全一致。
- 每个柔光箱采样灯请求 1024 阴影分辨率；Classic 主光为 2048。回到 Classic 会禁用所有额外采样灯；移除或禁用插件会一起清理。
- 模式、尺寸及质量随场景 / 插件预设保存。

### 推荐对比步骤

1. 固定人物、相机、姿势和曝光，打开插件 Custom UI。
2. 保持 Key light soft shadows 开启，选择 Balanced，尺寸先用 0.8 × 1.0。
3. 点击 A/B 按钮，观察鼻影、下巴影、颈部和肩部的过渡，同时观察帧率。
4. 看见明显多重阴影时，先缩小灯面，或切换 High。High 开销更大，不保证消除全部离散阴影。
5. 宽度和高度都设为 0，可检查采样灯收拢后的效果；其阴影分辨率仍不同于 Classic，不是严格的逐像素一致测试。

这是用离散聚光灯近似面积光，不是真正实时面积光或全局光照。轮廓光仍是单盏灯，不会随主光柔光箱尺寸变软。现有场景灯和环境光不会被修改；它们过强时，A/B 差异可能不明显。柔光箱主光最多产生 9 份阴影，加上轮廓光最多 10 盏投影灯，应按帧率选择质量。

## 0.2 更新：阴影与垂直角度

已安装旧版本时，在 VaM 的插件条目中点击 Reload（重新加载），再打开 Custom UI 查看新控件。

- 轮廓光现在默认投射软阴影，让背光遵循身体遮挡；同时降低正面补光和眼神光强度，保留暗部层次。
- 减小阴影偏移，主光使用 2048、轮廓光使用 1024 的请求阴影分辨率；实际质量仍受 Unity / VaM 画质设置限制。
- `Rig elevation offset` 整体抬高或压低四盏灯；`Key elevation` 和 `Rim elevation` 分别控制主光、轮廓光的基础仰角，单位为度。
- 正角度表示灯在目标上方，负角度表示下方。调整时保持灯到各自瞄准点的距离，重新朝向人物；不是把光束转离人物。最终角度限制在 -75° 到 85°，避免越过极点翻转。
- 默认主光 25°、轮廓光 30°。先试主光 20–40°、轮廓光 20–45°，观察鼻影、肩部和身体边缘，再微调。
- `Shadow strength` 调整主光和轮廓光的阴影强度。出现阴影脱离身体时降低偏移；出现皮肤条纹或自阴影斑点时略增偏移。偏移过小也可能产生伪影。

旧场景保存的亮度、风格等参数继续保留；新参数使用默认值。新默认光比会改变旧场景的外观。本次通过编译验证，具体场景的视觉效果仍需实测。

## 默认布光

- Key：左前上方的偏暖主光，提供脸部和身体层次；默认启用 Unity 软阴影。
- Fill：右前方的较弱中性补光，保留明暗关系。
- Rim：后侧略冷轮廓光，帮助人物与背景分离，默认启用软阴影以保留遮挡。
- Eye：低强度正面补光，用于眼部和脸部细节。

`Look` 提供 Soft Portrait（默认柔和人像）、Clean Studio（中性明亮）、Cinematic（更强明暗和冷暖对比）。材质、肤色、环境光和后处理都会影响结果，默认值尚需结合实际场景确认。

## 参数

| 控件 | 用途 |
| --- | --- |
| Generate / rebuild lighting | 开启或重建当前灯组，不会叠加重复灯 |
| Lighting layout / A/B: Classic / Softbox | 切换经典四灯与柔光箱主光 |
| Softbox quality | 4 或 9 个主光采样 |
| Softbox width / height (m) | 按人物尺寸缩放的灯面宽高，仅影响柔光箱模式 |
| Remove lighting | 移除本插件灯光；生成按钮可以恢复 |
| Overall brightness | 整组亮度 |
| Fill amount / Rim amount | 补光、轮廓光相对强度 |
| Rig distance | 灯与人物的距离；范围随人物尺寸调整 |
| Light direction | 整组灯围绕人物旋转 |
| Rig elevation offset | 四灯共同的仰角增量，-45° 到 45° |
| Key elevation / Rim elevation | 主光 / 轮廓光的基础仰角，-30° 到 75° |
| Follow smoothing (seconds) | 跟随平滑时间；0 为立即跟随 |
| Follow body turns | 开启时随骨盆水平转向；关闭时只平移 |
| Key light soft shadows | 主光软阴影开关 |
| Rim light soft shadows | 轮廓光软阴影开关；关闭可减轻性能开销 |
| Shadow strength | 主光和轮廓光的阴影强度 |
| Shadow depth bias / Shadow normal bias | 深度 / 法线偏移，用于平衡阴影贴合和自阴影伪影 |

灯组跟随实际身体的头、胸、骨盆位置，而非仅跟随 Person 主控制器，因此支持主控制器不移动的动作。仅跟随水平转向，不跟着身体倾斜翻滚；平滑处理减轻抖动，大幅瞬移后立即重定位。躺姿下骨盆方向接近竖直时保持最后有效水平朝向。

## 保存、性能和限制

- 保存场景或插件预设会保存注册参数，下次加载重建灯组。
- 灯组是插件管理的 Unity Light，不会出现在 VaM Atom 列表中。禁用、卸载插件时自动清理。
- 不修改已有场景灯光、环境光、曝光或全局画质设置；已有灯会叠加，可能需要降低本插件亮度。
- 默认主光和轮廓光投影，补光和眼神光不投影。Classic 为四盏实时逐像素灯；Softbox 为 7 或 12 盏。性能不足时优先回到 Classic，或关闭 Rim light soft shadows。
- 只照射 Default 图层，以避开 UI；自定义图层上的 CUA 不受本插件灯光照射。
- Unity 聚光灯的软阴影不是离线渲染器的真实面积光。现有场景的光源数量、皮肤着色器、阴影设置和后处理会影响效果。
- 每个人物建议只加载一份插件。多个实例的灯光会叠加。
- 中文说明配英文控件，避免未汉化 VaM 的字体缺字。

## 开发

源码在 `Custom/Scripts/Lai/QuickFill/QuickFill.cs`。运行 `tools/Build.ps1` 使用本机 VaM 程序集编译验证；生成 DLL 仅用于验证，VaM 实际加载 `.cs`。

运行 `tools/Test.ps1` 编译并检查实际采样辅助代码的总权重、对称中心、边界、非退化分布与部分遮挡条件，同时检查编译程序集的首个类型是 QuickFill : MVRScript。入口检查使用本机 VaM 的 Mono.Cecil，仅用于开发，不是插件运行依赖。这些检查不替代 Unity 实机渲染验证。
