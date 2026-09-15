using System.Diagnostics;
using System.Text.Json;
using VGGT.WinForms.Controls;

namespace VGGT.WinForms;

public sealed class SubjectMaskForm : Form
{
    private readonly string[] _images;
    private readonly string _python, _directory;
    private readonly SubjectCanvas _canvas=new();
    private readonly ListBox _frames=new() { Dock=DockStyle.Left,Width=270 };
    private readonly Label _status=new() { Dock=DockStyle.Bottom,Height=55 };
    private readonly FlowLayoutPanel _tools=new() { Dock=DockStyle.Top,AutoSize=true,WrapContents=true };
    private readonly bool[] _reviewed;
    private CancellationTokenSource? _work;
    private bool _refreshing;
    private readonly Dictionary<int,string> _automaticCandidates=[];
    public Dictionary<string,string> Masks { get; private set; }=new(StringComparer.OrdinalIgnoreCase);
    private string MaskPath(int i)=>Path.Combine(_directory,$"{i:0000}.png");
    private string HintPath(int i)=>Path.Combine(_directory,$"{i:0000}-hints.png");
    public SubjectMaskForm(string python,string[] images,Dictionary<string,string> existing)
    {
        Text="主體選取／遮罩預覽";Width=1250;Height=850;MinimumSize=new Size(900,650);
        _python=python;_images=images;_reviewed=new bool[images.Length];
        _directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VGGT Studio","subject-masks",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        for(int i=0;i<images.Length;i++)if(existing.TryGetValue(images[i],out var mask)&&File.Exists(mask))
        {File.Copy(mask,MaskPath(i));_reviewed[i]=true;}
        void Button(string text,Action action){var b=new Button{Text=text,AutoSize=true};b.Click+=(_,_)=>action();_tools.Controls.Add(b);}
        Button("自動找物件…",()=>_ = FindObjectsAsync());
        string[] toolNames=["框選主體","＋新增遮罩","－消除遮罩","多邊形新增","多邊形消除","保留提示","排除提示","平移"];
        for(int n=0;n<toolNames.Length;n++)
        {
            int index=n;var tool=new RadioButton{Text=toolNames[n],Appearance=Appearance.Button,AutoSize=true,Checked=n==0};
            tool.CheckedChanged+=(_,_)=>{if(tool.Checked){_canvas.Tool=index;_status.Text=index is 5 or 6?
                "提示模式：青色必須保留、紅色必須排除；畫完後按「依提示重新分割」。":
                index is 3 or 4?"多邊形：逐點點選，雙擊或 Enter 完成，Esc 取消。":"直接編輯遮罩；滾輪縮放，中鍵拖曳平移；Ctrl+Z 復原。";}};
            _tools.Controls.Add(tool);
        }
        _tools.SetFlowBreak(_tools.Controls[^1],true);
        var size=new NumericUpDown{Minimum=1,Maximum=100,Value=16,Width=65};
        size.ValueChanged+=(_,_)=>_canvas.BrushSize=(int)size.Value;
        _tools.Controls.Add(new Label{Text="筆刷像素",AutoSize=true});_tools.Controls.Add(size);
        Button("復原 Ctrl+Z",_canvas.Undo);Button("重做 Ctrl+Y",_canvas.Redo);Button("重設縮放",_canvas.ResetView);
        Button("完成多邊形",_canvas.FinishPolygon);
        var opacity=new NumericUpDown{Minimum=0,Maximum=100,Value=40,Width=65};
        opacity.ValueChanged+=(_,_)=>{_canvas.Opacity=(int)opacity.Value;_canvas.Invalidate();};
        _tools.Controls.Add(new Label{Text="遮罩不透明度 %",AutoSize=true});_tools.Controls.Add(opacity);
        _tools.SetFlowBreak(opacity,true);
        Button("框選後分割",()=>_ = GenerateAsync(false));
        Button("依提示重新分割",()=>_ = RefineAsync());
        Button("清除提示",_canvas.ClearHints);
        Button("追蹤至後續影格",()=>_ = GenerateAsync(true));
        Button("匯入本張遮罩",ImportMask);
        Button("清除本張遮罩",()=>
        {
            _canvas.ClearMask();
            _status.Text="已清除本張工作副本，可重新框選、繪製，或由較早影格追蹤。尚未套用到主畫面。";
        });
        Button("確認本張",()=>{if(!_canvas.HasMask||_canvas.HasHints){_status.Text="請先建立遮罩；尚有提示時，請重新分割或清除提示再確認。";return;}_reviewed[_frames.SelectedIndex]=true;RefreshFrames();});
        var view=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=125};
        view.Items.AddRange(["原始影像","綠色保留區","黑白遮罩"]);view.SelectedIndex=1;_canvas.View=1;
        view.SelectedIndexChanged+=(_,_)=>{_canvas.View=view.SelectedIndex;_canvas.Invalidate();};_tools.Controls.Add(view);
        Button("套用全部遮罩",()=>
        {
            if(_reviewed.Any(x=>!x)){_status.Text="請逐張檢查並按「確認本張」，再套用全部遮罩。";return;}
            Masks=images.Select((p,i)=>(p,i)).ToDictionary(x=>x.p,x=>MaskPath(x.i),StringComparer.OrdinalIgnoreCase);
            DialogResult=DialogResult.OK;Close();
        });
        _tools.SetFlowBreak(_tools.Controls[^1],true);
        var example=new Label
        {
            AutoSize=true, BackColor=Color.LightYellow, Padding=new Padding(5),
            Text="提示範例：保留橋塔、橋面與鋼索；排除天空、山與海面。\n操作：先框選分割 → 在背景畫紅色排除提示、橋梁畫青色保留提示 → 依提示重新分割。這裡使用塗畫提示，沒有文字指令輸入框。"
        };
        _tools.Controls.Add(example);
        _tools.SetFlowBreak(example,true);
        _tools.SizeChanged+=(_,_)=>example.MaximumSize=new Size(Math.Max(200,_tools.ClientSize.Width-16),0);
        Button("範例：畫排除提示",()=>
        {
            _tools.Controls.OfType<RadioButton>().Single(b=>b.Text=="排除提示").Checked=true;
            _status.Text="請直接在影像的天空、山與海面各塗幾筆紅色線，避開橋梁與鋼索；再按「依提示重新分割」。此按鈕只選取畫筆，不會自動辨識背景。";
            _canvas.Focus();
        });
        Button("範例：畫保留提示",()=>
        {
            _tools.Controls.OfType<RadioButton>().Single(b=>b.Text=="保留提示").Checked=true;
            _status.Text="請在橋塔與橋面內部塗幾筆青色線；細鋼索可縮小筆刷。提示畫完後按「依提示重新分割」。";
            _canvas.Focus();
        });
        var cancel=new Button{Text="取消目前處理",Dock=DockStyle.Bottom,Height=30};cancel.Click+=(_,_)=>_work?.Cancel();
        Controls.Add(_canvas);Controls.Add(_frames);Controls.Add(_tools);Controls.Add(_status);Controls.Add(cancel);
        _frames.SelectedIndexChanged+=(_,_)=>{if(!_refreshing&&_frames.SelectedIndex>=0)LoadFrame();};
        _canvas.Edited+=()=>
        {
            int i=_frames.SelectedIndex;
            if(_canvas.HasMask)_canvas.SaveMask(MaskPath(i));else if(File.Exists(MaskPath(i)))File.Delete(MaskPath(i));
            if(_canvas.HasHints)_canvas.SaveHints(HintPath(i));else if(File.Exists(HintPath(i)))File.Delete(HintPath(i));
            _reviewed[i]=false;RefreshFrames();
        };
        KeyPreview=true;
        KeyDown+=(_,e)=>
        {
            if(_work is not null)return;
            if(e.Control&&e.KeyCode==Keys.Z){_canvas.Undo();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.Y){_canvas.Redo();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.Enter&&_canvas.Focused){_canvas.FinishPolygon();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.Escape){_canvas.CancelPolygon();e.SuppressKeyPress=true;}
        };
        _status.Text="新增／消除直接修改遮罩；保留／排除提示需再按重新分割。滾輪縮放、中鍵平移。復原限本張最近20步，切換影格會清除復原歷史。";
        RefreshFrames();_frames.SelectedIndex=0;
        FormClosing+=(_,e)=>{if(_work is not null){e.Cancel=true;_work.Cancel();_status.Text="正在取消，完成後可關閉。";}};
    }
    private void RefreshFrames()
    {
        int index=_frames.SelectedIndex;_refreshing=true;_frames.BeginUpdate();_frames.Items.Clear();
        for(int i=0;i<_images.Length;i++)_frames.Items.Add($"{i+1:00} {(_reviewed[i]?"✓ 已確認":File.Exists(MaskPath(i))?"待確認":"未選取")} {Path.GetFileName(_images[i])}");
        _frames.SelectedIndex=index;_frames.EndUpdate();_refreshing=false;
    }
    private void LoadFrame(){int i=_frames.SelectedIndex;_canvas.LoadFrame(_images[i],File.Exists(MaskPath(i))?MaskPath(i):null,File.Exists(HintPath(i))?HintPath(i):null);}
    private void ImportMask()
    {
        using var dialog=new OpenFileDialog{Filter="黑白遮罩|*.png;*.bmp"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        try
        {
            using var image=Image.FromFile(_images[_frames.SelectedIndex]);using var mask=Image.FromFile(dialog.FileName);
            if(Math.Abs((double)image.Width/image.Height-(double)mask.Width/mask.Height)>.02)throw new InvalidOperationException("遮罩比例必須與原始影像一致；白色保留、黑色排除。");
            _canvas.ReplaceMask(dialog.FileName,true);
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,"匯入遮罩失敗");}
    }
    private async Task FindObjectsAsync()
    {
        if(_work is not null||_frames.SelectedIndex<0)return;
        int frame=_frames.SelectedIndex;
        _work=new();_tools.Enabled=_frames.Enabled=_canvas.Enabled=false;
        try
        {
            if(!_automaticCandidates.TryGetValue(frame,out var directory))
            {
                directory=Path.Combine(_directory,"objects-"+Guid.NewGuid().ToString("N"));
                var psi=new ProcessStartInfo(_python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
                psi.Environment["PYTHONUTF8"]="1";
                foreach(var arg in new[]{Path.Combine(AppContext.BaseDirectory,"python","automatic_objects.py"),"--image",_images[frame],"--output",directory})psi.ArgumentList.Add(arg);
                _status.Text="正在自動找物件；首次需下載約360 MB模型，影像不會上傳。可按下方取消目前處理。";
                using var process=Process.Start(psi)??throw new InvalidOperationException("無法啟動自動分割。");
                var errors=process.StandardError.ReadToEndAsync();
                async Task ReadProgress(){while(await process.StandardOutput.ReadLineAsync() is {} line)_status.Text=line;}
                var progress=ReadProgress();
                using var registration=_work.Token.Register(()=>{try{process.Kill(true);}catch{}});
                await process.WaitForExitAsync();await progress;string error=await errors;_work.Token.ThrowIfCancellationRequested();
                if(process.ExitCode!=0)throw new InvalidOperationException(error);
                _automaticCandidates[frame]=directory;
            }
            using var candidates=new AutomaticObjectsForm(directory);
            if(candidates.ShowDialog(this)==DialogResult.OK&&candidates.SelectedMaskPath is {} mask)
            {
                _canvas.ReplaceMask(mask,true);
                _status.Text="已合併選取物件。可用筆刷修正；請確認本張後追蹤其他影格，再套用全部遮罩。此次合併可復原。";
            }
            else _status.Text="未套用候選物件，原有遮罩保持不變。";
        }
        catch(OperationCanceledException){_status.Text="已取消自動找物件，原有遮罩保持不變。";}
        catch(Exception ex){_status.Text="自動找物件失敗，仍可使用框選或筆刷。";MessageBox.Show(this,ex.Message,"自動找物件");}
        finally{_work.Dispose();_work=null;_tools.Enabled=_frames.Enabled=_canvas.Enabled=true;}
    }
    private Task GenerateAsync(bool track)=>RunSegmentationAsync(track,false);
    private Task RefineAsync()=>RunSegmentationAsync(false,true);
    private async Task RunSegmentationAsync(bool track,bool refine)
    {
        if(_work is not null)return;
        int i=_frames.SelectedIndex;
        if(track&&(!_canvas.HasMask||_canvas.HasHints)){_status.Text="請先建立遮罩並套用或清除提示，再追蹤後續影格。";return;}
        if(refine&&(!_canvas.HasMask||!_canvas.HasHints)){_status.Text="請先建立初步遮罩，再畫保留／排除提示。";return;}
        var r=_canvas.Selection;
        if(!track&&!refine&&(r.Width<.005||r.Height<.005)){_status.Text="請使用「框選主體」拖曳出矩形。";return;}
        _work=new();_tools.Enabled=_frames.Enabled=_canvas.Enabled=false;
        string generated=Path.Combine(_directory,Guid.NewGuid().ToString("N"));
        try
        {
            string request=Path.Combine(_directory,"request.json");
            await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new {action=track?"track":refine?"refine":"segment",images=_images,start=i,
                rectangle=new[]{r.X,r.Y,r.Width,r.Height},mask=MaskPath(i),output=generated,
                hints=HintPath(i),
                anchors=Enumerable.Range(0,_images.Length).Where(j=>File.Exists(MaskPath(j))).ToDictionary(j=>j.ToString(),MaskPath)}));
            var psi=new ProcessStartInfo(_python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
            psi.Environment["PYTHONUTF8"]="1";
            foreach(var arg in new[]{Path.Combine(AppContext.BaseDirectory,"python","subject_masks.py"),"--request",request})psi.ArgumentList.Add(arg);
            using var process=Process.Start(psi)??throw new InvalidOperationException("無法啟動遮罩工具。");
            var error=process.StandardError.ReadToEndAsync();var output=process.StandardOutput.ReadToEndAsync();
            using var registration=_work.Token.Register(()=>{try{process.Kill(true);}catch{}});
            _status.Text="正在處理，請稍候；可按下方「取消目前處理」。";
            await process.WaitForExitAsync();string errors=await error;await output;_work.Token.ThrowIfCancellationRequested();
            if(process.ExitCode!=0)throw new InvalidOperationException(errors);
            for(int j=track?i+1:i;j<(track?_images.Length:i+1);j++)
            {
                // Reviewed/manual keyframes are never overwritten by propagation.
                if(track&&File.Exists(MaskPath(j)))continue;
                if(!track)_canvas.ReplaceMask(Path.Combine(generated,$"{j:0000}.png"),true);
                else {File.Copy(Path.Combine(generated,$"{j:0000}.png"),MaskPath(j),true);_reviewed[j]=false;}
            }
            RefreshFrames();_status.Text="完成。綠色為保留區；請逐張修正並確認。本張重新分割可復原；追蹤不覆寫既有遮罩。";
        }
        catch(OperationCanceledException){_status.Text="已取消，原有遮罩保持不變。";}
        catch(Exception ex){_status.Text="遮罩處理失敗。";MessageBox.Show(this,ex.Message,"主體選取");}
        finally{_work.Dispose();_work=null;_tools.Enabled=_frames.Enabled=_canvas.Enabled=true;}
    }
}
