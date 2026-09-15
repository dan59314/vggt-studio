using System.Reflection;
using VGGT.WinForms;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var main=new MainForm();main.StartPosition=FormStartPosition.Manual;main.Location=new Point(-20000,-20000);main.ShowInTaskbar=false;main.Size=new Size(1500,1000);main.Show();Application.DoEvents();
        object Get(string name)=>typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        void Help(Control control)=>typeof(MainForm).GetMethod("ActivateHelp",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[control]);
        var help=(ListBox)Get("_contextHelp");var split=(SplitContainer)Get("rootSplit");
        var upper=(SplitContainer)Get("_leftUpperSplit"); var lower=(SplitContainer)Get("_leftLowerSplit"); var tabs=lower.Panel1.Controls.OfType<TabControl>().Single();
        if(tabs.TabPages.Count!=4)throw new Exception("Expected four workflow tabs");
        tabs.SelectedIndex=1; Application.DoEvents();
        var flow=tabs.SelectedTab!.Controls.OfType<Panel>().Single();
        int initialHelpHeight=help.Height;
        if(help.Parent!=lower.Panel2||help.Height<80)throw new Exception("Help is not a separate 120px bottom pane");
        Help((Control)Get("_buildMesh"));
        if(!string.Join(" ",help.Items.Cast<object>()).Contains("請先"))throw new Exception("Disabled button help missing");
        var confidence=(NumericUpDown)Get("nudConfidence");Help(confidence);confidence.Value=35;
        typeof(MainForm).GetMethod("RefreshHelp",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[false]);
        if(!string.Join(" ",help.Items.Cast<object>()).Contains("35%"))throw new Exception("Current value missing");
        int top=help.Top;flow.AutoScrollPosition=new Point(0,700);Application.DoEvents();
        if(flow.AutoScrollPosition.Y==0)throw new Exception("Parameter tab does not scroll");
        int scrollY=flow.AutoScrollPosition.Y; tabs.SelectedIndex=2; Application.DoEvents(); tabs.SelectedIndex=1; Application.DoEvents();
        if(flow.AutoScrollPosition.Y!=scrollY||confidence.Value!=35)throw new Exception("Tab state was lost");
        if(Get("_runCts") is not null)throw new Exception("Switching tabs started computation");
        if(help.Top!=top||help.Height!=initialHelpHeight)throw new Exception("Help moved with scroll");
        upper.SplitterDistance+=30; lower.SplitterDistance-=25; Application.DoEvents();
        if(help.Height!=initialHelpHeight+25)throw new Exception("Help resizing failed");
        if(((Control)Get("lstImages")).Height<40)throw new Exception("Image list collapsed");
        flow.AutoScrollPosition=Point.Empty; Application.DoEvents();
        Help(confidence);
        if(flow.HorizontalScroll.Visible)throw new Exception("Unnecessary horizontal scrollbar");
        using var bitmap=new Bitmap(main.Width,main.Height);main.DrawToBitmap(bitmap,new Rectangle(Point.Empty,main.Size));bitmap.Save("validation/ContextHelpSmoke/layout.png");
        Console.WriteLine("Three panes, splitter resizing, independent scrolling, current values and disabled-control help verified.");
    }
}





