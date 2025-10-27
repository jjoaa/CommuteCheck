using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Services.Interface
{
    /*카메라 초기화, 시작, 프레임 캡처*/
    public interface ICameraService
    {
        event EventHandler<Bitmap> FrameCaptured;
        Task OpenCameraAsync();
        Task CloseCameraAsync();
    }
}