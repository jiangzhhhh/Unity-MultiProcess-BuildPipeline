using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

[Serializable]
class CACHE
{
    [SerializeField]
    public string asset;
    [SerializeField]
    public string dependencyHash;
    [SerializeField]
    public string[] depends;
}

public static class AssetDependCache
{
    static readonly string CACHE_DIR = "Library/DependCache";
    static Dictionary<string, CACHE> cacheInMemory = new Dictionary<string, CACHE>();

    static string PathToMD5(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        using (MD5 md5Hash = MD5.Create())
        {
            byte[] bytes = md5Hash.ComputeHash(Encoding.ASCII.GetBytes(path));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    static string CalcCacheFileName(string path)
    {
        string hashFileName = PathToMD5(path);
        string bucketDir = string.Format("{0}{1}", hashFileName[0], hashFileName[1]);
        return string.Format("{0}/{1}/{2}", CACHE_DIR, bucketDir, hashFileName);
    }

    static CACHE FetchDependCache(string pathName, bool recursive = true)
    {
        CACHE cache = null;
        string depHash = AssetDatabase.GetAssetDependencyHash(pathName).ToString();

        //memory
        if (cacheInMemory.TryGetValue(pathName, out cache) && cache.dependencyHash == depHash)
            return cache;
        else
            cache = null;

        //disk
        string cacheFile = CalcCacheFileName(pathName);
        if (File.Exists(cacheFile))
        {
            string str = File.ReadAllText(cacheFile);
            if (!string.IsNullOrEmpty(str))
            {
                cache = JsonUtility.FromJson<CACHE>(str);
                if (cache != null && cache.dependencyHash == depHash)
                {
                    cacheInMemory[pathName] = cache;
                    return cache;
                }
                else
                    cache = null;
            }
        }

        //rebuild
        if (cache == null)
        {
            cache = new CACHE();
            cache.asset = pathName;
            cache.dependencyHash = depHash;
            cache.depends = AssetDatabase.GetDependencies(pathName, false);
            string dir = Path.GetDirectoryName(cacheFile);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(cacheFile, JsonUtility.ToJson(cache, true));
            cacheInMemory[pathName] = cache;

            if (recursive)
            {
                foreach (var dep in AssetDatabase.GetDependencies(pathName, true))
                {
                    if (dep == pathName)
                        continue;
                    FetchDependCache(dep, false);
                }
            }
        }
        return cache;
    }

    static void GetDependenciesRecursive(string pathName, SortedList<string, string> results, HashSet<string> visited)
    {
        if (visited.Contains(pathName))
            return;
        visited.Add(pathName);

        var cache = FetchDependCache(pathName);
        foreach (var dep in cache.depends)
        {
            if (!results.ContainsKey(dep))
                results.Add(dep, dep);
        }

        foreach (var dep in cache.depends)
        {
            var cache2 = FetchDependCache(dep);
            foreach (var dep2 in cache2.depends)
                GetDependenciesRecursive(dep2, results, visited);
        }
    }

    public static string[] GetDependencies(string pathName, bool recursive=false)
    {
        var cache = FetchDependCache(pathName);
        if (!recursive)
            return cache.depends;
        else
        {
            SortedList<string, string> results = new SortedList<string, string>();
            HashSet<string> visited = new HashSet<string>();
            GetDependenciesRecursive(pathName, results, visited);
            return results.Values.ToArray();
        }
    }

    [MenuItem("Assets/AssetDependCache/Rebuild Depend Cache All(force)")]
    static void RebuildDependCacheAllHard(MenuCommand cmd) { RebuildDependCacheAll(true); }

    [MenuItem("Assets/AssetDependCache/Rebuild Depend Cache All")]
    static void RebuildDependCacheAllSoft(MenuCommand cmd) { RebuildDependCacheAll(false); }

    static void RebuildDependCacheAll(bool force)
    {
        Stopwatch sw = Stopwatch.StartNew();

        if (force)
            FileUtil.DeleteFileOrDirectory(CACHE_DIR);

        var all = AssetDatabase.GetAllAssetPaths()
            .Where(x => x.StartsWith("Assets"))
            .Where(x => !AssetDatabase.IsValidFolder(x))
            .Where(x => !x.EndsWith(".cs"))
            .ToArray();

        int index = 0;
        int total = all.Length;
        EditorApplication.CallbackFunction update = null;
        update = delegate ()
        {
            for (int i = 0; i < 512; ++i)
            {
                string path = all[index++];
                FetchDependCache(path, false);
                bool isCancel = EditorUtility.DisplayCancelableProgressBar("rebuilding", path, (float)index / total);
                if (isCancel || index >= total)
                {
                    EditorUtility.ClearProgressBar();
                    EditorApplication.update -= update;
                    if (index >= total)
                        UnityEngine.Debug.LogFormat("rebuild all depend cache, use time:{0}", sw.ElapsedMilliseconds / 1000f);
                    return;
                }
            }
        };
        EditorApplication.update -= update;
        EditorApplication.update += update;
    }

    [MenuItem("Assets/AssetDependCache/Rebuild Depend Cache(Force)")]
    static void RebuildDependCacheHard(MenuCommand cmd) { RebuildDependCache(true); }

    [MenuItem("Assets/AssetDependCache/Rebuild Depend Cache")]
    static void RebuildDependCacheSoft(MenuCommand cmd) { RebuildDependCache(false); }

    static void RebuildDependCache(bool force)
    {
        foreach (var obj in Selection.objects)
        {
            string asset = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(asset))
                return;

            Stopwatch sw = Stopwatch.StartNew();
            string cacheFile = CalcCacheFileName(asset);
            if (force)
                FileUtil.DeleteFileOrDirectory(cacheFile);
            FetchDependCache(asset);
            UnityEngine.Debug.LogFormat("rebuild depend cache {0}, use time:{1}", asset, sw.ElapsedMilliseconds / 1000f);
        }
    }

    [MenuItem("Assets/AssetDependCache/Select Depends")]
    static void SelectDepends(MenuCommand cmd)
    {
        string asset = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(asset))
            return;

        string[] deps = GetDependencies(asset, true);
        UnityEngine.Object[] select = new UnityEngine.Object[deps.Length];
        for (int i = 0; i < deps.Length; ++i)
            select[i] = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(deps[i]);
        Selection.objects = select;
    }
}
