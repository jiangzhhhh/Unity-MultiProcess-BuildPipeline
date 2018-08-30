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
    }


    [System.Serializable]
    public class BuildResult
    {
        [System.Serializable]
        public class Bundle
        {
            public string name;
            public string[] dependency;
            public string hash;
        }

        public float buildTime;
        public Bundle[] bundles;
    }
}