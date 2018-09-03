using System.Collections.Generic;
using System.IO;

namespace MultiProcessBuild
{
    static class WeightTable
    {
        public static int defaultWeight = 30;
        public static Dictionary<string, int> presetWeightTable = new Dictionary<string, int>
        {
            {".anim"   ,   1  },
            {".mat"    ,   19 },
            {".shader" ,   21 },
            {".prefab" ,   33 },
            {".asset"  ,   33 },
            {".fbx"    ,   35 },
            //txt
            {".txt"    ,   48 },
            {".bytes"  ,   49 },
            //texture
            {".jpg", 81},
            {".jpeg", 81},
            {".tif", 81},
            {".tiff", 81},
            {".tga", 81},
            {".gif", 81},
            {".png", 81},
            {".psd", 81},
            {".bmp", 81},
            {".iff", 81},
            {".pict", 81},
            {".pic", 81},
            {".pct", 81},
            {".exr", 81},
            //scene
            {".unity3d",   100 },
        };

        public static int GetWeight(string assetName)
        {
            string ext = Path.GetExtension(assetName).ToLower();
            int w = 0;
            if (!presetWeightTable.TryGetValue(ext, out w))
                w = defaultWeight;
            return w;
        }
    }
}
