using System.Collections.Generic;

namespace MultiProcessBuild
{
    class BundleNode
    {
        public string bundleName { get; private set; }
        public Dictionary<string, AssetNode> assets { get; private set; }
        public HashSet<BundleNode> deps { get; private set; }

        public int weight = 0;

        public BundleNode(string bundleName)
        {
            this.bundleName = bundleName;
            this.assets = new Dictionary<string, AssetNode>();
            this.deps = new HashSet<BundleNode>();
        }

        public void AddAsset(AssetNode assetNode)
        {
            assets.Add(assetNode.assetName, assetNode);
            AddWeight(assetNode.assetName);
        }

        public void AddWeight(string asset)
        {
            this.weight += WeightTable.GetWeight(asset);
        }

        public void AddDep(BundleNode dep)
        {
            if (!this.deps.Contains(dep))
                this.deps.Add(dep);
        }
    }
}
