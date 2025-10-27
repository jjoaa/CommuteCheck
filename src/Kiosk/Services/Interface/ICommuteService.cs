using Kiosk.Models;

namespace Kiosk.Services.Interface
{
    public interface ICommuteService
    {
        Task<CommuteCheckModel.CommuteResponse> SubmitAsync(
            CommuteCheckModel.CommuteRequest req,
            CancellationToken ct = default);
    }
}