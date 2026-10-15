using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildPhoneControllerPlayer
{
    [MenuItem("Build/Windows player with phone controllers")]
    public static void Build()
    {
        string project=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,".."));
        string folder=Path.Combine(project,"Builds","PhoneController");
        Directory.CreateDirectory(folder);
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=Path.Combine(folder,"we-are-table.exe"),
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.None
        });
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+result.summary.result);
        UnityEngine.Debug.Log("Phone-controller player built: "+folder);
    }
}
