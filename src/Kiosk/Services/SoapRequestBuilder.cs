using Kiosk.Services.Interface;
using Kiosk.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kiosk.Models;
using Newtonsoft.Json.Linq;

namespace Kiosk.Services;

public enum SoapApiVer
{
    Auto,
    Legacy, // GeneralService.asmx  (평문 JSON, GetHostDataJSON2 등)
    V2 // GeneralServiceV2.asmx (EncA JSON,  *_V2 메서드, InputJSONEncA)
}

public static class SoapRequestBuilder
{
    private static SoapApiVer ApiVersion { get; set; } = SoapApiVer.Auto;

    private static SoapApiVer ResolveVersion()
    {
        if (ApiVersion != SoapApiVer.Auto) return ApiVersion;
        var ep = Define.SERVER_API ?? string.Empty; // 예: "GeneralServiceV2.asmx"
        return ep.IndexOf("V2", StringComparison.OrdinalIgnoreCase) >= 0
            ? SoapApiVer.V2
            : SoapApiVer.Legacy;
    }

    public static string MakeHostSignInEnvelope_First(string password, JObject requestJson)
    {
        // 1차: 기본 PUBLIC_KEY 사용, InputJSONEncD 사용
        var authKeyEnc = CryptoUtils.EncryptWithPublicKey(Define.authKey, Define.PUBLIC_KEY);
        var passwordEnc = CryptoUtils.EncryptWithPublicKey(password, Define.PUBLIC_KEY);
        var inputEncD = CryptoUtils.EncryptWithPublicKey(
            requestJson.ToString(Newtonsoft.Json.Formatting.None), Define.PUBLIC_KEY);

        return $@"
            <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
                           xmlns:xsd='http://www.w3.org/2001/XMLSchema'
                           xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
              <soap:Body>
                <HostSignIn xmlns='http://tempuri.org/'>
                  <AuthKeyEncD>{authKeyEnc}</AuthKeyEncD>
                  <HostPasswordEncD>{passwordEnc}</HostPasswordEncD>
                  <InputJSONEncD>{inputEncD}</InputJSONEncD>
                  <Mode>SIGNIN</Mode>
                </HostSignIn>
              </soap:Body>
            </soap:Envelope>";
    }

    public static string MakeHostSignInEnvelope_Retry(
        string publicKeyFromServer, string sessionId, string deviceId, string secureKey, JObject requestJson)
    {
        // 2차: 서버가 내려준 PublicKey로 EncN/EncD
        var authKeyEnc = CryptoUtils.EncryptWithPublicKey(Define.authKey, Define.PUBLIC_KEY);
        var sessionIdEn = CryptoUtils.EncryptWithPublicKey(sessionId, publicKeyFromServer);
        var deviceIdEn = CryptoUtils.EncryptWithPublicKey(deviceId, publicKeyFromServer);
        var secureKeyEn = CryptoUtils.EncryptWithPublicKey(secureKey, publicKeyFromServer);
        var v = ResolveVersion();
        var inputNode = (v == SoapApiVer.V2)
            ? $"<InputJSONEncA>{CryptoUtils.Encrypt(requestJson.ToString(Newtonsoft.Json.Formatting.None))}</InputJSONEncA>" // V2는 EncA
            : $"<InputJSON>{requestJson.ToString(Newtonsoft.Json.Formatting.None)}</InputJSON>"; // Legacy는 평문

        return $@"
            <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
                           xmlns:xsd='http://www.w3.org/2001/XMLSchema'
                           xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
              <soap:Body>
                <HostSignIn xmlns='http://tempuri.org/'>
                  <AuthKeyEncD>{authKeyEnc}</AuthKeyEncD>
                  <SessionIDEncN>{sessionIdEn}</SessionIDEncN>
                  <SecureKeyEncD>{secureKeyEn}</SecureKeyEncD>
                  <DeviceIdEncN>{deviceIdEn}</DeviceIdEncN>
                  {inputNode}
                  <Mode>SIGNIN</Mode>
                </HostSignIn>
              </soap:Body>
            </soap:Envelope>";
    }

    //로그인
    public static JObject MakeHostSignInBody(string hostId, DeviceInfo device)
    {
        return new JObject
        {
            ["request"] = new JObject
            {
                ["host_id"] = hostId,
                ["device_os"] = device.DeviceOs,
                ["device_info"] = device.DeviceModel,
                ["app_ver"] = Define.APP_VER,
                ["device_id"] = device.DeviceId,
                ["app"] = device.App
            }
        };
    }

