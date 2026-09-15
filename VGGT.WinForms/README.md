# VGGT Studio - WinForms DesignTime UI

這是一個 Windows 桌面前端，透過官方 `facebookresearch/vggt` Python/PyTorch 實作執行 VGGT-1B，並以純 WinForms 自訂控制項預覽彩色 3D 點雲。`MainForm.Designer.cs` 可直接由 Visual Studio WinForms Designer 開啟。

## 輸入資料

- 必要：1 張以上、觀察同一靜態場景的 RGB 影像（JPG/PNG/BMP/WebP）。第一張影像是世界座標參考。
- 建議：相鄰視角有足夠重疊、曝光清楚、避免大量動態物體。模型不要求使用者提供相機內外參。
- 選用：CSV 查詢點，每列為 `x,y`；座標對應 VGGT 預處理後的第一張影像，用於 point tracking。
- 關閉程式時自動儲存目前影像清單與順序，下次啟動還原並略過不存在的檔案。移除／清除後的清單也會儲存；設定位置為 `%LOCALAPPDATA%\VGGT Studio\last-images.json`，只記錄路徑，不複製影像。

## 輸出資料

- `scene.ply`：經信心門檻與點數上限篩選的彩色點雲，直接顯示於 3D Viewport。
- `scene_mesh.glb`（選用）：由規則 point-map 網格建立的彩色三角 Mesh。跨越深度斷層、無效點或低信心點的三角形會被移除。
- `result.json`：每張影像的 3x3 intrinsic、3x4 camera-from-world extrinsic 與 tensor shapes。
- `images_depth/影像名稱.png` 與 `images_depth/影像名稱_Depth.png`：模型實際使用的 RGB 影像與相同尺寸的灰階深度預覽。重複檔名會自動加序號，避免覆寫。預覽按各張有效深度的 2–98 百分位正規化，不能當作公尺或跨影格共用尺度的深度值。
- `scene.rv3dproj`、`viewer_scene.glb`（產生 Mesh 時）：3D Viewer 第 10 版模型專案與內嵌原照片貼圖 GLB，與 `scene_mesh.glb` 使用相同內容，不再從頂點色重烘焙。
- `scene.camera-animation.json`：CameraAnimation 第 4 版相機路徑，照片清單順序對應每秒一個關鍵影格，使用線性轉場；不是由照片推估實際拍攝時間。
- `predictions.npz`（選用）：depth、confidence、world point map、depth+camera 反投影點、相機矩陣；提供 query CSV 時另含 track、visibility 與 track confidence。

## 環境設定

1. 安裝 Git 與 Python 3.10+。VGGT-1B 實務上需要 NVIDIA CUDA GPU。
2. RTX 50 系列不可使用舊版 `torch 2.5.1+cu118`。請建立獨立環境並安裝 CUDA 12.8 wheel，再安裝 VGGT editable package；不要直接執行會把 PyTorch 降級成 2.3.1 的舊 `requirements.txt`：

   ```powershell
   mkdir VGGT.Runtime
   git clone https://github.com/facebookresearch/vggt.git VGGT.Runtime\vggt
   python -m venv VGGT.Runtime\.venv
   .\VGGT.Runtime\.venv\Scripts\python.exe -m pip install --upgrade pip
   .\VGGT.Runtime\.venv\Scripts\python.exe -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128
   .\VGGT.Runtime\.venv\Scripts\python.exe -m pip install -e .\VGGT.Runtime\vggt
   .\VGGT.Runtime\.venv\Scripts\python.exe -m pip install -r .\VGGT.WinForms\python\requirements-postprocess.txt
   ```

3. 本機已配置於 `D:\SourceCode\AI_Projects\Codex Data\VGGT.Runtime`，程式啟動時會自動填入 Python 與 VGGT Repo。第一次推論會從 Hugging Face 下載 `facebook/VGGT-1B` 權重。
4. 建置：`dotnet build VGGT.WinForms.csproj`。

## 3D Viewport 操作

