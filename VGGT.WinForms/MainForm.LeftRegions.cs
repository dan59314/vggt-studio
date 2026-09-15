using System.Text.Json;

namespace VGGT.WinForms;

public partial class MainForm
{
    private SplitContainer _leftUpperSplit = null!;
    private SplitContainer _leftLowerSplit = null!;
    private static string LeftLayoutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VGGT Studio", "left-layout.json");
    private sealed record LeftLayout(int InputHeight, int HelpHeight);

    private void InitializeLeftRegions()
    {
        var scrolling = rootSplit.Panel1.Controls.OfType<FlowLayoutPanel>().First();
        var statusControls = scrolling.Controls.Cast<Control>().Take(3).ToArray();
        var inputBox = _workflowGroups[0];
        var inputFlow = (FlowLayoutPanel)inputBox.Controls[0];
        var inputControls = inputFlow.Controls.Cast<Control>().ToArray();

        _leftUpperSplit = new SplitContainer
        {
            Dock=DockStyle.Fill, Orientation=Orientation.Horizontal, SplitterWidth=7,
            Size=rootSplit.Panel1.ClientSize, FixedPanel=FixedPanel.Panel1,
            BackColor=SystemColors.ControlDark, AccessibleName="輸入區與參數區高度分隔線"
        };
        rootSplit.Panel1.Controls.Add(_leftUpperSplit);
        _leftUpperSplit.Panel1.BackColor=BackColor;
        _leftUpperSplit.Panel1MinSize=320;
        _leftUpperSplit.Panel2MinSize=240;
        _leftUpperSplit.SplitterDistance=430;
        _leftLowerSplit = new SplitContainer
        {
            Dock=DockStyle.Fill, Orientation=Orientation.Horizontal, SplitterWidth=7,
            Size=_leftUpperSplit.Panel2.ClientSize, FixedPanel=FixedPanel.Panel2,
            BackColor=SystemColors.ControlDark, AccessibleName="參數區與說明區高度分隔線"
        };
        _leftUpperSplit.Panel2.Controls.Add(_leftLowerSplit);
        _leftLowerSplit.Panel1MinSize=150;
        _leftLowerSplit.Panel2MinSize=80;
        _leftLowerSplit.Panel1.BackColor=BackColor;
        _leftLowerSplit.Panel2.BackColor=BackColor;
        _leftLowerSplit.Panel1.Controls.Add(scrolling);
        _contextHelp.Dock=DockStyle.Fill;
        _leftLowerSplit.Panel2.Controls.Add(_contextHelp);

        var top = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=4, Padding=new Padding(6) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<3;i++)top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        top.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        _leftUpperSplit.Panel1.Controls.Add(top);
        for(int i=0;i<3;i++) { top.Controls.Add(statusControls[i],0,i);statusControls[i].Dock=DockStyle.Fill; }
        inputBox.AutoSize=false;inputBox.Dock=DockStyle.Fill;top.Controls.Add(inputBox,0,3);
        var input = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=inputControls.Length };
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<inputControls.Length;i++)
        {
            bool list=inputControls[i]==lstImages;
            input.RowStyles.Add(new RowStyle(list?SizeType.Percent:SizeType.AutoSize,list?100:0));
            input.Controls.Add(inputControls[i],0,i);inputControls[i].Dock=DockStyle.Fill;
        }
        lstImages.IntegralHeight=false;
        inputBox.Controls.Remove(inputFlow);inputFlow.Dispose();inputBox.Controls.Add(input);
        InitializeWorkflowTabs(scrolling);
        _leftUpperSplit.BringToFront();

        Shown+=(_,_)=>
        {
            int inputMinimum=top.Padding.Vertical+statusControls.Sum(c=>c.Height+c.Margin.Vertical)
                +inputBox.Padding.Vertical+inputBox.Font.Height+inputBox.Margin.Vertical
                +inputControls.Where(c=>c!=lstImages).Sum(c=>c.Height+c.Margin.Vertical)+60;
            _leftUpperSplit.Panel1MinSize=Math.Min(inputMinimum,
                _leftUpperSplit.Height-_leftUpperSplit.SplitterWidth-_leftUpperSplit.Panel2MinSize);
            var layout=new LeftLayout(430,120);
            try { if(File.Exists(LeftLayoutPath))layout=JsonSerializer.Deserialize<LeftLayout>(File.ReadAllText(LeftLayoutPath))??layout; }
            catch(Exception ex) { AppendLog($"無法讀取面板配置：{ex.Message}"); }
            _leftUpperSplit.SplitterDistance=Math.Clamp(layout.InputHeight,_leftUpperSplit.Panel1MinSize,
                _leftUpperSplit.Height-_leftUpperSplit.SplitterWidth-_leftUpperSplit.Panel2MinSize);
            _leftLowerSplit.SplitterDistance=Math.Clamp(_leftLowerSplit.Height-_leftLowerSplit.SplitterWidth-layout.HelpHeight,
                _leftLowerSplit.Panel1MinSize,_leftLowerSplit.Height-_leftLowerSplit.SplitterWidth-_leftLowerSplit.Panel2MinSize);
        };
        FormClosed+=(_,_)=>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LeftLayoutPath)!);
                File.WriteAllText(LeftLayoutPath,JsonSerializer.Serialize(new LeftLayout(_leftUpperSplit.SplitterDistance,_leftLowerSplit.Panel2.Height)));
            }
            catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        };
    }

    private void InitializeWorkflowTabs(FlowLayoutPanel previous)
    {
        var tabs=new TabControl
        {
            Dock=DockStyle.Fill, Multiline=true, AccessibleName="重建工作流程分頁"
        };
        string[] titles=["② 快速推估","③ 點雲調整","④ 高品質重建","⑤ 網格預覽"];
        for(int i=0;i<titles.Length;i++)
        {
            var page=new TabPage(titles[i]) { Padding=new Padding(3), BackColor=BackColor };
            var scroll=new Panel { Dock=DockStyle.Fill, AutoScroll=true, Padding=new Padding(3) };
            var box=_workflowGroups[i+1];
            var contents=(FlowLayoutPanel)box.Controls[0];
            contents.Dock=DockStyle.None;
            box.Dock=DockStyle.None;
            box.AutoSize=false;
            scroll.Controls.Add(box);
            box.Location=new Point(scroll.Padding.Left,scroll.Padding.Top);
            contents.SizeChanged+=(_,_)=>box.Height=contents.Bottom+box.Padding.Bottom;
            page.Controls.Add(scroll);
            tabs.TabPages.Add(page);
            // Reserve scrollbar space so vertical scrolling does not create a horizontal bar.
            void FitContents()
            {
                int width=Math.Max(100,scroll.Width-SystemInformation.VerticalScrollBarWidth-scroll.Padding.Horizontal);
                box.SuspendLayout();contents.SuspendLayout();
                box.Width=width;
                box.MaximumSize=new Size(width,0);
                int inner=Math.Max(60,width-box.Padding.Horizontal-contents.Padding.Horizontal-6);
                contents.Location=new Point(box.Padding.Left,box.Font.Height+box.Padding.Top);
                contents.Width=width-box.Padding.Horizontal;
                contents.MaximumSize=new Size(width-box.Padding.Horizontal,0);
                foreach(Control child in contents.Controls)
                {
                    child.MaximumSize=new Size(inner,0);
                    if(child is FlowLayoutPanel row)
                    {
                        row.Width=inner;
                        row.WrapContents=true;
                        foreach(Control field in row.Controls)
                            if(field is Label)field.MaximumSize=new Size(Math.Min(200,inner-6),0);
                    }
                }
                contents.ResumeLayout(true);box.ResumeLayout(true);
                box.Height=contents.Bottom+box.Padding.Bottom;
            }
            scroll.SizeChanged+=(_,_)=>FitContents();
            FitContents();
        }
        var exportHint=_workflowGroups[^1];
        _workflowGroups.Remove(exportHint);
        exportHint.Dispose();
        _leftLowerSplit.Panel1.Controls.Add(tabs);
        previous.Dispose();
    }
}
