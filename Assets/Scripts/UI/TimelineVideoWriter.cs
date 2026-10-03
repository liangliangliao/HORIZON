using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Horizon.UI
{
    public static class TimelineVideoWriter
    {
        // Encoding runs outside the presentation loop. Every frame is captured by
        // the Unity main thread before this method receives its directory.
        public static Task<string> EncodeAsync(string frames, string pcm, string output)
        {
            return Task.Run(() => Encode(frames, pcm, output));
        }
        private static string Encode(string frames, string pcm, string output)
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                AndroidJNI.AttachCurrentThread();
                try
                {
                    using (var encoder = new AndroidJavaClass("com.horizon.media.TimelineEncoder"))
                        return encoder.CallStatic<string>("encode", frames, 60, 6, pcm, output);
                }
                finally { AndroidJNI.DetachCurrentThread(); }
#elif UNITY_EDITOR
                string temporary = output + ".tmp.mp4";
                string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                string bundled = Path.GetFullPath(Path.Combine(Application.dataPath, "../tools/media/ffmpeg"));
                var start = new System.Diagnostics.ProcessStartInfo(File.Exists(bundled) ? bundled : "ffmpeg",
                    "-loglevel error -y -framerate 6 -i " + Quote(Path.Combine(frames, "frame-%03d.png")) +
                    " -f s16le -ar 22050 -ac 1 -i " + Quote(pcm) +
                    " -t 10 -c:v libx264 -pix_fmt yuv420p -c:a aac -movflags +faststart " + Quote(temporary))
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    process.ErrorDataReceived += (_, __) => { }; process.BeginErrorReadLine();
                    if (!process.WaitForExit(60000)) { process.Kill(); return "video_encoding_timeout"; }
                    if (process.ExitCode != 0 || !File.Exists(temporary)) return "video_encoding_unavailable";
                }
                if (File.Exists(output)) File.Delete(output);
                File.Move(temporary, output); return "";
#else
                return "video_encoding_unavailable";
#endif
            }
            catch (Exception) { return "video_encoding_unavailable"; }
        }
    }
}
