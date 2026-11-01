# RAILWAY MISSION 3D

**寫實 3D 火車任務駕駛模擬遊戲（Android／Unity 6／個人開發）**

玩家扮演火車駕駛員，完成乘客運輸、準時抵達、精準停靠等任務，逐步解鎖新火車與新路線。
本專案依照 [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md) 的企劃開發，目前完成 **第一個可玩版本（MVP）：任務 01「首次列車駕駛」**。

---

## 目前可以玩什麼（v0.1.0 MVP）

| 項目 | 內容 |
|---|---|
| 路線 | 原創「青嶺線」約 3 km（台灣山線風格、1067 mm 窄軌）：楓林站 → 平原 → 溪谷鐵橋 → 青嶺隧道 → 下坡彎道 → 青山站 |
| 火車 | EMU 通勤電聯車 4 節編組（白色車體＋藍色腰帶，原創造型） |
| 任務 01 | 啟動火車 → 加速至 60 km/h → 不超速 → 通過鐵橋 → 進入隧道 → 抵達青山站 → 精準停在「停」字標 |
| 物理 | Spline 路徑＋自訂火車物理：牽引力／功率曲線、Davis 行駛阻力、坡度、空氣煞車延遲、緊急煞車（固定 50 Hz 步長，與畫面 FPS 分離） |
| 操作 | 寫實操作：油門、煞車分開控制＋緊急煞車、喇叭 |
| 視角 | 駕駛室（第一人稱）、車頭、追蹤（第三人稱，可拖曳旋轉）三種切換 |
| 評分 | 準時 300＋停靠 300＋安全 200＋平穩 200，依總分 1～3 星並獲得金幣 |
| 其他 | 速限號誌與前方速限預告、進站停車導引尺、繁中／英文切換、本機存檔、程式合成音效（馬達、軌道接縫、喇叭、煞車洩氣） |

> 第一版所有 3D 場景、火車與音效皆由程式產生，**不需要任何外部美術資源就能直接執行**；之後可逐步替換成 Blender 模型與實際錄音。

---

## 快速開始

1. 安裝 **Unity Hub** 與 **Unity 6.3 LTS（6000.3.x）**，安裝時勾選 **Android Build Support**（含 OpenJDK、Android SDK & NDK）。
2. `git clone https://github.com/apsmkimo/RAILWAYMISSION3D.git`
3. Unity Hub → **Add → Add project from disk** → 選擇此資料夾並開啟。
   - 第一次開啟會自動：套用 Android 設定（橫向、IL2CPP、ARM64、`com.apsmkimo.railwaymission3d`）、建立並套用 URP 行動版渲染設定。
4. 選單 **Railway Mission → 2. 建立 Mission01 場景**。
5. 按 **Play** 即可開始任務 01。
   （其實在任何場景按 Play 都會自動啟動 Demo。）

### 建置到 Android 手機

- 選單 **Railway Mission → 4. 建置 Android APK**，輸出到 `Builds/Android/RailwayMission3D.apk`；或
- File → Build Profiles → Android → Switch Platform → Build And Run（手機需開啟 USB 偵錯）。

### 雲端自動建置（選用）

