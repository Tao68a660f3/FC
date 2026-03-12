#nullable disable

using System;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FC.core
{
    public enum ScanMode { Horizontal, Vertical }
    public enum BitOrder { MSBFirst, LSBFirst }

    public class FontRender : IDisposable
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

        // --- 新增：百分比缩放属性 (默认 100 代表 100%) ---
        public int ScaleX { get; set; } = 100;
        public int ScaleY { get; set; } = 100;

        public void LoadFontFile(string path, float size)
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();
            _pfc = new PrivateFontCollection();
            _pfc.AddFontFile(path);
            _currentFont = new Font(_pfc.Families[0], size, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        // 核心渲染函数：更新接口以匹配新需求
        public byte[] RenderChar(string text)
        {
            return ConvertTo1Bpp(RenderCharToBitmap(text));
        }

        public Bitmap RenderCharToBitmap(string text)
        {
            Bitmap bmp = new Bitmap(CanvasWidth, CanvasHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                // --- 1. 计算缩放系数 ---
                float sx = ScaleX / 100.0f;
                float sy = ScaleY / 100.0f;

                // --- 2. 应用缩放变换 ---
                // 使用矩阵可以更稳定地控制缩放
                g.ScaleTransform(sx, sy);

                // --- 3. 绘制文字 ---
                // 关键：为了让 OffsetX/Y 保持物理像素感，绘图坐标需要除以缩放系数
                float drawX = OffsetX / sx;
                float drawY = OffsetY / sy;

                using (Brush b = new SolidBrush(Color.Black))
                {
                    g.DrawString(text, _currentFont, b, drawX, drawY);
                }

                return bmp;
            }
        }

        // 后面原有的 ConvertTo1Bpp, IsPixelBlack, ApplyBit 保持不变...
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
            // 增加极致保险：如果坐标超出当前位图实际尺寸，直接判定为“背景（非黑）”
            if (bmp == null || x < 0 || y < 0 || x >= bmp.Width || y >= bmp.Height)
                return true;

            Color c = bmp.GetPixel(x, y);
            // 根据你的颜色定义：White 是字，黑色或其他是背景
            // 判定 >= 128 确保即使是灰度像素也能被识别为“有像素”
            return c.R <= 128 || c.G <= 128 || c.B <= 128;
        }

        private void ApplyBit(byte[] data, int byteIdx, int bitOffset)
        {
            if (CurrentBitOrder == BitOrder.MSBFirst)
                data[byteIdx] |= (byte)(0x80 >> bitOffset);
            else
                data[byteIdx] |= (byte)(0x01 << bitOffset);
        }

        public void Dispose()
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();
        }
    }
}