using System.Reflection;
using System.Text.Json;
using VGGT.WinForms;
using VGGT.WinForms.Controls;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        string path="validation/DepthPreviewSmoke/depth.json";
        var data=JsonSerializer.Deserialize<DepthRangeForm.DepthData>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        using var form=new DepthRangeForm(path,0,100000){StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false};
        form.Show();Application.DoEvents();
        object Get(string name)=>typeof(DepthRangeForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        var preview=(PointCloudViewport)Get("_preview");var far=(NumericUpDown)Get("_far");var gray=(CheckBox)Get("_gray");
        if(preview.PointCount!=data.Samples.Length)throw new Exception("Initial sample count");
        far.Value=decimal.Round((decimal)data.Samples.Select(s=>s[3]).Order().ElementAt(data.Samples.Length/2),3);
        int expected=data.Samples.Count(s=>s[3]<=(double)far.Value);
        if(preview.PointCount!=data.Samples.Length)throw new Exception("Gray points missing");
        var zoom=typeof(PointCloudViewport).GetField("_zoom",BindingFlags.Instance|BindingFlags.NonPublic)!;zoom.SetValue(preview,4f);
        gray.Checked=false;if(preview.PointCount!=expected)throw new Exception("Hidden excluded count differs");
        if((float)zoom.GetValue(preview)! !=4f)throw new Exception("Range adjustment reset camera");
        gray.Checked=true;
        using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save("validation/DepthPreviewSmoke/preview.png");}
        var nearSlider=(TrackBar)Get("_nearSlider");nearSlider.Value=1000;
        typeof(DepthRangeForm).GetMethod("SetFromSlider",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[true]);
        if(form.NearDepth>=form.FarDepth)throw new Exception("Sliders crossed");
        Console.WriteLine($"PASS: {data.Total} histogram points; {data.Samples.Length} aligned samples; gray/hide preview, camera preservation and slider limits.");
    }
}
