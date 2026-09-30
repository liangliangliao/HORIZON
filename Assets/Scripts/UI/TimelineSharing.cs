using System;
using System.IO;
using UnityEngine;

namespace Horizon.UI
{
    public static class TimelineSharing
    {
        public static string Publish(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaObject uri = null, resolver = null;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    if (version.GetStatic<int>("SDK_INT") < 29) return "动画已保存，当前设备暂不支持直接分享。";
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                    resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                    var values = new AndroidJavaObject("android.content.ContentValues");
                    values.Call("put", "_display_name", Path.GetFileName(path));
                    values.Call("put", "mime_type", "image/gif");
                    values.Call("put", "relative_path", "Pictures/HORIZON/");
                    values.Call("put", "is_pending", new AndroidJavaObject("java.lang.Integer", 1));
                    using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
                        uri = resolver.Call<AndroidJavaObject>("insert", media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"), values);
                    if (uri == null) throw new IOException("Could not create the sharing image.");
                    using (AndroidJavaObject output = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                    {
                        byte[] bytes = File.ReadAllBytes(path);
                        output.Call("write", new object[] { bytes }); output.Call("close");
                    }
                    values.Call("clear"); values.Call("put", "is_pending", new AndroidJavaObject("java.lang.Integer", 0));
                    resolver.Call<int>("update", uri, values, null, null);
                    string address = uri.Call<string>("toString");
                    activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                    {
                        try
                        {
                            using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.SEND"))
                            using (var uriType = new AndroidJavaClass("android.net.Uri"))
                            using (var intentType = new AndroidJavaClass("android.content.Intent"))
                            {
                                intent.Call<AndroidJavaObject>("setType", "image/gif");
                                intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uriType.CallStatic<AndroidJavaObject>("parse", address));
                                intent.Call<AndroidJavaObject>("addFlags", 1);
                                activity.Call("startActivity", intentType.CallStatic<AndroidJavaObject>("createChooser", intent, "分享你的时间线"));
                            }
                        }
                        catch (Exception exception) { Debug.LogWarning("Timeline is saved to Pictures/HORIZON: " + exception.Message); }
                    }));
                }
                return "动画已保存到相册，选择一个地方分享。";
            }
            catch (Exception exception)
            {
                if (uri != null && resolver != null)
                    try { resolver.Call<int>("delete", uri, null, null); } catch (Exception) { }
                Debug.LogWarning("Timeline export is saved locally: " + exception.Message);
                return "动画已保存，系统分享暂时没有打开。";
            }
            finally { uri?.Dispose(); resolver?.Dispose(); }
#else
            return "十秒时间动画已保存。";
#endif
        }
    }
}
