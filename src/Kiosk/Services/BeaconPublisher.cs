using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Kiosk.Services.Interface;

namespace Kiosk.Services
{
    public class BeaconPublisher : IBeaconPublisher
    {
        private BluetoothLEAdvertisementPublisher _publisher;
    
        public void StartIBeacon(string uuid, ushort major, ushort minor)
        {
            // 이전 광고가 있으면 중지
            _publisher?.Stop();

            var beaconData = GetIBeaconPayload(uuid, major, minor);
            var manufacturerData = new BluetoothLEManufacturerData(0x004C, beaconData);

            var adv = new BluetoothLEAdvertisement();
            adv.ManufacturerData.Add(manufacturerData);

            _publisher = new BluetoothLEAdvertisementPublisher(adv);
            //_publisher.StatusChanged += (s, e) =>
            //{
            //    Console.WriteLine($"[BeaconPublisher] StatusChanged: {e.Status}");
            //    if (e.Status == BluetoothLEAdvertisementPublisherStatus.Started)
            //        Console.WriteLine("[BeaconPublisher] 광고 시작됨!");
            //    if (e.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
            //        Console.WriteLine($"[BeaconPublisher] 광고 중단됨. Reason={e.Error}");
            //};
            _publisher.Start();
            Console.WriteLine($"[BeaconPublisher] 비콘 광고 시작: UUID={uuid}, Major={major}, Minor={minor}");
        }
        public void StopIBeacon()
        {
            _publisher?.Stop();
            Console.WriteLine("[BeaconPublisher] 비콘 광고 중지됨");
        }

        private IBuffer GetIBeaconPayload(string uuid, ushort major, ushort minor)
        {
            var data = new byte[23];
            data[0] = 0x02; data[1] = 0x15; // iBeacon prefix

            // Guid 변환 (Big-endian)
            var guidBytes = Guid.Parse(uuid).ToByteArray();
            Array.Reverse(guidBytes, 0, 4);
            Array.Reverse(guidBytes, 4, 2);
            Array.Reverse(guidBytes, 6, 2);

            Array.Copy(guidBytes, 0, data, 2, 16);

            data[18] = (byte)(major >> 8);
            data[19] = (byte)(major & 0xFF);
            data[20] = (byte)(minor >> 8);
            data[21] = (byte)(minor & 0xFF);
            data[22] = 0xC5; // Tx Power

            return data.AsBuffer();
        }
    }
}