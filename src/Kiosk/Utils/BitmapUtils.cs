using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Kiosk.Utils;

public class BitmapUtils
{
    // Bitmap → BitmapImage 변환
    /*public static BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
    {
        using (var memory = new MemoryStream())
        {
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
    }*/
    
    // 처리용
    public static Bitmap DeepCopy(Bitmap src)
    {
        var dst = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(dst))
            g.DrawImageUnscaled(src, 0, 0);
        return dst;
    }
    
    // 안전 변환
    public static BitmapImage ToFrozenBitmapImage(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
        ms.Position = 0;

        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze(); // <— 크로스스레드 안전
        return img;
    }
}