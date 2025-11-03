using System.Drawing;

namespace Kiosk.Utils;

public class ROIUtils
{
    // 뷰 좌표계의 마스크 정보
    public static double _viewW, _viewH, _viewMaskCx, _viewMaskCy, _viewMaskR;

    public static void SetViewMask(double viewWidth, double viewHeight, double cx, double cy, double r)
    {
        _viewW = viewWidth;
        _viewH = viewHeight;
        _viewMaskCx = cx;
        _viewMaskCy = cy;
        _viewMaskR = r;
    }

    // ====== 핵심 2-2: UniformToFill 매핑 로직 ======
    // <Image Stretch="UniformToFill"> 상황에서
    //  - 뷰(화면) 좌표의 마스크 원(center, radius)을
    //  - 실제 비트맵 좌표의 사각형(원을 감싸는 정사각)으로 변환한다.
    public static System.Drawing.Rectangle ComputeRoiRectInImage(Bitmap bmp)
    {
        int imgW = bmp.Width;
        int imgH = bmp.Height;

        // 뷰 정보가 아직 없으면 전체 영역
        if (_viewW <= 0 || _viewH <= 0 || _viewMaskR <= 0)
            return new System.Drawing.Rectangle(0, 0, imgW, imgH);

        // UniformToFill: 확대 비율은 max(imgW/viewW, imgH/viewH)
        double scale = Math.Max(imgW / _viewW, imgH / _viewH);

        // 뷰 중심을 원점으로 이동 → 스케일 → 이미지 중심 복귀
        double imgCx = (_viewMaskCx - (_viewW / 2.0)) * scale + (imgW / 2.0);
        double imgCy = (_viewMaskCy - (_viewH / 2.0)) * scale + (imgH / 2.0);
        double imgR = _viewMaskR * scale;

        // 원 내부를 둘러싸는 정사각형
        int x = (int)Math.Round(imgCx - imgR);
        int y = (int)Math.Round(imgCy - imgR);
        int s = (int)Math.Round(imgR * 2.0);

        // 이미지 경계 클램프
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        if (x + s > imgW) s = imgW - x;
        if (y + s > imgH) s = imgH - y;

        // 너무 작으면 안전하게 전체 사용
        if (s < 50) return new System.Drawing.Rectangle(0, 0, imgW, imgH);

        return new System.Drawing.Rectangle(x, y, s, s);
    }
    
    // ====== 핵심 2-3: ROI 크롭 후 SDK 호출 ======
    public static Bitmap CropBitmap(Bitmap src, System.Drawing.Rectangle roi)
    {
        var dst = new Bitmap(roi.Width, roi.Height, src.PixelFormat);
        using (var g = System.Drawing.Graphics.FromImage(dst))
        {
            g.DrawImage(src,
                new System.Drawing.Rectangle(0, 0, roi.Width, roi.Height),
                roi,
                System.Drawing.GraphicsUnit.Pixel);
        }

        return dst;
    }
}