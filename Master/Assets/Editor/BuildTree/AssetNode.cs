using System.Collections.Generic;

namespace MultiBuild
{
    public class AssetNode
    {
        public string assetName { get; private set; }
        public HashSet<AssetNode> depends { get; private set; }         //引用别人
        public HashSet<AssetNode> references { get; private set; }      //被别人引用
        public BundleNode bundleNode { get; private set; }

        public AssetNode(string assetName, BundleNode bundleNode)
        {
            this.assetName = assetName;
            this.bundleNode = bundleNode;
            this.depends = new HashSet<AssetNode>();
            this.references = new HashSet<AssetNode>();

            bundleNode.AddAsset(this);
        }

        public void AddDep(AssetNode depNode)
        {
            this.depends.Add(depNode);
            depNode.references.Add(this);

            if (this.bundleNode != depNode.bundleNode)
                this.bundleNode.AddDep(depNode.bundleNode);
        }
    }
}
