using System.Reflection;
using VGGT.WinForms;
using VGGT.WinForms.Controls;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        string directory=Path.GetFullPath("validation/sam-bridge-candidates");
        using var form=new AutomaticObjectsForm(directory){StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000),ShowInTaskbar=false};
        form.Show();Application.DoEvents();
        var type=typeof(AutomaticObjectsForm);
        object Get(string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;
        object? Call(string name,params object[] args)=>type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,args);
        var list=(CheckedListBox)Get("_list");var picture=(PictureBox)Get("_picture");
        var data=(AutomaticObjectsForm.Manifest)Get("_data");
        Call("AcceptSelection");if(form.SelectedMaskPath is not null)throw new Exception("Empty selection accepted");
        int x=320,y=160;
        int expected=Enumerable.Range(0,list.Items.Count).OrderBy(i=>data.Objects[i].Area)
            .First(i=>((byte[])Call("Mask",i)!)[y*data.Width+x]!=0);
        float scale=Math.Min((float)picture.Width/data.Width,(float)picture.Height/data.Height);
        typeof(PictureBox).GetMethod("OnMouseClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(picture,
            [new MouseEventArgs(MouseButtons.Left,1,(int)((picture.Width-data.Width*scale)/2+(x+.5f)*scale),(int)((picture.Height-data.Height*scale)/2+(y+.5f)*scale),0)]);
        Application.DoEvents();if(!list.GetItemChecked(expected))throw new Exception("Image picking failed");
        int second=Enumerable.Range(0,list.Items.Count).OrderBy(i=>data.Objects[i].Area)
            .First(i=>i!=expected&&((byte[])Call("Mask",i)!)[320*data.Width+100]!=0);
        list.SetItemChecked(second,true);Application.DoEvents();
        var firstMask=(byte[])Call("Mask",expected)!;var secondMask=(byte[])Call("Mask",second)!;
        var union=(byte[])Call("Union")!;
        for(int i=0;i<union.Length;i++)if(union[i]!=(firstMask[i]|secondMask[i]))throw new Exception("Union mismatch");
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save("validation/AutoObjectsSmoke/preview.png");}
        Call("AcceptSelection");if(form.SelectedMaskPath is null)throw new Exception("No selected mask saved");
        using var canvas=new SubjectCanvas();
        canvas.LoadFrame(Path.Combine(directory,"source.png"),null);
        canvas.ReplaceMask(form.SelectedMaskPath,true);
        if(!canvas.HasMask||!canvas.CanUndo)throw new Exception("Selected mask not reversible");
        canvas.Undo();if(canvas.HasMask)throw new Exception("Undo did not restore empty mask");
        Console.WriteLine($"PASS: {list.Items.Count} real SAM candidates, image picking, multi-selection union, empty guard and undo.");
    }
}
