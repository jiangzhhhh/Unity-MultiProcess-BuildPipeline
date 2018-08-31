using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    [System.Serializable]
    public class BuildJob
    {
        [System.Serializable]
        public class AssetBundleBuild
        {
            public string assetBundleName;
            public string[] assetNames;
        }
        public string output;
        [SerializeField]
        public AssetBundleBuild[] builds;
        public int options;
        public int target;
        public int slaveID;

        public AssetBundleManifest Build()
        {
            if (!Directory.Exists(this.output))
                Directory.CreateDirectory(this.output);
            List<UnityEditor.AssetBundleBuild> builds = new List<UnityEditor.AssetBundleBuild>();
            foreach (var v in this.builds)
            {
                UnityEditor.AssetBundleBuild build = new UnityEditor.AssetBundleBuild();
                build.assetBundleName = v.assetBundleName;
                build.assetNames = v.assetNames;
                builds.Add(build);
            }
            return UnityEditor.BuildPipeline.BuildAssetBundles(this.output, builds.ToArray(), (BuildAssetBundleOptions)this.options, (BuildTarget)this.target);
        }
    }

    [System.Serializable]
    public class BuildManifest
    {
        [System.Serializable]
        public class AssetBundleBuild
        {
            public string assetBundleName;
            public string[] dependency;
            public string hash;
        }
        [SerializeField]
        public AssetBundleBuild[] builds;
        public float buildTime;
    }

    [System.Serializable]
    public class DepencdencyTree
    {
        [System.Serializable]
        public class Bundle
        {
            public string assetBundleName;
            public string[] assetNames;
            public string[] dependency;
            public int weight;
        }
        [SerializeField]
        public Bundle[] bundles;
    }
}