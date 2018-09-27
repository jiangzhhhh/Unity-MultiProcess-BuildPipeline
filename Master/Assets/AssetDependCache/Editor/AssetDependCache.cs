using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

class CACHE
{
    public Hash128 dependencyHash;
    public string[] depends;
}

public static class AssetDependCache
{
    static readonly string CACHE_DIR = "Library/DependCache";
    static Dictionary<Hash128, CACHE> cacheInMemory = new Dictionary<Hash128, CACHE>();

    static string CalcCacheFileName(Hash128 hash)
    {
        string digi = hash.ToString();
        string bucketDir = string.Format("{0}{1}", digi[0], digi[1]);
        return string.Format("{0}/{1}/{2}", CACHE_DIR, bucketDir, digi);
    }

    static CACHE ReadCache(string file)
    {
        if (!File.Exists(file))
            return null;

        using (FileStream fs = File.OpenRead(file))
        {
            CACHE cache = new CACHE();
            BinaryReader br = new BinaryReader(fs);
            uint i0 = br.ReadUInt32();
            uint i1 = br.ReadUInt32();
            uint i2 = br.ReadUInt32();
            uint i3 = br.ReadUInt32();
            cache.dependencyHash = new Hash128(i0, i1, i2, i3);
            ushort size = br.ReadUInt16();
            cache.depends = new string[size];
            for (int i = 0; i < size; ++i)
                cache.depends[i] = br.ReadString();
            return cache;
        }
    }

    static void WriteCache(string file, CACHE cache)
    {
        string dir = Path.GetDirectoryName(file);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (FileStream fs = File.OpenWrite(file))
        {
            BinaryWriter bw = new BinaryWriter(fs);
            byte[] buf = new byte[16];
            {
                IntPtr ptr = Marshal.AllocHGlobal(16);
                Marshal.StructureToPtr(cache.dependencyHash, ptr, false);
                Marshal.Copy(ptr, buf, 0, 16);
                Marshal.FreeHGlobal(ptr);
            }
            bw.Write(buf);
            bw.Write((ushort)cache.depends.Length);
            foreach (var dep in cache.depends)
                bw.Write(dep);
        }
    }

    static CACHE FetchDependCache(string pathName, bool recursive)
    {
        CACHE cache = null;
        Hash128 depHash = AssetDatabase.GetAssetDependencyHash(pathName);

        //memory
        if (cacheInMemory.TryGetValue(depHash, out cache) && cache.dependencyHash == depHash)
            return cache;
        else
            cache = null;

        //disk
        string cacheFile = CalcCacheFileName(depHash);
        cache = ReadCache(cacheFile);
        if (cache != null && cache.dependencyHash == depHash)
        {
            cacheInMemory[depHash] = cache;
            return cache;
        }
        else
            cache = null;

        //rebuild
        if (cache == null)
        {
            cache = new CACHE();
            cache.dependencyHash = depHash;
            cache.depends = AssetDatabase.GetDependencies(pathName, false);
            WriteCache(cacheFile, cache);
            cacheInMemory[depHash] = cache;

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

        var cache = FetchDependCache(pathName, false);
        foreach (var dep in cache.depends)
        {
            if (!results.ContainsKey(dep))
                results.Add(dep, dep);
        }

        foreach (var dep in cache.depends)
        {
            var cache2 = FetchDependCache(dep, false);
            foreach (var dep2 in cache2.depends)
                GetDependenciesRecursive(dep2, results, visited);
        }
    }

    public static string[] GetDependencies(string pathName, bool recursive = false)
    {
        var cache = FetchDependCache(pathName, false);
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
            Hash128 depHash = AssetDatabase.GetAssetDependencyHash(asset);
            string cacheFile = CalcCacheFileName(depHash);
            if (force)
                FileUtil.DeleteFileOrDirectory(cacheFile);
            FetchDependCache(asset, true);
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

    [MenuItem("Assets/AssetDependCache/Benchmark")]
    static void Benchmark(MenuCommand cmd)
    {
        var all = AssetDatabase.GetAllAssetPaths()
            .Where(x => x.StartsWith("Assets"))
            .Where(x => !AssetDatabase.IsValidFolder(x))
            .Where(x => !x.EndsWith(".cs"))
            .ToArray();

        {
            FileUtil.DeleteFileOrDirectory(CACHE_DIR);
            Stopwatch sw = Stopwatch.StartNew();
            foreach (var asset in all)
                GetDependencies(asset);
            sw.Stop();
            UnityEngine.Debug.LogFormat("no cache:{0}", sw.ElapsedMilliseconds / 1000f);
        }

        {
            cacheInMemory.Clear();
            Stopwatch sw = Stopwatch.StartNew();
            foreach (var asset in all)
                GetDependencies(asset);
            sw.Stop();
            UnityEngine.Debug.LogFormat("disk cache:{0}", sw.ElapsedMilliseconds / 1000f);
        }

        {
            Stopwatch sw = Stopwatch.StartNew();
            foreach (var asset in all)
                GetDependencies(asset);
            sw.Stop();
            UnityEngine.Debug.LogFormat("memory cache:{0}", sw.ElapsedMilliseconds / 1000f);
        }

        {
           Stopwatch sw = Stopwatch.StartNew();
           foreach (var asset in all)
               AssetDatabase.GetDependencies(asset);
           sw.Stop();
           UnityEngine.Debug.LogFormat("orign :{0}", sw.ElapsedMilliseconds / 1000f);
        }
    }
}
