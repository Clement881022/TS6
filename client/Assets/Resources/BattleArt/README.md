# 戰鬥美術素材與微調規格

2026-10-10。依最新指示，以既有手牌立繪／金框微調及素材拼稿交付，不重做人物模型，不重新渲染場景。

## 素材

- `GrassDirt.png`：沿用本輪已生成的草地／裸土 albedo，原素材在 `docs/ui-commercial-review/battle-revision-04/assets/ground-grass-dirt.png`。新增 Unity TextureImporter meta，sRGB、mipmap、Repeat。
- `NaturalGround.shader`／`NaturalGround.mat`：URP地表材質，採白色 tint，不染成綠色棋盤；接收主光陰影。地面使用連續UV，不能每格重設UV造成重複棋盤圖案。原模型、邏輯格與移動判定保持原有設定。材質供接入，尚未替換原 ProductionBoard prefab。
- `EnemyAim.shader`／`EnemyAim.mat`：URP透明紅線材質，白色頂點色、紅色材質，不寫深度、正常深度測試。給原生 LineRenderer／箭頭mesh使用，不需新增生成圖。模型遮擋採正常深度；正式可讀性依鏡頭驗收。
- `HandCompact.uss`：附加樣式候選，沿用原 `ChibiSkin/panel.png` 與 `PortraitArt`；244高牌身、138高人物區、費用和右上角大類型、名稱（牌面不再顯示持有人文字）；手牌不常駐效果數值。尚未加入 PageHost 載入清單，避免將本輪素材提案誤作已完成執行期接入。

沿用而非新製：全部人物模型、立繪、金框、damage／charge／draw意圖圖示。只新增Unity材質／樣式封裝，地表圖片是上一階段已生成的素材，沒有再次呼叫生成服務。

## Claude Code 接入

現有 `BattleScreen.Render.cs` 的 `FillIntent` 已讀取 `_battle.GetIntent(enemy)` 並顯示攻擊目標、蓄力或逼近；保留既有意圖計算。頭頂HUD必須繼續顯示該意圖。

紅色拋物線使用同一份實際intent.Target，起點敵人胸口、終點目標胸口；二次Bezier，控制點在兩端中點上方約1.2～1.8世界單位，24～32採樣點。LineRenderer寬約0.025～0.045世界單位，textureMode使用Tile，無箭頭的閃爍虛線，使用 `EnemyAim.mat`。只有Attack且Target存活時顯示；蓄力、逼近、死亡、目標為空時關閉。回合、位置或意圖更新時重算，不能由美術重新決定敵人瞄準誰。拼稿兩條線是展示關係，不是實際關卡AI資料。

手牌上限10張，保留現有 `LayoutHand` 的靠攏／重疊／選牌抬高，依實際手機畫布調間距。牌面欄位費用、名稱與右上類型圖示；詳細描述沿用右側 `_detail`。`sts-card-category` 使用沿用圖示；不加入持有人Label，依企劃提供的類型映射，不新增卡牌效果。套用附加USS後同步調整底部佔位，避免只缩牌身卻仍留大空區。

## 驗證

素材GUID引用、PNG路徑與既有底材均已核對。兩種比例3／7／10張的拼稿逐牌點選與右側詳情通過；敵人三組意圖、兩條紅色Bezier指示已展示。沒有Unity Shader編譯、材質導入或遊戲實機驗收紀錄，故本包狀態為「素材／接入規格已備妥」，不是已完成遊戲整合。

最新拼稿：`docs/ui-commercial-review/battle-revision-04/index.html`。本檔的修訂方向取代該提案README中先前純文字牌身的方向；原先離線人物渲染直接沿用，本轮没有再重製。

## 最新六點修訂
牌底持有人文字移除，只在右側詳情保留。敵人意圖放在名稱上方（先Intent，再Name／HP），不能只用USS換序，須由程式端调整HUD子元素順序。任務提示常駐畫面中央偏上，扁平長條、無教學提示；與右側選牌詳情分開。右下沿用cost／gold／pile_draw／pile_discard原素材與直列配置。牌型使用armor／damage／heal／status_atkup／status_armorbreak／draw既有圖示，文字僅作可存取標籤或詳情。紅色Bezier無箭頭，EnemyAim shader以UV產生虛線並以1秒週期閃爍；不新增敵人決策。

## 固定牌頭與圖像化詳情修訂
牌頭固定「費用／技能名稱／效果ICON」同一列。名稱不依露出寬度重排、不壓縮到牌邊；正常覆蓋順序是右牌蓋左牌，只有選中牌抬高並置頂。原有Unity LayoutHand／遍歷建立順序可沿用，不反轉層級。右側詳情採技能標題、費用徽章、持有人立繪、效果ICON與大數值、簡短目標標籤；移除冗長效果段落，正式版從實際卡牌資料取得完整數值與附加效果，不能照拼稿占位創造規則。此修訂取代早期「左蓋右且保留露出資訊欄」規格。
