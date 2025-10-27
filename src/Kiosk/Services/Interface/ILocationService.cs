using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Services.Interface
{
    public interface ILocationService
    {
        Task<List<LocationModel>> GetLocationsByHostOidAsync();
    }
}
