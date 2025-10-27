using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;
using Kiosk.Utils;

//SOAP API로 위치 목록을 가져오는 역할
namespace Kiosk.Services
{
    public class LocationService : ILocationService
    {
        private readonly ISoapClient _soapClient;
        private readonly ISessionService _sessionService;
        private readonly ILogger<LocationService> _logger;

        private string _soapEndpoint = Define.SERVER_API;
        private string _soapAction = "http://tempuri.org/GetHostDataJSON2";

        public LocationService(ISoapClient soapClient, ISessionService sessionService, ILogger<LocationService> logger)
        {
            _soapClient = soapClient;
            _sessionService = sessionService;
            _logger = logger;
        }

        public async Task<List<LocationModel>> GetLocationsByHostOidAsync()
        {
            var session = _sessionService.GetSession();
            _logger.LogInformation($"[GetSession] SessionId={session?.SessionId}, DeviceOid={session?.DeviceOid}");
            var result = new List<LocationModel>();

            // SOAP 요청 생성
            string requestXml = SoapRequestBuilder.MakeGetLocationListRequest(session);
            string responseXml = await _soapClient.SendAsync(requestXml, _soapAction, _soapEndpoint);
            _logger.LogInformation("===== GetHostDataJSON2 SOAP Response =====");

            // 응답 파싱
            var parsed = SoapJsonParser.ParseCommon(responseXml);
            if (parsed == null)
            {
                _logger.LogError("[LocationService] 응답 JSON을 파싱하지 못했습니다.");
                return result;
            }

            var (res, code, msg, body) = parsed.Value;

            // 4) 특수 에러 핸들링
            if (res == "-INVALID_SECURE_KEY")
            {
                _logger.LogWarning("[LocationService] INVALID_SECURE_KEY → 세션 갱신 필요");
                _sessionService.ClearSession();
                throw new InvalidOperationException("세션 만료 - 다시 로그인 필요");
            }

            if (res == "-INVALID_SESSION")
            {
                _logger.LogWarning("[LocationService] INVALID_SESSION");
                _sessionService.ClearSession();
                return result;
            }

            // 5) 정상 처리
            if (res == "+OK" && code == "100")
            {
                var items = SoapJsonParser.ExtractItems(body) ?? new JArray();
                foreach (var item in items)
                {
                    try
                    {
                        var location = item.ToObject<LocationModel>();
                        if (location != null) result.Add(location);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[LocationService] 항목 디시리얼라이즈 실패: {Item}", item);
                    }
                }
            }
            else
            {
                _logger.LogWarning("[LocationService] 실패: result={Result}, code={Code}, msg={Msg}", res, code, msg);
            }

            return result.OrderBy(x => x.LocationName).ToList();
        }
    }
}