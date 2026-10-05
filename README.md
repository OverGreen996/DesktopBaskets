<p align="center">
  <img src="docs/images/hero.svg" alt="Desktop Baskets：讓桌面有秩序，讓檔案留在原位" width="100%">
</p>

<p align="center">
  <strong>把遊戲、工作、雜項放進各自的桌面整理框。</strong><br>
  拖進去即可分類，原始檔案仍在原本的位置。
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-x64-202426?style=flat-square&amp;labelColor=202426&amp;color=fffa00" alt="Windows x64">
  <img src="https://img.shields.io/badge/.NET_Framework-4.8-202426?style=flat-square&amp;labelColor=202426&amp;color=bac5ca" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/UI-Native_WinForms-202426?style=flat-square&amp;labelColor=202426&amp;color=bac5ca" alt="原生 WinForms 介面">
</p>

<p align="center">
  <a href="https://github.com/OverGreen996/DesktopBaskets/releases/latest"><strong>下載安裝版</strong></a> &nbsp;·&nbsp;
  <a href="#介面預覽">介面預覽</a> &nbsp;·&nbsp;
  <a href="#開始使用">開始使用</a> &nbsp;·&nbsp;
  <a href="https://github.com/OverGreen996/DesktopBaskets/issues">回報問題</a>
</p>

## 桌面整理，原位保留

Desktop Baskets 是 Windows 原生桌面分類工具。以《明日方舟：終末地》的介面語彙為視覺方向，使用暗色半透明玻璃、細切角邊框、黃色旗角與即時字體，讓整理框融入桌面。

| 能力 | 實際行為 |
| :--- | :--- |
| **直接拖入** | 桌面檔案、資料夾與捷徑可直接分類；框外不再重複顯示原生圖示。已有籃框保持可見，不再整批隱藏再顯示。 |
| **原始路徑保留** | 分類與跨籃移動只改視覺歸屬，原檔不搬移、不複製，也不建立額外捷徑。 |
| **空位自動補齊** | 分類後，後面的未分類圖示依桌面順序補上空位；重新整理後保留已整理位置並清除隱藏圖示的選取框。 |
| **桌面圖示避讓** | 新增、移動或放大整理框時，先把擋到的未分類圖示擠到可用位置。 |
| **按格數縮放** | 沿用系統桌面的圖示間距；最小 5 × 1 格，超出範圍可捲動。 |
| **磁吸與鎖定** | 靠近其他框會吸附對齊；鎖定位置與大小後，仍可操作框內檔案。 |
| **即時資料** | 編號、檔案數、狀態、格數、版本與修訂日期都有實際用途。 |
| **低資源待命** | 30 秒無操作進入微待命，以事件喚醒；沒有持續動畫或桌面輪詢。 |

## 介面預覽

<p align="center">
  <img src="docs/images/basket.png" alt="整理框：真實字體、連續分類編號、即時檔案數與切角邊框" width="100%">
</p>

<p align="center"><sub>原生控制項繪製預覽。實際桌面上的玻璃底色會透出桌布；文字與圖示維持不透明。</sub></p>

<details>
<summary><strong>展開管理介面與最小尺寸預覽</strong></summary>

### 管理介面

![分類管理：視窗按鈕直接融入介面，沒有獨立標題列色帶](docs/images/manager.png)

### 5 × 1 格

![最小整理框仍保留標題、資訊與邊框，檔案超過五個可捲動](docs/images/minimum.png)

在 76 × 99 px 的桌面圖示間距下，最小尺寸為 **476 × 250 px**。其他桌面間距的實際尺寸會不同。

預覽使用示範項目，不包含開發者的個人桌面檔案。

</details>

## 開始使用

