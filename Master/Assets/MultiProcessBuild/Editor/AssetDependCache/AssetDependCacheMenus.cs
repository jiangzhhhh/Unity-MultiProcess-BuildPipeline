using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    public static partial class AssetDependCache
    {
        [MenuItem("Assets/Asset Depend/Cache/Rebuild Depend Cache All", false, 0)]
        static void RebuildDependCacheAllSoft(MenuCommand cmd) { RebuildDependCacheAll(false); }

        [MenuItem("Assets/Asset Depend/Cache/Rebuild Depend Cache", false, 1)]
        static void RebuildDependCacheSoft(MenuCommand cmd) { RebuildDependCache(false); }

        [MenuItem("Assets/Asset Depend/Cache/Rebuild Depend Cache All(force)", false, 2)]
        static void RebuildDependCacheAllHard(MenuCommand cmd) { RebuildDependCacheAll(true); }

        [MenuItem("Assets/Asset Depend/Cache/Rebuild Depend Cache(Force)", false, 3)]
        static void RebuildDependCacheHard(MenuCommand cmd) { RebuildDependCache(true); }

        static void RebuildDependCacheAll(bool force)
        {
            Stopwatch sw = Stopwatch.StartNew();

            if (force)
                FileUtil.DeleteFileOrDirectory(CACHE_DIR);

            HashSet<string> ignore = new HashSet<string>()
            {
                ".anim",
                ".bytes",
                ".txt",
                ".json",

                ".jpg",
                ".jpeg",
                ".tif",
                ".tiff",
                ".tga",
                ".gif",
                ".png",
                ".psd",
                ".bmp",
                ".iff",
                ".pict",
                ".pic",
                ".pct",
                ".exr",
            };

            var all = AssetDatabase.GetAllAssetPaths()
                .Where(x => x.StartsWith("Assets"))
                .Where(x => !AssetDatabase.IsValidFolder(x))
                .Where(x => !ignore.Contains(Path.GetExtension(x).ToLower()))
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
                    bool isCancel = EditorUtility.DisplayCancelableProgressBar("depend cache rebuilding", path, (float)index / total);
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

        [MenuItem("Assets/Asset Depend Cache/Select Depends")]
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

        [MenuItem("Assets/Asset Depend/Find Reference")]
        static void FindReference(MenuCommand cmd)
        {
            string asset = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(asset))
                return;
            FindReference(asset);
        }

        public static void FindReference(string asset)
        {
            Stopwatch sw = Stopwatch.StartNew();

            HashSet<string> containers = new HashSet<string>() { ".controller", ".unity", ".asset", ".prefab", ".mat", ".shader", ".cginc" };
            var all = AssetDatabase.GetAllAssetPaths()
                .Where(x => x.StartsWith("Assets"))
                .Where(x => !AssetDatabase.IsValidFolder(x))
                .Where(x => containers.Contains(Path.GetExtension(x).ToLower()))
                .ToArray();

            SortedList<string, string> result = new SortedList<string, string>();
            int index = 0;
            int total = all.Length;
            EditorApplication.CallbackFunction update = null;
            update = delegate ()
            {
                for (int i = 0; i < 512; ++i)
                {
                    string path = all[index++];
                    if (result.ContainsKey(path))
                        continue;

                    string[] deps = GetDependencies(path, false);
                    if (Array.IndexOf(deps, asset) != -1)
                    {
                        result.Add(path, path);
                        UnityEngine.Debug.LogFormat(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path), "{0}", path);
                    }

                    bool isCancel = EditorUtility.DisplayCancelableProgressBar("find reference...", path, (float)index / total);
                    if (isCancel || index >= total)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorApplication.update -= update;
                        if (index >= total)
                            UnityEngine.Debug.LogFormat("find reference, use time:{0}", sw.ElapsedMilliseconds / 1000f);
                        return;
                    }
                }
            };
            EditorApplication.update -= update;
            EditorApplication.update += update;
        }
    }
}
