using System.Numerics;
using System.Text.Json;
using VGGT.WinForms.Controls;
using VGGT.WinForms.Models;

namespace VGGT.WinForms;

public sealed class DepthRangeForm : Form
{
    public sealed class DepthData
    {
        public double Minimum {get;set;}
        public double Maximum {get;set;}
        public long Total {get;set;}
        public long[] Counts {get;set;}=[];
        public double[] Edges {get;set;}=[];
        public float[][] Samples {get;set;}=[];
        public bool Subject {get;set;}
    }
    private readonly DepthData _data;
    private readonly PointCloudViewport _preview=new(){Dock=DockStyle.Fill};
    private readonly NumericUpDown _near=new(){Minimum=0,Maximum=99999.999m,DecimalPlaces=3,Increment=.01m,Width=120};
    private readonly NumericUpDown _far=new(){Minimum=.001m,Maximum=100000,DecimalPlaces=3,Increment=.01m,Width=120};
    private readonly TrackBar _nearSlider=new(){Minimum=0,Maximum=1000,TickStyle=TickStyle.None,Dock=DockStyle.Fill};
    private readonly TrackBar _farSlider=new(){Minimum=0,Maximum=1000,TickStyle=TickStyle.None,Dock=DockStyle.Fill};
    private readonly CheckBox _gray=new(){Text="灰色顯示被深度排除的點",Checked=true,AutoSize=true};
    private readonly Label _status=new(){AutoSize=true};
    private readonly Panel _histogram=new(){Dock=DockStyle.Top,Height=115,BackColor=Color.White};
    private readonly PointCloud _reference;
    private bool _sync;
    private double Maximum=>Math.Max(.001,Math.Min(100000,_data.Maximum));
    public decimal NearDepth=>_near.Value;
    public decimal FarDepth=>_far.Value;
    public DepthRangeForm(string path,decimal near,decimal far)
    {
        _data=JsonSerializer.Deserialize<DepthData>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("深度資料無效。");
        Text="深度分布與範圍預覽";Size=new Size(1150,850);MinimumSize=new Size(850,650);StartPosition=FormStartPosition.CenterParent;
        _near.Value=Math.Clamp(near,_near.Minimum,_near.Maximum);_far.Value=Math.Clamp(far,_far.Minimum,_far.Maximum);
        _reference=PointCloud.FromVertices(_data.Samples.Select(s=>new PointVertex(new Vector3(s[0],s[1],s[2]),Color.FromArgb((int)s[4],(int)s[5],(int)s[6]))).ToList());
        var controls=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Padding=new Padding(8)};
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
        controls.Controls.Add(new Label{Text="最近深度",AutoSize=true},0,0);controls.Controls.Add(_nearSlider,1,0);controls.Controls.Add(_near,2,0);
        controls.Controls.Add(new Label{Text="最遠深度",AutoSize=true},0,1);controls.Controls.Add(_farSlider,1,1);controls.Controls.Add(_far,2,1);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};
        buttons.Controls.Add(_gray);
        var reset=new Button{Text="重設範圍",AutoSize=true};reset.Click+=(_,_)=>{_sync=true;_near.Value=0;_far.Value=100000;_sync=false;UpdatePreview();};buttons.Controls.Add(reset);
        var accept=new Button{Text="帶回③深度參數",AutoSize=true};
        accept.Click+=(_,_)=>{if(_near.Value>=_far.Value){_status.Text="最近深度必須小於最遠深度。";return;}DialogResult=DialogResult.OK;Close();};buttons.Controls.Add(accept);
        var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);
        var notes=new Label{AutoSize=true,Dock=DockStyle.Top,Padding=new Padding(6),
            Text="拖動滑桿即時預覽；橫軸為 log(1＋深度)，數字框可精確輸入。使用各相機的模型相對深度，不是公尺。\n"+
                "這裡只預覽深度限制"+(_data.Subject?"與目前主體遮罩":"")+"；尚未套用信心、平滑及補洞。帶回參數後，仍需按③「套用並更新點雲」。"};
        var footer=new Panel{Dock=DockStyle.Bottom,Height=55,Padding=new Padding(6)};footer.Controls.Add(_status);
        Controls.Add(_preview);Controls.Add(_histogram);Controls.Add(controls);Controls.Add(buttons);Controls.Add(notes);Controls.Add(footer);
        _near.ValueChanged+=(_,_)=>UpdatePreview();_far.ValueChanged+=(_,_)=>UpdatePreview();_gray.CheckedChanged+=(_,_)=>UpdatePreview();
        _nearSlider.Scroll+=(_,_)=>SetFromSlider(true);_farSlider.Scroll+=(_,_)=>SetFromSlider(false);
        _histogram.Paint+=DrawHistogram;_histogram.Resize+=(_,_)=>_histogram.Invalidate();
        _preview.SetPointCloud(_reference);UpdatePreview();
    }
    private double DepthAt(int value)=>Math.Exp(value/1000d*Math.Log(1+Maximum))-1;
    private int Position(double value)=>(int)Math.Clamp(Math.Round(Math.Log(1+Math.Max(0,value))/Math.Log(1+Maximum)*1000),0,1000);
    private void SetFromSlider(bool near)
    {
        decimal value=decimal.Round((decimal)DepthAt(near?_nearSlider.Value:_farSlider.Value),3);
        _sync=true;
        if(near)_near.Value=Math.Clamp(value,0,Math.Max(0,_far.Value-.001m));
        else _far.Value=Math.Clamp(value,Math.Min(100000,_near.Value+.001m),100000);
        _sync=false;UpdatePreview();
    }
    private void UpdatePreview()
    {
        if(_sync)return;_sync=true;
        _nearSlider.Value=Position((double)_near.Value);_farSlider.Value=Position((double)_far.Value);_sync=false;
        var vertices=new List<PointVertex>();int kept=0;
        foreach(var s in _data.Samples)
        {
            bool inside=s[3]>=(double)_near.Value&&s[3]<=(double)_far.Value;
            if(inside)kept++;if(!inside&&!_gray.Checked)continue;
            vertices.Add(new PointVertex(new Vector3(s[0],s[1],s[2]),inside?Color.FromArgb((int)s[4],(int)s[5],(int)s[6]):Color.FromArgb(80,80,80)));
        }
        _preview.SetPointCloud(PointCloud.FromVertices(vertices,_reference),false);
        _status.Text=$"樣本保留 {kept:N0}／{_data.Samples.Length:N0}，排除 {_data.Samples.Length-kept:N0}。完整深度分布共 {_data.Total:N0} 點。\n"+
            (_near.Value>=_far.Value?"範圍無效：最近深度必須小於最遠深度。":"灰色僅為預覽提示，不會作為灰色點輸出。");
        _histogram.Invalidate();
    }
    private void DrawHistogram(object? sender,PaintEventArgs e)
    {
        if(_data.Counts.Length==0)return;
        int width=Math.Max(1,_histogram.Width-24),height=_histogram.Height-28;
        double maximum=Math.Max(1,_data.Counts.Max());
        for(int i=0;i<_data.Counts.Length;i++)
        {
            float x=12+width*i/(float)_data.Counts.Length;
            float bar=(float)(_data.Counts[i]/maximum*(height-8));
            double center=(_data.Edges[i]+_data.Edges[i+1])/2;
            using var brush=new SolidBrush(center>=(double)_near.Value&&center<=(double)_far.Value?Color.SteelBlue:Color.LightGray);
            e.Graphics.FillRectangle(brush,x,height-bar,Math.Max(1,width/(float)_data.Counts.Length-1),bar);
        }
        double logMax=Math.Log(1+_data.Maximum);
        float X(decimal d)=>12+(float)(Math.Clamp(Math.Log(1+(double)d)/logMax,0,1)*width);
        e.Graphics.DrawLine(Pens.Green,X(_near.Value),0,X(_near.Value),height);
        e.Graphics.DrawLine(Pens.Red,X(_far.Value),0,X(_far.Value),height);
        e.Graphics.DrawString($"0　　深度分布（對數橫軸；柱高為點數）　　最大 {_data.Maximum:0.###}",Font,Brushes.Black,12,height+3);
    }
}
