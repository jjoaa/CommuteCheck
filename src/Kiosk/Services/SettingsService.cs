using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Globalization;

public class SettingsService : ISettingsService
{
    private readonly ISoapClient _soap;
    private readonly ISessionService _session;
    private readonly ILogger<SettingsService> _logger;

    private const string UpdateAction = "http://tempuri.org/UpdateHostDataJSON_V2";
    private const string GetAction = "http://tempuri.org/GetHostDataJSON_V2";
    private readonly string _endpoint = Define.SERVER_API;

    public SettingsService(ISoapClient soap, ISessionService session, ILogger<SettingsService> logger)
    {
        _soap = soap;
        _session = session;
        _logger = logger;
    }

    // [A] 직원 목록
    public async Task<SettingsModel.StaffPageResult> GetUserListAsync(
        long hostLocationOid, int pageNo, CancellationToken ct = default)
    {
        var s = _session.GetValidatedSession();
        var xml = SoapRequestBuilder.MakeGetUserListRequest(s, hostLocationOid.ToString(), pageNo);
        var resp = await _soap.SendAsync(xml, GetAction, _endpoint, ct);
        var parsed = SoapJsonParser.ParseCommon(resp);
        if (parsed == null)
        {
            return new SettingsModel.StaffPageResult
                { PageNo = pageNo, TotalCount = 0, Users = Array.Empty<SettingsModel.StaffListModel>() };
        }

        var (result, code, msg, body) = parsed.Value;
        if (code != "100" || body == null)
        {
            _logger.LogWarning("[GetUserList] Fail {Result}/{Code} - {Msg}", result, code, msg);
            return new SettingsModel.StaffPageResult
                { PageNo = pageNo, TotalCount = 0, Users = Array.Empty<SettingsModel.StaffListModel>() };
        }

        var itemsToken = body["item"] ?? body["items"] ?? body["Item"] ?? body["Items"];
        var rows = AsObjects(itemsToken);

        var users = new List<SettingsModel.StaffListModel>();
        foreach (var row in rows)
        {
            var uid = row.Value<long?>("user_oid") ?? 0L;
            users.Add(new SettingsModel.StaffListModel
            {
                UserOid = uid,
                UserName = row.Value<string>("user_name") ?? "",
                Role = row.Value<string>("role"),
                Level = row.Value<string>("level"),
            });
        }

        var totalCount = body.Value<int?>("Total") ?? users.Count;
        return new SettingsModel.StaffPageResult
        {
            PageNo = pageNo,
            TotalCount = totalCount,
            Users = users
        };
    }

    // [B] 직원 권한 업데이트
    public async Task<bool> UpdateUserLevelAsync(long hostLocationOid, long userOid, string level,
        CancellationToken ct = default)
    {
        var s = _session.GetValidatedSession();
        var xml = SoapRequestBuilder.MakeUpdateUserLevelRequest(s, hostLocationOid.ToString(), userOid, level);
        var resp = await _soap.SendAsync(xml, UpdateAction, _endpoint, ct);
        var parsed = SoapJsonParser.ParseCommon(resp);
        if (parsed == null) return false;
        var (result, code, msg, _) = parsed.Value;
        var ok = result == "+OK" && code == "100";
        if (!ok) _logger.LogWarning("[UpdateUserLevel] {Result}/{Code} - {Msg}", result, code, msg);
        return ok;
    }

    // [C] 직원 직무 업데이트
    public async Task<bool> UpdateUserRoleAsync(long hostLocationOid, long userOid, string role,
        CancellationToken ct = default)
    {
        var s = _session.GetValidatedSession();
        var xml = SoapRequestBuilder.MakeUpdateUserRoleRequest(s, hostLocationOid, userOid, role);
        var resp = await _soap.SendAsync(xml, UpdateAction, _endpoint, ct);
        var parsed = SoapJsonParser.ParseCommon(resp);
        if (parsed == null) return false;
        var (result, code, msg, _) = parsed.Value;
        var ok = result == "+OK" && code == "100";
        if (!ok) _logger.LogWarning("[UpdateUserRole] {Result}/{Code} - {Msg}", result, code, msg);
        return ok;
    }

