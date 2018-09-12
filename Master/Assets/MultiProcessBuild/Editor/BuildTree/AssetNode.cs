using System.Collections.Generic;

namespace MultiProcessBuild
{
    class AssetNode
    {
        public string assetName { get; private set; }
        public BundleNode bundleNode { get; private set; }

        public AssetNode(string assetName, BundleNode bundleNode)
        {
            this.assetName = assetName;
            this.bundleNode = bundleNode;
            bundleNode.AddAsset(this);
        }

        public void AddDep(AssetNode depNode)
        {
            if (this.bundleNode != depNode.bundleNode)
                this.bundleNode.AddDep(depNode.bundleNode);
        }
    }
}
