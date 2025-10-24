namespace Kiosk.Models;

public static class SettingsModel
{
    public sealed class InitSettingsModel
    {
        public required string UserName { get; init; } // 헤더
        public required long HostLocationOid { get; init; }
        public int PageNo { get; init; } = 1;
    }

    public sealed class StaffListModel
    {
        public required long UserOid { get; init; }
        public required string UserName { get; init; }
        public string? Role { get; init; } // 직무 (주방/홀/알바/…)
        public string? Department { get; init; } // 추후 부서
        public string? Level { get; init; } // "5","9"=관리자, "0"=일반 등
    }

    public sealed class CommuteListModel
    {
        public required DateTime WorkDate { get; init; } // 현지(한국) 자정 기준
        public required string UserName { get; init; }
        public DateTime? CheckInTime { get; init; }
        public DateTime? CheckOutTime { get; init; }
        public string Status { get; init; } = ""; // "퇴근", "근무중"
    }

    public sealed class StaffPageResult
    {
        public required int PageNo { get; init; }
        public required int TotalCount { get; init; }
        public required IReadOnlyList<StaffListModel> Users { get; init; }
    }
}