桌面介面分為六個群組：①輸入資料、②快速推估、③點雲調整與預覽、④高品質重建、⑤最終檢查與網格預覽、⑥匯出結果。

左側底部有獨立、固定 120 像素高的功能說明 ListBox，不隨群組捲動。滑鼠停留於主畫面的參數、標籤、按鈕、選項與選單，或以 Tab 切換焦點時，會顯示用途、目前值／範圍、調整影響與套用方式。停用按鈕也可查看操作條件。說明自動換行、保留最後內容，可選取後按 Ctrl+C 複製。

1. 加入照片或使用「檔案 → 輸入 → 影片抽幀與預覽」。照片可上移／下移；影片保持時間順序。
2. 「開始快速推估」均勻選取指定數量的影像（預設 3 張），儲存不可變的密集預測快取，再套用目前參數；不產生 GLB。
3. 修改信心、相對深度範圍、小型連通區域清理、保邊平滑與小孔洞尺寸後，按「套用並更新點雲」。這一步只使用 CPU 與快取，不執行 VGGT。粉紅色標示推估補點；「原始預測」顯示尚未後處理的抽樣點雲。
4. 「使用全部影像重建」對清單全部影像重新推估，套用相同處理參數。仍使用 518 像素的 VGGT 前處理；名稱並不代表更高模型解析度或已實作多視角融合。快速與高品質結果可在預覽上方切換，結果保留於本次程式工作階段。
5. 確認點雲後按「建立／更新網格預覽」。網格使用已套用的有效區域，不會另放寬信心篩選或自行追加補洞。中央提供網格幾何預覽（逐面顏色），完整照片貼圖請按「開啟 GLB」使用外部檢視器。也可切換編號 Camera 路徑。
6. 由「檔案 → 輸出」選擇 GLB、Viewer 專案、CameraAnimation、貼圖、模型影像與深度圖、PLY 或參數 JSON，分別選擇路徑與名稱。多張影像／貼圖輸出到使用者命名的新資料夾。

Viewer 專案以 `.rv3dproj` 儲存，旁邊建立同名 `.assets` 資料夾，包含內嵌貼圖 GLB、CameraAnimation 與參數；以相對路徑引用。搬移時請一併保留資源資料夾。CameraAnimation 可另從選單輸出為自行命名的檔案。

所有輸出成功後自動開啟檔案總管：單一檔案會被選取；貼圖或影像／深度圖資料夾則直接開啟其內容。取消或輸出失敗時不開啟。

狀態提示會指出「未套用變更」「輸入已變更」「網格需要更新」，不允許匯出過期結果。只改面數／貼圖尺寸，不需要重新套用點雲；改點大小只影響顯示。AI 輸出的深度預覽與匯出影像仍保留模型原始資料，後處理不覆寫原始預測。

相機中心使用 `-Rᵀt`，方向與上方向和 GLB 一起轉換為 `(x,-y,-z)`，並輸出目標程式所需的 Roll 與垂直 FOV。目標檢視器使用置中的透視投影與自身 FOV 範圍限制，無法完整表達 VGGT 的偏心主點。請保持影像依行進順序排列，才會有合理的瀏覽路徑。

## 影片輸入（短片第一版）

1. 使用「檔案 → 輸入 → 影片抽幀與預覽」，選擇 MP4、MOV、MKV、AVI、WebM 等影片（實際編碼支援依 FFmpeg）。
2. 設定起點、終點、取樣間隔、影格上限；目前範圍限影片前 120 秒，預設前 30 秒、最多 4 張，最高 32 張。需要 PATH 中的 FFmpeg，或在對話框指定 `ffmpeg.exe`；同目錄需有 `ffprobe.exe`。
3. 擷取後逐張查看、勾選影格；清晰度與相鄰畫面差異僅供比較，不自動刪除低紋理畫面。至少選兩張，按「使用勾選影格（取代清單）」。
4. 先快速推估與調整，再高品質重建、手動建立網格，最後使用「檔案 → 輸出」。影格越多顯存需求越高，CUDA 顯存不足時請減少保留影格。

