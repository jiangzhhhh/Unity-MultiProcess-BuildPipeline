using System.Diagnostics;
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
    }
}
