using UnityEditor;

namespace YourName.AvatarClosetTool.Editor
{
    public static class AvatarClosetToolMenu
    {
        [MenuItem("Tools/Avatar Closet")]
        private static void OpenAvatarClosetWindow()
        {
            AvatarClosetWindow.OpenWindow();
        }
    }
}
