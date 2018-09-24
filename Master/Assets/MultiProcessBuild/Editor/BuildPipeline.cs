using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    public static partial class BuildPipeline
    {
        static AssetBundleManifest OutputResult(string resultFile, float useTime, UnityEngine.AssetBundleManifest manifest)
        {
            if (manifest == null)
                return null;

            AssetBundleManifest result = new AssetBundleManifest();
            result.buildTime = useTime;
            List<AssetBundleManifest.AssetBundleBuild> bundles = new List<AssetBundleManifest.AssetBundleBuild>();
            foreach (var name in manifest.GetAllAssetBundles())
            {
                AssetBundleManifest.AssetBundleBuild bundle = new AssetBundleManifest.AssetBundleBuild();
                bundle.assetBundleName = name;
                bundle.dependency = manifest.GetDirectDependencies(name);
                bundle.hash = manifest.GetAssetBundleHash(name).ToString();
                bundles.Add(bundle);
            }
            result.builds = bundles.ToArray();
            File.WriteAllText(resultFile, JsonUtility.ToJson(result, true));
            return result;
        }

        [MenuItem("MultiProcessBuild/Build With build.json")]
        static void BuildJobSlave()
        {
            string buildJobPath = "./build.json";
            string[] CommandLineArgs = System.Environment.GetCommandLineArgs();
            int i = ArrayUtility.FindIndex<string>(CommandLineArgs, (x) => x == "-buildJob");
            if (i != -1)
                buildJobPath = CommandLineArgs[i + 1];
            string text = File.ReadAllText(buildJobPath);
            BuildJob job = JsonUtility.FromJson<BuildJob>(text);
            BuildJob(job);
        }

        static AssetBundleManifest BuildJob(BuildJob job)
        {
            var sw = new Stopwatch();
            var unity_manifest = job.Build();
            string resultFile = string.Format("{0}/result_{1}.json", job.output, job.slaveID);
            sw.Stop();
            return OutputResult(resultFile, sw.UseSecs, unity_manifest);
        }

        public static AssetBundleManifest BuildAssetBundles(string output, AssetBundleBuild[] builds, BuildAssetBundleOptions options, BuildTarget target)
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

            var jobs = tree.BuildJobs(Profile.SlaveCount + 1, output, options, target);
            AssetBundleManifest[] results = new AssetBundleManifest[jobs.Length];
            List<string> cmds = new List<string>();
            for (int jobID = 1; jobID < jobs.Length; ++jobID)
            {
                int slaveID = jobID - 1;
                BuildJob job = jobs[jobID];
                File.WriteAllText(string.Format("build_{0}.json", jobID), JsonUtility.ToJson(job, true));

                if ((options & BuildAssetBundleOptions.DryRunBuild) == 0)
                {
                    string cmd = string.Format(" -quit" +
                                           " -batchmode" +
                                           " -logfile {0}/log_{1}.txt" +
                                           " -projectPath {0} " +
                                           " -executeMethod MultiProcessBuild.BuildPipeline.BuildJobSlave" +
                                           " -buildJob {0}/build_{1}.json",
                                           Path.GetFullPath("."), jobID);
                    cmds.Add(cmd);
                }
            }

            bool allFinish = true;
            if (jobs.Length > 0)
            {
                var job = jobs[0];
                File.WriteAllText("build_0.json", JsonUtility.ToJson(job, true));

                if ((options & BuildAssetBundleOptions.DryRunBuild) == 0)
                {
                    var result = BuildJob(job);
                    results[0] = result;
                    if (result == null)
                        allFinish = false;
                }
            }

            //dryrun return null
            if ((options & BuildAssetBundleOptions.DryRunBuild) != 0)
                return null;

            MultiProcess.UnityFork(cmds.ToArray(), "building", "waiting for sub process...", (ps, idx) =>
            {
                var ExitCode = ps.ExitCode;
                if (ExitCode != 0)
                {
                    allFinish = false;
                    UnityEngine.Debug.LogErrorFormat("slave {0} code:{1}", idx, ExitCode);
                }
                else
                {
                    UnityEngine.Debug.LogFormat("slave {0} code:{1}", idx, ExitCode);
                    string resultFile = string.Format(string.Format("{0}/result_{1}.json", output, idx + 1));
                    results[idx + 1] = JsonUtility.FromJson<AssetBundleManifest>(File.ReadAllText(resultFile));
                }
            });

            if (allFinish)
            {
                AssetBundleManifest manifest = new AssetBundleManifest();
                manifest.buildTime = 0f;
                List<AssetBundleManifest.AssetBundleBuild> totalBuilds = new List<AssetBundleManifest.AssetBundleBuild>();
                foreach (var result in results)
                {
                    totalBuilds.AddRange(result.builds);
                    manifest.buildTime = Mathf.Max(manifest.buildTime, result.buildTime);
                }
                totalBuilds.Sort((a, b) => { return a.assetBundleName.CompareTo(b.assetBundleName); });
                manifest.builds = totalBuilds.ToArray();
                File.WriteAllText(string.Format("{0}/result.json", output), JsonUtility.ToJson(manifest, true));
                UnityEngine.Debug.LogFormat("all slave finish.");
                return manifest;
            }
            else
            {
                UnityEngine.Debug.LogErrorFormat("some slave error.");
                return null;
            }
        }
    }
}