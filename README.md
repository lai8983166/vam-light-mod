# QuickFill — 人物跟随补光

面向 VaM 1.x 的单文件 C# 插件。将 `Custom/Scripts/Lai/QuickFill/QuickFill.cs` 加到 **Person 人物的 Plugins** 中即可自动生成四灯布光。

## 已安装位置

`F:\Virt A Mate\Virt A Mate\Custom\Scripts\Lai\QuickFill\QuickFill.cs`

## 使用

1. 在 VaM 中打开场景，进入 Edit Mode，选中要补光的人物。
2. 人物的 Plugins → Add Plugin → Select File，选择上面的 `QuickFill.cs`。
3. 打开插件的 Custom UI。初次加载会依据当前视角布光，并自动跟随人物。
4. `Align lighting to current view`：从现在的观察方向重新布光。切换到合适的正面或四分之三侧面视角后点击。
5. 场景已经很亮时，先把 `Overall brightness` 调到 0.4–0.7；较暗场景从默认 1.0 开始。

## 默认布光

- Key：左前上方的偏暖主光，提供脸部和身体层次；默认启用 Unity 软阴影。
- Fill：右前方的较弱中性补光，保留明暗关系。
- Rim：后侧略冷轮廓光，帮助人物与背景分离。
- Eye：低强度正面补光，用于眼部和脸部细节。

`Look` 提供 Soft Portrait（默认柔和人像）、Clean Studio（中性明亮）、Cinematic（更强明暗和冷暖对比）。材质、肤色、环境光和后处理都会影响结果，默认值尚需结合实际场景确认。

## 参数

| 控件 | 用途 |
| --- | --- |
| Generate / rebuild lighting | 开启或重建四灯，不会叠加重复灯 |
| Remove lighting | 移除本插件灯光；生成按钮可以恢复 |
| Overall brightness | 整组亮度 |
| Fill amount / Rim amount | 补光、轮廓光相对强度 |
| Rig distance | 灯与人物的距离；范围随人物尺寸调整 |
| Light direction | 整组灯围绕人物旋转 |
| Follow smoothing (seconds) | 跟随平滑时间；0 为立即跟随 |
| Follow body turns | 开启时随骨盆水平转向；关闭时只平移 |
| Key light soft shadows | 主光软阴影开关 |

灯组跟随实际身体的头、胸、骨盆位置，而非仅跟随 Person 主控制器，因此支持主控制器不移动的动作。仅跟随水平转向，不跟着身体倾斜翻滚；平滑处理减轻抖动，大幅瞬移后立即重定位。躺姿下骨盆方向接近竖直时保持最后有效水平朝向。

## 保存、性能和限制

- 保存场景或插件预设会保存注册参数，下次加载重建灯组。
- 灯组是插件管理的 Unity Light，不会出现在 VaM Atom 列表中。禁用、卸载插件时自动清理。
- 不修改已有场景灯光、环境光、曝光或全局画质设置；已有灯会叠加，可能需要降低本插件亮度。
- 默认只有主光投影，其他三盏不投影。仍有四盏实时逐像素灯的性能开销。
- 只照射 Default 图层，以避开 UI；自定义图层上的 CUA 不受本插件灯光照射。
- Unity 聚光灯的软阴影不是离线渲染器的真实面积光。现有场景的光源数量、皮肤着色器、阴影设置和后处理会影响效果。
- 每个人物建议只加载一份插件。多个实例的灯光会叠加。
- 中文说明配英文控件，避免未汉化 VaM 的字体缺字。

## 开发

源码在 `Custom/Scripts/Lai/QuickFill/QuickFill.cs`。运行 `tools/Build.ps1` 使用本机 VaM 程序集编译验证；生成 DLL 仅用于验证，VaM 实际加载 `.cs`。
