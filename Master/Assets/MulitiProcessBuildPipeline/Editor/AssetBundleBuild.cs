using UnityEngine;

namespace MultiProcessBuildPipeline
{
    [System.Serializable]
    public class BuildJob
    {
        [System.Serializable]
        public class AssetBundleBuild{
            public string assetBundleName;
            public string[] assetNames;
        }

        public string output;
        [SerializeField]
        public AssetBundleBuild[] builds;
        public int options;
        public int target;
        public int slaveID;
    }


    [System.Serializable]
    public class Manifest{
        [System.Serializable]
        public class Bundle
        {
            public string name;
            public string[] dependency;
            public string hash;
            public long size;
        }

        public string output;
        public int buildAssetBundleOptions;
        public int buildTarget;
        public int slaveID;
        public Bundle[] data;
    }
}