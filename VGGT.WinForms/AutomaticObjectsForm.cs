using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace VGGT.WinForms;

public sealed class AutomaticObjectsForm : Form
{
    public sealed class Candidate
    {
        public int Id { get; set; }
        public string Mask { get; set; }="";
        public int Area { get; set; }
        public double Score { get; set; }
        public override string ToString()=>$"物件 {Id:000}　面積 {Area:N0}　品質 {Score:0.00}";
    }
    public sealed class Manifest
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public string Source { get; set; }="";
        public string Preview { get; set; }="";
        public List<Candidate> Objects { get; set; }=[];
    }
    private readonly Manifest _data;
    private readonly string _directory;
    private readonly CheckedListBox _list=new(){Dock=DockStyle.Fill,CheckOnClick=true,HorizontalScrollbar=true};
    private readonly PictureBox _picture=new(){Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(25,30,35)};
    private readonly ComboBox _view=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    private readonly Label _status=new(){Dock=DockStyle.Bottom,Height=50};
    private readonly Dictionary<int,byte[]> _masks=[];
    private readonly Bitmap _source,_all;
    private Bitmap? _rendered;
    private bool _batch;
    public string? SelectedMaskPath { get; private set; }

    public AutomaticObjectsForm(string directory)
    {
        _directory=directory;
        _data=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(directory,"objects.json")),
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("候選遮罩資料不完整。");
        Text="自動找物件 — 多選並合併要建模的區域";Size=new Size(1200,850);MinimumSize=new Size(850,600);
        using(var image=Image.FromFile(Path.Combine(directory,_data.Source)))_source=new Bitmap(image);
        using(var image=Image.FromFile(Path.Combine(directory,_data.Preview)))_all=new Bitmap(image);
        var split=new SplitContainer{Dock=DockStyle.Fill,Size=new Size(1150,700),SplitterDistance=310,Panel1MinSize=220,Panel2MinSize=200};
        split.Panel1.Controls.Add(_list);split.Panel2.Controls.Add(_picture);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true};
        void Button(string name,Action action){var b=new Button{Text=name,AutoSize=true};b.Click+=(_,_)=>action();bar.Controls.Add(b);}
        _view.Items.AddRange(["所有候選區域","已勾選物件","原始影像"]);_view.SelectedIndex=0;bar.Controls.Add(_view);
        Button("全部取消",()=>{_batch=true;for(int i=0;i<_list.Items.Count;i++)_list.SetItemChecked(i,false);_batch=false;RefreshPreview();});
        Button("合併選取並使用",AcceptSelection);
        Button("取消",()=>{DialogResult=DialogResult.Cancel;Close();});
        var guide=new Label{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(6),MaximumSize=new Size(1150,0),
            Text="勾選左側物件，可合併橋塔與橋面。點影像可切換該位置最小候選區域；重疊的大區域請由清單選取。\n候選可能包含天空或背景，也可能是物件的一部分；品質值不是物件名稱辨識信心。"};
        Controls.Add(split);Controls.Add(guide);Controls.Add(bar);Controls.Add(_status);
        foreach(var item in _data.Objects)_list.Items.Add(item);
        _list.ItemCheck+=(_,_)=>{if(!_batch)BeginInvoke(()=>{if(IsDisposed||Disposing)return;_view.SelectedIndex=1;RefreshPreview();});};
        _view.SelectedIndexChanged+=(_,_)=>RefreshPreview();
        _picture.MouseClick+=(_,e)=>
        {
            float scale=Math.Min((float)_picture.Width/_data.Width,(float)_picture.Height/_data.Height);
            int x=(int)Math.Floor((e.X-(_picture.Width-_data.Width*scale)/2)/scale);
            int y=(int)Math.Floor((e.Y-(_picture.Height-_data.Height*scale)/2)/scale);
            if(x<0||y<0||x>=_data.Width||y>=_data.Height)return;
            int found=Enumerable.Range(0,_data.Objects.Count).OrderBy(i=>_data.Objects[i].Area)
                .FirstOrDefault(i=>Mask(i)[y*_data.Width+x]!=0,-1);
            if(found>=0){_list.SelectedIndex=found;_list.SetItemChecked(found,!_list.GetItemChecked(found));}
            else _status.Text="此位置沒有候選遮罩，可在合併後用新增筆刷補上。";
        };
        RefreshPreview();
    }
    private byte[] Mask(int index)
    {
        if(_masks.TryGetValue(index,out var stored))return stored;
        using var image=new Bitmap(Path.Combine(_directory,_data.Objects[index].Mask));
        if(image.Width!=_data.Width||image.Height!=_data.Height)throw new InvalidDataException("候選遮罩尺寸不符。");
        using var bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))g.DrawImageUnscaled(image,0,0);
        var bits=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        var bytes=new byte[bits.Stride*bits.Height];
        try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);}finally{bitmap.UnlockBits(bits);}
        stored=new byte[_data.Width*_data.Height];
        for(int i=0;i<stored.Length;i++)stored[i]=bytes[i*4]>=128?(byte)255:(byte)0;
        _masks[index]=stored;return stored;
    }
    private byte[] Union()
    {
        var combined=new byte[_data.Width*_data.Height];
        foreach(int index in _list.CheckedIndices){var mask=Mask(index);for(int i=0;i<combined.Length;i++)combined[i]|=mask[i];}
        return combined;
    }
    private Bitmap Render(byte[] mask,bool overlay)
    {
        var bitmap=new Bitmap(_data.Width,_data.Height,PixelFormat.Format32bppArgb);
        if(overlay){using var g=Graphics.FromImage(bitmap);g.DrawImageUnscaled(_source,0,0);}
        var bits=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
        try
        {
            var bytes=new byte[bits.Stride*bits.Height];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);
            for(int i=0;i<mask.Length;i++)
            {
                int p=i*4;
                if(!overlay){bytes[p]=bytes[p+1]=bytes[p+2]=mask[i];}
                else if(mask[i]!=0){bytes[p]=(byte)(bytes[p]*.55+90*.45);bytes[p+1]=(byte)(bytes[p+1]*.55+255*.45);bytes[p+2]=(byte)(bytes[p+2]*.55);}
                bytes[p+3]=255;
            }
            Marshal.Copy(bytes,0,bits.Scan0,bytes.Length);
        }
        finally{bitmap.UnlockBits(bits);}
        return bitmap;
    }
    private void RefreshPreview()
    {
        _picture.Image=null;_rendered?.Dispose();_rendered=null;
        if(_view.SelectedIndex==1){_rendered=Render(Union(),true);_picture.Image=_rendered;}
        else _picture.Image=_view.SelectedIndex==2?_source:_all;
        _status.Text=$"找到 {_data.Objects.Count} 個候選區域，已勾選 {_list.CheckedItems.Count} 個。合併後仍需確認本張，才可套用到建模。";
    }
    private void AcceptSelection()
    {
        if(_list.CheckedItems.Count==0){_status.Text="請至少勾選一個要保留的候選區域。";return;}
        var union=Union();
        if(!union.Any(x=>x!=0)){_status.Text="選取的遮罩沒有有效像素。";return;}
        SelectedMaskPath=Path.Combine(_directory,"selected-"+Guid.NewGuid().ToString("N")+".png");
        using(var bitmap=Render(union,false))bitmap.Save(SelectedMaskPath,ImageFormat.Png);
        DialogResult=DialogResult.OK;Close();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){_picture.Image=null;_rendered?.Dispose();_source.Dispose();_all.Dispose();}
        base.Dispose(disposing);
    }
}
