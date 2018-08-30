using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    public static partial class BuildPipeline
    {
        static void OutputResult(string resultFile, float useTime, AssetBundleManifest manifest)
        {
            BuildResult result = new BuildResult();
            result.buildTime = useTime;
            List<BuildResult.Bundle> bundles = new List<BuildResult.Bundle>();
            foreach (var name in manifest.GetAllAssetBundles())
            {
                BuildResult.Bundle bundle = new BuildResult.Bundle();
                bundle.name = name;
                bundle.dependency = manifest.GetDirectDependencies(name);
                bundle.hash = manifest.GetAssetBundleHash(name).ToString();
                bundles.Add(bundle);
            }
            result.bundles = bundles.ToArray();
            File.WriteAllText(resultFile, JsonUtility.ToJson(result, true));
        }

        static void BuildJobSlave()
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
            float old_time = Time.realtimeSinceStartup;
            var unity_manifest = BuildJob(json.output, builds.ToArray(), flag, target);

            string resultFile = string.Format("{0}/result_{1}.json", json.output, json.slaveID + 1);
            OutputResult(resultFile, Time.realtimeSinceStartup - old_time, unity_manifest);
        }

        static AssetBundleManifest BuildJob(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions options, BuildTarget target)
        {
            if (!Directory.Exists(output))
                Directory.CreateDirectory(output);
            return UnityEditor.BuildPipeline.BuildAssetBundles(output, builds, options, target);
        }

        public static void BuildAssetBundles(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions options, BuildTarget target)
        {
            output = Path.GetFullPath(output);

            var tree = new BuildTree();
            foreach (var build in builds)
            {
                foreach (var asset in build.assetNames)
                {
                    tree.AddBuildAsset(asset, build.assetBundleName);
                }
            }

            List<string> slaves = new List<string>();
            int i = 0;
            string slaveRoot = Path.GetFullPath(Profile.SlaveRoot);
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
                for (int j = 0; j < jobBuild.Length; ++j)
                {
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
                string cmd = string.Format(" -quit" +
                                           " -batchmode" +
                                           " -logfile {0}/log.txt" +
                                           " -projectPath {0} " +
                                           " -executeMethod MultiProcessBuild.BuildPipeline.BuildJobSlave",
                                           slaveProj);
                var ps = Process.Start(Unity, cmd);
                pss[slaveID] = ps;
            }

            if (jobBuilds.Length > 0)
            {
                float old_time = Time.realtimeSinceStartup;
                var unity_manifest = BuildJob(output, jobBuilds[0], options, target);
                string resultFile = string.Format("{0}/result_{1}.json", output, 0);
                OutputResult(resultFile, Time.realtimeSinceStartup - old_time, unity_manifest);
            }

            bool allFinish = true;
            for (int slaveID = 0; slaveID < pss.Length; ++slaveID)
            {
                var ps = pss[slaveID];
                ps.WaitForExit();
                var ExitCode = ps.ExitCode;
                if (ExitCode != 0)
                {
                    allFinish = false;
                    UnityEngine.Debug.LogErrorFormat("slave {0} code:{1}", slaveID, ExitCode);
                }
                else
                    UnityEngine.Debug.LogFormat("slave {0} code:{1}", slaveID, ExitCode);
                ps.Dispose();
            }

            if (allFinish)
                UnityEngine.Debug.LogFormat("all slave finish.");
            else
                UnityEngine.Debug.LogErrorFormat("some slave error.");
        }
    }
}