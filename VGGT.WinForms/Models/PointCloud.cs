using System.Globalization;
using System.Numerics;

namespace VGGT.WinForms.Models;

public readonly record struct PointVertex(Vector3 Position, Color Color);

public sealed class PointCloud
{
    public IReadOnlyList<PointVertex> Points { get; }
    public Vector3 Center { get; }
    public float Radius { get; }
    public static PointCloud FromVertices(List<PointVertex> points) => new(points);
    public static PointCloud FromVertices(List<PointVertex> points,PointCloud reference) => new(points,reference);

    private PointCloud(List<PointVertex> points,PointCloud reference)
    {Points=points;Center=reference.Center;Radius=reference.Radius;}

    private PointCloud(List<PointVertex> points)
    {
        Points = points;
        if (points.Count == 0) return;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p.Position);
            max = Vector3.Max(max, p.Position);
        }
        Center = (min + max) * .5f;
        Radius = Math.Max((max - min).Length() * .5f, .001f);
    }

    public static PointCloud LoadAsciiPly(string path)
    {
        using var reader = new StreamReader(path);
        if (!string.Equals(reader.ReadLine()?.Trim(), "ply", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("不是有效的 PLY 檔案。");

        int count = 0;
        bool ascii = false;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("format ascii", StringComparison.OrdinalIgnoreCase)) ascii = true;
            if (line.StartsWith("element vertex ", StringComparison.OrdinalIgnoreCase))
                count = int.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2], CultureInfo.InvariantCulture);
            if (line.Trim() == "end_header") break;
        }
        if (!ascii) throw new NotSupportedException("預覽器目前支援 ASCII PLY；VGGT runner 會輸出此格式。");

        var points = new List<PointVertex>(Math.Max(0, count));
        for (int i = 0; i < count && (line = reader.ReadLine()) != null; i++)
        {
            var s = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (s.Length < 3) continue;
            float x = float.Parse(s[0], CultureInfo.InvariantCulture);
            float y = float.Parse(s[1], CultureInfo.InvariantCulture);
            float z = float.Parse(s[2], CultureInfo.InvariantCulture);
            var color = s.Length >= 6
                ? Color.FromArgb(int.Parse(s[3]), int.Parse(s[4]), int.Parse(s[5]))
                : Color.DeepSkyBlue;
            if (float.IsFinite(x) && float.IsFinite(y) && float.IsFinite(z))
                points.Add(new PointVertex(new Vector3(x, y, z), color));
        }
        return new PointCloud(points);
    }
}
