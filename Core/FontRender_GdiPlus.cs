#nullable disable

using System;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace FC.Core
{
    public class FontRenderGdiPlus : FontRenderBase
    {

        public override void LoadFontFile(string path, float size)
        {
            _currentFont?.Dispose();
            _pfc?.Dispose();
            _pfc = new PrivateFontCollection();
            _pfc.AddFontFile(path);
            _currentFont = new Font(_pfc.Families[0], size, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public override Bitmap RenderCharToBitmap(string text)
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
    }
}


        