using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kiosk.Views
{
    public partial class BackdropWindow : Window
    {
        private readonly Window _owner;

        public BackdropWindow(Window owner)
        {
            _owner = owner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ShowInTaskbar = false;
            ShowActivated = false; // 포커스/활성 훔치지 않기
            Topmost = false;
            Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
            IsHitTestVisible = true;

            Owner = owner;
            UpdateBounds();

            // 오너 이동/리사이즈/상태변경 시 백드롭 동기화
            _owner.LocationChanged += (_, __) => UpdateBounds();
            _owner.SizeChanged += (_, __) => UpdateBounds();
            _owner.StateChanged += (_, __) => UpdateBounds();
        }

        private void UpdateBounds()
        {
            var hwnd = new WindowInteropHelper(_owner).Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1) 보이는 창 경계(그림자 제외) 픽셀 단위로 얻기
            if (!TryGetVisibleBoundsPx(hwnd, out var px)) return;

            // 2) 현재 모니터 DPI로 DIP 변환
            var dpi = VisualTreeHelper.GetDpi(_owner); // per-monitor DPI
            double dipLeft = px.Left * 96.0 / dpi.PixelsPerInchX;
            double dipTop = px.Top * 96.0 / dpi.PixelsPerInchY;
            double dipWidth = (px.Right - px.Left) * 96.0 / dpi.PixelsPerInchX;
            double dipHeight = (px.Bottom - px.Top) * 96.0 / dpi.PixelsPerInchY;

            // 3) 딱 맞게 놓기 (반올림으로 미세 오차 제거)
            Left = Math.Round(dipLeft);
            Top = Math.Round(dipTop);
            Width = Math.Round(dipWidth);
            Height = Math.Round(dipHeight);
        }

        private static bool TryGetVisibleBoundsPx(IntPtr hwnd, out RECT rect)
        {
            // DWM 확장 프레임 경계: 그림자 제외, 실제 보이는 바깥 경계
            const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
            int size = Marshal.SizeOf<RECT>();
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, size);
            if (hr == 0) return true;

            // 폴백: 일반 윈도우 경계
            return GetWindowRect(hwnd, out rect);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(
            IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }
    }
}