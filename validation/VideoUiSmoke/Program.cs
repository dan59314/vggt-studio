using VGGT.WinForms;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var video = new VideoImportForm("python", "sample.mp4");
        video.CreateControl(); video.PerformLayout();
        using var main = new MainForm();
        main.CreateControl(); main.PerformLayout();
        if (main.MainMenuStrip is null || main.MainMenuStrip.Items[0].Text != "輸入") throw new Exception("Video menu missing");
        Console.WriteLine("Both forms constructed and laid out; video input menu present.");
    }
}
