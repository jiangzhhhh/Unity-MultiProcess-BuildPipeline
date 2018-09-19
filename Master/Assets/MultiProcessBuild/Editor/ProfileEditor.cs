using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    [CustomEditor(typeof(Profile))]
    class ProfileEditor : Editor
    {
        static Process MakeSymbolLink(string source, string dest)
        {
#if UNITY_EDITOR_WIN
            return Process.Start("cmd", string.Format("/c mklink /j \"{0}\" \"{1}\"", dest, source));
#elif UNITY_EDITOR_OSX
			return Process.Start("ln", string.Format("-s {0} {1}", source, dest));
#endif
        }

#if UNITY_EDITOR_OSX
        static void MakeSymbolLinkFlatDir(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            source = Path.GetFullPath(source);

            List<Process> pss = new List<Process>();
            foreach (var x in Directory.GetFiles(source, "*.*", SearchOption.TopDirectoryOnly))
            {
                var relPath = x.Replace(source, "");
                relPath = relPath.TrimStart('\\');
                relPath = relPath.TrimStart('/');
                pss.Add(MakeSymbolLink(x, Path.Combine(dest, relPath)));
            }
            foreach (var x in Directory.GetDirectories(source, "*.*", SearchOption.TopDirectoryOnly))
            {
                var relPath = x.Replace(source, "");
                relPath = relPath.TrimStart('\\');
                relPath = relPath.TrimStart('/');
                pss.Add(MakeSymbolLink(x, Path.Combine(dest, relPath)));
            }

            if(pss.Count > 0)
                MultiProcess.Start(pss.ToArray(), "waiting", "make symbol link...");
        }
#endif

        static void CreateSlave(int index)
        {
            string slaveDir = Path.GetFullPath(Profile.SlaveRoot);
            slaveDir = Path.Combine(slaveDir, string.Format("slave_{0}", index));
            Directory.CreateDirectory(slaveDir);
#if UNITY_EDITOR_OSX
            MakeSymbolLinkFlatDir(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
#else
            using (MakeSymbolLink(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"))) { }
#endif
            using (MakeSymbolLink(Path.GetFullPath("ProjectSettings"), Path.Combine(slaveDir, "ProjectSettings"))) { }

            UnityEngine.Debug.LogFormat("Create Slave Project: {0}", slaveDir);
        }

        public void SyncSlaveProjects()
        {
#if UNITY_EDITOR_WIN
            string bin = "robocopy";
#else
            string bin = "rsync";
#endif
            List<string> cmds = new List<string>();

            //Copy Libaray
            string library = Path.GetFullPath("Library");
            for (int i = 0; i < Profile.SlaveCount; ++i)
            {
                string slaveDir = Path.GetFullPath(Profile.SlaveRoot);
                slaveDir = Path.Combine(slaveDir, string.Format("slave_{0}", i));
                if (!Directory.Exists(slaveDir))
                    continue;

#if UNITY_EDITOR_OSX
                MakeSymbolLinkFlatDir(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
#endif

#if UNITY_EDITOR_WIN
                cmds.Add(string.Format("{0} {1}", library, Path.Combine(slaveDir, "Library")));
#else
                cmds.Add(string.Format("-r {0} {1}", library, slaveDir));
#endif
            }

            if (cmds.Count > 0)
                MultiProcess.Start(bin, cmds.ToArray(), "waiting", "syncing master project to slave projects.");
        }

        public override void OnInspectorGUI()
        {
            var so = new SerializedObject(this.target);

            var slaveCount = so.FindProperty("slaveCount");
            EditorGUILayout.PropertyField(slaveCount);
            so.ApplyModifiedProperties();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField("Slave Root", so.FindProperty("slaveRoot").stringValue);
                if (GUILayout.Button("..."))
                {
                    string path = EditorUtility.OpenFolderPanel("Set Slave Root To ...", so.FindProperty("slaveRoot").stringValue, "");
                    if (string.IsNullOrEmpty(path))
                        return;
                    so.FindProperty("slaveRoot").stringValue = path;
                    so.ApplyModifiedProperties();
                }
            }

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this.target)) && GUILayout.Button("Save Profile"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Profile To ...", "profile", "asset", "Save Profile To");
                if (string.IsNullOrEmpty(path))
                    return;
                AssetDatabase.CreateAsset(this.target, path);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Create Slaves"))
            {
                for (int i = 0; i < Profile.SlaveCount; ++i)
                    CreateSlave(i);
                SyncSlaveProjects();
            }

            if (GUILayout.Button("Sync Slaves"))
                SyncSlaveProjects();

            if (GUILayout.Button("Open Slave Root Directory"))
            {
#if UNITY_EDITOR_WIN
                using (Process.Start("explorer", Profile.SlaveRoot)) { }
#else
                using (Process.Start("open", Profile.SlaveRoot)) { }
#endif
            }
        }
    }
}
