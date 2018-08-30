using System;
using System.Collections.Generic;

namespace MultiProcessBuild
{
    class BundleNode
    {
        public string bundleName { get; private set; }
        public Dictionary<string, AssetNode> assets { get; private set; }
        public HashSet<BundleNode> deps { get; private set; }
        public HashSet<BundleNode> refs { get; private set; }

        public HashSet<BundleNode> group;

        public BundleNode(string bundleName)
        {
            this.bundleName = bundleName;
            this.assets = new Dictionary<string, AssetNode>();
            this.deps = new HashSet<BundleNode>();
            this.refs = new HashSet<BundleNode>();
        }

        public void AddAsset(AssetNode assetNode)
        {
            assets.Add(assetNode.assetName, assetNode);
        }

        public void AddDep(BundleNode dep)
        {
            if (!this.deps.Contains(dep))
            {
                this.deps.Add(dep);
                dep.refs.Add(this);
            }
        }

        static void WalkAsset(AssetNode asset, Action<AssetNode> walker, HashSet<AssetNode> visited)
        {
            if (visited.Contains(asset))
                return;
            visited.Add(asset);
            walker(asset);
            foreach (var dep in asset.depends)
            {
                WalkAsset(dep, walker, visited);
            }
        }
        public void WalkAssets(Action<AssetNode> walker)
        {
            HashSet<AssetNode> visited = new HashSet<AssetNode>();
            foreach (var asset in assets.Values)
            {
                WalkAsset(asset, walker, visited);
            }
        }
    }
}