    private static string BuildRequestEnvelope(
        SessionInfo session,
        string wsMethod,
        string mode,
        JObject reqObject
    )
    {
        // 보안 키 및 암호화 로직
        string secureKey = $"{session.HostOid}-{session.DeviceOid}";
        string authKeyEnc = CryptoUtils.EncryptWithPublicKey(Define.authKey, Define.PUBLIC_KEY);
        string secureKeyEnc = CryptoUtils.EncryptWithPublicKey(secureKey, Define.PUBLIC_KEY);
        string sessionIdEnc = CryptoUtils.EncryptWithPublicKey(session.SessionId, session.PublicKey);
        string deviceIdEnc = CryptoUtils.EncryptWithPublicKey(session.DeviceId, session.PublicKey);
        var v = ResolveVersion();

        // InputJSON 생성
        string inputWrapped = WrapRequest(reqObject); // => {"request": <reqObject>}
        var inputNode = (v == SoapApiVer.V2)
            ? $"<InputJSONEncA>{CryptoUtils.Encrypt(inputWrapped)}</InputJSONEncA>"
            : $"<InputJSON><![CDATA[{inputWrapped}]]></InputJSON>";

        // SOAP 엔벨로프 생성 (메서드 이름 동적 변경)
        return $@"
            <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
                           xmlns:xsd='http://www.w3.org/2001/XMLSchema'
                           xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
              <soap:Body>
                <{wsMethod} xmlns='http://tempuri.org/'>
                  <AuthKeyEncD>{authKeyEnc}</AuthKeyEncD>
                  <SessionIDEncN>{sessionIdEnc}</SessionIDEncN>
                  <SecureKeyEncD>{secureKeyEnc}</SecureKeyEncD>
                  <DeviceIdEncN>{deviceIdEnc}</DeviceIdEncN>
                  {inputNode}
                  <Mode>{mode}</Mode>
                </{wsMethod}>
              </soap:Body>
            </soap:Envelope>";
    }

    /*위치선택*/
    public static string MakeGetLocationListRequest(SessionInfo session)
    {
        var req = new JObject { ["app"] = session.AppMode ?? "0" };
        return BuildRequestEnvelope(session, "GetHostDataJSON2", "GET_HOST_LOCATION_LIST", req);
    }

    /*FCM기능으로 사용자 추가*/
    public static string MakeUpdateUserQueueRequest(SessionInfo session, string phoneNumber, int sdkMode = 0)
    {
        var req = new JObject
        {
            ["phone_num"] = phoneNumber,
            ["sdk_mode"] = sdkMode.ToString() ?? "0",
            ["host_location_oid"] = session.HostLocationOid?.ToString() ?? "",
            ["app"] = session.AppMode ?? "0"
        };
        return BuildRequestEnvelope(session, "UpdateHostDataJSON_V2", "UPDATE_USER_QUEUE_V2", req);
    }

    /* 얼굴인식 결과 1)초기 매칭결과 업데이트(requestUpdateFacialData) */
    public static string MakeUpdateFacialDataRequest(SessionInfo session, FacialResultModel.UpdateFacialDataRequest m)
    {
        var req = new JObject
        {
            ["host_location_oid"] = NumOrStr(m.HostLocationOid),
            ["user_oid"] = NumOrStr(m.UserOid),
            ["phone_num"] = m.PhoneNumber,
            ["result_value"] = m.Dist,
            ["app"] = NumOrStr(m.App ?? "0")
        };
        return BuildRequestEnvelope(session, "UpdateHostDataJSON_V2", "UPDATE_FACIAL_DATA", req);
    }

    /* 얼굴인식 결과 2) 최종 인증 결과 보고(SendFacialResult) */
    public static string MakeSendFacialResultRequest(SessionInfo session, FacialResultModel.SendFacialResultRequest m)
    {
        var req = new JObject
        {
            ["host_location_oid"] = NumOrStr(m.HostLocationOid),
            ["transaction_oid"] = NumOrStr(m.TransactionOid),
            ["user_oid"] = NumOrStr(m.UserOid),
            ["result"] = m.Result,
            ["app"] = NumOrStr(m.App ?? "0"),
            ["mode"] = NumOrStr(m.Mode ?? "0")
        };

        if (!string.IsNullOrWhiteSpace(m.Reason)) req["reason"] = m.Reason;
        if (!string.IsNullOrWhiteSpace(m.Evidence)) req["evidence"] = m.Evidence;

        return BuildRequestEnvelope(session, "UpdateHostDataJSON_V2", "UPDATE_TRANSACTION_RESULT_V2", req);
    }

