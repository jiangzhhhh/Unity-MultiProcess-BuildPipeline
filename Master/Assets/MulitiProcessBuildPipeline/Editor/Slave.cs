using System.IO;
using UnityEditor;

using Process = System.Diagnostics.Process;

namespace MultiProcessBuildPipeline
{
    public static partial class BuildPipeline{
        [MenuItem("MultiProcessBuildPipeline/CreateSlave")]
        static void CreateSlave()
        {
            for (int i = 0; i < Profile.slaveCount; ++i)
                CreateSlave(i);
        }

        static void MakeSymbolLink(string source, string dest)
        {
            UnityEngine.Debug.LogFormat("MakeLink: {0}->{1}", source, dest);
#if UNITY_EDITOR_WIN
            Process.Start(Path.GetFullPath("Tools/junction.exe"), string.Format("{0} {1}", dest, source));
#else
            Process.Start("ln", string.Format("-s {0} {1}", source, dest));
#endif
        }

        static void CreateSlave(int index)
        {
            string slaveDir = Path.GetFullPath(Profile.slaveRoot);
            slaveDir = Path.Combine(slaveDir, string.Format("slave_{0}", index));
            if (Directory.Exists(slaveDir))
                return;
            Directory.CreateDirectory(slaveDir);
            MakeSymbolLink(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
            MakeSymbolLink(Path.GetFullPath("ProjectSettings"), Path.Combine(slaveDir, "ProjectSettings"));
        }
    }
}