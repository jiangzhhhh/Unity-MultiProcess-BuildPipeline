using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Assertions;

namespace MultiProcessBuild
{
    class BuildTree
    {
        SortedDictionary<string, string> exportAssets = new SortedDictionary<string, string>();
        SortedDictionary<string, BundleNode> bundleNodes = new SortedDictionary<string, BundleNode>();
        SortedDictionary<string, AssetNode> assetNodes = new SortedDictionary<string, AssetNode>();

        void BuildDependency()
        {
            bundleNodes.Clear();
            assetNodes.Clear();

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

            BuildDependency();

            HashSet<HashSet<BundleNode>> groups = new HashSet<HashSet<BundleNode>>();
            Func<BundleNode, HashSet<BundleNode>> lookUp = (BundleNode node) =>
            {
                foreach (var group in groups)
                    if (group.Contains(node))
                        return group;
                return null;
            };
            Action<BundleNode, BundleNode> merge = (a, b) =>
            {
                var group1 = lookUp(a);
                var group2 = lookUp(b);
                if (group1 != group2)
                {
                    group1.UnionWith(group2);
                    groups.Remove(group2);
                }
            };

            List<BundleNode> allBundles = new List<BundleNode>(bundleNodes.Values);
            foreach (var bundle in allBundles)
                groups.Add(new HashSet<BundleNode> { bundle });

            foreach (var bundle in allBundles)
            {
                foreach (var dep in bundle.deps)
                    merge(bundle, dep);
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
