# 商店修改驗收，2026-10-10

依使用者四張批註圖及後續確認完成。使用者另明確授權 Codex 本次直接移除豪華通行證機制。

## 完成內容

- 四個分頁共用外框，分頁固定在商品捲動區上方；「儲值」改為「禮包」。
- 小月卡呈現元寶 300 ＋ 100 × 30、體力 60 × 30；大月卡呈現元寶 680 ＋ 200 × 30、體力 120 × 30。
- 商店獎勵圖示可點擊或以 Enter／空白鍵開啟名稱、數量與用途說明。
- 禮包、通行證及將魂商品的裝備使用武器、防具、飾品對應圖片，替換原先通用寶箱圖。
- 元寶六種商品維持三欄兩排，增加排間距與底部留白。
- 通行證改為橫向等級卡，每級上下排列免費與付費獎勵；進度區縮短說明文字。
- 將魂商品改為可橫向捲動的雙排小卡，加入武將肖像與物品圖片，移除頂部長篇描述。
- 移除豪華通行證商品、額外十級、980 元寶贈送與模擬工具購買呼叫；只保留 ¥30 普通通行證。
- 舊豪華存檔保留已獲得的元寶、等級及領取紀錄，當季權益正規化為普通付費通行證。舊未付款豪華訂單無法再兌付。

## 素材來源

沿用專案既有 ChibiSkin／UiSkin 資源圖、ChibiSkin 武將肖像與 EquipmentArt 裝備 PNG。這次新增的是 UI 版面、圖示接入及說明互動，沒有宣稱新製裝備原畫。

## 功能驗證

- 核心 294 項測試全部通過，含豪華商品拒絕、普通版無額外獎勵、舊權益保留。
- PlaySim 建置通過，零警告、零錯誤。
- Unity Windows 建置通過；1600 × 900 驗證輸出 28 張截圖。
- 自動驗證商店外框底部留白、分頁與商品區無重疊、元寶兩排間距、通行證橫向捲動、裝備圖示點擊開啟說明。
- 執行期與樣式錯誤為零；保留原有 HomeLayout 的 last-child 偽類警告。核心測試仍有原有 CampaignBalanceTests 的 xUnit1013 警告。

## 美術檢視

人工檢視以下六張實際遊戲截圖：月卡算式及道具數量清楚、元寶第二排完整顯示且未觸底、道具說明未裁切、通行證首尾等級與三個獎勵圖示完整、將魂雙排卡片無重疊。圖片風格沿用現有資源，未將功能通過等同全套素材達到商用品質。

| 頁面 | 截圖 |
| --- | --- |
| 禮包與月卡 | [gifts.png](ui-shop-review/gifts.png) |
| 元寶 | [recharge.png](ui-shop-review/recharge.png) |
| 通行證 | [pass.png](ui-shop-review/pass.png) |
| 通行證尾端 | [pass-end.png](ui-shop-review/pass-end.png) |
| 將魂 | [soul.png](ui-shop-review/soul.png) |
| 裝備說明 | [equipment-detail.png](ui-shop-review/equipment-detail.png) |

驗證命令：`dotnet test tests/SanGuo.Core.Tests --no-restore -m:1 --verbosity quiet`、`dotnet build tools/playsim/PlaySim.csproj --no-restore -m:1 --verbosity quiet`、`tools/verify-unified-ui.ps1`。
