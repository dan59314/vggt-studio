using System.Text.Json;

namespace VGGT.WinForms;

public partial class MainForm
{
    private sealed record WindowLayout(int X,int Y,int Width,int Height,bool Maximized,int Dpi,
        int LeftWidth,int PreviewHeight,int InputHeight,int HelpHeight,int WorkflowTab,int ResultTab);
    private static string WindowLayoutPath=>Path.Combine(Path.GetDirectoryName(ImageListPath)!,"window-layout.json");
    private WindowLayout? _lastWindowLayout;
    private bool _layoutRestoring, _layoutLoaded;
    private TabControl WorkflowTabs=>_leftLowerSplit.Panel1.Controls.OfType<TabControl>().Single();

    private WindowLayout CaptureWindowLayout()
    {
        var bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
        return new(bounds.X,bounds.Y,bounds.Width,bounds.Height,WindowState==FormWindowState.Maximized,DeviceDpi,
            rootSplit.SplitterDistance,rightSplit.SplitterDistance,_leftUpperSplit.SplitterDistance,
            _leftLowerSplit.Panel2.Height,WorkflowTabs.SelectedIndex,resultTabs.SelectedIndex);
    }
    private void ApplyWindowLayout(WindowLayout layout,bool window)
    {
        _layoutRestoring=true;
        try
        {
            double scale=(double)DeviceDpi/Math.Max(96,layout.Dpi);
            int Scaled(int value)=>(int)Math.Clamp(value*scale,0,100000);
            if(window)
            {
                var wanted=new Rectangle(layout.X,layout.Y,Math.Max(MinimumSize.Width,Scaled(layout.Width)),Math.Max(MinimumSize.Height,Scaled(layout.Height)));
                var area=Screen.FromRectangle(wanted).WorkingArea;
                wanted.Width=Math.Max(MinimumSize.Width,Math.Min(wanted.Width,area.Width));
                wanted.Height=Math.Max(MinimumSize.Height,Math.Min(wanted.Height,area.Height));
                wanted.X=Math.Clamp(wanted.X,area.Left,Math.Max(area.Left,area.Right-wanted.Width));
                wanted.Y=Math.Clamp(wanted.Y,area.Top,Math.Max(area.Top,area.Bottom-wanted.Height));
                StartPosition=FormStartPosition.Manual;Bounds=wanted;
                WindowState=layout.Maximized?FormWindowState.Maximized:FormWindowState.Normal;
            }
            else
            {
                void Set(SplitContainer split,int distance)
                {
                    int total=split.Orientation==Orientation.Vertical?split.ClientSize.Width:split.ClientSize.Height;
                    int maximum=total-split.SplitterWidth-split.Panel2MinSize;
                    if(maximum>=split.Panel1MinSize)split.SplitterDistance=Math.Clamp(distance,split.Panel1MinSize,maximum);
                }
                Set(rootSplit,Scaled(layout.LeftWidth));
                Set(rightSplit,Scaled(layout.PreviewHeight));
                Set(_leftUpperSplit,Scaled(layout.InputHeight));
                Set(_leftLowerSplit,_leftLowerSplit.ClientSize.Height-_leftLowerSplit.SplitterWidth-Scaled(layout.HelpHeight));
                WorkflowTabs.SelectedIndex=Math.Clamp(layout.WorkflowTab,0,WorkflowTabs.TabCount-1);
                resultTabs.SelectedIndex=Math.Clamp(layout.ResultTab,0,resultTabs.TabCount-1);
            }
        }
        finally{_layoutRestoring=false;}
    }
    private void InitializeWindowLayout()
    {
        WindowLayout? saved=null;
        try{if(File.Exists(WindowLayoutPath))saved=JsonSerializer.Deserialize<WindowLayout>(File.ReadAllText(WindowLayoutPath));}
        catch(Exception ex){AppendLog($"無法讀取視窗配置，使用預設配置：{ex.Message}");}
        Load+=(_,_)=>{if(saved is not null)ApplyWindowLayout(saved,true);};
        Shown+=(_,_)=>{if(saved is not null)ApplyWindowLayout(saved,false);_layoutLoaded=true;Remember();};
        void Remember()
        {
            if(_layoutLoaded&&!_layoutRestoring&&WindowState!=FormWindowState.Minimized)
                _lastWindowLayout=CaptureWindowLayout();
        }
        ResizeEnd+=(_,_)=>Remember();Resize+=(_,_)=>Remember();
        foreach(var split in new[]{rootSplit,rightSplit,_leftUpperSplit,_leftLowerSplit})
            split.SplitterMoved+=(_,_)=>Remember();
        WorkflowTabs.SelectedIndexChanged+=(_,_)=>Remember();resultTabs.SelectedIndexChanged+=(_,_)=>Remember();
        FormClosed+=(_,_)=>
        {
            try
            {
                Remember();if(_lastWindowLayout is null)return;
                Directory.CreateDirectory(Path.GetDirectoryName(WindowLayoutPath)!);
                string temporary=WindowLayoutPath+"."+Guid.NewGuid().ToString("N")+".tmp";
                File.WriteAllText(temporary,JsonSerializer.Serialize(_lastWindowLayout));
                File.Move(temporary,WindowLayoutPath,true);
            }
            catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);}
        };
    }
}
