# 美術與程式接入分工

使用者於 2026-10-09 指定：使用者負責企劃、Codex 負責美術、Claude Code 負責程式。

使用者隨後明確授權 Codex 可直接替換 UI 與建模。美術顯示接入、視覺版面、模型資產與對應預覽可直接處理；保留其他協作者的修改，不擴張遊戲規則與後端功能。第十階段已修正頭頸比例；第十一階段接入新版棋盤與 HUD，最新狀態見 `proportions-stage10-review.md` 與 `battle-stage11-review.md`。以下第三至第八階段內容保留作歷史紀錄，不再表示新版棋盤仍待 Claude Code 接入。

## 第三階段美術交接（歷史紀錄）

- 模型：`client/Assets/Resources/ProductionCharacters/`，32 個骨架底材候選；不是 32 個通過商用美術驗收的角色。
- 貼圖：同目錄 `Textures/`。關羽有身體、髮飾、臉、鬍髮、武器；盾兵、弓兵、醫士各有身體貼圖。
- 可再生成的美術製作工具：`client/Assets/Editor/ProductionCharacterBaker.cs`、`ProductionCharacterProps.cs`。
- 本輪實機證據：`build/model-quality/stage3-verified/`，20 張多角度及攻擊截圖，`frame-bounds.json` 與 `capture-manifest.json`。
- 品質紀錄：`docs/character-production-review.md`。

## 交由 Claude Code 維護的程式部分

本輪在分工指定前已加入的 `ArcherPoseRig.cs`、`CharacterView.cs` 持弓／死亡接入、`ModelStage.cs` 透明輪廓取景，後續由 Claude Code 維護。保持既有骨架動畫可播放、射擊時觸發 `Shoot()`、倒下時停止持弓校正；切換角色與角度時不可累積取景偏移。

第五階段 r_archer 已改用 `ProductionCharacters/Animations/r_archer_BowIdle.anim`、`r_archer_BowAttack.anim` 兩個完整骨架動畫素材，Prefab 不再帶有 ArcherPoseRig，避免舊校正覆蓋新美術姿勢。保持既有 CharacterClipSet 的 Idle／Attack 播放；其他舊弓兵的程式接入規格仍有效。製作工具 `ProductionArcherAnimation.cs` 僅在 Editor 執行，沒有新增執行期 IK 程式。

第六階段 bandit_archer 與 bandit_marksman 也各自改用 `Animations/<角色ID>_BowIdle.anim`、`<角色ID>_BowAttack.anim`，Prefab 不再帶有 ArcherPoseRig。這四種山賊的正式資產仍位於 ProductionCharacters，接入時保留各自 ID，不要把獵戶映射回舊 bandit_archer 模型。驗收與待修項見 `bandit-stage6-review.md`。

第七階段張飛重製素材已沿用相同 ID 與 CharacterClipSet，見 `zhangfei-stage7-review.md`。第八階段棋盤 Prefab、材質、接入尺寸與 HUD 美術修正規格見 `board-stage8-integration.md`；新棋盤尚未替換執行期 BuildFloor，接入由 Claude Code 處理。

驗證條件：模型旋轉後不出框、不空白、材質不呈粉紅色；弓與盾跟隨手骨；出牌、受擊、施法、死亡正常。HUD 與卡牌的文字／重疊檢查也必須保留。

## 下一輪由 Codex 處理的美術缺口

使用者最新將範圍收斂為「全武將都有基本模型就先停」。已補齊 24 個缺少 ProductionCharacters 的角色，同 ID Prefab 透過現有載入優先順序替換舊模型；29 個武將全部覆蓋，正式角色 Prefab 共 57 個。底材、116 張實機截圖檢查與未精修項見 [基本模型覆蓋紀錄](roster-basic-coverage-review.md)。此階段完成後停止 3D 工作，商用美術認可仍未完成。

第十四階段已整理 33 角色逐角實機缺口，見 `roster-stage14-review.md`。第十五階段關羽新青龍刀與双手握持曲線已直接接入同 ID Prefab，人物網格不改；正常四角及五動作共 100 張定格驗證見 `guanyu-glaive-stage15-review.md`，新刀仍待使用者美術認可。

第十六階段黃忠以原蒙皮底材重製衣甲／臉／白鬍貼圖，新增頭盔、紅纓、弓、弓弦與箭筒，改用完整骨架持弓動畫素材，Prefab 未使用執行期 ArcherPoseRig。正式接入同 ID；尚有披風附件相交與髮飾細節需精修，不能標示商用完成。驗證與具體待修見 `huangzhong-stage16-review.md`。

第十三階段針對 r_shield／r_archer 將整組頭部與頭飾縮至 0.80，並修改肩胸蒙皮網格；ProportionVersion=2 防止累積變形。既有 HeadScale 播放路徑沿用，全部動畫引用保留。見 [弓盾兵比例驗收](infantry-proportions-stage13-review.md)。其他角色未套用這次縮頭。

第十二階段已重製待機頭部俯角、胸肩與雙腿站姿，並收回先前過大的頸部延長量；各模型新增 `Animations/<ID>_UprightIdle.anim`，Prefab 的 SourceIdle 保留原底稿。驗收見 `posture-stage12-review.md`。攻擊／施法／受擊／倒下引用維持原值，不能將新待機覆蓋到這些動作欄位。

盾兵頭盔與短兵器、弓兵頭巾和臉型、醫士髮飾與道具已完成第一輪獨立模型重製，見 `infantry-stage3-review.md`。具名角色的造型辨識與衣裝輪廓仍需逐一重製；只有材質改善不能視為美術完成。

## 第三階段程式端姿勢需求

實機 `build/model-quality/infantry-v6/r_archer-front.png`：弓兵左肘與袖口抬得太靠近面部，弓身與袖口遮眼；需要持弓手向角色前方延伸、右手退至臉頰側，箭鏃指向目標，待機與射擊都不能穿臉。`r_shield-back.png`：袖口穿過盾牌背板，需要握盾手與前臂平行背板。美術端繼續縮短、收窄袖口；執行期骨架校正交由 Claude Code。以上是待接入規格，沒有代為宣稱程式已修好。
