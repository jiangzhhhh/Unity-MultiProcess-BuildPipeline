using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;

namespace MultiProcessBuild
{
    static class MultiProcess
    {
        public static int[] Start(Process[] pss, string title, string info)
        {
            int total = pss.Length;
            int[] exitCodes = new int[total];

            try
            {
                for (int i = 0; i < total; ++i)
                {
                    var ps = pss[i];
                    if (!ps.Start())
                        throw new System.Exception("Process Start Failed.");
                }

                while (true)
                {
                    int progress = pss.Count(x => x.HasExited);
                    if (EditorUtility.DisplayCancelableProgressBar(title, info, (float)progress / total))
                        throw new System.Exception("User Cancel.");
                    Thread.Sleep(200);
                    if (progress >= total)
                        break;
                }
                for (int i = 0; i < total; ++i)
                    exitCodes[i] = pss[i].ExitCode;
            }
            finally
            {
                for (int i = 0; i < total; ++i)
                    pss[i].Dispose();
                EditorUtility.ClearProgressBar();
            }

            return exitCodes;
        }

        public static int[] Start(string bin, string[] cmd, string title, string info)
        {
            Process[] pss = new Process[cmd.Length];
            for (int i = 0; i < cmd.Length; ++i)
            {
                var ps = new Process();
                ps.StartInfo.FileName = bin;
                ps.StartInfo.Arguments = cmd[i];
                pss[i] = ps;
            }
            return Start(pss, title, info);
        }

#if UNITY_EDITOR_WIN
        static void mklink(string source, string dest)
        {
            source = Path.GetFullPath(source);
            dest = Path.GetFullPath(dest);
            using (Process.Start("cmd", string.Format("/c mklink /j \"{0}\" \"{1}\"", dest, source))) { }
        }
#endif

        public static int[] UnityFork(string[] cmds, string title, string info)
        {
#if UNITY_EDITOR_WIN
            const string slaveRoot = "../Slaves";
            if (!Directory.Exists(slaveRoot))
                Directory.CreateDirectory(slaveRoot);
            int instanceCount = cmds.Length;
            for (int i = 0; i < instanceCount; ++i)
            {
                string slaveProject = string.Format("{0}/slave_{1}", slaveRoot, i);
                cmds[i] += " -projectPath " + Path.GetFullPath(slaveProject);
                if (!Directory.Exists(slaveProject))
                    Directory.CreateDirectory(slaveProject);
                if (!Directory.Exists(slaveProject + "/Assets"))
                    mklink("Assets", slaveProject + "/Assets");
                if (!Directory.Exists(slaveProject + "/ProjectSettings"))
                    mklink("ProjectSettings", slaveProject + "/ProjectSettings");
                if (!Directory.Exists(slaveProject + "/Library"))
                {
                    Directory.CreateDirectory(slaveProject + "/Library");
                    mklink("Library/metadata", slaveProject + "/Library/metadata");
                    mklink("Library/ShaderCache", slaveProject + "/Library/ShaderCache");
                    using (Process.Start("robocopy", string.Format("/s Library {0}/Library /xd metadata ShaderCache", slaveProject))) { }
                }
            }
            string Unity = EditorApplication.applicationPath;
            return Start(Unity, cmds, title, info);
#elif UNITY_EDITOR_OSX
            string Unity = EditorApplication.applicationPath + "/Contents/MacOS/Unity";
            const string UnityLockfile = "Temp/UnityLockfile";
            try
            {
                Directory.Move("Temp", "Temp_bak");
                int instanceCount = cmds.Length;
                Process[] pss = new Process[instanceCount];
                for (int i = 0; i < instanceCount; ++i)
                {
                    cmds[i] += " -projectPath " + Path.GetFullPath(".");
                    if (i > 0)
                    {
                        while (!File.Exists(UnityLockfile))
                            Thread.Sleep(200);
                        File.Delete(UnityLockfile);
                    }
                    var ps = Process.Start(Unity, cmds[i]);
                    pss[i] = ps;
                }
                return Start(pss, title, info);
            }
            finally
            {
                Directory.Move("Temp_bak", "Temp");
            }
#endif
        }
    }
}
