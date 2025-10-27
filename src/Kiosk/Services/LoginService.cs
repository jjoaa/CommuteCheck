using Microsoft.Extensions.Logging;
using Kiosk.Utils;
using Kiosk.Services.Interface;

namespace Kiosk.Services
{
    public class LoginResponse
    {
        public string Result { get; set; }
        public string ResultCode { get; set; }
        public long DeviceOid { get; set; }
        public long HostOid { get; set; }
        public string PublicKey { get; set; }
        public string SessionId { get; set; }
        public string AppMode { get; set; }
    }

    public class LoginService
    {
        private readonly ISoapClient _soapClient;
        private readonly ISessionService _sessionService;
        private readonly ILogger<LoginService> _logger;

        private readonly string _soapEndpoint = Define.SERVER_API;
        private readonly string _soapAction = "http://tempuri.org/HostSignIn";

        public LoginService(ISoapClient soapClient, ISessionService sessionService, ILogger<LoginService> logger)
        {
            _soapClient = soapClient;
            _sessionService = sessionService;
            _logger = logger;
        }

        public async Task<LoginResponse?> HostSignInAsync(string hostId, string password)
        {
            var app = hostId == "commax" ? "1" : "0";
            var device = EnvironmentHelper.BuildDeviceInfo(app);
            var bodyJson = SoapRequestBuilder.MakeHostSignInBody(hostId, device);

            // 1차
            var requestXml1 = SoapRequestBuilder.MakeHostSignInEnvelope_First(password, bodyJson);
            var responseXml1 = await _soapClient.SendAsync(requestXml1, _soapAction, _soapEndpoint);

            var parsed1 = SoapJsonParser.ParseLogin(responseXml1);
            if (parsed1?.Result == "+OK")
            {
                SaveSession(parsed1.Value.Data!, device.DeviceId);
                return parsed1.Value.Data;
            }

            // -INVALID_SECURE_KEY → 2차 요청
            if (parsed1?.Result == "-INVALID_SECURE_KEY" && parsed1.Value.Data is { } d)
            {
                var secureKey = $"{d.HostOid}-{d.DeviceOid}";

                var requestXml2 = SoapRequestBuilder.MakeHostSignInEnvelope_Retry(
                    d.PublicKey,
                    d.SessionId,
                    device.DeviceId,
                    secureKey,
                    bodyJson
                );

                var responseXml2 = await _soapClient.SendAsync(requestXml2, _soapAction, _soapEndpoint);
                var parsed2 = SoapJsonParser.ParseLogin(responseXml2);

                if (parsed2?.Result == "+OK")
                {
                    SaveSession(parsed2.Value.Data!, device.DeviceId);
                    return parsed2.Value.Data;
                }

                _logger.LogWarning("2차 로그인 실패: {Result}", parsed2?.Result);
                return null;
            }

            _logger.LogWarning("로그인 실패: {Result}", parsed1?.Result);
            return null;
        }

        private void SaveSession(LoginResponse resp, string deviceId)
        {
            _sessionService.SetSession(new SessionInfo
            {
                PublicKey = resp.PublicKey,
                SessionId = resp.SessionId,
                HostOid = resp.HostOid.ToString(),
                DeviceOid = resp.DeviceOid.ToString(),
                DeviceId = deviceId,
                AppMode = resp.AppMode ?? "0"
            });
            _logger.LogInformation($"[SaveSession] 저장됨: DeviceOid={resp.DeviceOid}, deviceId={deviceId}");
        }
    }
}