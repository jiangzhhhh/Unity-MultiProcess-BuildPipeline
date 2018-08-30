using System.Collections.Generic;
using UnityEditor;

static class Example
{
    [MenuItem("Example/Build")]
    static void Build()
    {
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

            {"Assets/Res/CubeC.prefab", new string[]{ "Assets/Res/CubeC.prefab" } },

            {"Assets/Res/CubeD.prefab", new string[]{
                "Assets/Res/CubeD.prefab",
                "Assets/Res/CubeD1.mat",
                "Assets/Res/CubeD2.mat",
                "Assets/Res/CubeD3.mat",
                "Assets/Res/CubeD4.mat",
                "Assets/Res/CubeD5.mat",
                "Assets/Res/CubeD6.mat",
                "Assets/Res/CubeD7.mat",
                "Assets/Res/CubeD8.mat",
                "Assets/Res/CubeD9.mat",
                "Assets/Res/CubeD10.mat",
            } },
        };

        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        foreach (var pair in buildSet)
        {
            AssetBundleBuild build = new AssetBundleBuild();
            build.assetBundleName = pair.Key;
            build.assetNames = pair.Value;
            builds.Add(build);
        }

        MultiProcessBuild.BuildPipeline.BuildAssetBundles("ab", builds.ToArray(), BuildAssetBundleOptions.None, BuildTarget.Android);
    }
}
