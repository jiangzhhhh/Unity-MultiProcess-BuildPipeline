using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Assertions;

namespace MultiBuild
{
    public class BuildTree
    {
        SortedDictionary<string, string> exportAssets = new SortedDictionary<string, string>();
        SortedDictionary<string, BundleNode> bundleNodes = new SortedDictionary<string, BundleNode>();
        SortedDictionary<string, AssetNode> assetNodes = new SortedDictionary<string, AssetNode>();

        void BuildDepency()
        {
            SortedDictionary<string, List<string>> buildMap = new SortedDictionary<string, List<string>>();
            foreach (var pair in exportAssets)
            {
                var asset = pair.Key;
                var bundle = pair.Value;

                List<string> assets;
                if (!buildMap.TryGetValue(bundle, out assets))
                {
                    assets = new List<string>();
                    buildMap.Add(bundle, assets);
                }
                assets.Add(asset);
            }
            foreach (var pair in buildMap)
            {
                var bundle = pair.Key;
                var assets = pair.Value;

                var bn = new BundleNode(bundle);
                bundleNodes.Add(bundle, bn);

                foreach (var asset in assets)
                {
                    var an = new AssetNode(asset, bn);
                    assetNodes.Add(asset, an);
                }
            }
            foreach (var an in assetNodes.Values)
            {
                string[] deps = AssetDatabase.GetDependencies(an.assetName, false);
                foreach (var dep in deps)
                {
                    AssetNode depN;
                    if (assetNodes.TryGetValue(dep, out depN))
                    {
                        an.AddDep(depN);
                    }
                }
            }
        }

        public void AddBuildAsset(string asset, string bundleName)
        {
            exportAssets[asset] = bundleName;
        }
        public AssetBundleBuild[][] BuildGroups(int jobs)
        {
            Assert.IsTrue(jobs > 0);

            BuildDepency();

            HashSet<HashSet<BundleNode>> groups = new HashSet<HashSet<BundleNode>>();
            Queue<BundleNode> que = new Queue<BundleNode>();
            foreach (var bn in bundleNodes.Values)
            {
                if (bn.deps.Count == 0)
                {
                    bn.group = new HashSet<BundleNode> { bn };
                    groups.Add(bn.group);
                    que.Enqueue(bn);
                }
            }
            while (que.Count > 0)
            {
                var front = que.Dequeue();
                foreach (var refer in front.refs)
                {
                    if (refer.group == null)
                    {
                        refer.group = front.group;
                        refer.group.Add(refer);
                    }
                    else if (refer.group != front.group)
                    {
                        front.group.UnionWith(refer.group);
                        groups.Remove(refer.group);
                        refer.group = front.group;
                    }
                    que.Enqueue(refer);
                }
            }

            while (groups.Count > jobs)
            {
                List<HashSet<BundleNode>> sortedGroups = new List<HashSet<BundleNode>>(groups);
                sortedGroups.Sort((a, b) => { return a.Count.CompareTo(b.Count); });

                var g1 = sortedGroups[0];
                var g2 = sortedGroups[1];
                g1.UnionWith(g2);
                groups.Remove(g2);
            }

            AssetBundleBuild[][] builds = new AssetBundleBuild[groups.Count][];
            int i = 0;
            foreach (var group in groups)
            {
                AssetBundleBuild[] set = new AssetBundleBuild[group.Count];
                int j = 0;
                foreach (var bn in group)
                {
                    var abb = set[j];
                    abb.assetBundleName = bn.bundleName;
                    abb.assetNames = new List<string>(bn.assets.Keys).ToArray();
                    set[j] = abb;
                    j++;
                }
                builds[i++] = set;
            }
            return builds;
        }
    }
}
