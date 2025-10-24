namespace Kiosk.Models
{
    public enum CommuteType
    {
        CheckIn,
        CheckOut
    }

    public class CommuteCheckModel
    {
        public class InitCommuteCheckModel
        {
            public required string UserOid { get; init; }
            public required string HostLocationOid { get; init; }
            public string? UserName { get; init; }
            public string? TransactionOid { get; init; }
            public required DateTime AuthTime { get; init; }
            public string? UserLevel { get; init; }
        }

        public class CommuteRequest
        {
            public required string UserOid { get; init; }
            public required string HostLocationOid { get; init; }
            public required string CommuteType { get; init; } // 0: 출근, 1: 퇴근
            public string IsReUpdate { get; set; } = "0"; // 0:기본, 1:강제 추가
            public string App { get; init; } = "0";
        }

        public class CommuteResponse
        {
            public bool Success { get; init; }
            public string Result { get; set; } = "SUCCESS";
            public string? Error { get; init; }
            public string? ResultCode { get; init; }
        }
    }
}