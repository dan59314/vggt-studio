using System.Reflection;
using VGGT.WinForms;
using VGGT.WinForms.Controls;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        string root=Path.GetFullPath("validation/SubjectSmoke");
        string[] images=[Path.Combine(root,"frame0.png"),Path.Combine(root,"frame1.png")];
        for(int i=0;i<2;i++)
        {
            using var image=new Bitmap(400,300);using var g=Graphics.FromImage(image);
            g.Clear(Color.DarkBlue);g.FillRectangle(Brushes.OrangeRed,100+i*6,70,150,150);
            g.DrawLine(Pens.White,100+i*6,70,250+i*6,220);image.Save(images[i]);
        }
        using var form=new SubjectMaskForm(Path.GetFullPath("VGGT.Runtime/.venv/Scripts/python.exe"),images,new());
        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
        object Get(string name)=>typeof(SubjectMaskForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        var canvas=(SubjectCanvas)Get("_canvas");
        RectangleF Bounds()=>(RectangleF)typeof(SubjectCanvas).GetProperty("ImageBounds",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(canvas)!;
        void Mouse(string method,float x,float y)
        {
            var bounds=Bounds();
            typeof(SubjectCanvas).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(canvas,
                [new MouseEventArgs(MouseButtons.Left,1,(int)(bounds.X+bounds.Width*x),(int)(bounds.Y+bounds.Height*y),0)]);
        }
        Mouse("OnMouseDown",.15f,.12f);Mouse("OnMouseMove",.8f,.85f);Mouse("OnMouseUp",.8f,.85f);
        void Generate(bool track)
        {
            var task=(Task)typeof(SubjectMaskForm).GetMethod("GenerateAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[track])!;
            var timeout=DateTime.UtcNow.AddSeconds(45);
            while(!task.IsCompleted&&DateTime.UtcNow<timeout){Application.DoEvents();Thread.Sleep(20);}
            if(!task.IsCompleted)throw new Exception("Segmentation timed out");task.GetAwaiter().GetResult();
        }
        Generate(false);if(!canvas.HasMask)throw new Exception("No segmentation mask");
        string directory=(string)Get("_directory");
        bool Kept(float x,float y)
        {using var m=new Bitmap(Path.Combine(directory,"0000.png"));return m.GetPixel((int)(x*m.Width),(int)(y*m.Height)).R>128;}
        void Stroke(int tool,float x,float y){canvas.Tool=tool;Mouse("OnMouseDown",x,y);Mouse("OnMouseUp",x,y);}
        Stroke(2,.4f,.4f);if(Kept(.4f,.4f))throw new Exception("Erase failed");
        canvas.Undo();if(!Kept(.4f,.4f))throw new Exception("Undo failed");
        canvas.Redo();if(Kept(.4f,.4f))throw new Exception("Redo failed");canvas.Undo();
        var beforeZoom=Bounds();
        typeof(SubjectCanvas).GetMethod("OnMouseWheel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(canvas,
            [new MouseEventArgs(MouseButtons.None,0,canvas.Width/2,canvas.Height/2,120)]);
        if(Bounds().Width<=beforeZoom.Width)throw new Exception("Zoom failed");
        Stroke(2,.4f,.4f);if(Kept(.4f,.4f))throw new Exception("Zoomed brush coordinates failed");canvas.Undo();
        canvas.Tool=7;var beforePan=Bounds();Mouse("OnMouseDown",.5f,.5f);Mouse("OnMouseMove",.55f,.5f);Mouse("OnMouseUp",.55f,.5f);
        if(Bounds().X<=beforePan.X)throw new Exception("Pan failed");canvas.ResetView();
        canvas.Tool=4;
        foreach(var p in new[]{(.3f,.3f),(.5f,.3f),(.5f,.5f),(.3f,.5f)}){Mouse("OnMouseDown",p.Item1,p.Item2);Mouse("OnMouseUp",p.Item1,p.Item2);}
        canvas.FinishPolygon();if(Kept(.4f,.4f))throw new Exception("Polygon erase failed");canvas.Undo();
        Stroke(5,.4f,.4f);Stroke(6,.1f,.1f);
        if(!canvas.HasHints||!Kept(.4f,.4f))throw new Exception("Hints modified mask directly");
        var refine=(Task)typeof(SubjectMaskForm).GetMethod("RefineAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,null)!;
        while(!refine.IsCompleted){Application.DoEvents();Thread.Sleep(20);}refine.GetAwaiter().GetResult();
        if(canvas.HasHints||!Kept(.4f,.4f)||Kept(.1f,.1f))throw new Exception("Refinement failed");
        canvas.Undo();if(!canvas.HasHints)throw new Exception("Refinement undo did not restore hints");canvas.Redo();
        canvas.ClearMask();if(canvas.HasMask)throw new Exception("Clear failed");canvas.Undo();
        if(!canvas.HasMask)throw new Exception("Clear undo failed");
        var tools=(FlowLayoutPanel)Get("_tools");
        void Click(string title)=>tools.Controls.OfType<Button>().Single(b=>b.Text==title).PerformClick();
        Click("套用全部遮罩");if(form.DialogResult==DialogResult.OK)throw new Exception("Unreviewed masks accepted");
        Click("確認本張");Generate(true);
        if(!File.Exists(Path.Combine(directory,"0001.png")))throw new Exception("Tracking output missing");
        var frames=(ListBox)Get("_frames");frames.SelectedIndex=1;Application.DoEvents();
        canvas.Tool=2;Mouse("OnMouseDown",.3f,.4f);Mouse("OnMouseUp",.3f,.4f);
        if(((bool[])Get("_reviewed"))[1])throw new Exception("Edited mask still reviewed");
        Click("確認本張");
        using(var preview=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(preview,new Rectangle(Point.Empty,form.Size));preview.Save(Path.Combine(root,"preview.png"));}
        Click("套用全部遮罩");
        if(form.Masks.Count!=2||form.DialogResult!=DialogResult.OK)throw new Exception("Commit failed");
        using var main=new MainForm();
        var mainType=typeof(MainForm);
        string Parameters()=>(string)mainType.GetMethod("PointParameters",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,null)!;
        string before=Parameters();
        mainType.GetField("_subjectMasks",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,form.Masks);
        ((CheckBox)mainType.GetField("_subjectOnly",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!).Checked=true;
        if(Parameters()==before)throw new Exception("Subject edit did not invalidate processed result");
        Console.WriteLine("UI segmentation, zoom/pan coordinates, erase, polygon, undo/redo, hint refinement, propagation, review and commit passed.");
    }
}