    /*출퇴근*/
    public static string MakeUpdateCommuteRecordRequest(SessionInfo session, CommuteCheckModel.CommuteRequest m)
    {
        var commuteTypeInt = (m.CommuteType == "0") ? 0 : 1;
        var isReUpdate = (m.IsReUpdate == "0") ? 0 : 1;

        var req = new JObject
        {
            ["user_oid"] = m.UserOid,
            ["host_location_oid"] = m.HostLocationOid,
            ["commute_type"] = commuteTypeInt, //m.CommuteType
            ["is_re_update"] = isReUpdate, //m.IsReUpdate
            ["app"] = m.App ?? "0"
        };
        return BuildRequestEnvelope(session, "UpdateHostDataJSON_V2", "UPDATE_COMMUTE_RECORD", req);
    }

    /*유저 리스트*/
    public static string MakeGetUserListRequest(SessionInfo session, string hostLocationOid, int pageNo)
    {
        var req = new JObject
        {
            ["page_no"] = pageNo.ToString(),
            ["host_location_oid"] = hostLocationOid.ToString(),
            ["app"] = "0"
        };
        return BuildRequestEnvelope(session, "GetHostDataJSON_V2", "GET_USER_LIST", req);
    }

    /*사용자 권한(level)변경/삭제 */
    public static string MakeUpdateUserLevelRequest(SessionInfo s, string hostLocationOid, long userOid, string level)
    {
        var req = new JObject
        {
            ["user_oid"] = userOid.ToString(),
            ["host_location_oid"] = hostLocationOid.ToString(),
            ["level"] = level, // "0","5","9","-1" 
            ["app"] = "0"
        };
        return BuildRequestEnvelope(s, "UpdateHostDataJSON_V2", "UPDATE_USER_FACIAL_LEVEL_V2", req);
    }

    /*사용자 직무 변경*/
    public static string MakeUpdateUserRoleRequest(SessionInfo s, long hostLocationOid, long userOid, string role)
    {
        var req = new JObject
        {
            ["user_oid"] = userOid.ToString(),
            ["host_location_oid"] = hostLocationOid.ToString(),
            ["role"] = role,
            ["app"] = "0"
        };
        return BuildRequestEnvelope(s, "UpdateHostDataJSON_V2", "UPDATE_USER_FACIAL_ROLE", req);
    }

    /*출퇴근기록 리스트*/
    public static string MakeGetCommuteHistoryRequest(SessionInfo s, long hostLocationOid,
        int pageNo, string startDate, string endDate, string userOid)
    {
        var req = new JObject
        {
            ["page_no"] = pageNo.ToString(),
            ["user_oid"] = userOid, // "0"=전체
            ["host_location_oid"] = hostLocationOid.ToString(),
            ["start_date"] = startDate, // "YYYY.MM.DD 00:00:00"
            ["end_date"] = endDate, // "YYYY.MM.DD 23:59:59"
            ["app"] = "0"
        };
        return BuildRequestEnvelope(s, "GetHostDataJSON_V2", "GET_COMMUTE_HISTORY", req);
    }

    /*직원 추가*/
    public static string MakeInviteStaffRequest(SessionInfo s, long hostLocationOid, string phone)
    {
        var req = new JObject
        {
            ["host_location_oid"] = hostLocationOid.ToString(),
            ["phone_num"] = phone,
            ["app"] = "0",
            ["sdk_mode"] = "0"
        };
        return BuildRequestEnvelope(s, "UpdateHostDataJSON_V2", "UPDATE_USER_FACIAL_V2", req);
    }

    // ===== helpers =====
    private static JToken NumOrStr(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return JValue.CreateNull();
        return long.TryParse(s, out var v) ? new JValue(v) : new JValue(s);
    }

    // JSON 래핑
    private static string WrapRequest(JObject inner) =>
        new JObject(new JProperty("request", inner))
            .ToString(Newtonsoft.Json.Formatting.None);
}