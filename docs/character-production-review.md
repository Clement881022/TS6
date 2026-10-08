# 角色模型重製階段紀錄

## 第一階段：可驗證的骨架模型樣板

2026-10-09。依使用者要求，由本專案內製作，不採購、不委製。這一階段建立精修、貼圖接入與實機驗收流程；尚未宣稱所有角色達到商用品質。

關羽改用專案已有的 UV 與骨架底材，產生獨立的精修網格。重新製作身體、頭飾與臉部三張貼圖，加入龍紋金甲、青綠織物、紅潤面部與黑髮；修正原貼圖 alpha 為染色遮罩而非透明度的處理。骨架待機、攻擊、受擊與倒下仍保留，新增頭部比例控制。模型與原底材分開保存，方便比較與繼續修整。

![第一階段真實 Unity 3D 渲染](art-chibi/guanyu-3d-stage1.png)

此圖由遊戲中的 RenderTexture 輸出，不是 AI 示意圖。身體、頭飾與臉部貼圖透過內建 imagegen 編輯既有 UV 圖集；輸出與提示詞記於 `art-chibi/character-surface-prompts.json`。

驗證：Windows 建置成功，1920×1080 共 31 張頁面與戰鬥截圖，runtime errors 為 0；另擷取關羽正面、側面、背面與骨架攻擊四張 1536×1536 圖。證據位於 `../build/model-sample2/1920x1080/` 與 `../build/model-quality/review/`。

自評仍待修正：臉型與立繪的一致性、長兵器形狀、鬍髮表面與人物輪廓。這些差距沒有因建置成功而被視為通過；其他角色不批次套用這個尚未完成的樣板。

## 檔案與流程

- 精修網格、材質、貼圖與 prefab：`../client/Assets/Resources/ProductionCharacters/`。
- 製作工具：`../client/Assets/Editor/ProductionCharacterBaker.cs`，以 Loop 細分修整網格並保留 UV、骨骼權重與 bind poses。
- 材質：`../client/Assets/Resources/Shaders/CharacterSurface.shader`，保留 UV 貼圖、金屬高光、柔和明暗與即時陰影。
- 載入：`HeroArt.Model` 優先載入精修角色；尚未完成的角色保留原版本。
- 實機多角度檢查：遊戲參數 `-sanguoShot <輸出資料夾> -sanguoModelReview guanyu`，使用隔離的截圖模式，不存入一般玩家進度。

後續按照已核定的明亮、精緻 Q 版方向，繼續修整關羽樣板，再拓展盾兵、弓兵、醫士與敵軍；每個可驗證階段直接 commit。
