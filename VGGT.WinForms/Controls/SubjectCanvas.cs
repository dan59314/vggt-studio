using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
namespace VGGT.WinForms.Controls;

public sealed class SubjectCanvas : Control
{
    private Bitmap? _image, _mask, _hints;
    private PointF? _start, _last;
    private Point _mouse, _panStart;
    private PointF _pan;
    private bool _panning, _hover;
    private float _zoom=1;
    private int _tool;
    private readonly List<PointF> _polygon=[];
    private readonly List<(Bitmap? Mask,Bitmap? Hints)> _undo=[],_redo=[];
    public RectangleF Selection { get; private set; }
    public int Tool { get=>_tool; set { _tool=value;_polygon.Clear();Invalidate(); } }
    public int View { get; set; }=1;
    public int Opacity { get; set; }=40;
    public int BrushSize { get; set; }=16;
    public event Action? Edited;
    public bool HasMask=>_mask is not null;
    public bool HasHints=>_hints is not null;
    public bool CanUndo=>_undo.Count>0;
    public bool CanRedo=>_redo.Count>0;
    public SubjectCanvas(){DoubleBuffered=true;BackColor=Color.FromArgb(25,30,35);Dock=DockStyle.Fill;TabStop=true;}
    public void LoadFrame(string image,string? mask,string? hints=null)
    {
        _image?.Dispose();_mask?.Dispose();_hints?.Dispose();_mask=_hints=null;
        ClearHistory(_undo);ClearHistory(_redo);Selection=RectangleF.Empty;_polygon.Clear();
        using var source=Image.FromFile(image);
        double scale=Math.Min(1,1024d/Math.Max(source.Width,source.Height));
        _image=new Bitmap(source,new Size(Math.Max(1,(int)Math.Round(source.Width*scale)),Math.Max(1,(int)Math.Round(source.Height*scale))));
        if(mask is not null)_mask=ReadBitmap(mask,true);
        if(hints is not null)_hints=ReadBitmap(hints,false);
        ResetView();
    }
    private Bitmap ReadBitmap(string path,bool binary)
    {
        using var source=Image.FromFile(path);
        var bitmap=new Bitmap(_image!.Width,_image.Height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap))
        {g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(source,new Rectangle(Point.Empty,bitmap.Size));}
        if(binary)
        {
            var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            try
            {
                var bytes=new byte[data.Stride*data.Height];System.Runtime.InteropServices.Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
                for(int i=0;i<bytes.Length;i+=4){byte value=bytes[i]>=128?(byte)255:(byte)0;bytes[i]=bytes[i+1]=bytes[i+2]=value;bytes[i+3]=255;}
                System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            }
            finally{bitmap.UnlockBits(data);}
        }
        return bitmap;
    }
    public void ReplaceMask(string path,bool clearHints=false){var next=ReadBitmap(path,true);Remember();_mask?.Dispose();_mask=next;if(clearHints){_hints?.Dispose();_hints=null;}Changed();}
    public void ClearMask(){Remember();_mask?.Dispose();_mask=null;_hints?.Dispose();_hints=null;Changed();}
    public void ClearHints(){if(_hints is null)return;Remember();_hints.Dispose();_hints=null;Changed();}
    public void SaveMask(string path){if(_mask is null)throw new InvalidOperationException("請先建立遮罩。");_mask.Save(path,ImageFormat.Png);}
    public void SaveHints(string path){if(_hints is not null)_hints.Save(path,ImageFormat.Png);}
    public void ResetView(){_zoom=1;_pan=PointF.Empty;Invalidate();}
    private static void ClearHistory(List<(Bitmap? Mask,Bitmap? Hints)> list)
    {foreach(var state in list){state.Mask?.Dispose();state.Hints?.Dispose();}list.Clear();}
    private (Bitmap?,Bitmap?) Snapshot()=>((Bitmap?)_mask?.Clone(),(Bitmap?)_hints?.Clone());
    private void Remember()
    {
        _undo.Add(Snapshot());ClearHistory(_redo);
        if(_undo.Count>20){_undo[0].Mask?.Dispose();_undo[0].Hints?.Dispose();_undo.RemoveAt(0);}
    }
    public void Undo()=>Restore(_undo,_redo);
    public void Redo()=>Restore(_redo,_undo);
    private void Restore(List<(Bitmap? Mask,Bitmap? Hints)> from,List<(Bitmap? Mask,Bitmap? Hints)> to)
    {
        if(from.Count==0)return;to.Add(Snapshot());var state=from[^1];from.RemoveAt(from.Count-1);
        _mask?.Dispose();_hints?.Dispose();_mask=state.Mask;_hints=state.Hints;_polygon.Clear();Changed();
    }
    private void Changed(){Invalidate();Edited?.Invoke();}
    private RectangleF ImageBounds
    {
        get
        {
            if(_image is null)return RectangleF.Empty;
            float scale=Math.Min((float)Width/_image.Width,(float)Height/_image.Height)*_zoom;
            return new RectangleF((Width-_image.Width*scale)/2+_pan.X,(Height-_image.Height*scale)/2+_pan.Y,_image.Width*scale,_image.Height*scale);
        }
    }
    private PointF Position(Point p)
    {var r=ImageBounds;return new PointF(Math.Clamp((p.X-r.X)/r.Width,0,1),Math.Clamp((p.Y-r.Y)/r.Height,0,1));}
    private PointF ScreenPoint(PointF p){var r=ImageBounds;return new PointF(r.X+p.X*r.Width,r.Y+p.Y*r.Height);}
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);if(_image is null||_start is not null)return;
        var before=ImageBounds;float x=(e.X-before.X)/before.Width,y=(e.Y-before.Y)/before.Height;
        _zoom=Math.Clamp(_zoom*(e.Delta>0?1.25f:.8f),1,16);var after=ImageBounds;
        _pan.X+=e.X-(after.X+x*after.Width);_pan.Y+=e.Y-(after.Y+y*after.Height);Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(_image is null)return;var r=ImageBounds;
        e.Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
        e.Graphics.DrawImage(View==2&&_mask is not null?_mask:_image,r);
        void Overlay(Bitmap bitmap,ColorMap[] colors)
        {
            using var a=new ImageAttributes();a.SetRemapTable(colors);
            e.Graphics.DrawImage(bitmap,Rectangle.Round(r),0,0,bitmap.Width,bitmap.Height,GraphicsUnit.Pixel,a);
        }
        if(View==1&&_mask is not null)Overlay(_mask,[new ColorMap{OldColor=Color.White,NewColor=Color.FromArgb(Opacity*255/100,0,255,90)},new ColorMap{OldColor=Color.Black,NewColor=Color.Transparent}]);
        if(_hints is not null)Overlay(_hints,[new ColorMap{OldColor=Color.Black,NewColor=Color.Transparent},
            new ColorMap{OldColor=Color.White,NewColor=Color.FromArgb(230,0,255,255)},new ColorMap{OldColor=Color.Red,NewColor=Color.FromArgb(230,255,60,60)}]);
        using var outline=new Pen(Color.Gold,2);
        if(!Selection.IsEmpty)e.Graphics.DrawRectangle(outline,r.X+Selection.X*r.Width,r.Y+Selection.Y*r.Height,Selection.Width*r.Width,Selection.Height*r.Height);
        if(_polygon.Count>0)
        {
            var vertices=_polygon.Select(ScreenPoint).ToList();vertices.Add(_mouse);
            e.Graphics.DrawLines(outline,vertices.ToArray());foreach(var p in vertices.SkipLast(1))e.Graphics.FillEllipse(Brushes.Gold,p.X-3,p.Y-3,6,6);
        }
        if(_hover&&(Tool is 1 or 2 or 5 or 6)&&r.Contains(_mouse))
        {
            float diameter=BrushSize*r.Width/_image.Width;
            using var pen=new Pen(Tool is 2 or 6?Color.Red:Color.Cyan,2);
            e.Graphics.DrawEllipse(pen,_mouse.X-diameter/2,_mouse.Y-diameter/2,diameter,diameter);
        }
    }
    protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);_hover=true;}
    protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);_hover=false;Invalidate();}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(!Enabled||_image is null)return;Focus();_mouse=e.Location;
        if(e.Button==MouseButtons.Middle||(Tool==7&&e.Button==MouseButtons.Left))
        {_panning=true;_panStart=e.Location;Capture=true;return;}
        if(e.Button!=MouseButtons.Left||!ImageBounds.Contains(e.Location))return;
        if(Tool is 3 or 4){if(e.Clicks==1)_polygon.Add(Position(e.Location));Invalidate();return;}
        Capture=true;_start=Position(e.Location);_last=_start;
        if(Tool!=0){Remember();PaintStroke(_start.Value);}
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);_mouse=e.Location;
        if(_panning){_pan.X+=e.X-_panStart.X;_pan.Y+=e.Y-_panStart.Y;_panStart=e.Location;Invalidate();return;}
        if(_start is not null)
        {
            var p=Position(e.Location);
            if(Tool==0)Selection=new RectangleF(Math.Min(p.X,_start.Value.X),Math.Min(p.Y,_start.Value.Y),Math.Abs(p.X-_start.Value.X),Math.Abs(p.Y-_start.Value.Y));
            else PaintStroke(p);
        }
        Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);if(_panning){_panning=false;Capture=false;return;}
        if(_start is null)return;_start=null;_last=null;Capture=false;if(Tool!=0)Changed();
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);if(Tool is 3 or 4)FinishPolygon();}
    protected override bool IsInputKey(Keys keyData)=>(keyData&Keys.KeyCode) is Keys.Enter or Keys.Escape||base.IsInputKey(keyData);
    public void CancelPolygon(){_polygon.Clear();Invalidate();}
    public void FinishPolygon()
    {
        if(_image is null||_polygon.Count<3)return;Remember();EnsureMask();
        using(var g=Graphics.FromImage(_mask!))using(var b=new SolidBrush(Tool==3?Color.White:Color.Black))
            g.FillPolygon(b,_polygon.Select(p=>new PointF(p.X*_image.Width,p.Y*_image.Height)).ToArray());
        _polygon.Clear();Changed();
    }
    private void EnsureMask(){if(_mask is not null)return;_mask=new Bitmap(_image!.Width,_image.Height);using var g=Graphics.FromImage(_mask);g.Clear(Color.Black);}
    private void PaintStroke(PointF p)
    {
        if(_image is null)return;bool hint=Tool is 5 or 6;
        if(hint&&_hints is null){_hints=new Bitmap(_image.Width,_image.Height);using var fill=Graphics.FromImage(_hints);fill.Clear(Color.Black);}
        if(!hint)EnsureMask();var target=hint?_hints!:_mask!;
        using var g=Graphics.FromImage(target);var color=Tool is 1 or 5?Color.White:hint?Color.Red:Color.Black;
        using var pen=new Pen(color,BrushSize){StartCap=LineCap.Round,EndCap=LineCap.Round};var last=_last??p;
        g.DrawLine(pen,last.X*target.Width,last.Y*target.Height,p.X*target.Width,p.Y*target.Height);
        using var brush=new SolidBrush(color);g.FillEllipse(brush,p.X*target.Width-BrushSize/2f,p.Y*target.Height-BrushSize/2f,BrushSize,BrushSize);
        _last=p;Invalidate();
    }
    protected override void Dispose(bool disposing)
    {if(disposing){_image?.Dispose();_mask?.Dispose();_hints?.Dispose();ClearHistory(_undo);ClearHistory(_redo);}base.Dispose(disposing);}
}
