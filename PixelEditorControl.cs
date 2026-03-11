#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace FC.ui
{
    public class PixelEditorControl : UserControl
    {
        // --- 核心属性 ---
        private Bitmap _currentBitmap;
        public Bitmap CurrentBitmap
        {
            get => _currentBitmap;
            set { _currentBitmap = value; Invalidate(); }
        }

        public int CanvasW { get; set; } = 16;
        public int CanvasH { get; set; } = 16;
        public int ActiveWidth { get; set; } = 8; // 有效宽度线

        // --- 交互事件 ---
        public event Action DataChanged;

        public PixelEditorControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(20, 20, 20);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (_currentBitmap == null) return;

            // 1. 计算单个像素的缩放尺寸
            float scale = Math.Min((float)this.Width / CanvasW, (float)this.Height / CanvasH) * 0.9f;
            float offsetX = (this.Width - CanvasW * scale) / 2;
            float offsetY = (this.Height - CanvasH * scale) / 2;

            // 2. 绘制网格背景
            using (Pen gridPen = new Pen(Color.FromArgb(50, 50, 50), 1f))
            {
                for (int i = 0; i <= CanvasW; i++)
                    g.DrawLine(gridPen, offsetX + i * scale, offsetY, offsetX + i * scale, offsetY + CanvasH * scale);
                for (int j = 0; j <= CanvasH; j++)
                    g.DrawLine(gridPen, offsetX, offsetY + j * scale, offsetX + CanvasW * scale, offsetY + j * scale);
            }

            // 3. 核心：直接绘制位图中的像素点
            for (int y = 0; y < _currentBitmap.Height; y++)
            {
                for (int x = 0; x < _currentBitmap.Width; x++)
                {
                    Color c = _currentBitmap.GetPixel(x, y);
                    // 只要 R/G/B 任一通道有亮度，就认为有像素 (对应 Color.White)
                    if (c.R > 128 || c.G > 128 || c.B > 128)
                    {
                        g.FillRectangle(Brushes.Lime, offsetX + x * scale + 1, offsetY + y * scale + 1, scale - 1, scale - 1);
                    }
                }
            }

            // 4. 绘制有效宽度指示线 (红色)
            using (Pen p = new Pen(Color.Red, 2f) { DashStyle = DashStyle.Dash })
            {
                float lineX = offsetX + ActiveWidth * scale;
                g.DrawLine(p, lineX, offsetY, lineX, offsetY + CanvasH * scale);
            }

            // 5. 绘制画布边界线 (蓝色)
            g.DrawRectangle(Pens.RoyalBlue, offsetX, offsetY, CanvasW * scale, CanvasH * scale);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            HandleMouse(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                HandleMouse(e);
        }

        private void HandleMouse(MouseEventArgs e)
        {
            if (_currentBitmap == null) return;

            // 逆向计算鼠标点击的像素坐标
            float scale = Math.Min((float)this.Width / CanvasW, (float)this.Height / CanvasH) * 0.9f;
            float offsetX = (this.Width - CanvasW * scale) / 2;
            float offsetY = (this.Height - CanvasH * scale) / 2;

            int px = (int)((e.X - offsetX) / scale);
            int py = (int)((e.Y - offsetY) / scale);

            if (px >= 0 && px < CanvasW && py >= 0 && py < CanvasH)
            {
                // 左键画笔（白色），右键橡皮（黑色）
                Color newColor = (e.Button == MouseButtons.Left) ? Color.White : Color.Black;

                if (_currentBitmap.GetPixel(px, py) != newColor)
                {
                    _currentBitmap.SetPixel(px, py, newColor);
                    Invalidate();
                    DataChanged?.Invoke(); // 触发 UI 同步信号
                }
            }
        }
    }
}