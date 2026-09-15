using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace VGGT.WinForms.Services;

public sealed class VideoFrame
{
    public string Path { get; set; } = "";
    public double TimeSeconds { get; set; }
    public double Sharpness { get; set; }
    public double? Difference { get; set; }
}

public sealed class VideoFrames
{
    public string VideoPath { get; set; } = "";
    public List<VideoFrame> Frames { get; set; } = [];
}

public static class VideoFrameService
{
    public static async Task<VideoFrames> ExtractAsync(string python, string video, string output,
        decimal start, decimal end, decimal interval, int limit, string ffmpeg,
        Action<string> log, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment["PYTHONUTF8"] = "1";
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "python", "video_frames.py"),
            "--video", video, "--output", output, "--start", start.ToString(CultureInfo.InvariantCulture),
            "--end", end.ToString(CultureInfo.InvariantCulture), "--interval", interval.ToString(CultureInfo.InvariantCulture),
            "--limit", limit.ToString(), "--ffmpeg", ffmpeg }) psi.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = psi };
        process.Start();
        using var registration = cancellationToken.Register(() => { try { process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
        var errors = process.StandardError.ReadToEndAsync();
        var lines = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line) log(line);
        });
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            // Drain the redirected pipes before disposing the process/streams.
            await process.WaitForExitAsync(CancellationToken.None);
            await lines;
            await errors;
            throw;
        }
        await lines;
        string stderr = await errors;
        if (process.ExitCode != 0) throw new InvalidOperationException("影片擷取失敗：" + stderr);
        return JsonSerializer.Deserialize<VideoFrames>(await File.ReadAllTextAsync(Path.Combine(output, "video_frames.json"), cancellationToken),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("缺少影格資料。");
    }
}
