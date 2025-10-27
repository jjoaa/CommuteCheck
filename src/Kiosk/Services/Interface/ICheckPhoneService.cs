using System.Threading.Tasks;
using Kiosk.Models;

namespace Kiosk.Services.Interface
{
    public interface ICheckPhoneService
    {
        Task<CheckPhoneModel> UpdateUserQueueAsync(string phoneNumber);
    }
}