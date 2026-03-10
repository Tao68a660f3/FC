using System;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Imaging;
using System.IO;

namespace FC.core
{
    public enum ScanMode { Horizontal, Vertical } // 横向扫描 vs 纵向扫描
    public enum BitOrder { MSBFirst, LSBFirst }   // 高位在前 (0x80) vs 低位在前 (0x01)

    internal class FontRender : IDisposable
    {
        private PrivateFontCollection _pfc;
        private Font _currentFont;
        public ScanMode CurrentScanMode { get; set; } = ScanMode.Horizontal;
        public BitOrder CurrentBitOrder { get; set; } = BitOrder.MSBFirst;

        // 渲染配置
        public int CanvasWidth { get; set; } = 16;
        public int CanvasHeight { get; set; } = 16;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;

        // 加载外部 .ttf 文件
        public void LoadFontFile(string path, float size)
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();

            _pfc = new PrivateFontCollection();
            _pfc.AddFontFile(path);

            // 使用 Pixel 单位确保点阵精确
            _currentFont = new Font(_pfc.Families[0], size, FontStyle.Regular, GraphicsUnit.Pixel);

            // 默认画布大小等于字号
            CanvasWidth = (int)size;
            CanvasHeight = (int)size;
        }

        // 渲染单个字符并返回点阵字节
        public byte[] RenderChar(string text)
        {
            using (Bitmap bmp = new Bitmap(CanvasWidth, CanvasHeight, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                g.Clear(Color.White);

                using (Brush b = new SolidBrush(Color.Black))
                {
                    g.DrawString(text, _currentFont, b, OffsetX, OffsetY);
                }

                return ConvertTo1Bpp(bmp);
            }
        }

        private byte[] ConvertTo1Bpp(Bitmap bmp)
        {
            if (CurrentScanMode == ScanMode.Horizontal)
            {
                // --- 横向取模：一行一行地扫 ---
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
                // --- 纵向取模：一列一列地扫 (常见于 OLED/LCD) ---
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

        // 辅助函数 1：判断像素是否为黑
        private bool IsPixelBlack(Bitmap bmp, int x, int y) => bmp.GetPixel(x, y).R < 128;

        // 辅助函数 2：根据位序设置字节里的位
        private void ApplyBit(byte[] data, int byteIdx, int bitOffset)
        {
            if (CurrentBitOrder == BitOrder.MSBFirst)
                data[byteIdx] |= (byte)(0x80 >> bitOffset); // 高位在起始 (0x80, 0x40...)
            else
                data[byteIdx] |= (byte)(0x01 << bitOffset); // 低位在起始 (0x01, 0x02...)
        }

        public void Dispose()
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();
        }
    }
}