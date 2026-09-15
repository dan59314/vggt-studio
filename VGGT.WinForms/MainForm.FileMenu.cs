using System.Text.Json.Nodes;

namespace VGGT.WinForms;

public partial class MainForm
{
    private ToolStripMenuItem? _fileOutput;
    private readonly List<ToolStripMenuItem> _meshExports=[];
    private void InitializeFileMenu()
    {
        var menu=new MenuStrip();var file=new ToolStripMenuItem("檔案");
        var input=new ToolStripMenuItem("輸入");
        input.DropDownItems.Add("照片…",null,btnAddImages_Click);
        input.DropDownItems.Add("資料夾…",null,btnAddFolder_Click);
        input.DropDownItems.Add("影片抽幀與預覽…",null,(_,_)=>ImportVideo());
        _fileOutput=new ToolStripMenuItem("輸出");
        void Item(string text,Action action,bool mesh=false)
        {
            var item=new ToolStripMenuItem(text,null,(_,_)=>
            {
                if(_runCts is not null)return;
                try { action(); } catch(Exception ex) { MessageBox.Show(this,ex.Message,"輸出失敗",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            });
            _fileOutput.DropDownItems.Add(item);if(mesh)_meshExports.Add(item);
        }
        Item("GLB 模型…",ExportGlb,true);
        Item("3D Viewer 專案與資源（.rv3dproj）…",ExportViewerProject,true);
        Item("CameraAnimation 路徑…",()=>SaveResultFile(_result!.CameraAnimationPath!,"CameraAnimation|*.camera-animation.json","scene.camera-animation.json"));
        Item("貼圖 PNG（命名資料夾）…",()=>ExportImageFolder(true),true);
        Item("模型影像與深度圖（命名資料夾）…",()=>ExportImageFolder(false));
        Item("點雲 PLY…",()=>SaveResultFile(_result!.PlyPath,"PLY 點雲|*.ply","scene.ply"));
        Item("處理參數 JSON…",SaveSettings);
        file.DropDownItems.Add(input);file.DropDownItems.Add(_fileOutput);
        menu.Items.Add(file);MainMenuStrip=menu;Controls.Add(menu);rootSplit.BringToFront();
        RefreshFileMenu();
    }
    private void RefreshFileMenu()
    {
        if(_fileOutput is null)return;
        bool current=_result is not null && !_inputDirty && _runCts is null && _appliedParameters==PointParameters();
        _fileOutput.Enabled=current;
        foreach(var item in _meshExports)item.Enabled=current&&MeshCurrent;
        _fileOutput.ToolTipText=current?"選擇格式後指定路徑與檔名":"請完成推估並套用目前參數";
        if(MainMenuStrip is not null)MainMenuStrip.Enabled=_runCts is null;
    }
    private void SaveResultFile(string relative,string filter,string name)
    {
        using var dialog=new SaveFileDialog{Filter=filter,FileName=name};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string source=Path.Combine(_resultDirectory!,relative);
        if(!string.Equals(Path.GetFullPath(source),Path.GetFullPath(dialog.FileName),StringComparison.OrdinalIgnoreCase))
            File.Copy(source,dialog.FileName,true);
        lblStatus.Text="已輸出："+dialog.FileName;
        RevealOutput(dialog.FileName, true);
    }
    private string? ChooseNamedFolder(string name)
    {
        using var dialog=new SaveFileDialog { Title="選擇輸出位置並輸入新資料夾名稱",Filter="資料夾名稱|*.*",FileName=name,AddExtension=false,OverwritePrompt=false };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return null;
        if(Directory.Exists(dialog.FileName)||File.Exists(dialog.FileName))throw new IOException("此名稱已存在，請輸入新的資料夾名稱。");
        return dialog.FileName;
    }
    private void ExportImageFolder(bool textures)
    {
        string? target=ChooseNamedFolder(textures?"scene_textures":"scene_images_depth");if(target is null)return;
        string staging=target+".tmp-"+Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(staging);
            if(textures)
                foreach(var source in Directory.GetFiles(Path.Combine(_resultDirectory!,"textures"),"*.png"))File.Copy(source,Path.Combine(staging,Path.GetFileName(source)));
            else
                foreach(var pair in _result!.ImageDepthPairs)
                    foreach(var relative in new[]{pair.ImagePath,pair.DepthPath})File.Copy(Path.Combine(_resultDirectory!,relative),Path.Combine(staging,Path.GetFileName(relative)));
            Directory.Move(staging,target);lblStatus.Text="已輸出："+target;
            RevealOutput(target, false);
        }
        finally { CleanupExportStaging(staging); }
    }
    private void ExportViewerProject()
    {
        if(!MeshCurrent)return;
        using var dialog=new SaveFileDialog { Filter="Rv3d Viewer 專案|*.rv3dproj",DefaultExt="rv3dproj",FileName="scene.rv3dproj" };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string assets=WriteViewerPackage(dialog.FileName);
        lblStatus.Text="已輸出："+dialog.FileName;
        AppendLog($"模型與 Camera 路徑：{assets}；搬移專案時請一併保留此資料夾。");
        RevealOutput(dialog.FileName, true);
    }
    private string WriteViewerPackage(string targetPath)
    {
        string folder=Path.GetDirectoryName(targetPath)!;
        string name=Path.GetFileNameWithoutExtension(targetPath);
        string assets=Path.Combine(folder,name+".assets");
        if(Directory.Exists(assets)||File.Exists(assets))assets+= "-"+Guid.NewGuid().ToString("N")[..8];
        string staging=assets+".tmp-"+Guid.NewGuid().ToString("N");
        string temporaryProject=targetPath+".tmp-"+Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(staging);
            File.Copy(_lastGlbPath!,Path.Combine(staging,"model.glb"));
            File.Copy(Path.Combine(_resultDirectory!,_result!.CameraAnimationPath!),Path.Combine(staging,"camera.camera-animation.json"));
            File.WriteAllText(Path.Combine(staging,"processing-settings.json"),Parameters());
            var project=JsonNode.Parse(File.ReadAllText(Path.Combine(_resultDirectory!,_result.ViewerProjectPath!)))!;
            project["name"]=name;
            project["models"]![0]!["assetPath"]=Path.GetRelativePath(folder,Path.Combine(assets,"model.glb"));
            File.WriteAllText(temporaryProject,project.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            Directory.Move(staging,assets);
            File.Move(temporaryProject,targetPath,true);
            return assets;
        }
        finally { CleanupExportStaging(staging);if(File.Exists(temporaryProject))File.Delete(temporaryProject); }
    }
    private static void CleanupExportStaging(string staging)
    {
        // Only this operation's freshly allocated temporary directory is removed.
        if(Directory.Exists(staging))try{Directory.Delete(staging,true);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    private void RevealOutput(string path, bool selectFile)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = selectFile ? $"/select,\"{fullPath}\"" : $"\"{fullPath}\""
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"輸出已完成，但無法開啟檔案總管：{ex.Message}\n\n{path}",
                "開啟輸出位置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
