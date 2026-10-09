# 地表貼圖生成紀錄

內建 imagegen，非 API／CLI。新製貼圖，沒有重绘人物或修改既有圖片。複製原輸出至 `assets/ground-grass-dirt.png`，再透過 Blender 材質直接映射到平面，非影像拼貼。

完整提示詞：

> Use case: stylized-concept. Asset type: seamless tileable square albedo texture for a 3D mobile Three Kingdoms battlefield ground. Top-down orthographic flat surface ONLY, no horizon, no perspective, no lighting shadows, no characters, no objects, no UI or writing or grid lines. Muted olive grass intermixed with broad irregular sandy beige compacted dirt patches and scattered very small pebbles. Hand-painted natural game material, soft chunky shapes to suit richly colored chibi 3D characters, natural realistic distribution rather than a checkerboard. Low contrast, no bright details, no large rocks, no foliage casting shadows. Seamless edges. Fill entire square, opaque.

不透明背景。未製作額外沙地變體；本輪地表為草土混合。提示詞要求可平鋪，但未單獨驗證所有邊界接縫，因此不宣稱已通過量產貼圖驗收。
