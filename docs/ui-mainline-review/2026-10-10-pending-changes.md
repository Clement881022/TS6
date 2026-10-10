# 既有待提交修改確認

- `WorldBossPage.cs` 增加既有 ChallengeTabs，使用現有 challenge-tabs-floating 樣式並保留頂部間距。已檢視本次建置的 worldboss.png，兩個分頁顯示正常。沒有更改挑戰或排行規則。
- `accessory_crit.svg.meta` 的匯入器引用改為與其他 SVG 相同的 Unity 內建匯入器；素材本身的 GUID 不變。本次建置成功匯入該 SVG。
- `BattleArt/README.md.meta` 為既有 README 的 TextScriptImporter 識別檔，不更改美術接入規格內容。
- Unity 同時修正六個 MapArt SVG 的匯入器引用。先前 create-map-icons.ps1 替換 GUID 的正則範圍過大，也替換了內嵌 importer GUID；現在限定只替換檔案頂層 guid。六個素材 GUID 保留，使用 Unity 修正的內建匯入器引用。
- 移除上述 meta 空欄位的行尾空白，保留所有實際匯入設定。

驗證：沿用剛完成的 Unity Windows 建置與 31 張 UI 擷取結果，執行期／樣式錯誤 0；主線 SVG 箭頭與世界首領分頁已檢視。世界首領截圖為離線未解鎖狀態，不代表模型顯示與線上排行已完成驗收。
