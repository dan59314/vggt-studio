using VGGT.WinForms;
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var video = new VideoImportForm("python", "sample.mp4");
        video.CreateControl(); video.PerformLayout();
        var labels = Descendants(video).OfType<Label>().Select(x => x.Text).ToArray();
        if (!labels.Contains("拍攝模式") || !labels.Contains("品質") ||
            !labels.Contains("終點（0＝完整影片）") || !labels.Contains("影格數（0＝自動）"))
            throw new Exception("Smart video analysis controls missing: " + string.Join(" | ", labels));
        var choices = Descendants(video).OfType<ComboBox>().SelectMany(x => x.Items.Cast<object>().Select(i => i.ToString())).ToArray();
        if (!choices.Contains("主體環繞") || !choices.Contains("場景走拍") || !choices.Contains("高品質"))
            throw new Exception("Capture modes or quality presets missing");
        using var main = new MainForm();
        main.CreateControl(); main.PerformLayout();
        if (Descendants(main).OfType<Button>().All(x => x.Text != "一鍵建立環繞物體模型"))
            throw new Exception("One-click orbit reconstruction button missing");
        if (main.MainMenuStrip?.Items[0] is not ToolStripMenuItem file ||
            file.DropDownItems.OfType<ToolStripMenuItem>().All(x => x.Text != "輸入"))
            throw new Exception("Video menu missing");
        Console.WriteLine("Both forms constructed and laid out; video input menu present.");
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
