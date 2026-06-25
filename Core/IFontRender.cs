#nullable disable

using System;
using System.Drawing;

namespace FC.Core
{
    public interface IFontRender : IDisposable
    {
        ScanMode CurrentScanMode { get; set; }
        BitOrder CurrentBitOrder { get; set; }
        int CanvasWidth { get; set; }
        int CanvasHeight { get; set; }
        int OffsetX { get; set; }
        int OffsetY { get; set; }
        int ScaleX { get; set; }
        int ScaleY { get; set; }

        void LoadFontFile(string path, float size);
        byte[] RenderChar(string text);
        Bitmap RenderCharToBitmap(string text);
        byte[] ConvertTo1Bpp(Bitmap bmp);
        bool IsPixelBlack(Bitmap bmp, int x, int y);
    }
}
