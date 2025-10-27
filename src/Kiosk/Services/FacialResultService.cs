using System.Globalization;
using System.Xml.Linq;
using Kiosk.Models;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Kiosk.Services
{
    public class FacialResultService : IFacialResultService
    {
        private readonly ISoapClient _soapClient;
        private readonly ISessionService _sessionService;
        private readonly ILogger<FacialResultService> _logger;

        private readonly string _soapEndpoint = Define.SERVER_API;
        private readonly string _soapAction = "http://tempuri.org/UpdateHostDataJSON_V2";

        public FacialResultService(ISoapClient soapClient, ISessionService sessionService,
            ILogger<FacialResultService> logger)
        {
            _soapClient = soapClient;
            _sessionService = sessionService;
            _logger = logger;
        }

        //초기 매칭 결과 업데이트
        public async Task<FacialResultModel.UpdateFacialDataResponse> UpdateFacialDataAsync(
            FacialResultModel.UpdateFacialDataRequest req, CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var session = _sessionService.GetSession();
                var requestXml = SoapRequestBuilder.MakeUpdateFacialDataRequest(session, req);

                var responseXml = await _soapClient.SendAsync(requestXml, _soapAction, _soapEndpoint, cts.Token);
                _logger.LogInformation("=== SOAP UPDATE_FACIAL_DATA Response ===");
                //_logger.LogInformation("=== SOAP UPDATE_FACIAL_DATA Response ===\n{xml}", responseXml);

                var parsed = SoapJsonParser.ParseCommon(responseXml);
                if (parsed == null) return new() { Success = false, Error = "INVALID_RESPONSE" };

                var (result, code, _, body) = parsed.Value;
                if (result == "+OK" && code == "100")
                {
                    var transactionOid = body.Value<string>("TransactionOid");
                    return new() { Success = true, TransactionOid = transactionOid };
                }

                if (result == "-INVALID_SESSION") _sessionService.Invalidate();
                return new() { Success = false, Error = result ?? "UNKNOWN_ERROR" };
            }
            catch (OperationCanceledException)
            {
                return new() { Success = false, Error = "TIMEOUT" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateFacialDataAsync failed");
                return new() { Success = false, Error = ex.Message };
            }
        }

        // 최종 인증 결과 보고
        public async Task<FacialResultModel.SendFacialResultResponse> SendFacialResultAsync(
            FacialResultModel.SendFacialResultRequest req, CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                var session = _sessionService.GetSession();
                var requestXml = SoapRequestBuilder.MakeSendFacialResultRequest(session, req);

                var responseXml = await _soapClient.SendAsync(requestXml, _soapAction, _soapEndpoint, cts.Token);
                _logger.LogInformation("=== SOAP UPDATE_TRANSACTION_RESULT_V2 Response ===");
                //_logger.LogInformation("=== SOAP UPDATE_TRANSACTION_RESULT_V2 Response ===\n{xml}", responseXml);

                var parsed = SoapJsonParser.ParseCommon(responseXml);
                if (parsed == null) return new() { Success = false, Error = "INVALID_RESPONSE" };

                var (result, code, _, body) = parsed.Value;

                var resp = new FacialResultModel.SendFacialResultResponse
                {
                    Result = result,
                    ResultCode = code,
                    TransactionOid = body?["TransactionOid"]?.ToString(),
                    HostLocationOid = body?["HostLocationOid"]?.ToString(),
                    UserOid = body?["UserOid"]?.ToString(),
                    UserName = body?["UserName"]?.ToString(),
                    UserLevel = body?["UserLevel"]?.ToString(),
                };

                switch (result)
                {
                    case "+OK" when code == "100":
                        resp.Success = true;
                        return resp;
                    case "-INVALID_SESSION":
                        _sessionService.Invalidate();
                        break;
                }

                return new() { Success = false, Error = result ?? "UNKNOWN_ERROR" };
            }
            catch (OperationCanceledException)
            {
                return new() { Success = false, Error = "TIMEOUT" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendFacialResultAsync failed");
                return new() { Success = false, Error = ex.Message };
            }
        }
    }
}