# 戰鬥與人物構圖修正版驗收

2026-10-09。本輪交付檔為 `../build/strategy-win/SanGuo.exe`，截圖取自實際 Windows 遊戲，不是設計示意圖。

## 人物與構圖

33 組人物新增獨立 `bust_*.png` 半身展示素材，並重新裁切頭像。`portrait-framing.json` 保存各角色的臉部定位，讓臉部中心對齊展示區，不再以武器、披風的整張圖片外框決定人物位置。完整立繪仍供招募展示使用；半身與頭像不共用同一裁切。

呂布、夏侯惇、公孫瓚、張飛、趙雲、荀彧重新製作立繪，分別以方天畫戟、單刀防禦、拉弓、丈八蛇矛、直立長槍、閱讀卷軸區分人物輪廓。招募人物圖與名稱列分開配置，保留完整武器和腳部。

![人物置中實機畫面](../build/battle-final/1366x768/06-heroes.png)

![招募不同人物姿態](../build/battle-final/1920x1080/02-gacha-main.png)

## 戰鬥

- 擴大預設棋盤畫面，改為有厚度的獨立玉石格、青玉底座、銅邊與角印。
- 更新 Q 版戰鬥模型及兵器。可編輯來源為 `../art/chibi-combat-v2.blend`，產生工具為 `../tools/blender/chibi_v2.py`。
- 依盾兵、長兵、弓兵、術士、劍兵區分待機與攻擊動作。模型採程序化幾何與兵種動作配置，並非每位武將都有獨立製作的骨架動畫。
- 手牌保留扇形排列與懸停上抬。卡面使用短摘要，完整規則仍保留在詳細說明；長描述不再溢出卡框。
- 單位 HUD 限制在戰場容器內，避開其他 HUD；縮放後超出戰場的標籤隱藏，頂部操作按鈕不受遮擋。
- 修正 3D 展示的模型軸向與渲染材質，避免人物倒向或變成黑色剪影。

![預設戰鬥與長卡牌描述](../build/battle-final/1920x1080/25-battle-status-card.png)

## 驗證結果

隔離副本建置成功，三種解析度各完成 31 張新截圖，共 93 張。每種解析度完成 12 次戰鬥 HUD 邊界、HUD 重疊與實際文字尺寸檢查；全名冊卡牌摘要均納入文字高度檢查。動作驗證確認同場至少三種不同兵種動作配置。

| 解析度 | 截圖 | Runtime errors | 戰鬥排版檢查 |
| --- | ---: | ---: | ---: |
| 1920×1080 | 31 | 0 | 12 |
| 1366×768 | 31 | 0 | 12 |
| 1280×1024 | 31 | 0 | 12 |

檢查流程包含主城導航、招募、養成、3D 展示、編隊、選牌、出牌、勝利與回城，以及長狀態卡、十張手牌、不同兵種動作、擁擠戰場與最大縮放。截圖與 `capture-manifest.json`、`player.log` 保存於 `../build/battle-final/<解析度>/`。這是遊戲內自動流程驗證與截圖目視檢查。

重現指令：

```powershell
./tools/verify-strategy-ui.ps1 -Width 1920 -Height 1080 -ReviewName battle-final
./tools/verify-strategy-ui.ps1 -Width 1366 -Height 768 -ReviewName battle-final -SkipBuild
./tools/verify-strategy-ui.ps1 -Width 1280 -Height 1024 -ReviewName battle-final -SkipBuild
```

人物裁切可透過 `../tools/center-chibi-portraits.ps1` 重建；立繪重製提示詞與裁切定位分別保存於 `art-chibi/pose-refinement.json`、`art-chibi/portrait-framing.json`。
