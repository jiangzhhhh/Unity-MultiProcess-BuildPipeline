using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuildPipeline
{
    public static partial class BuildPipeline
    {
        public static void BuildJobSlave()
        {
            string jsonTxt = File.ReadAllText("./build.json");
            BuildJob json = JsonUtility.FromJson<BuildJob>(jsonTxt);

            List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
            foreach (var jsonBuild in json.builds)
            {
                AssetBundleBuild build = new AssetBundleBuild();
                build.assetBundleName = jsonBuild.assetBundleName;
                build.assetNames = jsonBuild.assetNames;
                builds.Add(build);
            }

            BuildAssetBundleOptions flag = (BuildAssetBundleOptions)json.options;
            BuildTarget target = (BuildTarget)json.target;
            var unity_manifest = BuildJob(json.output, builds.ToArray(), flag, target);

            Manifest manifest = new Manifest();
            manifest.output = json.output;
            manifest.buildAssetBundleOptions = json.options;
            manifest.buildTarget = json.target;
            manifest.slaveID = json.slaveID;
            List<Manifest.Bundle> bundles = new List<Manifest.Bundle>();
            foreach(var name in unity_manifest.GetAllAssetBundles()){
                Manifest.Bundle bundle = new Manifest.Bundle();
                bundle.name = name;
                bundle.dependency = unity_manifest.GetDirectDependencies(name);
                bundle.hash = unity_manifest.GetAssetBundleHash(name).ToString();
                bundles.Add(bundle);
            }
            manifest.data = bundles.ToArray();
            File.WriteAllText(string.Format("{0}/manifest_{1}.json", json.output, json.slaveID), JsonUtility.ToJson(manifest, true));
        }

        static AssetBundleManifest BuildJob(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions options, BuildTarget target)
        {
            if (!Directory.Exists(output))
                Directory.CreateDirectory(output);
			return UnityEditor.BuildPipeline.BuildAssetBundles(output, builds, options, target);
        }

        public static void BuildAssetBundles(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions options, BuildTarget target){
            output = Path.GetFullPath(output);

            var tree = new BuildTree();
            foreach(var build in builds){
                foreach(var asset in build.assetNames){
                    tree.AddBuildAsset(asset, build.assetBundleName);
                }
            }

            List<string> slaves = new List<string>();
            int i = 0;
            string slaveRoot = Path.GetFullPath(Profile.slaveRoot);
            while (true)
            {
                string slaveProj = Path.Combine(slaveRoot, string.Format("slave_{0}", i++));
                if (!Directory.Exists(slaveProj))
                    break;
                slaves.Add(slaveProj);
            }

            string Unity = EditorApplication.applicationPath;
            Process[] pss = new Process[slaves.Count];
            var jobBuilds = tree.BuildGroups(slaves.Count + 1);
            for (int jobID = 1; jobID < jobBuilds.Length; ++jobID)
            {
                int slaveID = jobID - 1;
                var jobBuild = jobBuilds[jobID];
                BuildJob.AssetBundleBuild[] myBuilds = new BuildJob.AssetBundleBuild[jobBuild.Length];
                for (int j = 0; j < jobBuild.Length;++j){
                    var x = new BuildJob.AssetBundleBuild();
                    x.assetBundleName = jobBuild[j].assetBundleName;
                    x.assetNames = jobBuild[j].assetNames;
                    myBuilds[j] = x;
                }

                BuildJob json = new BuildJob();
                json.output = output;
                json.builds = myBuilds;
                json.options = (int)options;
                json.target = (int)target;
                json.slaveID = slaveID;
                string jsonTxt = JsonUtility.ToJson(json, true);

                string slaveProj = slaves[slaveID];
                File.WriteAllText(slaveProj + "/build.json", jsonTxt);
                string cmd = string.Format("-quit" +
                                           "-batchmode" +
                                           "-logfile {0}/log.txt" +
                                           "-projectPath {0} " +
                                           "-executeMethod MultiProcessBuildPipeline.BuildPipeline.BuildJobSlave",
                                           slaveProj);
                var ps = Process.Start(Unity, cmd);
                pss[slaveID] = ps;
            }

            if (jobBuilds.Length > 0)
            {
                BuildJob(output, jobBuilds[0], options, target);
            }

            foreach (var ps in pss)
            {
                ps.WaitForExit();
                ps.Dispose();
            }

            UnityEngine.Debug.LogFormat("build success. slave:{0}", slaves.Count);
        }
    }
}