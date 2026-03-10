#nullable disable

using System;
using System.Drawing;
using System.Windows.Forms;

namespace FC.ui
{
    public class PixelEditorControl : UserControl
    {
        // 核心数据绑定
        public byte[] CurrentData { get; set; }
        public int CanvasW { get; set; } = 16;
        public int CanvasH { get; set; } = 16;
        public int ActiveWidth { get; set; } = 8; // 有效宽度（用于变宽字体）

        public PixelEditorControl()
        {
            this.DoubleBuffered = true; // 开启双缓冲，彻底解决闪烁
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.Cursor = Cursors.Cross;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (CurrentData == null || CurrentData.Length == 0) return;

            Graphics g = e.Graphics;
            // 1. 计算格子大小（保持正方形，取宽高中的最小值）
            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);
            if (cellSize <= 0) cellSize = 1;

            // 计算居中偏移量，让画布在控件正中间
            int offsetX = (Width - CanvasW * cellSize) / 2;
            int offsetY = (Height - CanvasH * cellSize) / 2;

            // 2. 绘制有效宽度遮罩（淡淡的蓝色背景）
            using (SolidBrush maskBrush = new SolidBrush(Color.FromArgb(40, 0, 122, 204)))
            {
                g.FillRectangle(maskBrush, offsetX, offsetY, ActiveWidth * cellSize, CanvasH * cellSize);
            }

            // 3. 绘制像素点和网格
            using (Pen gridPen = new Pen(Color.FromArgb(50, 50, 50)))
            {
                int bytesPerRow = (CanvasW + 7) / 8;

                for (int y = 0; y < CanvasH; y++)
                {
                    for (int x = 0; x < CanvasW; x++)
                    {
                        int rectX = offsetX + x * cellSize;
                        int rectY = offsetY + y * cellSize;

                        // 绘制网格线
                        g.DrawRectangle(gridPen, rectX, rectY, cellSize, cellSize);

                        // 根据位数据判断是否填充（MSBFirst 逻辑）
                        int byteIdx = y * bytesPerRow + (x / 8);
                        if (byteIdx < CurrentData.Length)
                        {
                            if ((CurrentData[byteIdx] & (0x80 >> (x % 8))) != 0)
                            {
                                g.FillRectangle(Brushes.White, rectX + 1, rectY + 1, cellSize - 1, cellSize - 1);
                            }
                        }
                    }
                }
            }

            // 4. 绘制有效宽度边界线（明亮的蓝色）
            using (Pen borderPen = new Pen(Color.DodgerBlue, 2))
            {
                int borderX = offsetX + ActiveWidth * cellSize;
                g.DrawLine(borderPen, borderX, offsetY, borderX, offsetY + CanvasH * cellSize);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) => HandleMouse(e);
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None) HandleMouse(e);
        }

        private void HandleMouse(MouseEventArgs e)
        {
            if (CurrentData == null) return;

            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);
            int offsetX = (Width - CanvasW * cellSize) / 2;
            int offsetY = (Height - CanvasH * cellSize) / 2;

            // 将鼠标坐标转换为网格坐标
            int x = (e.X - offsetX) / cellSize;
            int y = (e.Y - offsetY) / cellSize;

            if (x >= 0 && x < CanvasW && y >= 0 && y < CanvasH)
            {
                int bytesPerRow = (CanvasW + 7) / 8;
                int byteIdx = y * bytesPerRow + (x / 8);

                if (byteIdx < CurrentData.Length)
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        CurrentData[byteIdx] |= (byte)(0x80 >> (x % 8));
                    }
                    else if (e.Button == MouseButtons.Right)
                    {
                        CurrentData[byteIdx] &= (byte)~(0x80 >> (x % 8));
                    }
                    this.Invalidate(); // 触发重绘
                }
            }
        }
    }
}