    // [D] 직원 추가
    public async Task<bool> Invite_StaffRegularAsync(long hostLocationOid, string phone, CancellationToken ct = default)
    {
        var s = _session.GetValidatedSession();
        var xml = SoapRequestBuilder.MakeInviteStaffRequest(s, hostLocationOid, phone);
        var resp = await _soap.SendAsync(xml, UpdateAction, _endpoint, ct);
        var parsed = SoapJsonParser.ParseCommon(resp);
        if (parsed == null) return false;
        var (result, code, msg, _) = parsed.Value;
        var ok = result == "+OK" && code == "100";
        if (!ok) _logger.LogWarning("[Invite_StaffRegular] {Result}/{Code} - {Msg}", result, code, msg);
        return ok;
    }

    // [E] 직원 삭제
    public Task<bool> DeleteUserAsync(long hostLocationOid, long userOid, CancellationToken ct = default)
        => UpdateUserLevelAsync(hostLocationOid, userOid, "-1", ct);

    // [F] 출퇴근 조회(일/월)
    public async Task<IReadOnlyList<SettingsModel.CommuteListModel>> GetDailyCommuteAsync(
        long hostLocationOid, int year, int month, int? day = null, long userOid = 0, CancellationToken ct = default)
    {
        var s = _session.GetValidatedSession();

        // KST 기준 날짜 범위 생성 → 서버 포맷 "YYYY.MM.DD HH:mm:ss"
        var (startKst, endKst) = BuildKstRange(year, month, day);
        var start = startKst.ToString("yyyy.MM.dd HH:mm:ss");
        var end = endKst.ToString("yyyy.MM.dd HH:mm:ss");

        var xml = SoapRequestBuilder.MakeGetCommuteHistoryRequest(
            s, hostLocationOid,
            pageNo: 1,
            startDate: start,
            endDate: end,
            userOid: userOid == 0 ? "0" : userOid.ToString()
        );

        var resp = await _soap.SendAsync(xml, GetAction, _endpoint, ct);
        var parsed = SoapJsonParser.ParseCommon(resp);
        if (parsed == null) return Array.Empty<SettingsModel.CommuteListModel>();

        var (_, code, msg, body) = parsed.Value;
        if (code != "100" || body == null) return Array.Empty<SettingsModel.CommuteListModel>();

        var itemsToken = body["item"] ?? body["items"] ?? body["Item"] ?? body["Items"];
        var rows = AsObjects(itemsToken).ToList();

        if (rows.Count > 0)
        {
            var first = rows[0];
        }

        var list = new List<SettingsModel.CommuteListModel>();
        foreach (var row in rows)
        {
            var ci = ParseKst(row.Value<string>("start_time"));
            var co = ParseKst(row.Value<string>("end_time"));

            list.Add(new SettingsModel.CommuteListModel
            {
                UserName = row.Value<string>("user_name") ?? "",
                CheckInTime = ci,
                CheckOutTime = co,
                WorkDate = (ci ?? co ?? DateTime.MinValue).Date,
                Status = co.HasValue ? "퇴근" : (ci.HasValue ? "근무중" : "")
            });
        }

        return list;
    }

    // ===== helpers =====
    private static (DateTime startKst, DateTime endKst) BuildKstRange(int year, int month, int? day)
    {
        var kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        DateTime startLocal, endLocal;
        if (day.HasValue)
        {
            startLocal = new DateTime(year, month, day.Value, 0, 0, 0, DateTimeKind.Unspecified);
            endLocal = new DateTime(year, month, day.Value, 23, 59, 59, DateTimeKind.Unspecified);
        }
        else
        {
            var lastDay = DateTime.DaysInMonth(year, month);
            startLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            endLocal = new DateTime(year, month, lastDay, 23, 59, 59, DateTimeKind.Unspecified);
        }

        // KST naive → 그대로 사용 
        return (startLocal, endLocal);
    }

    private static IEnumerable<JObject> AsObjects(JToken? token)
    {
        if (token is JArray arr) return arr.OfType<JObject>();
        if (token is JObject obj) return new[] { obj };
        return Enumerable.Empty<JObject>();
    }

    private static DateTime? ParseKst(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        const string fmt = "yyyy.MM.dd HH:mm:ss";
        if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d))
            return d;
        return null;
    }
}