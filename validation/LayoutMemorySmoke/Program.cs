using System.Reflection;
using System.Text.Json;
using VGGT.WinForms;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form=new MainForm{StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false};
        form.Show();Application.DoEvents();
        var type=typeof(MainForm);
        object Get(string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        object Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,args)!;
        var root=(SplitContainer)Get("rootSplit");var right=(SplitContainer)Get("rightSplit");
        var upper=(SplitContainer)Get("_leftUpperSplit");var lower=(SplitContainer)Get("_leftLowerSplit");
        var tabs=lower.Panel1.Controls.OfType<TabControl>().Single();
        root.SplitterDistance+=30;right.SplitterDistance-=20;upper.SplitterDistance+=20;lower.SplitterDistance-=15;tabs.SelectedIndex=2;
        Application.DoEvents();var saved=Call("CaptureWindowLayout");
        string json=JsonSerializer.Serialize(saved);
        string path=Path.Combine(Path.GetTempPath(),"vggt-layout-test-"+Guid.NewGuid().ToString("N")+".json");
        File.WriteAllText(path,json);
        try
        {
            root.SplitterDistance-=25;right.SplitterDistance+=10;upper.SplitterDistance-=10;lower.SplitterDistance+=10;tabs.SelectedIndex=0;
            var loaded=JsonSerializer.Deserialize(File.ReadAllText(path),saved.GetType())!;
            Call("ApplyWindowLayout",loaded,false);Application.DoEvents();
            if(JsonSerializer.Serialize(Call("CaptureWindowLayout"))!=json)throw new Exception("Splitter/tab layout did not round trip");
            var node=System.Text.Json.Nodes.JsonNode.Parse(json)!;
            node["LeftWidth"]=int.MaxValue;node["InputHeight"]=-500;node["HelpHeight"]=int.MaxValue;
            Call("ApplyWindowLayout",JsonSerializer.Deserialize(node.ToJsonString(),saved.GetType())!,false);
            if(upper.SplitterDistance<upper.Panel1MinSize||lower.SplitterDistance<lower.Panel1MinSize)throw new Exception("Minimum sizes not respected");
            Console.WriteLine("Serialized layout round trip restored four splitters and tabs; invalid positions safely clamped.");
        }
        finally{File.Delete(path);}
    }
}
