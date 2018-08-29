using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiBuild
{
    public static class Builder
    {
        static int slaveCount = 3;
        static string output = "ab";

        [System.Serializable]
        public class AssetBundleJson
        {
            public string assetBundleName;
            public string[] assets;
        }
        [System.Serializable]
        public class AssetBundleBuildJson
        {
            public string output;
            public AssetBundleJson[] builds;
            public int flags;
            public int target;
        }

        [MenuItem("Build/Build")]
        static void Build()
        {
            BuildMaster(output, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        }

        [MenuItem("Build/CreateSlave")]
        static void CreateSlave()
        {
            for (int i = 0; i < slaveCount; ++i)
                CreateSlave(i);
        }

        static void MakeSymbolLink(string source, string dest)
        {
            UnityEngine.Debug.LogFormat("MakeLink: {0}->{1}", source, dest);
#if UNITY_EDITOR_WIN
            Process.Start(Path.GetFullPath("Tools/junction.exe"), string.Format("{0} {1}", dest, source));
#else
            Process.Start("ln", string.Format("-s {0} {1}", source, dest));
#endif
        }

        static void CreateSlave(int index)
        {
            string slaveDir = Path.GetFullPath("../");
            slaveDir = Path.Combine(slaveDir, string.Format("slave_{0}", index));
            if (Directory.Exists(slaveDir))
                return;
            Directory.CreateDirectory(slaveDir);
            MakeSymbolLink(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
            MakeSymbolLink(Path.GetFullPath("ProjectSettings"), Path.Combine(slaveDir, "ProjectSettings"));
        }

        static void BuildMaster(string output, BuildAssetBundleOptions flag, BuildTarget target)
        {
            output = Path.GetFullPath(output);

            SortedDictionary<string, string[]> buildSet = new SortedDictionary<string, string[]>
            {
                {"Assets/Res/64k.txt", new string[]{ "Assets/Res/64k.txt" } },

                {"Assets/Res/128k.txt", new string[]{ "Assets/Res/128k.txt"  } },

                {"Assets/Res/196k.txt", new string[]{ "Assets/Res/196k.txt" } },

                {"Assets/Res/CubeA.mat", new string[]{ "Assets/Res/CubeA.mat" } },
                {"Assets/Res/CubeA.prefab", new string[]{ "Assets/Res/CubeA.prefab" } },

                {"Assets/Res/CubeB.prefab", new string[]{ "Assets/Res/CubeB.prefab" } },
                {"Assets/Res/CubeB1.mat", new string[]{ "Assets/Res/CubeB1.mat" } },
                {"Assets/Res/CubeB2.mat", new string[]{ "Assets/Res/CubeB2.mat" } },
            };

            List<string> slaves = new List<string>();
            int i = 0;
            string slaveRoot = Path.GetFullPath("../");
            while (true)
            {
                string slaveProj = Path.Combine(slaveRoot, string.Format("slave_{0}", i++));
                if (!Directory.Exists(slaveProj))
                    break;
                slaves.Add(slaveProj);
            }

            var tree = new MultiBuild.BuildTree();
            foreach (var kv in buildSet)
            {
                string bundleName = kv.Key;
                string[] assets = kv.Value;
                foreach (var asset in assets)
                {
                    tree.AddBuildAsset(asset, bundleName);
                }
            }

            string Unity = EditorApplication.applicationPath;
            Process[] pss = new Process[slaves.Count];
            var jobBuilds = tree.BuildGroups(slaves.Count + 1);
            for (int jobID = 1; jobID < jobBuilds.Length; ++jobID)
            {
                var jobBuild = jobBuilds[jobID];
                List<AssetBundleJson> jsonBuilds = new List<AssetBundleJson>();
                foreach (var build in jobBuild)
                {
                    AssetBundleJson jsonBuild = new AssetBundleJson();
                    jsonBuild.assetBundleName = build.assetBundleName;
                    jsonBuild.assets = build.assetNames;
                    jsonBuilds.Add(jsonBuild);
                }
                AssetBundleBuildJson json = new AssetBundleBuildJson();
                json.output = output;
                json.builds = jsonBuilds.ToArray();
                json.flags = (int)flag;
                json.target = (int)target;
                string jsonTxt = JsonUtility.ToJson(json, true);

                string slaveProj = slaves[jobID - 1];
                File.WriteAllText(slaveProj + "/build.json", jsonTxt);
                string cmd = string.Format("-quit -batchmode -logfile {0}/log.txt -projectPath {0} -executeMethod MultiBuild.Builder.BuildJobSlave", slaveProj);
                var ps = Process.Start(Unity, cmd);
                pss[jobID - 1] = ps;
            }

            if (jobBuilds.Length > 0)
            {
                BuildJob(output, jobBuilds[0], flag, target);
            }

            foreach (var ps in pss)
            {
                ps.WaitForExit();
            }

            UnityEngine.Debug.LogFormat("build success.");
        }

        public static void BuildJobSlave()
        {
            string jsonTxt = File.ReadAllText("./build.json");
            AssetBundleBuildJson json = JsonUtility.FromJson<AssetBundleBuildJson>(jsonTxt);

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            foreach (var jsonBuild in json.builds)
            {
                AssetBundleBuild build = new AssetBundleBuild();
                build.assetBundleName = jsonBuild.assetBundleName;
                build.assetNames = jsonBuild.assets;
                builds.Add(build);
            }

            BuildAssetBundleOptions flag = (BuildAssetBundleOptions)json.flags;
            BuildTarget target = (BuildTarget)json.target;
            BuildJob(json.output, builds.ToArray(), flag, target);
        }

        static void BuildJob(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions flag, BuildTarget target)
        {
            if (!Directory.Exists(output))
                Directory.CreateDirectory(output);
            BuildPipeline.BuildAssetBundles(output, builds, flag, target);
        }
    }
}