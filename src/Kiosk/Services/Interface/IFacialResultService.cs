using Kiosk.Models;

namespace Kiosk.Services.Interface;

public interface IFacialResultService
{
    Task<FacialResultModel.UpdateFacialDataResponse> UpdateFacialDataAsync(
        FacialResultModel.UpdateFacialDataRequest req, CancellationToken ct = default);

    Task<FacialResultModel.SendFacialResultResponse> SendFacialResultAsync(
        FacialResultModel.SendFacialResultRequest req, CancellationToken ct = default);
}