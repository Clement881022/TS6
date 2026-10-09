# 美術與程式接入分工

使用者於 2026-10-09 指定：使用者負責企劃、Codex 負責美術、Claude Code 負責程式。

## 本輪美術交接

- 模型：`client/Assets/Resources/ProductionCharacters/`，32 個骨架底材候選；不是 32 個通過商用美術驗收的角色。
- 貼圖：同目錄 `Textures/`。關羽有身體、髮飾、臉、鬍髮、武器；盾兵、弓兵、醫士各有身體貼圖。
- 可再生成的美術製作工具：`client/Assets/Editor/ProductionCharacterBaker.cs`、`ProductionCharacterProps.cs`。
- 本輪實機證據：`build/model-quality/stage3-verified/`，20 張多角度及攻擊截圖，`frame-bounds.json` 與 `capture-manifest.json`。
- 品質紀錄：`docs/character-production-review.md`。

## 交由 Claude Code 維護的程式部分

本輪在分工指定前已加入的 `ArcherPoseRig.cs`、`CharacterView.cs` 持弓／死亡接入、`ModelStage.cs` 透明輪廓取景，後續由 Claude Code 維護。保持既有骨架動畫可播放、射擊時觸發 `Shoot()`、倒下時停止持弓校正；切換角色與角度時不可累積取景偏移。

驗證條件：模型旋轉後不出框、不空白、材質不呈粉紅色；弓與盾跟隨手骨；出牌、受擊、施法、死亡正常。HUD 與卡牌的文字／重疊檢查也必須保留。

## 下一輪由 Codex 處理的美術缺口

盾兵頭盔與短兵器、弓兵頭巾和臉型、醫士髮飾與道具尚未對齊立繪。具名角色的造型辨識與衣裝輪廓仍需逐一重製；只有材質改善不能視為美術完成。美術製作中提出的執行期改動，整理成需求供程式端接入。
