using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

static class Example
{
    [MenuItem("Example/Build")]
    static void Build(){
        SortedDictionary<string, string[]> buildSet = new SortedDictionary<string, string[]>
        {
            {"Assets/Res/64k.txt", new string[]{ "Assets/Res/64k.txt" } },

            {"Assets/Res/128k.txt", new string[]{ "Assets/Res/128k.txt"  } },

            {"Assets/Res/196k.txt", new string[]{ "Assets/Res/196k.txt" } },

            {"Assets/Res/CubeA.mat", new string[]{ "Assets/Res/CubeA.mat" } },
            {"Assets/Res/CubeA.prefab", new string[]{ "Assets/Res/CubeA.prefab" } },

            {"Assets/Res/CubeB.prefab", new string[]{ "Assets/Res/CubeB.prefab" } },
            {"Assets/Res/CubeB1.mat", new string[]{ "Assets/Res/CubeB1.mat" } },
            {"Assets/Res/CubeB2.mat", new string[]{ "Assets/Res/CubeB2.mat" } },
        };

        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        foreach(var pair in buildSet){
            AssetBundleBuild build = new AssetBundleBuild();
            build.assetBundleName = pair.Key;
            build.assetNames = pair.Value;
            builds.Add(build);
        }

        MultiProcessBuildPipeline.BuildPipeline.BuildAssetBundles("ab", builds.ToArray(), BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
    }
}
