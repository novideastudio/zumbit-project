using System.Collections.Generic;
using UnityEngine;

public static class ArtLoader
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string path)
    {
        if (cache.TryGetValue(path, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(path);
        cache[path] = s;
        return s;
    }

    public static void Clear() => cache.Clear();
}
