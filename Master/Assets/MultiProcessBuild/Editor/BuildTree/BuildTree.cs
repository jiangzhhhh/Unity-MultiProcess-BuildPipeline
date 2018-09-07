using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Assertions;

namespace MultiProcessBuild
{
    class BuildTree
    {
        SortedDictionary<string, string> exportAssets = new SortedDictionary<string, string>();
        SortedDictionary<string, BundleNode> bundleNodes = new SortedDictionary<string, BundleNode>();
        SortedDictionary<string, AssetNode> assetNodes = new SortedDictionary<string, AssetNode>();
        bool dirty = false;

        void BuildDependency()
        {
            if (!dirty)
                return;
            dirty = false;

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
                var bn = an.bundleNode;
                string[] deps = AssetDatabase.GetDependencies(an.assetName, true);
                foreach (var dep in deps)
                {
                    AssetNode depN;
                    if (assetNodes.TryGetValue(dep, out depN))
                        an.AddDep(depN);
                    else
                        bn.AddWeight(dep);
                }
            }
        }

        public void AddBuildAsset(string asset, string bundleName)
        {
            exportAssets[asset] = bundleName;
            dirty = true;
        }

        class BuildGroup : IEnumerable<BundleNode>
        {
            HashSet<BundleNode> set = new HashSet<BundleNode>();

            public void Add(BundleNode item)
            {
                this.set.Add(item);
                this.Weight += item.weight;
            }
            public void UnionWith(BuildGroup other)
            {
                this.set.UnionWith(other.set);
                this.Weight += other.Weight;
            }
            public bool Contains(BundleNode item) { return this.set.Contains(item); }
            IEnumerator<BundleNode> IEnumerable<BundleNode>.GetEnumerator() { return this.set.GetEnumerator(); }
            public IEnumerator GetEnumerator() { return this.set.GetEnumerator(); }
            public int Count { get { return this.set.Count; } }
            public int Weight { get; private set; }
        }

        public BuildJob[] BuildJobs(int nJobs, string output, BuildAssetBundleOptions options, BuildTarget target)
        {
            Assert.IsTrue(nJobs > 0);

            BuildDependency();

            HashSet<BuildGroup> groups = new HashSet<BuildGroup>();
            Func<BundleNode, BuildGroup> lookUp = (BundleNode node) =>
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

            foreach (var bundle in bundleNodes.Values)
            {
                var group = new BuildGroup();
                group.Add(bundle);
                groups.Add(group);
            }

            foreach (var bundle in bundleNodes.Values)
            {
                foreach (var dep in bundle.deps)
                    merge(bundle, dep);
            }

            while (groups.Count > nJobs)
            {
                List<BuildGroup> sortedGroups = new List<BuildGroup>(groups);
                sortedGroups.Sort((a, b) => { return a.Weight.CompareTo(b.Weight); });

                var g1 = sortedGroups[0];
                var g2 = sortedGroups[1];
                g1.UnionWith(g2);
                groups.Remove(g2);
            }

            List<BuildJob> jobs = new List<BuildJob>();
            foreach (var group in groups)
            {
                BuildJob job = new BuildJob();
                job.output = output;
                job.slaveID = jobs.Count;
                job.options = (int)options;
                job.target = (int)target;
                List<BuildJob.AssetBundleBuild> jobBuilds = new List<BuildJob.AssetBundleBuild>();
                foreach (BundleNode bn in group)
                {
                    var jobBuild = new BuildJob.AssetBundleBuild();
                    jobBuild.assetBundleName = bn.bundleName;
                    jobBuild.assetNames = new List<string>(bn.assets.Keys).ToArray();
                    jobBuild.weight = bn.weight;
                    job.weight += bn.weight;
                    jobBuilds.Add(jobBuild);
                }
                job.builds = jobBuilds.ToArray();
                jobs.Add(job);
            }
            return jobs.ToArray();
        }
    }
}
