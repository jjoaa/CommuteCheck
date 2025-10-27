using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace Kiosk.Services;

public static class SoapJsonParser
{
    private static readonly string[] DefaultNodeCandidates = new[]
    {
        "HostSignInResult",
        "UpdateHostDataJSON_V2Result",
        "GetHostDataJSON2Result",
        "GetHostDataJSON_V2Result",
        // "GetKioskLandmarkUploadUrlResult"
    };

    //로그인 전용 파싱 (HostSignIn)
    public static (string? Result, LoginResponse? Data)? ParseLogin(string responseXml)
    {
        var parsed = ParseCommon(responseXml, "HostSignInResult");
        if (parsed == null) return null;

        var (result, _, _, body) = parsed.Value;

        var data = new LoginResponse
        {
            Result = body.Value<string>("Result"),
            ResultCode = body.Value<string>("ResultCode"),
            HostOid = body.Value<long?>("host_oid") ?? 0,
            DeviceOid = body.Value<long?>("host_device_oid") ?? 0,
            PublicKey = body.Value<string>("public_key"),
            SessionId = body.Value<string>("session_id"),
            AppMode = body.Value<string>("app_mode") ?? "0"
        };

        return (result, data);
    }

    //공통 파싱
    public static (string? Result, string? ResultCode, string? Message, JObject Body)? ParseCommon(
        string responseXml, params string[] nodeNames)
    {
        // 자동 판별
        var obj = ExtractJsonAuto(responseXml, nodeNames);
        if (obj == null) return null;

        JObject body = obj;

        // response 속성이 있고 문자열 JSON인 경우 처리
        var responseToken = body["response"];
        if (responseToken != null)
        {
            if (responseToken is JValue sv && sv.Type == JTokenType.String && TryParseJson((string)sv, out var inner))
            {
                body = inner;
            }
            else if (responseToken is JObject responseObj)
            {
                body = responseObj;
            }
            else if (responseToken is JArray responseArr)
            {
                body = WrapArray(responseArr);
            }
        }

        return (
            body.Value<string>("Result"),
            body.Value<string>("ResultCode"),
            body.Value<string>("Message") ?? body.Value<string>("Msg"),
            body
        );
    }

    // 평문 JSON 경우
    public static JObject? ExtractJson(string responseXml, params string[] nodeNames)
    {
        var names = (nodeNames != null && nodeNames.Length > 0) ? nodeNames : DefaultNodeCandidates;

        var xdoc = XDocument.Parse(responseXml);
        var node = xdoc.Descendants().FirstOrDefault(e => names.Contains(e.Name.LocalName));
        if (node == null) return null;

        var text = System.Net.WebUtility.HtmlDecode(node.Value?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 평문 JSON (객체/배열 모두 허용)
        if (TryParseJsonToken(text, out var tok))
        {
            if (tok is JObject o) return o;
            if (tok is JArray a) return WrapArray(a);
        }

        return null;
    }

    //자동 복호화
    public static JObject? ExtractJsonAuto(string responseXml, params string[] nodeNames)
    {
        var names = (nodeNames != null && nodeNames.Length > 0) ? nodeNames : DefaultNodeCandidates;

        var xdoc = XDocument.Parse(responseXml);
        var methodNode = xdoc.Descendants().FirstOrDefault(e => names.Contains(e.Name.LocalName));
        if (methodNode == null) return null;

        var resultNode = methodNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "Result") ?? methodNode;
        var raw = System.Net.WebUtility.HtmlDecode(resultNode.Value?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 1) 평문 JSON (객체/배열 모두)
        if (TryParseJsonToken(raw, out var plainTok))
        {
            if (plainTok is JObject po) return po;
            if (plainTok is JArray pa) return WrapArray(pa);
        }

        // 2) 복호화 후 JSON 시도 (V2에서 흔함)
        var dec = CryptoUtils.Decrypt(raw);

        // 복호화된 문자열이 배열로 시작하는 경우 처리 - 이 부분이 핵심 수정
        dec = dec?.Trim() ?? "";
        if (TryParseJsonToken(dec, out var decTok))
        {
            // 배열인 경우 표준 응답 형식으로 래핑
            if (decTok is JObject jo) return jo;
            if (decTok is JArray ja) return WrapArray(ja);
        }

        return null;
    }

    private static bool TryParseJson(string s, out JObject obj)
    {
        obj = default!;
        try
        {
            s = s.Trim();
            if (s.Length == 0) return false;

            var token = JToken.Parse(s);

            // JObject인 경우
            if (token is JObject o)
            {
                obj = o;
                return true;
            }

            // JArray인 경우 - 특별 플래그와 함께 래핑
            if (token is JArray)
            {
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonToken(string s, out JToken token)
    {
        token = default;
        try
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            token = JToken.Parse(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static JObject WrapArray(JArray arr) => new JObject
    {
        ["Reslut"] = "+OK",
        ["ResultCode"] = "100",
        ["Item"] = arr
    };

    public static JArray? ExtractItems(JObject body)
    {
        // 가장 흔한 키들 우선순위로 탐색
        return (JArray?)(
            body["Item"] ??
            body["Items"] ??
            body["rows"] ??
            body["list"] ??
            body["data"]
        );
    }
}