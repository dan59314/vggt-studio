using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace VGGT.WinForms.Services;

public sealed record VggtRunOptions(
    string PythonExe, string RepoPath, string ModelId, IReadOnlyList<string> Images,
    string OutputDirectory, double ConfidencePercentile, int MaxPoints, int MaxMeshFaces,
    string? QueryPointsCsv, bool SaveDenseArrays, bool UseDepthAndCamera, bool GenerateMesh,
    double[]? FrameTimes = null, string? VideoPath = null);

public sealed class VggtResult
{
    public Dictionary<string, int> ProcessingStats { get; init; } = [];
    public List<ImageDepthPair> ImageDepthPairs { get; init; } = [];
    public string? ViewerProjectPath { get; init; }
    public string? CameraAnimationPath { get; init; }
    public string PlyPath { get; init; } = "";
    public string? MeshGlbPath { get; init; }
    public string? DenseArrayPath { get; init; }
    public RuntimeResult? Runtime { get; init; }
    public List<string> DepthPreviews { get; init; } = [];
    public List<CameraResult> Cameras { get; init; } = [];
    public Dictionary<string, int[]> Shapes { get; init; } = [];
}

public sealed class ImageDepthPair
{
    public string ImagePath { get; init; } = "";
    public string DepthPath { get; init; } = "";
}

public sealed class RuntimeResult
{
    public string Device { get; init; } = "";
    public string Gpu { get; init; } = "";
    public string Pytorch { get; init; } = "";
    public string Cuda { get; init; } = "";
    public string Dtype { get; init; } = "";
    public double InferenceSeconds { get; init; }
    public double PeakGpuMemoryMb { get; init; }
}

public sealed class CameraResult
{
    public int Index { get; init; }
    public string Image { get; init; } = "";
    public float[][] Intrinsic { get; init; } = [];
    public float[][] Extrinsic { get; init; } = [];
}

public sealed class VggtProcessService
{
    public async Task<VggtResult> ProcessCacheAsync(string python, string cache, string output, string settings,
        bool mesh, Action<string> log, Action<int,string> progress, CancellationToken cancellationToken)
    {
        string settingsPath = output + "-settings.json";
        await File.WriteAllTextAsync(settingsPath, settings, cancellationToken);
        var psi = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment["PYTHONUTF8"] = "1";
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory,"python","process_cached.py"),
            "--cache",cache,"--output",output,"--settings",settingsPath }) psi.ArgumentList.Add(arg);
        if (mesh) psi.ArgumentList.Add("--mesh");
        using var process = new Process { StartInfo = psi };
        process.Start();
        var errors = process.StandardError.ReadToEndAsync();
        var lines = Task.Run(async () => { while (await process.StandardOutput.ReadLineAsync() is {} line) ParseLine(line,log,progress); });
        using var registration = cancellationToken.Register(() => { try { process.Kill(true); } catch { } });
        await process.WaitForExitAsync(cancellationToken);
        await lines;
        string error = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return JsonSerializer.Deserialize<VggtResult>(await File.ReadAllTextAsync(Path.Combine(output,"result.json"),cancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive=true }) ?? throw new InvalidDataException("沒有有效處理結果。");
    }
    public async Task<VggtResult> RunAsync(VggtRunOptions options, Action<string> log,
        Action<int, string> progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        string listPath = Path.Combine(options.OutputDirectory, "input_images.txt");
        await File.WriteAllLinesAsync(listPath, options.Images, cancellationToken);
        string runner = Path.Combine(AppContext.BaseDirectory, "python", "vggt_runner.py");
        if (!File.Exists(runner)) throw new FileNotFoundException("找不到 Python runner。", runner);

        var psi = new ProcessStartInfo(options.PythonExe)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true, WorkingDirectory = options.RepoPath,
        };
        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var arg in new[] { runner, "--repo", options.RepoPath, "--images-file", listPath,
                     "--output", options.OutputDirectory, "--model", options.ModelId,
                     "--confidence-percentile", options.ConfidencePercentile.ToString(System.Globalization.CultureInfo.InvariantCulture),
                     "--max-points", options.MaxPoints.ToString(), "--max-mesh-faces", options.MaxMeshFaces.ToString() })
            psi.ArgumentList.Add(arg);
        if (!string.IsNullOrWhiteSpace(options.QueryPointsCsv)) { psi.ArgumentList.Add("--query-points"); psi.ArgumentList.Add(options.QueryPointsCsv); }
        if (options.SaveDenseArrays) psi.ArgumentList.Add("--save-dense");
        if (options.UseDepthAndCamera) psi.ArgumentList.Add("--depth-camera-points");
        if (options.GenerateMesh) psi.ArgumentList.Add("--generate-mesh");
        if (options.FrameTimes is not null)
        {
            string timelinePath = Path.Combine(options.OutputDirectory, "video_timeline.json");
            await File.WriteAllTextAsync(timelinePath, JsonSerializer.Serialize(new {
                videoPath = options.VideoPath, times = options.FrameTimes }), cancellationToken);
            psi.ArgumentList.Add("--timeline"); psi.ArgumentList.Add(timelinePath);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) ParseLine(e.Data, log, progress); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) log("[stderr] " + e.Data); };
        if (!process.Start()) throw new InvalidOperationException("無法啟動 Python。");
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch { } });
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0) throw new InvalidOperationException($"VGGT 推論失敗，Python 結束碼 {process.ExitCode}。請查看日誌。");

        string manifest = Path.Combine(options.OutputDirectory, "result.json");
        var result = JsonSerializer.Deserialize<VggtResult>(await File.ReadAllTextAsync(manifest, cancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return result ?? throw new InvalidDataException("result.json 格式錯誤。");
    }

    private static void ParseLine(string line, Action<string> log, Action<int, string> progress)
    {
        if (line.StartsWith("PROGRESS|"))
        {
            var p = line.Split('|', 3);
            if (p.Length == 3 && int.TryParse(p[1], out int value)) progress(value, p[2]);
        }
        else log(line);
    }
}
