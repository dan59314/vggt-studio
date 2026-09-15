namespace VGGT.WinForms;

public partial class MainForm
{
    private readonly ListBox _contextHelp = new()
    {
        Dock=DockStyle.Bottom,Height=120,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawVariable,
        SelectionMode=SelectionMode.MultiExtended,BackColor=Color.FromArgb(246,249,253),
        ForeColor=Color.FromArgb(30,50,70),AccessibleName="功能與參數說明"
    };
    private readonly Dictionary<Control,Func<string[]>> _controlHelp=[];
    private Func<string[]>? _activeHelp;
    private string _helpText="";
    private Control? _hoverHelpControl;
    private Point _helpMouse;

    private void InitializeContextHelp()
    {
        InitializeLeftRegions();
        _contextHelp.MeasureItem+=(_,e)=>
        {
            if(e.Index<0||e.Index>=_contextHelp.Items.Count)return;
            e.ItemHeight=TextRenderer.MeasureText(_contextHelp.Items[e.Index].ToString(),_contextHelp.Font,
                new Size(Math.Max(40,_contextHelp.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-12),int.MaxValue),
                TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+6;
        };
        _contextHelp.DrawItem+=(_,e)=>
        {
            if(e.Index<0)return;
            e.DrawBackground();
            var bounds=new Rectangle(e.Bounds.X+5,e.Bounds.Y+3,
                Math.Max(40,_contextHelp.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-12),e.Bounds.Height-6);
            TextRenderer.DrawText(e.Graphics,_contextHelp.Items[e.Index].ToString(),_contextHelp.Font,bounds,
                (e.State&DrawItemState.Selected)!=0?SystemColors.HighlightText:_contextHelp.ForeColor,
                TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        };
        _contextHelp.Resize+=(_,_)=>RefreshHelp(true);
        _contextHelp.KeyDown+=(_,e)=>
        {
            if(e.Control&&e.KeyCode==Keys.C)
            {
                var items=_contextHelp.SelectedItems.Count>0?_contextHelp.SelectedItems.Cast<object>():_contextHelp.Items.Cast<object>();
                string text=string.Join(Environment.NewLine,items);if(text.Length>0)Clipboard.SetText(text);
                e.SuppressKeyPress=true;
            }
        };
        void Parameter(NumericUpDown control,string name,string unit,string meaning,string action)
        {
            _controlHelp[control]=()=>[$"【{name}】",$"目前：{control.Value:0.###}{unit}（範圍 {control.Minimum:0.###}～{control.Maximum:0.###}）",meaning,action];
        }
        Parameter(_quickCount,"快速推估影像數"," 張","從清單均勻選取代表視角；數量不超過輸入影像總數。越多通常越慢、越占顯存。","修改後按「開始快速推估」；這會重新執行 AI。");
        Parameter(nudConfidence,"濾除低信心","%","逐張影像以信心百分位篩選。20 表示濾除約最低 20%；不是信心分數必須大於 20。降低可減少缺口，但可能增加雜點。","修改後按「套用並更新點雲」，不必重新執行 AI。");
        Parameter(_nearDepth,"最近深度","","排除距離各張相機太近的點。使用模型相對深度，不是公尺；必須小於最遠深度。","修改後套用點雲。太高可能刪除前景物體。");
        Parameter(_farDepth,"最遠深度","","排除超出此深度的點。使用模型相對深度，不是公尺；必須大於最近深度。","修改後套用點雲。降低可清除遠方雜點，也可能刪除牆面。");
        Parameter(_components,"移除小區域"," 像素","在每張深度影像中移除小於此面積的連通區域；0 關閉。不是 3D 鄰居數。過大會刪除細小物體。","修改後套用點雲；比較桌腳、欄杆等細節是否仍保留。");
        Parameter(_smooth,"保邊平滑"," 級","0 關閉，1～5 逐步提高深度平滑容許範圍，減少局部起伏；太強可能減少細節。","修改後套用點雲；不改寫原始 AI 預測。");
        Parameter(_holeArea,"小孔洞上限"," 像素","0 關閉。只補小於等於此面積、封閉且深度連續、近似平面的缺口；不補所有洞，也不封閉外輪廓。","修改後套用點雲；切換「補點標示（粉紅）」檢查推估部分。");
        Parameter(nudMaxPoints,"預覽點數上限"," 點","空間取樣後最多顯示的點數。提高可減少預覽空隙，但增加處理與繪圖負擔；不提高 AI 精度，也不限制 GLB 面數。","修改後套用點雲；GLB 仍使用完整的有效點網格。");
        Parameter(nudPointSize,"顯示點大小"," 像素","只改變點在螢幕上的大小。放大可遮住顯示縫隙，但不會補出幾何或改變 GLB。","立即生效，不需重新推估或建立網格。");
        Parameter(nudMaxFaces,"Mesh 面數上限"," 面","限制三角形數量。降低會使用較粗網格；提高可保留更多幾何細節，但不會增加照片細節。","只需「建立／更新網格預覽」，不用重新套用點雲或執行 AI。");
        Parameter(_textureSize,"貼圖最長邊"," 像素","每張 GLB 貼圖的最大邊長。大圖會縮小，小圖不放大；提高可能讓貼圖更清晰，也增加檔案大小。","只需更新網格。中央為幾何預覽，完整貼圖請開啟 GLB。");
        _controlHelp[_quickRun]=()=>["【開始快速推估】","使用指定數量的代表影像，執行 AI、快取原始預測，再套用點雲參數；不自動建立 GLB。",RunHelp()];
        _controlHelp[btnRun]=()=>["【使用全部影像重建】",$"目前清單共 {lstImages.Items.Count} 張。使用全部視角重新推估，再套用目前參數。","仍使用同一 VGGT 解析度；更多視角不保證更好，也更占顯存。",RunHelp()];
        _controlHelp[_apply]=()=>["【套用並更新點雲】","以快取執行篩選、清理、平滑與補洞；不重跑 AI。更新後舊網格需重新建立。",_runCts is not null?"目前工作執行中，請等待或取消。":_parameterStatus.Text];
        _controlHelp[_buildMesh]=()=>["【建立／更新網格預覽】","依已套用的點雲和面數／貼圖設定建立 GLB 快取。完成後再由「檔案 → 輸出」另存。",_runCts is not null?"目前工作執行中，請等待或取消。":_meshStatus.Text];
        _controlHelp[btnCancel]=()=>["【取消目前工作】",_runCts is null?"目前沒有執行中的工作，所以按鈕停用。":"停止目前推估或後處理，保留先前已完成的結果。"];
        _controlHelp[btnOpenGlb]=()=>["【開啟 GLB】","使用系統預設檢視器查看完整照片貼圖，並在檔案總管選取快取 GLB。",MeshCurrent?"網格已更新，可開啟。":_meshStatus.Text];
        _controlHelp[_resultChoice]=()=>["【切換結果】",$"目前：{_quality}。","快速與高品質結果分別保留於本次工作階段；切換不重新推估。若參數與結果不同，需重新套用。"];
        _controlHelp[_viewChoice]=()=>["【預覽模式】",$"目前：{_viewChoice.Text}","原始預測為抽樣原始點雲；粉紅色代表補點；網格為逐面顏色的幾何預覽；Camera 路徑以金色連線與編號顯示。"];
        _controlHelp[lstImages]=()=>["【輸入影像清單】",$"共 {lstImages.Items.Count} 張；選取可查看照片。Ctrl／Shift 可多選後移除。","照片依清單順序建立路徑，影片保留時間順序。更改清單後，原結果會標記需要重新推估。"];
        _controlHelp[viewport]=()=>["【3D 預覽】","左鍵拖曳旋轉、右鍵拖曳平移、滾輪縮放。點大小只影響點雲顯示。","幾何預覽不顯示完整貼圖細節；請用「開啟 GLB」檢查貼圖。"]; 
        _controlHelp[progress]=()=>["【工作進度】",$"目前 {progress.Value}%：{lblStatus.Text}","讀取模型、AI 推估、點雲處理與建模的耗時不同；百分比不是剩餘時間估計。"];
        RegisterHelpControls(this);
        if(MainMenuStrip is not null)foreach(ToolStripItem item in MainMenuStrip.Items)RegisterMenuHelp(item);
        _activeHelp=()=>["【功能與參數說明】","將滑鼠移到元件，或用 Tab 切換焦點，這裡會顯示用途、目前數值與調整影響。","此區固定於左側底部，不隨群組捲動。可選取說明後按 Ctrl+C 複製。"];
        RefreshHelp();
        var timer=new System.Windows.Forms.Timer(components ??=new System.ComponentModel.Container()) {Interval=180};
        timer.Tick+=(_,_)=>
        {
            if(!Visible||Form.ActiveForm!=this)return;
            var mouse=Cursor.Position;
            Control? control=HelpControlAt(mouse);
            if(control!=_hoverHelpControl||mouse!=_helpMouse)
            {
                _hoverHelpControl=control;_helpMouse=mouse;
                if(control is not null)ActivateHelp(control);
            }
            RefreshHelp();
        };
        timer.Start();
    }
    private string RunHelp()=>_runCts is not null?"目前正在處理，請等待或取消。":lstImages.Items.Count==0?"請先加入照片或影片影格。":"可開始；原始預測將保存在輸出位置。";
    private void RegisterHelpControls(Control root)
    {
        if(root==_contextHelp)return;
        if(root is Label && root.Parent?.Controls.OfType<NumericUpDown>().FirstOrDefault() is {} parameter &&
            _controlHelp.TryGetValue(parameter,out var parameterHelp))_controlHelp[root]=parameterHelp;
        if(!_controlHelp.ContainsKey(root) && root is Button)
        {
            string description=root.Text switch
            {
                "加入影像"=>"選擇同一場景的照片，可多選；加入後需重新推估。",
                "加入資料夾"=>"加入資料夾內支援的影像，按檔名排序，不包含子資料夾。",
                "影片抽幀…"=>"選取影片片段、間隔與影格數，預覽勾選後取代目前清單。",
                "移除"=>"從清單移除選取影像，不會刪除原始檔案。",
                "清除"=>"清空輸入清單，不會刪除照片；關閉時會記住清空後的清單。",
                "上移" or "下移"=>"調整選取照片的順序；第一張為參考座標。影片影格不能改變時間順序。",
                "執行環境／輸出位置…"=>"指定 Python、VGGT Repo、預測快取位置、模型與選用查詢點 CSV。最終輸出位置另由輸出選單選擇。",
                "重設參數"=>"恢復點雲參數預設值；需再按套用。不會重新推估，也不更改 Mesh 面數與貼圖大小。",
                "重設視角"=>"恢復預設旋轉、縮放與平移，不改變模型資料。",
                "上下翻轉"=>"將預覽方向上下翻轉，不改變輸出模型座標。",
                _=>"執行按鈕所示的操作。"
            };
            _controlHelp[root]=()=>[$"【{root.Text}】",description,root.Enabled?"可操作。":_runCts is not null?"目前工作執行中，暫時停用。":"目前不符合操作條件，請參考群組狀態提示。"];
        }
        if(!_controlHelp.ContainsKey(root) && root is CheckBox check)
            _controlHelp[root]=()=>[$"【{check.Text}】",check.Checked?"目前已勾選：啟用此選項。":"目前未勾選：不啟用此選項。"];
        root.MouseEnter+=(_,_)=>ActivateHelp(root);
        root.Enter+=(_,_)=> { _helpMouse=Cursor.Position;_hoverHelpControl=HelpControlAt(_helpMouse);ActivateHelp(root); };
        foreach(Control child in root.Controls)RegisterHelpControls(child);
    }
    private Control? HelpControlAt(Point screenPoint)
    {
        Control current=this;
        if(!ClientRectangle.Contains(PointToClient(screenPoint)))return null;
        while(current.GetChildAtPoint(current.PointToClient(screenPoint),GetChildAtPointSkip.Invisible) is {} child)current=child;
        return current;
    }
    private void ActivateHelp(Control control)
    {
        for(Control? current=control;current is not null;current=current.Parent)
        {
            if(current==_contextHelp)return;
            if(_controlHelp.TryGetValue(current,out var provider)){_activeHelp=provider;RefreshHelp();return;}
        }
    }
    private void RegisterMenuHelp(ToolStripItem item)
    {
        item.MouseEnter+=(_,_)=>
        {
            _activeHelp=()=>[$"【檔案選單：{item.Text}】",item.Text?.Contains("輸出")==true||item.OwnerItem==_fileOutput
                ?"選擇格式後，自訂輸出位置與檔名；多張貼圖／深度圖使用命名資料夾。完成後自動開啟檔案總管。"
                :"選擇要執行的檔案操作。",
                item.Enabled?"可操作。":_runCts is not null?"目前工作執行中，請等待完成。":_fileOutput?.Enabled==false?"請先完成推估並套用目前點雲參數。":"請先建立或更新網格。"];
            RefreshHelp();
        };
        if(item is ToolStripDropDownItem parent)foreach(ToolStripItem child in parent.DropDownItems)RegisterMenuHelp(child);
    }
    private void RefreshHelp(bool force=false)
    {
        if(_activeHelp is null||_contextHelp.IsDisposed)return;
        var lines=_activeHelp();string text=string.Join("\n",lines);
        if(!force&&text==_helpText)return;
        _helpText=text;
        _contextHelp.BeginUpdate();_contextHelp.Items.Clear();_contextHelp.Items.AddRange(lines);_contextHelp.EndUpdate();
    }
}
