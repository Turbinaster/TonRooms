using System.Runtime.InteropServices;
// Read more about creating JS plugins: https://www.patrykgalach.com/2020/04/27/unity-js-plugin/
/// <summary>
/// Class with a JS Plugin functions for WebGL.
/// </summary>
public static class PluginJS
{
    [DllImport("__Internal")]
    public static extern void SendMessageToPage(string text);
    [DllImport("__Internal")]
    public static extern void ShowFriendsTeleport();
    [DllImport("__Internal")]
    public static extern void HideFriendsTeleport();
    [DllImport("__Internal")]
    public static extern void SendMessageToPage1(string text);
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    public static extern void LoadImageToPng(string url, string receiver, string successCallback, string errorCallback, string requestId);
#else
    public static void LoadImageToPng(string url, string receiver, string successCallback, string errorCallback, string requestId) { }
#endif
}
