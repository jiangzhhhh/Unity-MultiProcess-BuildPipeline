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

        static void mklink(string source, string dest)
        {
            source = Path.GetFullPath(source);
            dest = Path.GetFullPath(dest);
#if UNITY_EDITOR_WIN
            using (Process.Start("cmd", string.Format("/c mklink /j \"{0}\" \"{1}\"", dest, source))) { }
#elif UNITY_EDITOR_OSX
            using (Process.Start("ln", string.Format("-sf \"{0}\" \"{1}\"", source, dest))) { }
#endif
        }

        static void rsync(string source, string dest, params string[] ignores)
        {
#if UNITY_EDITOR_WIN
            string ignore = "";
            if (ignores.Length > 0)
            {
                ignore = "/xd";
                foreach (var x in ignores)
                    ignore += x + " ";
            }
            string arg = string.Format("/s {0} {1} {2}", source, dest, ignore);
            using (Process.Start("robocopy", arg)) { }
#elif UNITY_EDITOR_OSX
            string ignore = "";
            if (ignores.Length > 0)
            {
                foreach (var x in ignores)
                    ignore += " --exclude=" + x;
            }
            string arg = string.Format("-r {0} {1} {2}", source, Path.GetDirectoryName(dest), ignore);
            using (Process.Start("rsync", arg)) { }
#endif
        }

        public static int[] UnityFork(string[] cmds, string title, string info)
        {
            const string slaveRoot = "../Slaves";
            if (!Directory.Exists(slaveRoot))
                Directory.CreateDirectory(slaveRoot);
            for (int i = 0; i < cmds.Length; ++i)
            {
                string slaveProject = string.Format("{0}/slave_{1}", slaveRoot, i);
                cmds[i] += " -projectPath " + Path.GetFullPath(slaveProject);
                if (!Directory.Exists(slaveProject))
                    Directory.CreateDirectory(slaveProject);

                if (!Directory.Exists(slaveProject + "/Assets"))
                {
#if UNITY_EDITOR_WIN
                    mklink("Assets", slaveProject + "/Assets");
#elif UNITY_EDITOR_OSX
                    Directory.CreateDirectory(slaveProject + "/Assets");
#endif
                }

#if UNITY_EDITOR_OSX
                foreach (var file in Directory.GetFiles("Assets", "*.*", SearchOption.TopDirectoryOnly))
                    mklink(file, slaveProject + "/Assets");
                foreach (var dir in Directory.GetDirectories("Assets", "*.*", SearchOption.TopDirectoryOnly))
                    mklink(dir, slaveProject + "/Assets");
#endif

                if (!Directory.Exists(slaveProject + "/ProjectSettings"))
                    mklink("ProjectSettings", slaveProject + "/ProjectSettings");
                if (!Directory.Exists(slaveProject + "/Library"))
                {
                    Directory.CreateDirectory(slaveProject + "/Library");
                    mklink("Library/metadata", slaveProject + "/Library/metadata");
                    mklink("Library/ShaderCache", slaveProject + "/Library/ShaderCache");
                    mklink("Library/AtlasCache", slaveProject + "/Library/AtlasCache");
                }
                rsync("Library", slaveProject + "/Library", "metadata", "ShaderCache", "AtlasCache", "DependCache");
            }
            string Unity = EditorApplication.applicationPath;
#if UNITY_EDITOR_OSX
            Unity += "/Contents/MacOS/Unity";
#endif
            return Start(Unity, cmds, title, info);
        }
    }
}
