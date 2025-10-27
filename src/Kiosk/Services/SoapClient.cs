using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Kiosk.Services
{
    /* SOAP 전송 */
    public interface ISoapClient
    {
        Task<string> SendAsync(string soapXml, string soapAction, string endpoint);
        Task<string> SendAsync(string soapXml, string soapAction, string endpoint, CancellationToken ct);
    }

    public class SoapClient : ISoapClient
    {
        private static readonly SocketsHttpHandler _handler = new()
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10
        };

        private static readonly HttpClient _http = new(_handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        // 기본 per-request 타임아웃
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(15);

        // DI에서 매개변수 없는 생성자로 싱글턴 등록 가능
        public SoapClient()
        {
        }

        public Task<string> SendAsync(string soapXml, string soapAction, string endpoint)
            => SendAsync(soapXml, soapAction, endpoint, CancellationToken.None);

        public async Task<string> SendAsync(string soapXml, string soapAction, string endpoint, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("endpoint is required.", nameof(endpoint));
            if (string.IsNullOrWhiteSpace(soapAction))
                throw new ArgumentException("soapAction is required.", nameof(soapAction));
            if (soapXml is null)
                throw new ArgumentNullException(nameof(soapXml));

            using var content = new StringContent(soapXml, Encoding.UTF8, "text/xml");

            content.Headers.Add("SOAPAction", $"\"{soapAction}\"");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/xml")
            {
                CharSet = "utf-8"
            };

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (DefaultTimeout > TimeSpan.Zero)
                linked.CancelAfter(DefaultTimeout);

            using var resp = await _http.PostAsync(endpoint, content, linked.Token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            return await resp.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
        }
    }
}