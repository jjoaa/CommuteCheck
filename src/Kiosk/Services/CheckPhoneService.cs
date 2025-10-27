using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using Kiosk.Models;

namespace Kiosk.Services
{
    public class CheckPhoneService : ICheckPhoneService
    {
        private readonly ISoapClient _soapClient;
        private readonly ISessionService _sessionService;
        private readonly ILogger<CheckPhoneService> _logger;
        private readonly FirebaseService _firebase;

        private string _soapEndpoint = Define.SERVER_API;
        private string _soapAction = "http://tempuri.org/UpdateHostDataJSON_V2";

        public CheckPhoneService(
            ISoapClient soapClient,
            ISessionService sessionService,
            ILogger<CheckPhoneService> logger,
            FirebaseService firebase)
        {
            _soapClient = soapClient;
            _sessionService = sessionService;
            _logger = logger;
            _firebase = firebase;
        }

        /*대기자 추가*/
        public async Task<CheckPhoneModel> UpdateUserQueueAsync(string phoneNumber)
        {
            try
            {
                var session = _sessionService.GetValidatedSession();

                string requestXml = SoapRequestBuilder.MakeUpdateUserQueueRequest(session, phoneNumber);
                string responseXml = await _soapClient.SendAsync(requestXml, _soapAction, _soapEndpoint);
                _logger.LogInformation("=== UpdateHostDataJSON_V2 SOAP Response ===");

                // 1) 공통 파서로 응답 파싱
                var parsed = SoapJsonParser.ParseCommon(responseXml);
                if (parsed == null)
                {
                    return new CheckPhoneModel { Success = false, Type = "ERROR_RESPONSE_FORMAT" };
                }

                var (result, code, msg, body) = parsed.Value;

                var hostLoc = body.Value<long?>("HostLocationOid") ?? 0;
                var userOid = body.Value<long?>("UserOid") ?? 0;

                // 2) 모델 매핑
                var model = new CheckPhoneModel
                {
                    Success = (result == "+OK" && code == "100"),
                    Result = result,
                    HostLocationOid = hostLoc, // long
                    UserOid = userOid,
                    FirebaseToken = body.Value<string>("FirebaseToken"),
                    PhoneNumber = phoneNumber,
                    Type = "0" // 기본값 (이후 RTDB 확인으로 OK_NAME/에러 분기)
                };

                // 3) 가입되지 않은 유저
                if (result == "-INVALID_USER")
                {
                    model.Success = false;
                    model.Type = "ERROR_USER";
                    return model;
                }

                // 4) 등록 유저면 RTDB로 이름/랜드마크 확인 (기존 로직 유지)
                if (model.Success && model.HostLocationOid > 0 && model.UserOid > 0)
                {
                    const int timeoutMs = 10_000; // 10초
                    var sw = Stopwatch.StartNew();

                    var wu = await _firebase.UpdateUserQueue(
                        model.HostLocationOid.ToString(),
                        model.UserOid.ToString(),
                        requireName: true, requireLandmark: true,
                        timeoutMs: timeoutMs
                    );

                    sw.Stop();

                    if (wu != null)
                    {
                        model.Type = "OK_NAME";
                        model.UserName = wu.Name;
                        model.Landmark = wu.Landmark;
                        return model;
                    }
                    else
                    {
                        model.Success = false;
                        model.Type = (sw.ElapsedMilliseconds >= timeoutMs - 50)
                            ? "ERROR_TIMEOUT"
                            : "ERROR_NOT_REGISTERED_ON_DEVICE";
                        return model;
                    }
                }

                // 5) 그 외
                model.Success = false;
                model.Type = "ERROR_UNKNOWN";
                return model;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "대기자 추가 중 오류 발생");
                return new CheckPhoneModel
                {
                    Success = false,
                    Result = "-ERROR",
                    Type = "ERROR_EXCEPTION"
                };
            }
        }
    }
}