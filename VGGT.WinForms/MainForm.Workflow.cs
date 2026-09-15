using System.Text.Json;
using VGGT.WinForms.Models;
using VGGT.WinForms.Services;

namespace VGGT.WinForms;

public partial class MainForm
{
    private readonly NumericUpDown _quickCount = new() { Minimum=1, Maximum=16, Value=3 };
    private readonly NumericUpDown _nearDepth = new() { Minimum=0, Maximum=100000, DecimalPlaces=3 };
    private readonly NumericUpDown _farDepth = new() { Minimum=.001m, Maximum=100000, Value=100000, DecimalPlaces=3 };
    private readonly NumericUpDown _components = new() { Maximum=1000, Increment=10 };
    private readonly NumericUpDown _smooth = new() { Maximum=5 };
    private readonly NumericUpDown _holeArea = new() { Maximum=1024, Increment=16 };
    private readonly NumericUpDown _textureSize = new() { Minimum=512, Maximum=4096, Increment=512, Value=2048 };
    private readonly Label _workflowStatus = new() { AutoSize=true, MaximumSize=new Size(1100,0) };
    private readonly Label _inputStatus = new() { AutoSize=true, MaximumSize=new Size(410,0) };
    private readonly Label _parameterStatus = new() { AutoSize=true, MaximumSize=new Size(410,0) };
    private readonly Label _meshStatus = new() { AutoSize=true, MaximumSize=new Size(410,0) };
    private readonly Button _quickRun = new() { Text="開始快速推估", AutoSize=true };
    private readonly Button _apply = new() { Text="套用並更新點雲", AutoSize=true };
    private readonly Button _buildMesh = new() { Text="建立／更新網格預覽", AutoSize=true };
    private readonly Button _exportGlb = new() { Text="匯出 GLB…", AutoSize=true };
    private readonly Button _exportProject = new() { Text="匯出 Viewer 專案與路徑…", AutoSize=true };
    private readonly ComboBox _resultChoice = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=160 };
    private readonly ComboBox _viewChoice = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=220 };
    private readonly List<GroupBox> _workflowGroups = [];
    private string? _cacheDirectory;
    private string? _appliedParameters;
    private string? _meshParameters;
    private string _quality = "尚未推估";
    private string _activeInputs = "";
    private bool _inputDirty;
    private bool _workflowReady;
    private Snapshot? _quickSnapshot, _highSnapshot;
    private sealed record Snapshot(VggtResult Result,string Output,string Cache,string? Applied,string? Mesh,string Quality,string Inputs);

    private void InitializeWorkflow()
    {
        rootSplit.Panel1.Controls.Clear();
        rootSplit.SplitterDistance = Math.Min(480,Width/2);
        var flow = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoScroll=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, Padding=new Padding(6) };
        rootSplit.Panel1.Controls.Add(flow);
        var cancelRow = new FlowLayoutPanel { AutoSize=true, Width=435 };
        btnCancel.Dock=DockStyle.None;btnCancel.AutoSize=true;btnCancel.Text="取消目前工作";
        cancelRow.Controls.Add(btnCancel);flow.Controls.Add(cancelRow);
        progress.Dock=DockStyle.None;progress.Width=430;flow.Controls.Add(progress);
        lblStatus.Dock=DockStyle.None;lblStatus.AutoSize=true;lblStatus.MaximumSize=new Size(420,0);flow.Controls.Add(lblStatus);
        FlowLayoutPanel Group(string title)
        {
            var box = new GroupBox { Text=title, Width=435, AutoSize=true, Padding=new Padding(8) };
            var contents = new FlowLayoutPanel { Dock=DockStyle.Fill, AutoSize=true, FlowDirection=FlowDirection.TopDown, WrapContents=false, Padding=new Padding(3) };
            box.Controls.Add(contents);flow.Controls.Add(box);_workflowGroups.Add(box);return contents;
        }
        void Row(FlowLayoutPanel panel,string caption,Control control)
        {
            var row = new FlowLayoutPanel { AutoSize=true, Width=400 };
            row.Controls.Add(new Label { Text=caption, MinimumSize=new Size(200,28), MaximumSize=new Size(200,0), AutoSize=true, TextAlign=ContentAlignment.MiddleLeft });
            control.Dock=DockStyle.None;control.Width=140;control.Margin=new Padding(3);row.Controls.Add(control);panel.Controls.Add(row);
        }
        void Buttons(FlowLayoutPanel panel, params Control[] controls)
        {
            var row=new FlowLayoutPanel { AutoSize=true, Width=405 };
            foreach(var c in controls) { c.Dock=DockStyle.None;c.AutoSize=true;c.Margin=new Padding(3);row.Controls.Add(c); }
            panel.Controls.Add(row);
        }
        Button Button(string text,Action action) { var b=new Button { Text=text,AutoSize=true }; b.Click+=(_,_)=>action();return b; }
        var input=Group("① 輸入資料");
        Buttons(input,btnAddImages,btnAddFolder,Button("影片抽幀…",ImportVideo));
        lstImages.Dock=DockStyle.None;lstImages.Size=new Size(400,145);input.Controls.Add(lstImages);
        lstImages.FormattingEnabled=true;
        lstImages.Format+=(_,e)=> { if(e.ListItem is string path)e.Value=Path.GetFileName(path); };
        Buttons(input,btnRemove,btnClear,Button("上移",()=>MoveInput(-1)),Button("下移",()=>MoveInput(1)));
        input.Controls.Add(_inputStatus);Buttons(input,Button("執行環境／輸出位置…",EditEnvironment));
        InitializeSubjectSelection(input);
        var quick=Group("② 快速推估");
        Row(quick,"代表影像數（均勻選取）",_quickCount);Buttons(quick,_quickRun);
        quick.Controls.Add(new Label { Text="先用少量視角檢查場景，不產生 GLB。",AutoSize=true });
        _quickRun.Click+=async(_,_)=>await RunWorkflowAsync(true);
        var tune=Group("③ 點雲調整與預覽");
        Buttons(tune,_depthInspect);
        _depthInspect.Click+=async(_,_)=>await InspectDepthAsync();
        _controlHelp[_depthInspect]=()=>["【深度分布與範圍預覽】","讀取既有快取，不重跑 AI。用近／遠滑桿與灰色排除點預覽，確認後帶回深度參數。",
            "只預覽原始深度與目前主體遮罩；信心、平滑、補洞等效果需回③套用。數值是各相機的模型相對深度，不是公尺。"];
        Row(tune,"濾除低信心（%）",nudConfidence);Row(tune,"最近深度（模型相對單位）",_nearDepth);Row(tune,"最遠深度（模型相對單位）",_farDepth);
        Row(tune,"移除小區域（影像像素數）",_components);Row(tune,"保邊平滑（0＝關閉）",_smooth);Row(tune,"小孔洞上限（像素，0＝關閉）",_holeArea);
        Row(tune,"預覽點數上限",nudMaxPoints);Row(tune,"顯示點大小（不改幾何）",nudPointSize);
        Buttons(tune,_apply,Button("重設參數",ResetProcessing));tune.Controls.Add(_parameterStatus);
        _apply.Click+=async(_,_)=>await ProcessWorkflowAsync(false);
        var high=Group("④ 高品質重建");
        btnRun.Text="使用全部影像重建";Buttons(high,btnRun);
        high.Controls.Add(new Label { Text="使用清單全部視角重新推估，再套用目前參數。\n仍為 VGGT 重建；未加入跨段融合。",AutoSize=true,MaximumSize=new Size(400,0) });
        var inspect=Group("⑤ 最終檢查與網格預覽");
        Row(inspect,"Mesh 面數上限",nudMaxFaces);Row(inspect,"貼圖最長邊（不放大小圖）",_textureSize);
        Buttons(inspect,_buildMesh,btnOpenGlb);inspect.Controls.Add(_meshStatus);
        inspect.Controls.Add(new Label { Text="中央檢查網格形狀；完整貼圖以外部 GLB 檢視器查看。",AutoSize=true,MaximumSize=new Size(400,0) });
        _buildMesh.Click+=async(_,_)=>await ProcessWorkflowAsync(true);
        var export=Group("⑥ 匯出結果");
        export.Controls.Add(new Label { Text="請使用「檔案 → 輸出」選擇檔案類型、路徑與名稱。\nGLB／Viewer 專案需要最新網格。",AutoSize=true,MaximumSize=new Size(400,0) });
        _resultChoice.Items.AddRange(["快速結果","高品質結果"]);
        _resultChoice.SelectedIndexChanged+=(_,_)=>SwitchResult();
        _viewChoice.Items.AddRange(["處理後點雲","原始預測點雲","補點標示（粉紅）","網格幾何預覽","Camera 路徑"]);
        _viewChoice.SelectedIndexChanged+=(_,_)=>ShowWorkflowView();
        viewTools.Controls.Add(_resultChoice);viewTools.Controls.Add(_viewChoice);
        _viewChoice.SelectedIndex=0;
        _workflowStatus.Dock=DockStyle.Top;rootSplit.Panel2.Controls.Add(_workflowStatus);rightSplit.BringToFront();
        foreach(var numeric in new[]{nudConfidence,_nearDepth,_farDepth,_components,_smooth,_holeArea,nudMaxPoints,nudMaxFaces,_textureSize})
            numeric.ValueChanged+=(_,_)=>RefreshWorkflow();
        var inputTab=new TabPage("輸入影像");var picture=new PictureBox { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom };
        inputTab.Controls.Add(picture);resultTabs.TabPages.Insert(0,inputTab);
        lstImages.SelectedIndexChanged+=(_,_)=> { picture.Image?.Dispose();picture.Image=null;if(lstImages.SelectedItem is string p && File.Exists(p)) { using var image=Image.FromFile(p);picture.Image=new Bitmap(image); } };
        _workflowReady=true;RefreshWorkflow();
    }

    private string Parameters() => JsonSerializer.Serialize(new { confidence=(double)nudConfidence.Value,near=(double)_nearDepth.Value,far=(double)_farDepth.Value,
        components=(int)_components.Value,smooth=(int)_smooth.Value,holeArea=(int)_holeArea.Value,maxPoints=(int)nudMaxPoints.Value,maxFaces=(int)nudMaxFaces.Value,textureSize=(int)_textureSize.Value,subjectMasks=ActiveSubjectMasks });
    private string PointParameters() => JsonSerializer.Serialize(new { confidence=(double)nudConfidence.Value,near=(double)_nearDepth.Value,far=(double)_farDepth.Value,
        components=(int)_components.Value,smooth=(int)_smooth.Value,holeArea=(int)_holeArea.Value,maxPoints=(int)nudMaxPoints.Value,subjectMasks=ActiveSubjectMasks });
    private string InputSignature() => JsonSerializer.Serialize(new { Images=lstImages.Items.Cast<string>().ToArray(),Video=_videoSelection,Model=txtModel.Text,Repo=txtRepo.Text,Query=txtQuery.Text });
    private void ResetProcessing() { nudConfidence.Value=20;_nearDepth.Value=0;_farDepth.Value=100000;_components.Value=0;_smooth.Value=0;_holeArea.Value=0;nudMaxPoints.Value=150000; }
    private bool MeshCurrent => _result is not null && !_inputDirty && _meshParameters==Parameters() && _lastGlbPath is not null && File.Exists(_lastGlbPath);
    private void RefreshWorkflow()
    {
        if(!_workflowReady)return;
        bool busy=_runCts is not null, has=_cacheDirectory is not null;
        foreach(var group in _workflowGroups)group.Enabled=!busy;
        btnCancel.Enabled=busy;_resultChoice.Enabled=!busy;_viewChoice.Enabled=!busy;
        _quickRun.Enabled=!busy&&lstImages.Items.Count>0;btnRun.Enabled=_quickRun.Enabled;
        _apply.Enabled=!busy&&has&&!_inputDirty;
        _depthInspect.Enabled=!busy&&has&&!_inputDirty;
        bool applied=has&&_appliedParameters==PointParameters()&&!_inputDirty;
        _buildMesh.Enabled=!busy&&applied;_exportGlb.Enabled=_exportProject.Enabled=btnOpenGlb.Enabled=!busy&&MeshCurrent;
        _inputStatus.Text=$"共 {lstImages.Items.Count} 張；{(_videoSelection is null?"照片依清單順序":"影片保留時間順序")}";
        _parameterStatus.Text=!has?"請先完成快速或高品質推估。":_inputDirty?"輸入已變更，請重新推估。":applied?"目前參數已套用。":"有未套用的變更，請更新點雲。";
        _meshStatus.Text=MeshCurrent?"網格已更新，可預覽與匯出。":!applied?"請先套用點雲參數。":"尚未建立網格，或設定已變更。";
        string stats=_result is null?"":string.Join("　",_result.ProcessingStats.Select(p=>$"{(p.Key=="validPoints"?"有效點":p.Key=="repairedPoints"?"補點":"預覽點")} {p.Value:N0}"));
        _workflowStatus.Text=$"目前：{_quality}　{stats}\n{_parameterStatus.Text}　{_meshStatus.Text}";
        RefreshFileMenu();
    }
    private void InputChanged() { if(_cacheDirectory is not null)_inputDirty=_activeInputs!=InputSignature();RefreshWorkflow(); }
    private void MoveInput(int delta)
    {
        if(_videoSelection is not null) { MessageBox.Show(this,"影片影格保持時間順序。");return; }
        int i=lstImages.SelectedIndex,j=i+delta;if(i<0||j<0||j>=lstImages.Items.Count)return;
        var item=lstImages.Items[i];lstImages.Items.RemoveAt(i);lstImages.Items.Insert(j,item);lstImages.SelectedIndex=j;InputChanged();
    }
    private void SaveSnapshot()
    {
        if(_result is null||_resultDirectory is null||_cacheDirectory is null)return;
        var snapshot=new Snapshot(_result,_resultDirectory,_cacheDirectory,_appliedParameters,_meshParameters,_quality,_activeInputs);
        if(_quality=="快速結果")_quickSnapshot=snapshot;else _highSnapshot=snapshot;
        if(_runCts is not null)_resultChoice.SelectedIndex=_quality=="快速結果"?0:1;
    }
    private void SwitchResult()
    {
        if(_runCts is not null||_resultChoice.SelectedIndex<0)return;
        var snapshot=_resultChoice.SelectedIndex==0?_quickSnapshot:_highSnapshot;
        if(snapshot is null){_resultChoice.SelectedIndex=_quality=="快速結果"?0:_quality=="高品質結果"?1:-1;return;}
        _result=snapshot.Result;_cacheDirectory=snapshot.Cache;_quality=snapshot.Quality;
        _appliedParameters=snapshot.Applied;_meshParameters=snapshot.Mesh;
        _activeInputs=snapshot.Inputs;_inputDirty=_activeInputs!=InputSignature();
        LoadResult(snapshot.Result,snapshot.Output);RefreshWorkflow();ShowWorkflowView();
    }
    private async Task RunWorkflowAsync(bool quick)
    {
        if(_runCts is not null||lstImages.Items.Count==0)return;
        if(!Directory.Exists(txtRepo.Text)){MessageBox.Show(this,"請在執行環境指定 VGGT Repo。");return;}
        var images=lstImages.Items.Cast<string>().ToArray();var times=GetVideoTimes();
        if(quick&&images.Length>_quickCount.Value)
        {
            int count=(int)_quickCount.Value;
            var ids=Enumerable.Range(0,count).Select(i=>count==1?0:(int)Math.Round(i*(images.Length-1d)/(count-1))).ToArray();
            images=ids.Select(i=>images[i]).ToArray();if(times is not null)times=ids.Select(i=>times[i]).ToArray();
        }
        string output=Path.Combine(txtOutput.Text,DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+(quick?"_quick":"_high"));
        _runCts=new();RefreshWorkflow();
        try
        {
            ValidateSubjectCoverage(images);
            var options=new VggtRunOptions(txtPython.Text,txtRepo.Text,txtModel.Text,images,output,0,(int)nudMaxPoints.Value,(int)nudMaxFaces.Value,
                string.IsNullOrWhiteSpace(txtQuery.Text)?null:txtQuery.Text,true,true,false,times,_videoSelection?.VideoPath);
            var result=await _service.RunAsync(options,s=>BeginInvoke(()=>AppendLog(s)),(v,s)=>BeginInvoke(()=>{progress.Value=Math.Clamp(v,0,100);lblStatus.Text=s;}),_runCts.Token);
            _cacheDirectory=output;_quality=quick?"快速結果":"高品質結果";_result=result;
            _activeInputs=InputSignature();_inputDirty=false;_appliedParameters=null;_meshParameters=null;
            LoadResult(result,output);SaveSnapshot();
            lblStatus.Text="推估已快取，正在套用點雲參數。";
            await ProcessCurrentAsync(false,_runCts.Token);
        }
        catch(OperationCanceledException){lblStatus.Text="已取消；保留已完成結果。";}
        catch(Exception ex){AppendLog(ex.ToString());MessageBox.Show(this,ex.Message,"推估失敗");}
        finally{_runCts.Dispose();_runCts=null;RefreshWorkflow();if(_closeAfterWork)BeginInvoke(Close);}
    }
    private async Task ProcessCurrentAsync(bool mesh,CancellationToken token)
    {
        if(_nearDepth.Value>=_farDepth.Value)throw new InvalidOperationException("最遠深度必須大於最近深度。");
        ValidateSubjectCoverage(_result!.Cameras.Select(c=>c.Image));
        string settings=Parameters();
        string output=Path.Combine(_cacheDirectory!,"processed_"+Guid.NewGuid().ToString("N"));
        var result=await _service.ProcessCacheAsync(txtPython.Text,_cacheDirectory!,output,settings,mesh,
            s=>BeginInvoke(()=>AppendLog(s)),(v,s)=>BeginInvoke(()=>{progress.Value=Math.Clamp(v,0,100);lblStatus.Text=s;}),token);
        _result=result;_appliedParameters=PointParameters();_meshParameters=mesh?settings:null;
        LoadResult(result,output);SaveSnapshot();
        _viewChoice.SelectedIndex=mesh?3:0;ShowWorkflowView();
        lblStatus.Text=mesh?"網格已建立，可手動匯出。":"點雲已更新，未重新執行 AI。";
    }
    private async Task ProcessWorkflowAsync(bool mesh)
    {
        if(_runCts is not null||_cacheDirectory is null||_inputDirty)return;
        _runCts=new();RefreshWorkflow();
        try{await ProcessCurrentAsync(mesh,_runCts.Token);}
        catch(OperationCanceledException){lblStatus.Text="已取消；保留原結果。";}
        catch(Exception ex){AppendLog(ex.ToString());MessageBox.Show(this,ex.Message,"更新失敗");}
        finally{_runCts.Dispose();_runCts=null;RefreshWorkflow();if(_closeAfterWork)BeginInvoke(Close);}
    }
    private void ShowWorkflowView()
    {
        if(_cacheDirectory is null||_resultDirectory is null||_result is null)return;
        try
        {
            if(_viewChoice.SelectedIndex==3)
            {
                string mesh=Path.Combine(_resultDirectory,"mesh_preview.json");
                if(!File.Exists(mesh)){lblStatus.Text="請先建立網格預覽。";return;}
                viewport.SetSurface(mesh);return;
            }
            string path=_viewChoice.SelectedIndex==1?Path.Combine(_cacheDirectory,"scene.ply"):
                _viewChoice.SelectedIndex==2?Path.Combine(_resultDirectory,"repaired.ply"):Path.Combine(_resultDirectory,_result.PlyPath);
            if(!File.Exists(path)){lblStatus.Text="請先套用參數。";return;}
            viewport.SetPointCloud(PointCloud.LoadAsciiPly(path));
            if(_viewChoice.SelectedIndex==4)viewport.SetCameraPath(_result.Cameras.Select(c=>
            {
                var e=c.Extrinsic;return new System.Numerics.Vector3(
                    -(e[0][0]*e[0][3]+e[1][0]*e[1][3]+e[2][0]*e[2][3]),
                    e[0][1]*e[0][3]+e[1][1]*e[1][3]+e[2][1]*e[2][3],
                    -(e[0][2]*e[0][3]+e[1][2]*e[1][3]+e[2][2]*e[2][3]));
            }).ToArray());
        }
        catch(Exception ex){AppendLog(ex.Message);}
    }
    private void ExportGlb()
    {
        if(!MeshCurrent){MessageBox.Show(this,"請先更新網格。");return;}
        SaveResultFile(_lastGlbPath!,"GLB 模型|*.glb","scene.glb");
    }
    private void SaveSettings()
    {
        using var dialog=new SaveFileDialog { Filter="JSON 設定|*.json",FileName="processing-settings.json" };
        if(dialog.ShowDialog(this)==DialogResult.OK)try
        {
            File.WriteAllText(dialog.FileName,Parameters());
            lblStatus.Text="已輸出："+dialog.FileName;
            RevealOutput(dialog.FileName,true);
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message);}
    }
    private void EditEnvironment()
    {
        using var dialog=new Form { Text="執行環境與輸出位置",Size=new Size(760,370),StartPosition=FormStartPosition.CenterParent };
        var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,AutoScroll=true };
        var fields=new[]{("Python",txtPython),("VGGT Repo",txtRepo),("輸出資料夾",txtOutput),("模型",txtModel),("查詢點 CSV（選用）",txtQuery)};
        var edits=new List<TextBox>();
        foreach(var (caption,source) in fields){layout.Controls.Add(new Label{Text=caption,AutoSize=true});var edit=new TextBox{Text=source.Text,Width=700};edits.Add(edit);layout.Controls.Add(edit);}
        var ok=new Button{Text="套用",DialogResult=DialogResult.OK};layout.Controls.Add(ok);dialog.Controls.Add(layout);
        if(dialog.ShowDialog(this)==DialogResult.OK){for(int i=0;i<fields.Length;i++)fields[i].Item2.Text=edits[i].Text;InputChanged();}
    }
}
