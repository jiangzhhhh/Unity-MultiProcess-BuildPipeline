using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;

namespace MultiProcessBuild
{
    static class MultiProcess
    {
        static int[] Start(Process[] pss, string title, string info)
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

        static int[] Start(string bin, string[] cmd, string title, string info)
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

        static Process mklink(string source, string dest)
        {
            source = Path.GetFullPath(source);
            dest = Path.GetFullPath(dest);
            var ps = new Process();
#if UNITY_EDITOR_WIN
            ps.StartInfo.FileName = "cmd";
            ps.StartInfo.Arguments = string.Format("/c mklink /j \"{0}\" \"{1}\"", dest, source);
#elif UNITY_EDITOR_OSX
            ps.StartInfo.FileName = "ln";
            ps.StartInfo.Arguments = string.Format("-sf \"{0}\" \"{1}\"", source, dest);
#endif
            return ps;
        }

        static Process rsync(string source, string dest, params string[] ignores)
        {
            var ps = new Process();
#if UNITY_EDITOR_WIN
            string ignore = "";
            if (ignores.Length > 0)
            {
                ignore = "/xd ";
                foreach (var x in ignores)
                    ignore += x + " ";
            }
            ps.StartInfo.FileName = "robocopy";
            ps.StartInfo.Arguments = string.Format("/s {0} {1} {2}", source, dest, ignore);
#elif UNITY_EDITOR_OSX
            string ignore = "";
            if (ignores.Length > 0)
            {
                foreach (var x in ignores)
                    ignore += " --exclude=" + x;
            }
            ps.StartInfo.FileName = "rsync";
            ps.StartInfo.Arguments = string.Format("-r {0} {1} {2}", source, Path.GetDirectoryName(dest), ignore);
#endif
            return ps;
        }

        public static int[] UnityFork(string[] cmds, string title, string info)
        {
            const string slaveRoot = "../Slaves";
            if (!Directory.Exists(slaveRoot))
                Directory.CreateDirectory(slaveRoot);

            List<Process> linkPSs = new List<Process>();
            List<Process> rsyncPSs = new List<Process>();

            for (int i = 0; i < cmds.Length; ++i)
            {
                string slaveProject = string.Format("{0}/slave_{1}", slaveRoot, i);
                cmds[i] += " -projectPath " + Path.GetFullPath(slaveProject);
                if (!Directory.Exists(slaveProject))
                    Directory.CreateDirectory(slaveProject);

                if (!Directory.Exists(slaveProject + "/Assets"))
                {
#if UNITY_EDITOR_WIN
                    linkPSs.Add(mklink("Assets", slaveProject + "/Assets"));
#elif UNITY_EDITOR_OSX
                    Directory.CreateDirectory(slaveProject + "/Assets");
#endif
                }

#if UNITY_EDITOR_OSX
                foreach (var file in Directory.GetFiles("Assets", "*.*", SearchOption.TopDirectoryOnly))
                    linkPSs.Add(mklink(file, slaveProject + "/Assets"));
                foreach (var dir in Directory.GetDirectories("Assets", "*.*", SearchOption.TopDirectoryOnly))
                    linkPSs.Add(mklink(dir, slaveProject + "/Assets"));
#endif

                if (!Directory.Exists(slaveProject + "/ProjectSettings"))
                    linkPSs.Add(mklink("ProjectSettings", slaveProject + "/ProjectSettings"));
                if (!Directory.Exists(slaveProject + "/Library"))
                {
                    Directory.CreateDirectory(slaveProject + "/Library");
                    linkPSs.Add(mklink("Library/metadata", slaveProject + "/Library/metadata"));
                    linkPSs.Add(mklink("Library/ShaderCache", slaveProject + "/Library/ShaderCache"));
                    linkPSs.Add(mklink("Library/AtlasCache", slaveProject + "/Library/AtlasCache"));
                }
                rsyncPSs.Add(rsync("Library", slaveProject + "/Library", "metadata", "ShaderCache", "AtlasCache", "DependCache"));
            }

            Start(linkPSs.ToArray(), "make slave projects", "mklinking");
            Start(rsyncPSs.ToArray(), "make slave projects", "rsyncing");

            string Unity = EditorApplication.applicationPath;
#if UNITY_EDITOR_OSX
            Unity += "/Contents/MacOS/Unity";
#endif
            return Start(Unity, cmds, title, info);
        }
    }
}