- `Script compile check`：每次推送 C# 檔都會在 GitHub Actions 檢查腳本能否編譯（不需 Unity 授權）。
- `Android debug APK`：每次推送遊戲內容到 main（或在 Actions 頁面手動執行）都會以 GameCI 建置 **Debug APK**，完成後在該次執行的 Artifacts 下載 `RailwayMission3D-debug-apk`。
  - 需先在 repo 的 Settings → Secrets and variables → Actions 新增：`UNITY_LICENSE`（Unity 個人授權檔 `Unity_lic.ulf` 的完整內容；Windows 位於 `C:\ProgramData\Unity\Unity_lic.ulf`，macOS 位於 `/Library/Application Support/Unity/Unity_lic.ulf`）、`UNITY_EMAIL`、`UNITY_PASSWORD`（[說明](https://game.ci/docs/github/activation)）。
  - 未設定時流程會略過建置並顯示提示。

---

## 操作方式

| 動作 | 手機 | 鍵盤（編輯器測試） |
|---|---|---|
| 油門 | 右下把手往上推 | `W` / `S` |
| 煞車 | 左下把手往下拉 | `↓` / `↑` |
| 緊急煞車 | 下方紅色 EB 按鈕（停車後才能解除） | `空白鍵` |
| 喇叭 | 右上「喇叭」 | `H` |
| 切換視角 | 右上「視角」 | `C` |
| 轉動視角 | 拖曳畫面空白處（追蹤視角可縮放：滑鼠滾輪） | 滑鼠拖曳 |
| 暫停 | 右上「暫停」 | `Esc` |

**小技巧：** 注意下方的「前方速限」預告，限速區要整列車尾通過後才能加速；進站前約 300 m 開始逐步煞車，看著停車導引尺停在 ±1 m 內即可拿滿分。

---

## 專案結構

```text
Assets/RailwayMission/
├── Scripts/                     # 執行期程式（RailwayMission.asmdef）
│   ├── Core/        GameManager（總控制＋自動啟動）、MeshBuilder、MaterialLibrary、Loc（雙語）
│   ├── Track/       TrackPath（Catmull-Rom Spline、距離查表）、RouteData（車站／地標／速限）、RouteAsset
│   ├── Train/       TrainData（ScriptableObject）、TrainPhysics、TrainController、TrainVisualBuilder
│   ├── Mission/     MissionData（ScriptableObject）、MissionManager、MissionScoring
│   ├── Environment/ WorldBuilder（地形、鐵軌、橋梁、隧道、車站、電車線、樹林、河流）、PathExtruder
│   ├── Camera/      TrainCameraController（駕駛室／車頭／追蹤）
│   ├── UI/          DrivingHUD（手機駕駛介面、任務簡報、暫停、結算）
│   ├── Audio/       TrainAudio（程式合成音效）
│   ├── Save/        SaveManager（PlayerPrefs + JSON）
│   └── Data/        DefaultContent（青嶺線、EMU、任務 01 的內建資料）
└── Editor/                      # 編輯器工具（選單 Railway Mission）
    ├── RailwayMissionEditor.cs  # Android 設定、建立場景、建立資料資產、建置 APK
    └── URP/                     # 自動建立 URP 行動版設定
```

### 核心模組對照企劃

| 企劃中的類別 | 實作 |
|---|---|
| TrainController / TrainPhysics | `Train/TrainController.cs`、`Train/TrainPhysics.cs` |
| TrainTrack / TrackManager | `Track/TrackPath.cs`、`Track/RouteData.cs`（`TrackRoute`） |
| MissionManager | `Mission/MissionManager.cs` |
| StationController | `Track/RouteData.cs`（`Station`）＋ `MissionManager` 停靠判斷 |
| SpeedLimitManager | `TrackRoute.SpeedLimitAt / EffectiveLimit / NextSpeedChange` |
| CameraController | `Camera/TrainCameraController.cs` |
| AudioManager | `Audio/TrainAudio.cs` |
| UIManager | `UI/DrivingHUD.cs` |
| SaveManager | `Save/SaveManager.cs` |
| GameManager | `Core/GameManager.cs` |

### 新增任務／火車／路線（不用改程式）

選單 **Railway Mission → 3. 建立可編輯的資料資產**，會在 `Assets/RailwayMission/Data/` 產生
`QinglingLine`（路線）、`EMU_Commuter`（火車）、`Mission01_FirstDrive`（任務）三個 ScriptableObject。
複製後在 Inspector 修改，再指定到場景中 `RailwayMission` 物件的 GameManager 欄位即可。

---

## 開發進度

| 優先級 | 開發項目 | 狀態 |
|---|---|---|
| P0 | Unity 專案架構 | ✅ 完成 |
| P0 | 火車行駛系統 | ✅ 完成（牽引、阻力、坡度、煞車延遲、緊急煞車） |
| P0 | 鐵軌系統 | ✅ 完成（直線、彎道、坡度、橋梁、隧道） |
| P1 | 駕駛 UI | ✅ 完成（第一版 IMGUI，之後可改 uGUI／UI Toolkit 美化） |
| P1 | 攝影機系統 | ✅ 三種視角 |
| P1 | 第一張地圖 | ✅ 程式產生的山區鐵路（待換正式美術） |
| P2 | 任務系統 | 🟡 任務 01 完成，架構支援 15 個任務 |
| P2 | 金幣及解鎖 | 🟡 金幣與最佳紀錄已存檔，解鎖畫面待做 |
| P3 | 廣告及內購 | ⬜ 未開始 |

下一步建議：任務 02～05（精準停靠、依時刻表發車、三站載客、夜間行駛）、雨天／夜晚天氣、Blender 正式車體與車站模型、主選單與火車選擇畫面。
