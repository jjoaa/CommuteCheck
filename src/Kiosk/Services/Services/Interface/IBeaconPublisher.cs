using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Services.Interface
{
    public interface IBeaconPublisher
    {
        void StartIBeacon(string uuid, ushort major, ushort minor);
        void StopIBeacon();
    }
}
