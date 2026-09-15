using VGGT.WinForms.Services;

namespace VGGT.WinForms;

public sealed class VideoImportForm : Form
{
    private readonly string _python;
    private readonly string _video;
    private readonly NumericUpDown _start = new() { Maximum = 119, DecimalPlaces = 1 };
    private readonly NumericUpDown _end = new() { Minimum = .1m, Maximum = 120, Value = 30, DecimalPlaces = 1 };
    private readonly NumericUpDown _interval = new() { Minimum = .1m, Maximum = 120, Value = 1, DecimalPlaces = 1 };
    private readonly NumericUpDown _limit = new() { Minimum = 2, Maximum = 32, Value = 4 };
    private readonly TextBox _ffmpeg = new() { Text = "ffmpeg", Width = 250 };
    private readonly Button _extract = new() { Text = "擷取／重新擷取", AutoSize = true };
    private readonly Button _accept = new() { Text = "使用勾選影格（取代清單）", AutoSize = true, Enabled = false };
    private readonly Label _status = new() { AutoSize = true, Text = "先擷取影格，再取消勾選模糊或重複畫面。" };
    private readonly ListView _frames = new() { Dock = DockStyle.Fill, View = View.LargeIcon, CheckBoxes = true, MultiSelect = false };
    private readonly PictureBox _preview = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(25,25,25) };
    private readonly ImageList _thumbnails = new() { ImageSize = new Size(160, 100), ColorDepth = ColorDepth.Depth32Bit };
    private readonly CancellationTokenSource _cts = new();
    private bool _busy;
    private bool _closeRequested;
    public VideoFrames? Selection { get; private set; }

    public VideoImportForm(string python, string video)
    {
        _python = python; _video = video;
        Text = "影片影格預覽 — " + Path.GetFileName(video);
        Size = new Size(1050, 740); MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var options = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        void Field(string text, Control control) { options.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(8) }); options.Controls.Add(control); }
        Field("起點（秒）", _start); Field("終點（秒）", _end); Field("間隔（秒）", _interval); Field("最多影格", _limit);
        Field("FFmpeg", _ffmpeg);
        var browse = new Button { Text = "瀏覽…", AutoSize = true };
        browse.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe" }; if (d.ShowDialog(this) == DialogResult.OK) _ffmpeg.Text = d.FileName; };
        options.Controls.Add(browse); options.Controls.Add(_extract);
        options.Controls.Add(new Label { Text = "第一版支援前 120 秒；超過影格上限時均勻拉大間隔。顯存不足請減少影格。", AutoSize = true, Margin = new Padding(8) });
        var split = new SplitContainer { Width = 1000, Dock = DockStyle.Fill, SplitterDistance = 400 };
        _frames.LargeImageList = _thumbnails;
        split.Panel1.Controls.Add(_frames); split.Panel2.Controls.Add(_preview);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        var all = new Button { Text = "全選" }; var none = new Button { Text = "全不選" };
        all.Click += (_, _) => { foreach (ListViewItem item in _frames.Items) item.Checked = true; };
        none.Click += (_, _) => { foreach (ListViewItem item in _frames.Items) item.Checked = false; };
        var cancel = new Button { Text = "取消" }; cancel.Click += (_, _) => Close();
        actions.Controls.AddRange([all, none, _accept, cancel]);
        layout.Controls.Add(options, 0, 0); layout.Controls.Add(split, 0, 1);
        layout.Controls.Add(_status, 0, 2); layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        _extract.Click += Extract_Click;
        _frames.SelectedIndexChanged += (_, _) =>
        {
            _preview.Image?.Dispose(); _preview.Image = null;
            if (_frames.SelectedItems.Count == 0) return;
            var frame = (VideoFrame)_frames.SelectedItems[0].Tag!;
            using var image = Image.FromFile(frame.Path);
            _preview.Image = new Bitmap(image);
            _status.Text = $"{frame.TimeSeconds:0.000} 秒｜清晰度 {frame.Sharpness:0.0}｜與上一張差異 {frame.Difference?.ToString("0.0") ?? "—"}（分數僅供比較）";
        };
        _accept.Click += (_, _) =>
        {
            var frames = _frames.CheckedItems.Cast<ListViewItem>().Select(i => (VideoFrame)i.Tag!).OrderBy(f => f.TimeSeconds).ToList();
            if (frames.Count < 2) { MessageBox.Show(this, "請至少保留兩張影格。"); return; }
            Selection = new VideoFrames { VideoPath = _video, Frames = frames };
            DialogResult = DialogResult.OK; Close();
        };
    }

    private async void Extract_Click(object? sender, EventArgs e)
    {
        if (_start.Value >= _end.Value) { MessageBox.Show(this, "終點必須大於起點。"); return; }
        _busy = true; _extract.Enabled = _accept.Enabled = false;
        _status.Text = "正在讀取影片並擷取影格…";
        try
        {
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VGGT Studio", "VideoFrames", Guid.NewGuid().ToString("N"));
            var result = await VideoFrameService.ExtractAsync(_python, _video, cache, _start.Value, _end.Value,
                _interval.Value, (int)_limit.Value, _ffmpeg.Text,
                line => BeginInvoke(() => _status.Text = line.Split('|').Last()), _cts.Token);
            _frames.Items.Clear(); _thumbnails.Images.Clear();
            foreach (var frame in result.Frames)
            {
                using var image = Image.FromFile(frame.Path);
                using var thumbnail = new Bitmap(160, 100);
                using (var graphics = Graphics.FromImage(thumbnail))
                {
                    graphics.Clear(Color.Black);
                    float scale = Math.Min(160f / image.Width, 100f / image.Height);
                    graphics.DrawImage(image, (160-image.Width*scale)/2, (100-image.Height*scale)/2, image.Width*scale, image.Height*scale);
                }
                _thumbnails.Images.Add(thumbnail);
                _frames.Items.Add(new ListViewItem($"{frame.TimeSeconds:0.000}s  清晰度 {frame.Sharpness:0}", _thumbnails.Images.Count-1) { Tag = frame, Checked = true });
            }
            _accept.Enabled = result.Frames.Count >= 2;
            _status.Text = $"已擷取 {result.Frames.Count} 張。勾選要重建的影格，點選可放大預覽。";
        }
        catch (OperationCanceledException) { _status.Text = "已取消"; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "影片擷取失敗"); }
        finally { _busy = false; _extract.Enabled = true; if (_closeRequested) Close(); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy) { e.Cancel = true; _closeRequested = true; _cts.Cancel(); }
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _preview.Image?.Dispose(); _thumbnails.Dispose(); _cts.Dispose(); }
        base.Dispose(disposing);
    }
}
