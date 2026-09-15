using System.Diagnostics;

namespace VGGT.WinForms;

public partial class MainForm
{
    private readonly Button _depthInspect=new(){Text="深度分布與範圍預覽…",AutoSize=true};
    private async Task InspectDepthAsync()
    {
        if(_runCts is not null||_cacheDirectory is null||_inputDirty)return;
        _runCts=new();RefreshWorkflow();
        string temporary=Path.Combine(Path.GetTempPath(),"VGGT-depth-"+Guid.NewGuid().ToString("N"));
        try
        {
            ValidateSubjectCoverage(_result!.Cameras.Select(c=>c.Image));
            Directory.CreateDirectory(temporary);
            string settings=Path.Combine(temporary,"settings.json"),output=Path.Combine(temporary,"depth.json");
            await File.WriteAllTextAsync(settings,Parameters());
            var psi=new ProcessStartInfo(txtPython.Text){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
            psi.Environment["PYTHONUTF8"]="1";
            foreach(var arg in new[]{Path.Combine(AppContext.BaseDirectory,"python","depth_inspection.py"),"--cache",_cacheDirectory,"--settings",settings,"--output",output})psi.ArgumentList.Add(arg);
            using var process=Process.Start(psi)??throw new InvalidOperationException("無法讀取深度快取。");
            var errors=process.StandardError.ReadToEndAsync();var stdout=process.StandardOutput.ReadToEndAsync();
            using var registration=_runCts.Token.Register(()=>{try{process.Kill(true);}catch{}});
            lblStatus.Text="正在讀取深度分布（不重新執行 AI）…";
            await process.WaitForExitAsync();string error=await errors;await stdout;_runCts.Token.ThrowIfCancellationRequested();
            if(process.ExitCode!=0)throw new InvalidOperationException(error);
            using var dialog=new DepthRangeForm(output,_nearDepth.Value,_farDepth.Value);
            if(dialog.ShowDialog(this)==DialogResult.OK)
            {
                _nearDepth.Value=dialog.NearDepth;_farDepth.Value=dialog.FarDepth;
                lblStatus.Text="深度範圍已帶回，請按「套用並更新點雲」。";
            }
            else lblStatus.Text="已取消深度預覽，參數保持不變。";
        }
        catch(OperationCanceledException){lblStatus.Text="已取消深度預覽。";}
        catch(Exception ex){AppendLog(ex.ToString());MessageBox.Show(this,ex.Message,"深度預覽");}
        finally
        {
            _runCts.Dispose();_runCts=null;RefreshWorkflow();if(_closeAfterWork)BeginInvoke(Close);
            try
            {
                foreach(string name in new[]{"settings.json","depth.json"}){string path=Path.Combine(temporary,name);if(File.Exists(path))File.Delete(path);}
                if(Directory.Exists(temporary))Directory.Delete(temporary);
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);}
        }
    }
}
