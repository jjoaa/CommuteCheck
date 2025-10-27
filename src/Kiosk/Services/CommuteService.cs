using System.Xml.Linq;
using Kiosk.Models;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Kiosk.Services;

public class CommuteService : ICommuteService
{
    private readonly ISoapClient _soapClient;
    private readonly ISessionService _sessionService;
    private readonly ILogger<CommuteService> _logger;

    private string _soapEndpoint = Define.SERVER_API;
    private string _soapAction = "http://tempuri.org/UpdateHostDataJSON_V2";

    public CommuteService(
        ISoapClient soapClient,
        ISessionService sessionService,
        ILogger<CommuteService> logger)
    {
        _soapClient = soapClient;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<CommuteCheckModel.CommuteResponse> SubmitAsync(
        CommuteCheckModel.CommuteRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var session = _sessionService.GetValidatedSession();

            string requestXml = SoapRequestBuilder.MakeUpdateCommuteRecordRequest(session, request);
            string responseXml = await _soapClient.SendAsync(requestXml, _soapAction, _soapEndpoint, ct);

            var parsed = SoapJsonParser.ParseCommon(responseXml);
            if (parsed == null)
            {
                _logger.LogWarning("[Commute] Invalid response format (no JSON payload)");
                return new CommuteCheckModel.CommuteResponse
                {
                    Success = false,
                    Result = "INVALID_RESPONSE",
                    ResultCode = null,
                    Error = "INVALID_RESPONSE"
                };
            }

            var (result, code, msg, body) = parsed.Value;
            // result: "+OK"/"-INVALID_COMMUTE_TYPE" 등, code: "100"/"101" 등
            var success = (result == "+OK" && code == "100");

            if (success)
            {
                //_logger.LogInformation("[Commute] Success: type={Type}, code={Code}", request.CommuteType, code);
                return new CommuteCheckModel.CommuteResponse
                {
                    Success = true,
                    Result = result, // "+OK"
                    ResultCode = code // "100"
                };
            }
            else
            {
                _logger.LogWarning("[Commute] Failed: result={Result}, code={Code}, msg={Msg}", result, code, msg);
                if (result == "-INVALID_SESSION") _sessionService.Invalidate();

                return new CommuteCheckModel.CommuteResponse
                {
                    Success = false,
                    Result = result, // "-INVALID_COMMUTE_TYPE" 등
                    ResultCode = code, // "101" 등  ← ★ 여기 중요
                    Error = msg ?? result ?? "UNKNOWN_ERROR"
                };
            }
        }
        catch (OperationCanceledException)
        {
            return new CommuteCheckModel.CommuteResponse
            {
                Success = false,
                Result = "CANCELED",
                ResultCode = null,
                Error = "요청이 취소되었습니다(타임아웃/취소)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Commute] 예외 발생");
            return new CommuteCheckModel.CommuteResponse
            {
                Success = false,
                Result = "EXCEPTION",
                ResultCode = null,
                Error = "예기치 못한 오류가 발생했습니다."
            };
        }
    }
}