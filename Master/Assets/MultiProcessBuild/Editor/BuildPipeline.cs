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
            BuildManifest result = new BuildManifest();
            result.buildTime = useTime;
            List<BuildManifest.AssetBundleBuild> bundles = new List<BuildManifest.AssetBundleBuild>();
            foreach (var name in manifest.GetAllAssetBundles())
            {
                BuildManifest.AssetBundleBuild bundle = new BuildManifest.AssetBundleBuild();
                bundle.assetBundleName = name;
                bundle.dependency = manifest.GetDirectDependencies(name);
                bundle.hash = manifest.GetAssetBundleHash(name).ToString();
                bundles.Add(bundle);
            }
            result.builds = bundles.ToArray();
            File.WriteAllText(resultFile, JsonUtility.ToJson(result, true));
        }

        static void BuildJobSlave()
        {
            string text = File.ReadAllText("./build.json");
            BuildJob job = JsonUtility.FromJson<BuildJob>(text);
            BuildJob(job);
        }

        static AssetBundleManifest BuildJob(BuildJob job)
        {
            long ot = System.DateTime.Now.Ticks;
            var unity_manifest = job.Build();
            string resultFile = string.Format("{0}/result_{1}.json", job.output, job.slaveID);
            OutputResult(resultFile, (System.DateTime.Now.Ticks - ot) / 10000000f, unity_manifest);
            return unity_manifest;
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
            var jobs = tree.BuildJobs(slaves.Count + 1, output, options, target);
            List<Process> pss = new List<Process>();
            for (int jobID = 1; jobID < jobs.Length; ++jobID)
            {
                int slaveID = jobID - 1;
                BuildJob job = jobs[jobID];
                string slaveProj = slaves[slaveID];
                File.WriteAllText(slaveProj + "/build.json", JsonUtility.ToJson(job, true));
                string cmd = string.Format(" -quit" +
                                           " -batchmode" +
                                           " -logfile {0}/log.txt" +
                                           " -projectPath {0} " +
                                           " -executeMethod MultiProcessBuild.BuildPipeline.BuildJobSlave",
                                           slaveProj);
                var ps = Process.Start(Unity, cmd);
                pss.Add(ps);
            }

            bool allFinish = true;
            if (jobs.Length > 0)
            {
                var job = jobs[0];
                File.WriteAllText("build.json", JsonUtility.ToJson(job, true));

                long ot = System.DateTime.Now.Ticks;
                var unity_manifest = BuildJob(job);
                if (unity_manifest == null)
                    allFinish = false;
            }
            for (int slaveID = 0; slaveID < pss.Count; ++slaveID)
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

        public static void OutputDependencyTree(string output, AssetBundleBuild[] builds)
        {
            var tree = new BuildTree();
            foreach (var build in builds)
            {
                foreach (var asset in build.assetNames)
                {
                    tree.AddBuildAsset(asset, build.assetBundleName);
                }
            }
            var dependencyTree = tree.GetDependencyTree();
            File.WriteAllText(output, JsonUtility.ToJson(dependencyTree, true));
        }
    }
}