抽幀使用解碼後的顯示時間戳記，支援可變幀率。超過數量上限時會拉大取樣間隔以涵蓋選定片段，不只截取片段開頭。這可能降低相鄰畫面重疊；快速移動的影片宜縮短片段。Camera 時間以第一張保留影格歸零，後續保留影片時間差，移除影格也不會壓縮中間時間。原影片時間保存在推論輸出的 `video_timeline.json` 與 `result.json`。

抽出的影格保存在 `%LOCALAPPDATA%\VGGT Studio\VideoFrames`；關閉重開會保留影格清單與時間資料。加入一般照片會切回每張 1 秒的照片時間模式，並記錄日誌。尚未實作長影片分段重建、座標對齊或體積融合。

- 左鍵拖曳：旋轉
- 右鍵拖曳：平移
- 滾輪：縮放
- 「重設視角」：回到預設相機

## GLB Mesh

桌面程式使用「建立／更新網格預覽」，完成後再手動輸出。以下信心四分之一與自動補洞描述只適用於獨立 CLI 的直接建模；桌面工作流程嚴格使用已套用的點雲設定。程式依「Mesh 面數上限」計算完整粗網格，以符合預算，不再抽取部分三角形造成缺面。Mesh 使用點雲濾除百分位的四分之一（介面 20% 時 Mesh 為 5%），減少連續表面被信心篩選切碎；仍保留深度斷層與無效區域檢查。

在三角化前，只對不超過 256 個模型像素、完全封閉、邊界深度差小於中位深度 4%、且邊界近似平面的孔洞插值。補邊、外輪廓、大型缺口與深度斷層不補。原始 NPZ 預測不會被改寫。

每個視角直接以原照片做 UV 貼圖，最長邊上限 2048 像素，不放大小圖；保留比 518 像素模型輸入與稀疏頂點色更豐富的影像細節。貼圖內嵌 GLB，使用雙面非金屬材質。既有相容匯出完整保留所有視角與貼圖。

此方法仍是各視角的局部表面，尚非 TSDF 多視角體積融合或封閉表面重建。有明顯預測偏差的視角仍可能留下接縫、重影、孔洞或重疊表面。Mesh 放寬篩選也可能保留更多不可靠表面；原照片模糊或未拍到的細節無法藉此還原。

## 點雲品質與調整

- 預設低信心濾除比例改為 20%，逐視角計算，避免整個較低信心的視角消失。若雜點增加，可提高此值；若缺口多，可降低。
- 預設改用官方 `pad` 預處理，保留完整視野，不再裁掉直式照片上下部分。以幾何遮罩排除補邊及無效深度；不以黑／白色像素判定補邊。CLI 可用 `--image-mode crop` 相容舊處理，實際模式記錄在 `result.json` 的 `preprocessMode`。查詢點 CSV 需對應新版實際模型影像座標。
- 超出點數上限時，以自適應 voxel 分組並平均座標與顏色，取代隨機抽點，改善空間覆蓋。近鄰顏色平均可能柔化細節，提高點數上限可減少此影響。
- 預覽以置中的圓點顯示，減少方塊感；點大小只影響顯示，不會補出缺失幾何。
- NPZ 新增 `image_valid_mask`，記錄有效影像區域與深度遮罩，尚未套用信心篩選。

驗證幾何處理：在專案根目錄執行 `.\VGGT.Runtime\.venv\Scripts\python.exe -m unittest discover -s VGGT.WinForms/python -p test_geometry_cleanup.py -v`。測試涵蓋空間取樣、低信心視角、深度斷層、面數限制、網格連通性、GLB 顏色／雙面材質及重合視角合併。

此應用程式是官方研究模型的桌面整合層，不是把約 10 億參數的 Transformer 重新以 C# 訓練或實作。這讓輸出與官方模型保持一致，也便於未來替換為 ONNX/TensorRT 後端。
