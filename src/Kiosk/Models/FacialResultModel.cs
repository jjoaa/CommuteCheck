namespace Kiosk.Models;

public class FacialResultModel
{
    public sealed class UpdateFacialDataRequest
    {
        public required string UserOid { get; init; }
        public required string HostLocationOid { get; init; }
        public string? App { get; set; }
        public string? PhoneNumber { get; set; }
        public double Dist { get; init; } // minDist 
    }

    public sealed class UpdateFacialDataResponse
    {
        public string? TransactionOid { get; init; }
        public bool Success { get; init; }
        public string? Error { get; init; }
    }

    public sealed class SendFacialResultRequest
    {
        public required string TransactionOid { get; init; }
        public string? HostLocationOid { get; set; }
        public required string UserOid { get; init; }
        public string? Mode { get; set; } //"0" = 자동, "1" = 수동
        public string? App { get; set; }
        public string Result { get; set; } = "SUCCESS";
        public string? Reason { get; set; }
        public string? Evidence { get; set; }
    }

    public sealed class SendFacialResultResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }

        public string? TransactionOid { get; init; }
        public string? HostLocationOid { get; init; }
        public string? UserOid { get; init; }
        public string? UserName { get; init; }
        public string? UserLevel { get; init; }
        public string? Result { get; init; }
        public string? ResultCode { get; init; }
    }
}