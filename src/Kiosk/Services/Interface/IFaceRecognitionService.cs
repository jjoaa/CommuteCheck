using Kiosk.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Services.Interface
{
    public interface IFaceRecognitionService
    {
        Task<RecognitionResult> RecognizeAsync(Bitmap frame, ObservableCollection<WaitUser> waitUsers);
        Task PreWarmAsync(Bitmap anyFrame); // 예열 (첫 프레임 1회)
    }

    public class RecognitionResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; } // 실패 메시지
        public string? UserName { get; set; }
        public string? UserOid { get; set; }

        public double? Dist { get; set; } // 매칭 거리
        // public Bitmap? CroppedAligned { get; init; } // ← 업로드용
    }
}