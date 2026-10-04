using System;
using System.IO;
using UnityEngine;

namespace Horizon.UI
{
    public static class TimelineSharing
    {
        public static string Publish(string path)
        {
            bool video = string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.horizon.media.TimelineShareBridge"))
                    if (bridge.CallStatic<bool>("publish", activity, path))
                        return video ? "十秒视频已保存，选择一个地方分享。" : "十秒GIF已保存，选择一个地方分享。";
            }
            catch (Exception) { }
            return video ? "视频已保存，系统分享暂时没有打开。" : "GIF已保存，系统分享暂时没有打开。";
#else
            return video ? "十秒视频与音轨已保存。" : "十秒时间动画已保存。";
#endif
        }
    }
}
