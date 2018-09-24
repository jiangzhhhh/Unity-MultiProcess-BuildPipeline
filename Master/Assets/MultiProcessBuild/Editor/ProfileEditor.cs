using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    [CustomEditor(typeof(Profile))]
    class ProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var so = new SerializedObject(this.target);

            var slaveCount = so.FindProperty("slaveCount");
            EditorGUILayout.PropertyField(slaveCount);
            so.ApplyModifiedProperties();

            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this.target)) && GUILayout.Button("Save Profile"))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Profile To ...", "profile", "asset", "Save Profile To");
                if (string.IsNullOrEmpty(path))
                    return;
                AssetDatabase.CreateAsset(this.target, path);
            }
        }
    }
}
