# 主城方框、底部留白與 icon hover 修正

日期：2026-10-10

## 實際變更

- 上方玩家與資源框改為直角矩形，整列水平置中，框內內容置中；頭像外框一併取消圓角。
- 底部功能列樣式高度由 220 改為 176，取消底部 32 的大留白；出征與關卡標籤配合調整位置。
- 六個選單按鈕的 normal、hover、active 共用尺寸、透明背景與固定 scale、translate，避免較早載入的 hover 高度 132、padding 8 覆蓋正常版面。
- hover 只套用於 home-fn-icon：scale 1.06，140ms ease-out；文字、分隔線、按鈕背景與點擊範圍維持不動。
- 美術使用既有主城背景、頭像與 icon，本次沒有新增繪製素材。

## 功能驗證

Unity 6000.3.25f1 Windows 打包成功。測試在 build/ui-layout-client-copy 執行，沒有改動原專案的截圖程式。

測試副本保留 CommercialUiCapture 的實際 UI 合成截圖方式，將流程縮限至主城，以 VisualElement 的 hover pseudo state 驗證實際解析後的樣式：

- 1920×1080、1600×900：icon worldBound 寬度比值為 1.06，移出後恢復原尺寸。
- hover 前後按鈕與文字 worldBound 的位置、大小保持一致；背景 alpha 為 0。
- 玩家、資源框 borderTopLeftRadius 為 0。
- 最終執行紀錄沒有 Exception、樣式解析失敗或編譯錯誤。
- 1600×900 渲染的底部黑條約 147 實際像素，對應參考尺寸 176。Unity 像素對齊後 resolvedStyle 高度為 176.4，因此該尺寸以截圖核對；早期驗證使用過嚴的絕對尺寸斷言曾失敗，最終改為紀錄尺寸並視覺核對。

原始執行結果保留於 build/home-ui-review/{1920x1080,1600x900}/verification.txt 與 player.log。測試副本的臨時程式沒有接入遊戲執行期。

## 美術驗收

已檢視本目錄三張實際 Unity 渲染截圖：上方方框整列置中，六個 icon 與文字沒有被裁切，底部黑色留白縮減；hover 截圖只有武將 icon 輕微放大，沒有方形底色或整個按鈕縮放。

此次驗收限於使用者要求的主城版面與 hover 修正，沒有宣稱其他頁面或全部素材已達商用品質。

- home-1920x1080.png：正常狀態。
- home-hover-1920x1080.png：武將 icon hover。
- home-1600x900.png：較小解析度版面。