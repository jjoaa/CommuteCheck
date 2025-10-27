namespace Kiosk.Services;

using System.Collections.Concurrent;

public record DailyPunchState(bool HasOpen, bool Closed, DateTime? LastCheckoutUtc);

public interface IPunchStateCache
{
    DailyPunchState Get(string userOid, string hostLocationOid, DateTime nowUtc);
    void MarkOpen(string userOid, string hostLocationOid, DateTime nowUtc);
    void MarkClosed(string userOid, string hostLocationOid, DateTime nowUtc, DateTime? lastCheckoutUtc = null);
    void Reset(string userOid, string hostLocationOid, DateTime nowUtc); // 선택: 지점 전환 시 초기화
}

/*근무일 계산, 로컬 캐시*/
public class PunchStateCache : IPunchStateCache
{
    private readonly Dictionary<string, DailyPunchState> _map = new();

    private static string Key(string userOid, string hostLocationOid, DateTime nowUtc)
    {
        // nowUtc → KST로 변환 후 근무일 계산 /*교대근무 새벽4시 기준*/
        var kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), kst);

        var cutoff = new DateTime(local.Year, local.Month, local.Day, 4, 0, 0);
        var workday = (local < cutoff) ? local.Date.AddDays(-1) : local.Date;
        return $"{userOid}:{hostLocationOid}:{workday:yyyy-MM-dd}";
    }

    public DailyPunchState Get(string userOid, string hostLocationOid, DateTime nowUtc)
    {
        var key = Key(userOid, hostLocationOid, nowUtc);
        return _map.TryGetValue(key, out var s) ? s : new DailyPunchState(false, false, null);
    }

    public void MarkOpen(string userOid, string hostLocationOid, DateTime nowUtc)
    {
        var key = Key(userOid, hostLocationOid, nowUtc);
        if (_map.TryGetValue(key, out var cur))
            _map[key] = cur with { HasOpen = true, Closed = false }; // LastCheckoutUtc 유지
        else
            _map[key] = new DailyPunchState(true, false, null);
    }

    public void MarkClosed(string userOid, string hostLocationOid, DateTime nowUtc, DateTime? lastCheckoutUtc = null)
    {
        var key = Key(userOid, hostLocationOid, nowUtc);
        if (_map.TryGetValue(key, out var cur))
            _map[key] = cur with
            {
                HasOpen = false, Closed = true, LastCheckoutUtc = lastCheckoutUtc ?? cur.LastCheckoutUtc
            };
        else
            _map[key] = new DailyPunchState(false, true, lastCheckoutUtc);
    }

    public void Reset(string userOid, string hostLocationOid, DateTime nowUtc)
    {
        var key = Key(userOid, hostLocationOid, nowUtc);
        _map[key] = new DailyPunchState(false, false, null);
    }
}
/*public record DailyPunchState(bool HasOpen, bool Closed, DateTime? LastCheckoutUtc);

public interface IPunchStateCache
{
    DailyPunchState Get(string userOid, DateTime nowUtc);
    void MarkOpen(string userOid, DateTime nowUtc);
    void MarkClosed(string userOid, DateTime nowUtc, DateTime? lastCheckoutUtc = null); // ← 변경
}

/*근무일 계산, 로컬 캐시#1#
public class PunchStateCache : IPunchStateCache
{
    private readonly Dictionary<string, DailyPunchState> _map = new();

    private static string Key(string userOid, DateTime nowUtc)
    {
        // nowUtc → KST로 변환 후 근무일 계산 /*교대근무 새벽4시 기준#1#
        var kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); // Linux면 "Asia/Seoul"
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), kst);

        var cutoff = new DateTime(local.Year, local.Month, local.Day, 4, 0, 0);
        var workday = (local < cutoff) ? local.Date.AddDays(-1) : local.Date;
        return $"{userOid}:{workday:yyyy-MM-dd}";
    }

    public DailyPunchState Get(string userOid, DateTime nowUtc)
    {
        var key = Key(userOid, nowUtc);
        return _map.TryGetValue(key, out var s) ? s : new DailyPunchState(false, false, null);
    }

    public void MarkOpen(string userOid, DateTime nowUtc)
    {
        var key = Key(userOid, nowUtc);
        var s = Get(userOid, nowUtc);
        _map[key] = s with { HasOpen = true, Closed = false /* LastCheckoutUtc 유지 #1# };
    }

    public void MarkClosed(string userOid, DateTime nowUtc, DateTime? lastCheckoutUtc = null)
    {
        var key = Key(userOid, nowUtc);
        var s = Get(userOid, nowUtc);
        _map[key] = s with { HasOpen = false, Closed = true, LastCheckoutUtc = lastCheckoutUtc ?? s.LastCheckoutUtc };
    }
}*/