1. 到 [Releases](https://github.com/OverGreen996/DesktopBaskets/releases/latest)，下載 **DesktopBaskets-Setup-0.4.7-x64.exe**。
2. 執行安裝程式。預設只安裝到目前 Windows 帳號，不需要系統管理員權限。
3. 從開始功能表開啟 **Desktop Baskets**，新增「遊戲」、「工作」、「雜項」等分類。
4. 把桌面項目拖進框內，雙擊即可開啟。

安裝程式提供 **桌面捷徑** 與 **登入 Windows 時啟動** 兩個可選項，預設都不勾選。也提供 ZIP 免安裝版。

> 更新前，請先在程式或系統匣選 **「退出並還原圖示」**。按管理視窗的 × 只會藏到系統匣，桌面整理仍持續運作。

### 常用操作

| 操作 | 方法 |
| :--- | :--- |
| 自訂名稱 | 雙擊整理框標題，或開啟分類設定。 |
| 移動／縮放 | 拖曳標頭移動；拖曳邊緣、角落或右下十字按格數縮放。 |
| 鎖定／解鎖 | 點擊黃色狀態圓點，或從分類選單設定。 |
| 查看更多檔案 | 使用滾輪、右側細捲動條、方向鍵或 PageUp / PageDown。 |
| 移出分類 | 將框內圖示直接拖到桌面空白處，原生圖示會出現在落點附近，路徑不變。 |
| 檔案右鍵 | 開啟 Windows Shell 原生選單，使用系統的開啟、複製、刪除、內容等功能。 |
| 調整玻璃 | 分類設定中的底色濃度可設為 40–100%，預設 68%。 |
| 暫停或退出 | 使用「暫停並還原圖示」或「退出並還原圖示」。 |

## 安裝、設定與復原

- **系統**：Windows 10 / 11 x64，需 .NET Framework 4.8 或更新版本。
- **程式位置**：`%LOCALAPPDATA%\Programs\DesktopBaskets`。
- **分類與圖示位置備份**：`%LOCALAPPDATA%\DesktopBaskets\state.json`。
- **解除安裝**：先退出並還原圖示，再從 Windows 已安裝的應用程式移除。分類設定與桌面原始檔案會保留。
- **安裝檔簽章**：目前未提供 Authenticode 簽章。Release 同時附上 SHA-256 校驗檔。

目前支援具有實際檔案路徑的桌面項目。資源回收筒等特殊 Shell 項目尚不在拖入分類範圍內。

<details>
<summary><strong>資源設計與驗證紀錄</strong></summary>

原生 WinForms / C#，沒有瀏覽器引擎。檔案區使用虛擬化繪製，只畫可見項目；圖示快取最多 64 份，不為每個檔案建立控制項。

微待命會停止閒置計時器，清空圖示快取並一次性回收工作集；點擊、拖曳、鍵盤與捲動仍可喚醒。

0.4.0 的兩個玻璃框、18 個項目待命量測：工作集約 **10.9 MiB**、私有提交約 **54.5 MiB**，約 10 秒觀測期間 CPU 約 **0.156%（單核心計算）**、持續重繪 0 次。這是先前版本在單一電腦的量測，不是本版或所有電腦的保證。

0.4.5 的相同待命測試：工作集約 **10.5 MiB**、私有提交約 **32.4 MiB**；約 10 秒內 CPU 約 **0.156%（單核心）**、重繪與桌面事件皆為 0 次。這仍是先前版本的單機量測。

0.4.6 在本次環境的待命 CPU 約 **12.34%（單核心）**，同環境重測 0.4.5 約 **11.89%**；新版工作集約 **30.5 MiB**。觀測期間重繪、桌面事件與版面檢查皆為 0，偏高 CPU 的原因尚未確定，不能把此結果稱為極低待命。

拖出驗證結合拖曳事件測試與實際 Explorer 圖示返回落點，尚未完成端對端滑鼠拖放。原生內容視窗已實際確認，複製、剪下與測試檔案刪除均已執行。

本版檢查結果見 [驗證紀錄](docs/verification.json)：原始路徑、格數與比例、1,000 項捲動與裁切、鎖定、視窗控制，以及安裝／解除安裝流程。

</details>

## 從原始碼建置

Windows 上安裝 .NET SDK（6.0 或更新版本）及 .NET Framework 4.8 targeting pack：

```powershell
./scripts/build.ps1
```

產出位於 `dist/`。製作安裝版另需 [Inno Setup 6.7.3 或更新版本](https://jrsoftware.org/isdl.php)：

```powershell
./scripts/build.ps1 -Installer -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

```text
src/DesktopBaskets/    原生桌面程式
installer/            安裝程式腳本與繁體中文語系
scripts/              建置腳本
docs/                 說明、介面預覽與驗證結果
```

GitHub Actions 會建置免安裝版並執行版面與檔案路徑檢查。安裝版由 Inno Setup 腳本建置，正式檔案附於 Releases。

## 字型、相依與視覺來源

英文展示字體使用 **Russo One**；資訊與中文使用 Windows 系統字型。文字與邊框皆即時繪製，不以參考截圖當作 UI 背景。

相依與字型授權見 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。專案原始碼目前未指定授權條款。

本專案為獨立桌面工具，視覺方向受《明日方舟：終末地》啟發，未包含官方遊戲素材，亦非官方產品。
