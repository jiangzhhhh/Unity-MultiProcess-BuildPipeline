using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    [CustomEditor(typeof(Profile))]
    class ProfileEditor : Editor
    {
        static void MakeSymbolLink(string source, string dest)
        {
#if UNITY_EDITOR_WIN
            Process.Start(Path.GetFullPath("Tools/junction.exe"), string.Format("{0} {1}", dest, source));
#elif UNITY_EDITOR_OSX
			Process.Start("ln", string.Format("-s {0} {1}", source, dest));
#endif
        }

        static void MakeSymbolLinkFlatDir(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            dest = Path.GetDirectoryName(dest);
            foreach (var x in Directory.GetFiles(source, "*.*", SearchOption.TopDirectoryOnly))
            {
                var relPath = FileUtil.GetProjectRelativePath(x);
                MakeSymbolLink(x, Path.Combine(dest, relPath));
            }
            foreach (var x in Directory.GetDirectories(source, "*.*", SearchOption.TopDirectoryOnly))
            {
                var relPath = FileUtil.GetProjectRelativePath(x);
                MakeSymbolLink(x, Path.Combine(dest, relPath));
            }
        }

        static void CreateSlave(int index)
        {
            string slaveDir = Path.GetFullPath(Profile.SlaveRoot);
            slaveDir = Path.Combine(slaveDir, string.Format("slave_{0}", index));
            Directory.CreateDirectory(slaveDir);
#if UNITY_EDITOR_OSX
            MakeSymbolLinkFlatDir(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
#else
			MakeSymbolLink(Path.GetFullPath("Assets"), Path.Combine(slaveDir, "Assets"));
#endif
            MakeSymbolLink(Path.GetFullPath("ProjectSettings"), Path.Combine(slaveDir, "ProjectSettings"));

            UnityEngine.Debug.LogFormat("Create Slave Project: {0}", slaveDir);
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
            }

            if (GUILayout.Button("Cold Startup"))
            {
                try
                {
                    //Copy Libaray
                    string library = Path.GetFullPath("Library");
                    for (int i = 0; i < Profile.SlaveCount; ++i)
                    {
                        string slaveDir = Path.GetFullPath(Profile.SlaveRoot);
                        if (!Directory.Exists(slaveDir))
                            continue;
                        string slaveLibrary = Path.Combine(slaveDir, string.Format("slave_{0}/Library", i));
                        EditorUtility.DisplayProgressBar("copying", string.Format("copying Library from Master to Slave {0}", i), (float)i / Profile.SlaveCount);
                        if (Directory.Exists(slaveLibrary))
                            Directory.Delete(slaveLibrary, true);
                        FileUtil.CopyFileOrDirectory(library, slaveLibrary);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (GUILayout.Button("Open Slave Root Directory"))
            {
#if UNITY_EDITOR_WIN
                Process.Start("explorer", Profile.SlaveRoot);
#else
                Process.Start("open", Profile.SlaveRoot);
#endif
            }
        }
    }
}
