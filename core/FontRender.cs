#nullable disable

using FC.UI;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices; // ⚠️ 新增：调用底层 API 必须需要它
using System.Windows.Forms;

namespace FC.Core
{
    public enum ScanMode
    {
        Horizontal, Vertical
    }
    public enum BitOrder
    {
        MSBFirst, LSBFirst
    }

    public class FontRender : IDisposable
    {
        // =================================================================
        // 🛡️ Win32 纯净原生 GDI 沙盒与内存注入 API 声明大本营
        // =================================================================

        // --- 1. 常量定义 ---
        private const int GM_ADVANCED = 2;       // 开启高级 2D 世界矩阵模式
        private const uint DIB_RGB_COLORS = 0;   // 告知 GetDIBits 直接使用原始 RGB 颜色格式

        // --- 2. 图形上下文与画布创建 API ---
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        // --- 3. 字体、画刷创建与画布绘制 API ---
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFontW(
            int nHeight, int nWidth, int nEscapement, int nOrientation,
            int fnWeight, uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut,
            uint fdwCharSet, uint fdwOutputPrecision, uint fdwClipPrecision,
            uint fdwQuality, uint fdwPitchAndFamily, string lpszFace);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint crColor);

        [DllImport("user32.dll")]
        private static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hBrush);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool TextOutW(IntPtr hdc, int x, int y, string lpString, int length);

        // --- 4. GDI 对象控制与渲染模式 API ---
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern uint SetTextColor(IntPtr hdc, uint crColor);

        [DllImport("gdi32.dll")]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        private static extern int SetGraphicsMode(IntPtr hdc, int iMode);

        [DllImport("gdi32.dll")]
        private static extern bool SetWorldTransform(IntPtr hdc, ref XFORM lpxform);

        // --- 5. 核心像素搬运：绕过 Graphics 注入内存的 API ---
        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            [Out] IntPtr lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool RemoveFontResourceExW(string lpszFilename, uint fl, IntPtr pdv);

        // 常量定义：FR_PRIVATE 代表该字体仅对当前进程可见，程序关闭或卸载时自动销毁
        private const uint FR_PRIVATE = 0x10;


        // =================================================================
        // 📦 依赖的 Win32 原生辅助结构体（直接嵌在类内作为私有成员）
        // =================================================================

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left; public int Top; public int Right; public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XFORM
        {
            public float eM11; public float eM12;
            public float eM21; public float eM22;
            public float eDx; public float eDy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight;
            public ushort biPlanes; public ushort biBitCount; public uint biCompression;
            public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter;
            public uint biClrUsed; public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors; // 32位真彩色，由于不使用调色板，只留出首个单元占位即可
        }

        //===========================================================

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

        // 保持原有的内核长驻列表，防止 Win32 内核层死锁
        private static readonly HashSet<string> _registeredFontPaths = new HashSet<string>();

        public void LoadFontFile(string path, float size)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return;

            try
            {
                // 1. 【核心修正】：彻底物理粉碎旧的托管字体和整个 PFC 集合，斩断追加污染！
                _currentFont?.Dispose();
                _pfc?.Dispose();

                // 2. 每次加载都给它一个绝对纯净、没有任何历史包袱的全新家园
                _pfc = new PrivateFontCollection();
                _pfc.AddFontFile(path);

                if (_pfc.Families.Length == 0)
                    return;

                // 3. 稳妥提取当前唯一的这个 FontFamily
                FontFamily currentFamily = _pfc.Families[0];
                _currentFont = new Font(currentFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);

                // 4. Win32 内核层长驻注册（确保 CreateFontW 认识它，此操作不受 PFC 销毁影响）
                if (!_registeredFontPaths.Contains(path))
                {
                    int result = AddFontResourceExW(path, FR_PRIVATE, IntPtr.Zero);
                    if (result > 0)
                    {
                        _registeredFontPaths.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                // 捕捉可能的 IO 或字库文件损坏异常，防止界面崩溃
                System.Diagnostics.Debug.WriteLine($"字体加载失败: {ex.Message}");
            }
        }

        // 核心渲染函数：更新接口以匹配新需求
        public byte[] RenderChar(string text)
        {
            return ConvertTo1Bpp(RenderCharToBitmap(text));
        }

        // =================================================================
        // 🚀 用这个全新重构的方法，替换掉你原本的那个 RenderCharToBitmap 即可！
        // =================================================================
        public Bitmap RenderCharToBitmap(string text)
        {
            // 1. 先把 C# 层的最终接收画布准备好
            Bitmap bmp = new Bitmap(CanvasWidth, CanvasHeight, PixelFormat.Format32bppArgb);
            if (_currentFont == null)
                return bmp;

            // 2. 提前计算缩放矩阵参数
            int baseHeight = (int)Math.Round(_currentFont.Size);
            float sx = ScaleX / 100.0f;
            float sy = ScaleY / 100.0f;
            if (sx <= 0.001f)
                sx = 0.01f;
            if (sy <= 0.001f)
                sy = 0.01f;

            int drawX = (int)Math.Round(OffsetX / sx);
            int drawY = (int)Math.Round(OffsetY / sy);
            // 🚀 【核心修正】：完美兼容系统内置字体与第三方外置 TTF 文件
            string fontName = _currentFont.FontFamily.Name;

            // 如果你使用了 PrivateFontCollection 加载外部文件，且集合里有有效的字体族
            if (_pfc != null && _pfc.Families.Length > 0)
            {
                // 优先提取外部字库在 Windows 内存中注册的真实原生名称
                fontName = _pfc.Families[_pfc.Families.Length - 1].Name;
            }

            // 3. 彻底进入独立的 Win32 原生纯净 GDI 沙盒
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            IntPtr memBitmap = CreateCompatibleBitmap(screenDC, CanvasWidth, CanvasHeight);
            IntPtr hOldBmp = SelectObject(memDC, memBitmap);

            try
            {
                // 4. 原生 GDI 刷白背景
                IntPtr hBrush = CreateSolidBrush(0x00FFFFFF); // 纯白
                RECT rect = new RECT { Left = 0, Top = 0, Right = CanvasWidth, Bottom = CanvasHeight };
                [DllImport("user32.dll")] static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hBrush);
                FillRect(memDC, ref rect, hBrush);
                DeleteObject(hBrush);

                // 5. 原生 GDI 创建硬核无抗锯齿宋体
                IntPtr hFont = CreateFontW(
                    baseHeight, 0, 0, 0,
                    _currentFont.Bold ? 700 : 400, _currentFont.Italic ? 1u : 0u,
                    0, 0, 1, 0, 0, 3, 0, fontName
                );
                IntPtr hOldFont = SelectObject(memDC, hFont);
                SetTextColor(memDC, 0x00000000); // 纯黑字
                SetBkMode(memDC, 1);             // 透明

                // 6. 施加高级世界矩阵 X/Y 轴形变
                const int GM_ADVANCED = 2;
                SetGraphicsMode(memDC, GM_ADVANCED);
                XFORM xform = new XFORM { eM11 = sx, eM12 = 0, eM21 = 0, eM22 = sy, eDx = 0, eDy = 0 };
                SetWorldTransform(memDC, ref xform);

                // 7. 画字（此时字已经完美躺在内存 memBitmap 里面了）
                TextOutW(memDC, drawX, drawY, text, text.Length);

                // =================================================================
                // 🚀 【名场面：纯内存指针越过 Graphics 强行灌入像素】
                // =================================================================

                // 8. 锁定 C# Bitmap 的底层物理内存，拿到它的首地址指针
                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, CanvasWidth, CanvasHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb
                );

                try
                {
                    // 配置读取协议：我们需要把 GDI 的位图转化为 32 位带有 Alpha 通道的真彩色像素块
                    BITMAPINFO bmi = new BITMAPINFO();
                    bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                    bmi.bmiHeader.biWidth = CanvasWidth;
                    // 💡 关键细节：Windows 传统的 DIB 位图纵坐标是反的（自底向上）。
                    // 传入负的高度值（-CanvasHeight）可以让操作系统自动帮我们把图像上下翻转过来，对齐 C# 坐标系！
                    bmi.bmiHeader.biHeight = -CanvasHeight;
                    bmi.bmiHeader.biPlanes = 1;
                    bmi.bmiHeader.biBitCount = 32; // 32位 4字节 RGB
                    bmi.bmiHeader.biCompression = 0; // BI_RGB（不压缩）

                    // 9. 核心搬运：绕过所有托管限制，直接将 GDI 像素阵列吐进 C# 内存指针 bmpData.Scan0 
                    const uint DIB_RGB_COLORS = 0;
                    GetDIBits(memDC, memBitmap, 0, (uint)CanvasHeight, bmpData.Scan0, ref bmi, DIB_RGB_COLORS);
                }
                finally
                {
                    // 10. 搬运完毕，立刻解锁
                    bmp.UnlockBits(bmpData);
                }

                // 清理 GDI 字体
                SelectObject(memDC, hOldFont);
                DeleteObject(hFont);
            }
            finally
            {
                // 11. 释放所有原生 GDI 对象
                SelectObject(memDC, hOldBmp);
                DeleteObject(memBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }

            return bmp;
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