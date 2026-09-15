using VGGT.WinForms;
using VGGT.WinForms.Services;
using System.Reflection;
using System.Text.Json;
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var main=new MainForm();
        main.Size=new Size(1550,1100);main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.ShowInTaskbar=false;main.Show();Application.DoEvents();
        object? Get(string name)=>typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main);
        void Set(string name,object? value)=>typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,value);
        object? Call(string name,params object[] args)=>typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,args);
        if(args.Length>0)
        {
            using var timer=new System.Windows.Forms.Timer {Interval=500};timer.Tick+=(_,_)=>File.WriteAllText("validation/WorkflowSmoke/live.log",((TextBox)Get("txtLog")!).Text);timer.Start();
            var list=(ListBox)Get("lstImages")!;list.Items.Clear();
            foreach(var path in File.ReadAllLines("validation/geometry-cleanup/input_images.txt"))list.Items.Add(path.Trim());
            ((TextBox)Get("txtOutput")!).Text=Path.GetFullPath("validation/workflow-ai");
            ((NumericUpDown)Get("_quickCount")!).Value=2;
            ((NumericUpDown)Get("nudMaxPoints")!).Value=30000;
            ((NumericUpDown)Get("nudMaxFaces")!).Value=50000;
            void Wait(Task task){while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(30);}task.GetAwaiter().GetResult();}
            Wait((Task)Call("RunWorkflowAsync",true)!);
            string cache=(string)Get("_cacheDirectory")!;
            if(cache is null||File.Exists(Path.Combine(cache,"scene_mesh.glb")))throw new Exception("Quick inference did not defer GLB");
            if(Get("_appliedParameters") is null)throw new Exception("Quick postprocessing failed");
            Wait((Task)Call("ProcessWorkflowAsync",true)!);
            if(Get("_lastGlbPath") is not string glb||!File.Exists(glb))throw new Exception("Manual mesh failed");
            if(!((ToolStripMenuItem)Get("_fileOutput")!).Enabled)throw new Exception("Exports not ready");
            Console.WriteLine("Actual UI pipeline passed: quick GPU inference, cached processing, deferred GLB, manual mesh and ready exports.");
            return;
        }
        string root=Path.GetFullPath("validation/workflow-processed");
        var result=JsonSerializer.Deserialize<VggtResult>(File.ReadAllText(Path.Combine(root,"result.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        foreach(var (name,value) in new[]{("nudConfidence",5),("nudMaxPoints",50000),("nudMaxFaces",100000),("_textureSize",1024),("_components",4),("_smooth",1),("_holeArea",64)}) ((NumericUpDown)Get(name)!).Value=value;
        Set("_result",result);Set("_cacheDirectory",Path.GetFullPath("validation/livingroom-photo-mesh"));Set("_quality","高品質結果");
        Set("_activeInputs",Call("InputSignature"));Set("_appliedParameters",Call("PointParameters"));Set("_meshParameters",Call("Parameters"));
        Call("LoadResult",result,root);Call("RefreshWorkflow");
        Directory.CreateDirectory("validation/renamed-export");Call("WriteViewerPackage",Path.GetFullPath("validation/renamed-export/客廳.rv3dproj"));
        if(!((ToolStripMenuItem)Get("_fileOutput")!).Enabled)throw new Exception("Current export disabled");
        ((ComboBox)Get("_viewChoice")!).SelectedIndex=3;
        Application.DoEvents();
        using var bitmap=new Bitmap(main.Width,main.Height);main.DrawToBitmap(bitmap,main.ClientRectangle);bitmap.Save("validation/WorkflowSmoke/layout.png");
        ((NumericUpDown)Get("_textureSize")!).Value=2048;
        if(((Button)Get("_exportGlb")!).Enabled)throw new Exception("Stale mesh export enabled");
        if(!((ToolStripMenuItem)Get("_fileOutput")!).Enabled)throw new Exception("Mesh-only edit invalidated point exports");
        ((NumericUpDown)Get("nudConfidence")!).Value=6;
        if(((ToolStripMenuItem)Get("_fileOutput")!).Enabled)throw new Exception("Unapplied exports enabled");
        Console.WriteLine("Loaded real mesh; geometry rendered; stale-point and stale-mesh export guards passed.");
    }
}
