#nullable disable

using System;
using System.Drawing;
using System.Drawing.Text;

namespace FC.Core
{
    /// <summary>
    /// 抽象基类：提取两个渲染引擎的公共字段、属性和方法，
    /// 子类只需实现 LoadFontFile 和 RenderCharToBitmap。
    /// </summary>
    public abstract class FontRenderBase : IFontRender
    {
        // ===== 公共字段 =====
        protected PrivateFontCollection _pfc;
        protected Font _currentFont;

        // ===== 公共属性 =====
        public ScanMode CurrentScanMode { get; set; } = ScanMode.Horizontal;
        public BitOrder CurrentBitOrder { get; set; } = BitOrder.MSBFirst;
        public int CanvasWidth { get; set; } = 16;
        public int CanvasHeight { get; set; } = 16;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;
        public int ScaleX { get; set; } = 100;
        public int ScaleY { get; set; } = 100;

        // ===== 子类必须实现 =====
        public abstract void LoadFontFile(string path, float size);
        public abstract Bitmap RenderCharToBitmap(string text);

        // ===== 公共实现（完全相同） =====
        public byte[] RenderChar(string text)
        {
            return ConvertTo1Bpp(RenderCharToBitmap(text));
        }

        public byte[] ConvertTo1Bpp(Bitmap bmp)
        {
            if (CurrentScanMode == ScanMode.Horizontal)
            {
                int bytesPerRow = (CanvasWidth + 7) / 8;
                byte[] data = new byte[bytesPerRow * CanvasHeight];
                for (int y = 0; y < CanvasHeight; y++)
                {
                    for (int x = 0; x < CanvasWidth; x++)
                    {
                        if (IsPixelBlack(bmp, x, y))
                        {
                            int byteIdx = y * bytesPerRow + (x / 8);
                            int bitOffset = (x % 8);
                            ApplyBit(data, byteIdx, bitOffset);
                        }
                    }
                }
                return data;
            }
            else
            {
                int bytesPerCol = (CanvasHeight + 7) / 8;
                byte[] data = new byte[bytesPerCol * CanvasWidth];
                for (int x = 0; x < CanvasWidth; x++)
                {
                    for (int y = 0; y < CanvasHeight; y++)
                    {
                        if (IsPixelBlack(bmp, x, y))
                        {
                            int byteIdx = x * bytesPerCol + (y / 8);
                            int bitOffset = (y % 8);
                            ApplyBit(data, byteIdx, bitOffset);
                        }
                    }
                }
                return data;
            }
        }

        public bool IsPixelBlack(Bitmap bmp, int x, int y)
        {
            if (bmp == null || x < 0 || y < 0 || x >= bmp.Width || y >= bmp.Height)
                return true;

            Color c = bmp.GetPixel(x, y);
            return c.R <= 128 || c.G <= 128 || c.B <= 128;
        }

        private void ApplyBit(byte[] data, int byteIdx, int bitOffset)
        {
            if (CurrentBitOrder == BitOrder.MSBFirst)
                data[byteIdx] |= (byte)(0x80 >> bitOffset);
            else
                data[byteIdx] |= (byte)(0x01 << bitOffset);
        }

        public virtual void Dispose()
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();
        }
    }
}
