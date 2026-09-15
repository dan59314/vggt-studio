using System.Text.Json;

namespace VGGT.WinForms;

public partial class MainForm
{
    private readonly CheckBox _subjectOnly=new(){Text="只保留選取主體",AutoSize=true};
    private Dictionary<string,string> _subjectMasks=new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string,string>? ActiveSubjectMasks=>_subjectOnly.Checked?_subjectMasks:null;
    private static string SubjectSettingsPath=>Path.Combine(Path.GetDirectoryName(ImageListPath)!,"subject-settings.json");
    private sealed record SubjectSettings(bool Enabled,Dictionary<string,string> Masks);
    private void InitializeSubjectSelection(FlowLayoutPanel input)
    {
        var row=new FlowLayoutPanel{AutoSize=true,Width=405};
        var edit=new Button{Text="主體選取／遮罩…",AutoSize=true};
        row.Controls.Add(_subjectOnly);row.Controls.Add(edit);input.Controls.Add(row);
        _subjectOnly.CheckedChanged+=(_,_)=>RefreshWorkflow();
        edit.Click+=(_,_)=>
        {
            if(lstImages.Items.Count==0){MessageBox.Show(this,"請先加入影像或影片抽幀。");return;}
            using var dialog=new SubjectMaskForm(txtPython.Text,lstImages.Items.Cast<string>().ToArray(),_subjectMasks);
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            _subjectMasks=dialog.Masks;_subjectOnly.Checked=true;RefreshWorkflow();
            lblStatus.Text="主體遮罩已更新；請在③套用並更新點雲，或先執行快速推估。";
        };
        try
        {
            if(File.Exists(SubjectSettingsPath)&&JsonSerializer.Deserialize<SubjectSettings>(File.ReadAllText(SubjectSettingsPath)) is {} saved)
            {_subjectMasks=new(saved.Masks,StringComparer.OrdinalIgnoreCase);_subjectOnly.Checked=saved.Enabled;}
        }
        catch(Exception ex){AppendLog($"無法讀取主體設定：{ex.Message}");}
        FormClosed+=(_,_)=>
        {
            try{Directory.CreateDirectory(Path.GetDirectoryName(SubjectSettingsPath)!);File.WriteAllText(SubjectSettingsPath,JsonSerializer.Serialize(new SubjectSettings(_subjectOnly.Checked,_subjectMasks)));}
            catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);}
        };
        _controlHelp[_subjectOnly]=()=>["【只保留選取主體】",_subjectOnly.Checked?"目前：啟用":"目前：停用（完整場景）",
            "先在「主體選取／遮罩」框選、追蹤並逐張確認。相機仍使用完整影像推估；遮罩限制點雲、補洞及 GLB 幾何。",
            "切換後在③套用點雲，再於⑤重建網格。原始預測點雲仍包含背景，請選「處理後點雲」檢查。"];
        _controlHelp[edit]=()=>["【主體選取／遮罩】","自動找物件可多選合併；亦可用新增／消除筆刷、多邊形、縮放平移、復原重做與提示重新分割。",
            "綠色為保留區；視角差異大或細鋼索可能失準，需手動修正。可由較晚影格再追蹤，既有遮罩不被覆寫。"];
    }
    private void ValidateSubjectCoverage(IEnumerable<string> images)
    {
        if(!_subjectOnly.Checked)return;
        var missing=images.FirstOrDefault(p=>!_subjectMasks.TryGetValue(p,out var mask)||!File.Exists(mask));
        if(missing is not null)throw new InvalidOperationException($"缺少已確認的主體遮罩：{Path.GetFileName(missing)}。請先開啟主體選取，或取消「只保留選取主體」。");
    }
}
