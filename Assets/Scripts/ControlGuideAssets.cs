using System.Collections.Generic;
using UnityEngine;

/// <summary>Language-specific controls art shares the same filenames and runtime key mapping.</summary>
public static class ControlGuideAssets
{
    static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
    public static Texture2D Load(string name)
    {
        string path="ControlGuides/"+(L.Code+"/")+name;
        if(!cache.TryGetValue(path,out var image)||image==null){image=Resources.Load<Texture2D>(path);if(image!=null)cache[path]=image;}
        return image;
    }
    public static Texture2D Localized(Texture2D source) => source!=null ? Load(source.name)??source : null;
}
