using System.ComponentModel;
using System.Numerics;
using VGGT.WinForms.Models;

namespace VGGT.WinForms.Controls;

public sealed class PointCloudViewport : UserControl
{
    private PointCloud? _cloud;
    private float _yaw = -.55f;
    private float _pitch = -.25f;
    private float _zoom = 2.6f;
    private Vector2 _pan;
    private Point _mouseDown;
    private bool _rotating;
    private bool _panning;
    private bool _flipVertical;
    private int[][]? _faces;
    private Vector3[]? _cameraPath;
    private sealed class SurfaceData
    {
        public float[][] Vertices { get; set; } = [];
        public int[][] Faces { get; set; } = [];
    }
    public void SetSurface(string path)
    {
        var data=System.Text.Json.JsonSerializer.Deserialize<SurfaceData>(File.ReadAllText(path),
            new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidDataException("網格資料無效。");
        SetPointCloud(PointCloud.FromVertices(data.Vertices.Select(v=>new PointVertex(new Vector3(v[0],v[1],v[2]),
            Color.FromArgb((int)v[3],(int)v[4],(int)v[5]))).ToList()));
        _faces=data.Faces;Invalidate();
    }
    public void SetCameraPath(Vector3[] points) { _cameraPath=points;Invalidate(); }

    [DefaultValue(true)] public bool ShowAxes { get; set; } = true;
    [DefaultValue(2)] public int PointSize { get; set; } = 2;
    [Browsable(false)] public int PointCount => _cloud?.Points.Count ?? 0;

    public PointCloudViewport()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(13, 23, 34);
        ForeColor = Color.Gainsboro;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void SetPointCloud(PointCloud? cloud,bool resetView=true)
    {
        _cloud = cloud;
        _faces=null;_cameraPath=null;
        if(resetView)ResetView();else Invalidate();
    }

    public void ResetView()
    {
        _yaw = -.55f; _pitch = -.25f; _zoom = 2.6f; _pan = Vector2.Zero;
        Invalidate();
    }

    public void ToggleVerticalFlip()
    {
        _flipVertical = !_flipVertical;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        if (_cloud is null || _cloud.Points.Count == 0)
        {
            using var f = new Font(Font.FontFamily, 12f);
            TextRenderer.DrawText(g, "VGGT 3D Viewport\r\n完成推論或開啟 PLY 後顯示點雲\r\n左鍵旋轉｜右鍵平移｜滾輪縮放", f,
                ClientRectangle, Color.FromArgb(145, 170, 190), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var orientation = _flipVertical ? Matrix4x4.CreateRotationX(MathF.PI) : Matrix4x4.Identity;
        var rotation = orientation * Matrix4x4.CreateRotationY(_yaw) * Matrix4x4.CreateRotationX(_pitch);
        float scale = Math.Min(ClientSize.Width, ClientSize.Height) * .43f / _cloud.Radius * _zoom / 2.6f;
        float cx = ClientSize.Width * .5f + _pan.X;
        float cy = ClientSize.Height * .5f + _pan.Y;
        var projected = new List<(float z, float x, float y, Color c)>(_cloud.Points.Count);
        foreach (var vertex in _cloud.Points)
        {
            var p = Vector3.Transform(vertex.Position - _cloud.Center, rotation);
            float perspective = 1f / Math.Max(.25f, 1.8f + p.Z / (_cloud.Radius * 2.2f));
            projected.Add((p.Z, cx + p.X * scale * perspective, cy - p.Y * scale * perspective, vertex.Color));
        }
        if(_faces is not null)
        {
            foreach(var face in _faces.OrderByDescending(f=>(projected[f[0]].z+projected[f[1]].z+projected[f[2]].z)/3))
            {
                var a=projected[face[0]];var b=projected[face[1]];var c=projected[face[2]];
                if(Math.Max(a.x,Math.Max(b.x,c.x))<0||Math.Min(a.x,Math.Min(b.x,c.x))>Width||
                   Math.Max(a.y,Math.Max(b.y,c.y))<0||Math.Min(a.y,Math.Min(b.y,c.y))>Height)continue;
                using var brush=new SolidBrush(Color.FromArgb((a.c.R+b.c.R+c.c.R)/3,(a.c.G+b.c.G+c.c.G)/3,(a.c.B+b.c.B+c.c.B)/3));
                g.FillPolygon(brush,[new PointF(a.x,a.y),new PointF(b.x,b.y),new PointF(c.x,c.y)]);
            }
            if(ShowAxes)DrawAxes(g,rotation);
            TextRenderer.DrawText(g,$"{_faces.Length:N0} triangles | 幾何預覽（完整貼圖請開啟 GLB）",Font,new Point(10,10),ForeColor);
            return;
        }
        projected.Sort((a, b) => b.z.CompareTo(a.z));
        int size = Math.Clamp(PointSize, 1, 6);
        foreach (var p in projected)
        {
            if (p.x < 0 || p.y < 0 || p.x >= Width || p.y >= Height) continue;
            using var brush = new SolidBrush(p.c);
            g.FillEllipse(brush, p.x - size * .5f, p.y - size * .5f, size, size);
        }
        if (ShowAxes) DrawAxes(g, rotation);
        if(_cameraPath is {Length: >0})
        {
            var path=_cameraPath.Select(v=>
            {
                var p=Vector3.Transform(v-_cloud.Center,rotation);
                float perspective=1f/Math.Max(.25f,1.8f+p.Z/(_cloud.Radius*2.2f));
                return new PointF(cx+p.X*scale*perspective,cy-p.Y*scale*perspective);
            }).ToArray();
            using var pen=new Pen(Color.Gold,2);
            if(path.Length>1)g.DrawLines(pen,path);
            for(int i=0;i<path.Length;i++){g.FillEllipse(Brushes.Gold,path[i].X-4,path[i].Y-4,8,8);TextRenderer.DrawText(g,(i+1).ToString(),Font,Point.Round(path[i]),Color.White);}
        }
        string orientationLabel = _flipVertical ? " | 上下翻轉" : string.Empty;
        TextRenderer.DrawText(g, $"{_cloud.Points.Count:N0} points{orientationLabel}", Font, new Point(10, 10), ForeColor);
    }

    private void DrawAxes(Graphics g, Matrix4x4 rotation)
    {
        var origin = new PointF(42, Height - 42);
        DrawAxis(Vector3.UnitX, Color.IndianRed, "X");
        DrawAxis(Vector3.UnitY, Color.LightGreen, "Y");
        DrawAxis(Vector3.UnitZ, Color.DeepSkyBlue, "Z");
        void DrawAxis(Vector3 axis, Color color, string label)
        {
            var v = Vector3.TransformNormal(axis, rotation) * 28;
            var end = new PointF(origin.X + v.X, origin.Y - v.Y);
            using var pen = new Pen(color, 2);
            g.DrawLine(pen, origin, end);
            TextRenderer.DrawText(g, label, Font, Point.Round(end), color);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); _mouseDown = e.Location;
        _rotating = e.Button == MouseButtons.Left; _panning = e.Button == MouseButtons.Right;
        Capture = true;
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _rotating = _panning = false; Capture = false; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var dx = e.X - _mouseDown.X; var dy = e.Y - _mouseDown.Y; _mouseDown = e.Location;
        if (_rotating) { _yaw += dx * .009f; _pitch = Math.Clamp(_pitch + dy * .009f, -1.5f, 1.5f); Invalidate(); }
        if (_panning) { _pan += new Vector2(dx, dy); Invalidate(); }
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e); _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12f : .89f), .25f, 12f); Invalidate();
    }
}
