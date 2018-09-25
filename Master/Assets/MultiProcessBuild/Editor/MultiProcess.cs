using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;

namespace MultiProcessBuild
{
    static class MultiProcess
    {
        public static int[] Start(Process[] pss, string title, string info, System.Action<Process, int> onProcessExited = null)
        {
            int progress = 0;
            int total = pss.Length;
            int[] exitCodes = new int[total];

            try
            {
                EditorUtility.DisplayProgressBar(title, info, 0f);
                while (progress < total)
                {
                    for (int i = 0; i < total; ++i)
                    {
                        var ps = pss[i];
                        if (ps == null)
                            continue;

                        if (ps.WaitForExit(200))
                        {
                            if (onProcessExited != null)
                                onProcessExited(ps, i);
                            exitCodes[i] = ps.ExitCode;
                            progress++;
                            ps.Dispose();
                            pss[i] = null;
                            EditorUtility.DisplayProgressBar(title, info, (float)progress / total);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return exitCodes;
        }

        public static int[] Start(string bin, string[] cmd, string title, string info, System.Action<Process, int> onProcessExited = null)
        {
            Process[] pss = new Process[cmd.Length];
            for (int i = 0; i < cmd.Length; ++i)
                pss[i] = Process.Start(bin, cmd[i]);
            return Start(pss, title, info, onProcessExited);
        }

#if UNITY_EDITOR_WIN
        static void mklink(string source, string dest)
        {
            source = Path.GetFullPath(source);
            dest = Path.GetFullPath(dest);
            using (Process.Start("cmd", string.Format("/c mklink /j \"{0}\" \"{1}\"", dest, source))) { }
        }
#endif

        public static int[] UnityFork(string[] cmds, string title, string info, System.Action<Process, int> onProcessExited = null)
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
            return Start(Unity, cmds, title, info, onProcessExited);
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
                return Start(pss, title, info, onProcessExited);
            }
            finally
            {
                Directory.Move("Temp_bak", "Temp");
            }
#endif
        }
    }
}
