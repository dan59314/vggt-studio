using System.Diagnostics;
using System.Text.Json;
using VGGT.WinForms.Models;
using VGGT.WinForms.Services;

namespace VGGT.WinForms;

public partial class MainForm : Form
{
    private readonly VggtProcessService _service = new();
    private CancellationTokenSource? _runCts;
    private VggtResult? _result;
    private string? _lastGlbPath;
    private string? _resultDirectory;
    private VideoFrames? _videoSelection;
    private bool _closeAfterWork;
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".webp"];
    private static string ImageListPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VGGT Studio", "last-images.json");

    public MainForm()
    {
        InitializeComponent();
        if (IsDesignTime()) return;

        txtOutput.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VGGT_Output");
        ConfigureLocalVggtRuntime();
        string localRunner = Path.Combine(AppContext.BaseDirectory, "python", "vggt_runner.py");
        AppendLog($"Runner: {localRunner}");
        RestoreImageList();
        InitializeWorkflow();
        InitializeFileMenu();
        InitializeContextHelp();
        InitializeWindowLayout();

    }

    private void ImportVideo()
    {
        if (_runCts is not null) { MessageBox.Show(this, "請等待推論完成。"); return; }
        using var dialog = new OpenFileDialog { Filter = "影片|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v|所有檔案|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        using var preview = new VideoImportForm(txtPython.Text, dialog.FileName);
        if (preview.ShowDialog(this) != DialogResult.OK || preview.Selection is null) return;
        lstImages.Items.Clear();
        AddImages(preview.Selection.Frames.Select(f => f.Path));
        _videoSelection = preview.Selection;
        InputChanged();
        AppendLog($"影片：{_videoSelection.VideoPath}；已選取 {_videoSelection.Frames.Count} 張，Camera 路徑保留影片時間間隔。");
    }

    private void RestoreImageList()
    {
        try
        {
            if (!File.Exists(ImageListPath)) return;
            using var document = JsonDocument.Parse(File.ReadAllText(ImageListPath));
            string[]? paths;
            VideoFrames? video = null;
            if (document.RootElement.ValueKind == JsonValueKind.Array)
                paths = document.RootElement.Deserialize<string[]>();
            else
            {
                paths = document.RootElement.GetProperty("Images").Deserialize<string[]>();
                if (document.RootElement.TryGetProperty("Video", out var savedVideo)) video = savedVideo.Deserialize<VideoFrames>();
            }
            if (paths is null) return;
            AddImages(paths.Where(p => !string.IsNullOrWhiteSpace(p)));
            _videoSelection = video;
            if (_videoSelection is not null)
            {
                var selected = lstImages.Items.Cast<string>().Select(path =>
                    _videoSelection.Frames.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))).ToArray();
                if (selected.Any(f => f is null || !double.IsFinite(f.TimeSeconds)) ||
                    selected.Zip(selected.Skip(1)).Any(pair => pair.First!.TimeSeconds >= pair.Second!.TimeSeconds))
                {
                    _videoSelection = null;
                    AppendLog("影片時間資料不完整，已改用照片序列時間。");
                }
            }
            AppendLog($"已還原 {lstImages.Items.Count} 張影像；不存在的檔案已略過。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            AppendLog($"無法還原上次影像清單：{ex.Message}");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || IsDesignTime()) return;
        if (_runCts is not null)
        {
            e.Cancel = true; _closeAfterWork = true; _runCts.Cancel();
            lblStatus.Text = "正在取消工作，完成後關閉。";
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ImageListPath)!);
            // Replace only after the entire list is written, preserving image order.
            string temporaryPath = ImageListPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonSerializer.Serialize(
                    new { Images = lstImages.Items.Cast<string>().ToArray(), Video = _videoSelection }));
                File.Move(temporaryPath, ImageListPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"無法儲存影像清單，下次開啟可能無法還原：{ex.Message}",
                "儲存影像清單", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static bool IsDesignTime()
    {
        if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            return true;

        // The out-of-process WinForms designer does not always set Control.DesignMode
        // while the form constructor is running.
        string processName = Process.GetCurrentProcess().ProcessName;
        return processName.Contains("DesignToolsServer", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("devenv", StringComparison.OrdinalIgnoreCase);
    }

    private void ConfigureLocalVggtRuntime()
    {
        var candidates = new List<string>();
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            candidates.Add(Path.Combine(current.FullName, "VGGT.Runtime"));
            candidates.Add(Path.Combine(current.FullName, "..", "VGGT.Runtime"));
            current = current.Parent;
        }
        candidates.Add(@"D:\SourceCode\AI_Projects\Codex Data\VGGT.Runtime");

        foreach (string candidate in candidates.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string python = Path.Combine(candidate, ".venv", "Scripts", "python.exe");
            string repo = Path.Combine(candidate, "vggt");
            if (!File.Exists(python) || !File.Exists(Path.Combine(repo, "vggt", "models", "vggt.py"))) continue;
            txtPython.Text = python;
            txtRepo.Text = repo;
            lblStatus.Text = "已偵測 VGGT CUDA 12.8 執行環境。";
            AppendLog($"Python: {python}\r\nVGGT Repo: {repo}");
            return;
        }

        AppendLog("尚未偵測到 VGGT.Runtime；請手動指定 Python 與 VGGT Repo。");
    }

    private void btnAddImages_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "影像|*.jpg;*.jpeg;*.png;*.bmp;*.webp", Multiselect = true };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddImages(dlg.FileNames);
    }

    private void btnAddFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "選擇包含同一場景影像的資料夾" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        AddImages(Directory.EnumerateFiles(dlg.SelectedPath).Where(x => ImageExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)).OrderBy(x => x));
    }

    private void AddImages(IEnumerable<string> paths)
    {
        if (_videoSelection is not null)
        {
            AppendLog("已加入一般照片；本次清單改用每張 1 秒的照片路徑時間。");
            _videoSelection = null;
        }
        var existing = lstImages.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(File.Exists)) if (existing.Add(path)) lstImages.Items.Add(path);
        lblStatus.Text = $"已加入 {lstImages.Items.Count} 張影像；第一張為參考座標。";
        InputChanged();
    }

    private void btnRemove_Click(object? sender, EventArgs e)
    {
        foreach (var item in lstImages.SelectedItems.Cast<object>().ToArray()) lstImages.Items.Remove(item);
        InputChanged();
    }

    private void btnClear_Click(object? sender, EventArgs e) { lstImages.Items.Clear(); _videoSelection = null; InputChanged(); }

    private void btnPython_Click(object? sender, EventArgs e) =>
        BrowseFile(txtPython, "Python|python.exe|所有檔案|*.*");

    private void btnRepo_Click(object? sender, EventArgs e) => BrowseFolder(txtRepo);

    private void btnOutput_Click(object? sender, EventArgs e) => BrowseFolder(txtOutput);

    private void btnQuery_Click(object? sender, EventArgs e) =>
        BrowseFile(txtQuery, "CSV|*.csv|所有檔案|*.*");

    private void btnCancel_Click(object? sender, EventArgs e) => _runCts?.Cancel();

    private double[]? GetVideoTimes()
    {
        if (_videoSelection is null) return null;
        var times = _videoSelection.Frames.ToDictionary(f => f.Path, f => f.TimeSeconds, StringComparer.OrdinalIgnoreCase);
        return lstImages.Items.Cast<string>().Select(p => times[p]).ToArray();
    }

    private void btnResetView_Click(object? sender, EventArgs e) => viewport.ResetView();

    private void btnFlipVertical_Click(object? sender, EventArgs e) => viewport.ToggleVerticalFlip();

    private void nudPointSize_ValueChanged(object? sender, EventArgs e)
    {
        viewport.PointSize = (int)nudPointSize.Value;
        viewport.Invalidate();
    }

    private async void btnRun_Click(object? sender, EventArgs e) => await RunWorkflowAsync(false);

    private void LoadResult(VggtResult result, string output)
    {
        _resultDirectory = output;
        string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.Combine(output, p);
        var ply = Resolve(result.PlyPath);
        viewport.SetPointCloud(PointCloud.LoadAsciiPly(ply));
        _lastGlbPath = string.IsNullOrWhiteSpace(result.MeshGlbPath) ? null : Resolve(result.MeshGlbPath);
        btnOpenGlb.Enabled = _lastGlbPath is not null && File.Exists(_lastGlbPath);
        lstDepth.Items.Clear(); foreach (var p in result.DepthPreviews) lstDepth.Items.Add(Resolve(p));
        if (lstDepth.Items.Count > 0) lstDepth.SelectedIndex = 0;
        gridCameras.Rows.Clear();
        foreach (var c in result.Cameras)
        {
            float fx = c.Intrinsic.Length > 1 ? c.Intrinsic[0][0] : 0, fy = c.Intrinsic.Length > 1 ? c.Intrinsic[1][1] : 0;
            string t = c.Extrinsic.Length > 2 ? $"{c.Extrinsic[0][3]:0.###}, {c.Extrinsic[1][3]:0.###}, {c.Extrinsic[2][3]:0.###}" : "";
            gridCameras.Rows.Add(c.Index, Path.GetFileName(c.Image), $"{fx:0.##} / {fy:0.##}", t);
        }
        if (result.Runtime is not null)
        {
            AppendLog($"GPU 確認：{result.Runtime.Gpu} ({result.Runtime.Device})\r\n" +
                      $"PyTorch {result.Runtime.Pytorch} / CUDA {result.Runtime.Cuda} / {result.Runtime.Dtype}\r\n" +
                      $"純 GPU 推論：{result.Runtime.InferenceSeconds:0.000} 秒；峰值配置顯存：{result.Runtime.PeakGpuMemoryMb:N0} MiB");
        }
        AppendLog($"載入 {viewport.PointCount:N0} 個預覽點。\r\n輸出資料夾：{output}" +
            (_lastGlbPath is null ? "" : $"\r\nGLB Mesh：{_lastGlbPath}"));
    }

    private void btnOpenPly_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "PLY 點雲|*.ply" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { viewport.SetPointCloud(PointCloud.LoadAsciiPly(dlg.FileName)); lblStatus.Text = $"已載入 {viewport.PointCount:N0} points"; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "PLY 載入失敗"); }
    }

    private void btnOpenGlb_Click(object? sender, EventArgs e)
    {
        if (_lastGlbPath is null || !File.Exists(_lastGlbPath))
        {
            MessageBox.Show(this, "找不到最近產生的 GLB 檔案。", "開啟 GLB",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Exception? openModelError = null;
        try
        {
            Process.Start(new ProcessStartInfo(_lastGlbPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            openModelError = ex;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"/select,\"{_lastGlbPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"無法開啟檔案總管：{ex.Message}", "開啟 GLB",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (openModelError is not null)
        {
            MessageBox.Show(this,
                $"已在檔案總管中選取 GLB，但系統無法啟動預設模型檢視器：{openModelError.Message}",
                "開啟 GLB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void lstDepth_SelectedIndexChanged(object? sender, EventArgs e)
    {
        picDepth.Image?.Dispose(); picDepth.Image = null;
        if (lstDepth.SelectedItem is string path && File.Exists(path))
        { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read); picDepth.Image = Image.FromStream(fs).Clone() as Image; }
    }

    private void AppendLog(string text) { if (txtLog.TextLength > 0) txtLog.AppendText(Environment.NewLine); txtLog.AppendText(text); }
    private void BrowseFolder(TextBox target) { using var d = new FolderBrowserDialog { SelectedPath = Directory.Exists(target.Text) ? target.Text : "" }; if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.SelectedPath; }
    private void BrowseFile(TextBox target, string filter) { using var d = new OpenFileDialog { Filter = filter }; if (d.ShowDialog(this) == DialogResult.OK) target.Text = d.FileName; }
}
