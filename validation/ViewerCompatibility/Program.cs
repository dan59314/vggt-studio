using System.Numerics;
using Rv3dViewer.Core;
using Rv3dViewer.CameraAnimationPlugin;
var root = Path.GetFullPath(args[0]);
bool singleFile=File.Exists(root);
var project = await new JsonProjectStore().LoadAsync(singleFile?root:Path.Combine(root, "scene.rv3dproj"));
if(singleFile)root=Path.GetDirectoryName(root)!;
var animation = await CameraAnimationSerializer.LoadAsync(singleFile?Path.Combine(root,Path.GetDirectoryName(project.Models[0].AssetPath)!,"camera.camera-animation.json"):Path.Combine(root, "scene.camera-animation.json"));
if (project.Models.Count != 1 || animation.Keyframes.Count != (args.Length > 1 ? int.Parse(args[1]) : 2)) throw new Exception("Wrong counts");
if (!File.Exists(Path.Combine(root, project.Models[0].AssetPath))) throw new Exception("Missing mesh");
foreach (var frame in animation.Keyframes)
{
    var camera = frame.ToCameraState();
    var originalUp = camera.Up;
    CameraController.UpdateUpFromRoll(camera);
    if (Vector3.Distance(originalUp, camera.Up) > .0001f) throw new Exception("Roll/up mismatch");
}
Console.WriteLine($"Viewer native readers passed: project v{project.FormatVersion}, animation v{animation.Version}, {animation.Keyframes.Count} frames; model exists; roll/up consistent.");
