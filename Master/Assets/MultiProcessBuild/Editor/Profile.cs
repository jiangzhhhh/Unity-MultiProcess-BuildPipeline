using UnityEditor;
using UnityEngine;

namespace MultiProcessBuild
{
    public class Profile : ScriptableObject
    {
        [SerializeField]
        int slaveCount = 3;
        [SerializeField]
        string slaveRoot = "..";

        public static int SlaveCount { get { return Instance.slaveCount; } }

        static Profile _instance = null;
        static Profile Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<Profile>("profile");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        _instance = Profile.CreateInstance<Profile>();
                    }
#endif
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        [MenuItem("MultiProcessBuild/Setting")]
        public static void Open()
        {
            Selection.activeObject = Profile.Instance;
        }
#endif
    }
}