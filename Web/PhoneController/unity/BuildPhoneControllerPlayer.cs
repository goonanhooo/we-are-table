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
        string output=Environment.GetEnvironmentVariable("WAT_WINDOWS_OUTPUT");
        if(string.IsNullOrEmpty(output))output=Path.Combine(project,"Builds","PhoneController","we-are-table.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=output,
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.CompressWithLz4HC
        });
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-summary.json"),UnityEngine.JsonUtility.ToJson(new BuildSummary {
            result=result.summary.result.ToString(),output=result.summary.outputPath,bytes=result.summary.totalSize,
            errors=result.summary.totalErrors,warnings=result.summary.totalWarnings
        },true));
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+result.summary.result);
        UnityEngine.Debug.Log("Windows phone-controller player built: "+output);
    }
    [MenuItem("Build/macOS player with phone controllers")]
    public static void Mac()
    {
        string project=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,".."));
        string output=Environment.GetEnvironmentVariable("WAT_MAC_OUTPUT");
        if(string.IsNullOrEmpty(output))output=Path.Combine(project,"Builds","PhoneController-Mac","we-are-table.app");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var type=Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
        var architecture=type?.GetProperty("architecture",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
        if(architecture!=null)architecture.SetValue(null,Enum.Parse(architecture.PropertyType,"x64ARM64"));
        else PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone,2);
        var options=new BuildPlayerOptions {
            scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
            locationPathName=output,
            target=BuildTarget.StandaloneOSX,
            options=BuildOptions.CompressWithLz4HC
        };
        var result=BuildPipeline.BuildPlayer(options);
        if(result.summary.result==BuildResult.Succeeded && UnityEngine.Application.platform==UnityEngine.RuntimePlatform.OSXEditor)
            SignMacBundle(output);
        var summary=new {result=result.summary.result.ToString(),output=result.summary.outputPath,bytes=result.summary.totalSize,errors=result.summary.totalErrors,warnings=result.summary.totalWarnings};
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-summary.json"),UnityEngine.JsonUtility.ToJson(new BuildSummary {result=summary.result,output=summary.output,bytes=summary.bytes,errors=summary.errors,warnings=summary.warnings},true));
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception("macOS build failed: "+result.summary.result);
        UnityEngine.Debug.Log("Mac phone-controller player built: "+output);
    }
    static void SignMacBundle(string path)
    {
        // Unity can copy a Steam dylib whose embedded signature no longer matches.
        // Re-sign the local test bundle and retain any existing JIT entitlements.
        string quoted="\""+path.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
        RunCodeSign("--force --deep --sign - --timestamp=none --preserve-metadata=entitlements "+quoted);
        RunCodeSign("--verify --deep --strict "+quoted);
        UnityEngine.Debug.Log("Mac test signature verified: "+path);
    }

    static void RunCodeSign(string arguments)
    {
        var start=new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign",arguments) {
            UseShellExecute=false,RedirectStandardError=true,CreateNoWindow=true
        };
        using(var process=System.Diagnostics.Process.Start(start))
        {
            string error=process.StandardError.ReadToEnd();
            process.WaitForExit();
            if(process.ExitCode!=0)throw new Exception("Mac code signing failed: "+error);
        }
    }
    [Serializable] sealed class BuildSummary {public string result,output;public ulong bytes;public int errors,warnings;}

}
