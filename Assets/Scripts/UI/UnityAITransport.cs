using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Horizon.Game;
using UnityEngine.Networking;

namespace Horizon.UI
{
    public sealed class UnityAITransport : IAITransport
    {
        public async Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using (var web = new UnityWebRequest(request.Url, "POST"))
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));
                web.downloadHandler = new DownloadHandlerBuffer();
                web.timeout = request.TimeoutSeconds; web.redirectLimit = 0;
                foreach (var header in request.Headers) web.SetRequestHeader(header.Key, header.Value);
                UnityWebRequestAsyncOperation pending = web.SendWebRequest();
                while (!pending.isDone)
                {
                    if (cancellation.IsCancellationRequested) { web.Abort(); cancellation.ThrowIfCancellationRequested(); }
                    if (web.downloadedBytes > 65536) { web.Abort(); throw new AIException(AIError.InvalidContent); }
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                if (web.downloadedBytes > 65536) throw new AIException(AIError.InvalidContent);
                if (web.result == UnityWebRequest.Result.ConnectionError) throw new AIException(AIError.Network);
                return new AIResponse((int)web.responseCode, web.downloadHandler.text);
            }
        }
    }
}
