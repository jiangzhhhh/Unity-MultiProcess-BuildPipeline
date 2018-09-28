using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace AssetFinder
{
    class CACHE
    {
        public Hash128 dependencyHash;
        public string[] depends;
    }

    public static partial class AssetDependCache
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
            if (!results.ContainsKey(pathName))
                results.Add(pathName, pathName);

            var cache = FetchDependCache(pathName, false);
            foreach (var dep in cache.depends)
            {
                if (!results.ContainsKey(dep))
                    results.Add(dep, dep);

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
    }
}
