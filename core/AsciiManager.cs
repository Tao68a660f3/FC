#nullable disable
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Drawing2D;

namespace FC.core
{
    public class AsciiCharEntry : IDisposable
    {
        public Bitmap Glyph { get; set; } // 本体像素数据
        public int Width { get; set; } = 8; // 导出到协议的宽度
        public bool IsManual { get; set; } = true; // 锁定标志：手动编辑或导入后为 true

        public void Dispose() => Glyph?.Dispose();
    }

    public class AsciiManager : IDisposable
    {
        public AsciiCharEntry[] AsciiSet { get; } = new AsciiCharEntry[256];

        // 预览图：滑块拖动时实时生成，不破坏本体
        public Bitmap PreviewBitmap { get; private set; }

        public AsciiManager()
        {
            for (int i = 0; i < 256; i++)
                AsciiSet[i] = new AsciiCharEntry { Glyph = new Bitmap(16, 16) };
            PreviewBitmap = new Bitmap(16, 16);
        }

        // --- 1. 矢量预览接口 (仅反映 FontRender 的参数效果) ---
        public void UpdateVectorPreview(int idx, FontRender renderer)
        {
            if (idx < 0 || idx > 255) return;

            using (Bitmap raw = renderer.RenderCharToBitmap(((char)idx).ToString()))
            {
                PreviewBitmap?.Dispose();
                // 预览时也进行处理，确保预览看到的颜色/宽度与生成后一致
                PreviewBitmap = ProcessRenderedBitmap(raw, out _);
            }
        }

        // --- 2. 位图平移预览接口 (反映对本体 Glyph 的物理位移效果) ---
        public void UpdateShiftPreview(int idx, int dx, int dy)
        {
            if (idx < 0 || idx > 255) return;
            PreviewBitmap?.Dispose();
            // 基于本体 Glyph 生成平移后的预览图，不触碰本体 Glyph
            PreviewBitmap = ShiftBitmap(AsciiSet[idx].Glyph, dx, dy);
        }

        // --- 3. 物理移位确认 (Master级操作：将位移写死到本体) ---
        public void ApplyShift(int idx, int dx, int dy)
        {
            if (idx < 0 || idx > 255 || (dx == 0 && dy == 0)) return;

            Bitmap oldBmp = AsciiSet[idx].Glyph;
            // 直接利用已有的 ShiftBitmap 逻辑生成新图
            AsciiSet[idx].Glyph = ShiftBitmap(oldBmp, dx, dy);

            oldBmp.Dispose();
            AsciiSet[idx].IsManual = true; // 物理操作后自动锁定
        }

        // --- 4. 矢量生成确认 (将矢量底稿写死到本体) ---
        public void GenerateFromVector(int idx, FontRender renderer)
        {
            if (idx < 0 || idx > 255) return;

            using (Bitmap raw = renderer.RenderCharToBitmap(((char)idx).ToString()))
            {
                if (raw == null) return;

                AsciiSet[idx].Glyph?.Dispose();
                // 调用抽离逻辑
                AsciiSet[idx].Glyph = ProcessRenderedBitmap(raw, out int w);
                AsciiSet[idx].Width = w;
                AsciiSet[idx].IsManual = true; // 确认生成后锁定
            }
        }

        // --- 5. 内部平移算法 (私有工具) ---
        private Bitmap ShiftBitmap(Bitmap src, int dx, int dy)
        {
            Bitmap newBmp = new Bitmap(src.Width, src.Height);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.Clear(Color.Black); // 背景补黑
                g.InterpolationMode = InterpolationMode.NearestNeighbor; // 保持像素锐利
                g.DrawImage(src, dx, dy);
            }
            return newBmp;
        }

        // --- 3. 落笔确认 (Apply) ---
        public void ApplyToCurrent(int idx, int newWidth)
        {
            if (PreviewBitmap == null) return;
            AsciiSet[idx].Glyph?.Dispose();
            AsciiSet[idx].Glyph = new Bitmap(PreviewBitmap);
            AsciiSet[idx].Width = newWidth;
            AsciiSet[idx].IsManual = true; // 自动锁定
        }

        // --- 4. 批量与管理 ---
        public void BatchRender(FontRender renderer, bool forceAll)
        {
            for (int i = 0; i < 256; i++)
            {
                if (forceAll || !AsciiSet[i].IsManual)
                {
                    using (Bitmap raw = renderer.RenderCharToBitmap(((char)i).ToString()))
                    {
                        AsciiSet[i].Glyph?.Dispose();
                        // 调用抽离逻辑：自动反转颜色并获取宽度
                        AsciiSet[i].Glyph = ProcessRenderedBitmap(raw, out int w);
                        AsciiSet[i].Width = w;
                        AsciiSet[i].IsManual = false;
                    }
                }
            }
        }

        public void UnlockAll()
        {
            foreach (var e in AsciiSet) e.IsManual = false;
        }

        public void NewBlank(int w, int h)
        {
            for (int i = 0; i < 256; i++)
            {
                AsciiSet[i].Glyph?.Dispose();
                AsciiSet[i].Glyph = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(AsciiSet[i].Glyph)) g.Clear(Color.Black);
                AsciiSet[i].Width = w / 2;
                AsciiSet[i].IsManual = true; // 纯手工模式起始
            }
        }

        private Bitmap ProcessRenderedBitmap(Bitmap rawBmp, out int measuredWidth)
        {
            if (rawBmp == null)
            {
                measuredWidth = 8;
                return new Bitmap(16, 16);
            }

            // 1. 标准化格式并处理颜色反转
            // 渲染器输出是白底黑字，我们要转成黑底白字（White代表点）
            Bitmap processed = new Bitmap(rawBmp.Width, rawBmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int y = 0; y < rawBmp.Height; y++)
            {
                for (int x = 0; x < rawBmp.Width; x++)
                {
                    Color c = rawBmp.GetPixel(x, y);
                    processed.SetPixel(x, y, c.R < 128 ? Color.White : Color.Black);
                }
            }

            // 2. 自动测量物理宽度
            measuredWidth = CalculateWidth(processed);

            return processed;
        }

        // --- 5. 协议导入 (BIN / FONT / BMP) ---

        public bool ImportFromBin(string path, out int canvasW, out int canvasH)
        {
            canvasW = 16; canvasH = 16;
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    if (new string(br.ReadChars(4)) != "FONT") return false;
                    canvasH = br.ReadByte();
                    canvasW = br.ReadByte();
                    int bpc = br.ReadUInt16(); // 每字符占用的字节数
                    br.ReadBytes(8); // 跳过保留位

                    for (int i = 0; i < 256; i++) AsciiSet[i].Width = br.ReadByte();

                    for (int i = 0; i < 256; i++)
                    {
                        byte[] data = br.ReadBytes(bpc);
                        AsciiSet[i].Glyph?.Dispose();
                        // 关键：Convert1BppToBitmap 内部已经包含了 XOR i 和位解析逻辑
                        AsciiSet[i].Glyph = Convert1BppToBitmap(data, canvasW, canvasH, false, i);
                        AsciiSet[i].IsManual = true;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        // 增加 out 参数，确保尺寸能带回 UI
        public bool ImportFromFontText(string path, out int canvasW, out int canvasH)
        {
            canvasW = 16; canvasH = 16; // 默认值
            string[] lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length < 3) return false;

            try
            {
                // 1. 解析并导出画布尺寸
                var sizeParts = lines[1].Split(',');
                canvasW = int.Parse(sizeParts[0]);
                canvasH = int.Parse(sizeParts[1]);

                for (int i = 2; i + 2 < lines.Length; i += 3)
                {
                    int ascii = int.Parse(lines[i].Split(',')[0]);
                    int charW = int.Parse(lines[i + 1].Split(',')[0]);

                    // 处理数据行，支持末尾带逗号的情况
                    byte[] data = lines[i + 2].TrimEnd(',')
                                  .Split(',')
                                  .Select(s => Convert.ToByte(s.Trim(), 16))
                                  .ToArray();

                    if (ascii >= 0 && ascii < 256)
                    {
                        AsciiSet[ascii].Glyph?.Dispose();
                        // 使用解析出的尺寸创建位图
                        AsciiSet[ascii].Glyph = Convert1BppToBitmap(data, canvasW, canvasH, true, ascii);
                        AsciiSet[ascii].Width = charW;
                        AsciiSet[ascii].IsManual = true;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public void ImportFromBmp(string path, int cellW, int cellH)
        {
            using (Bitmap src = new Bitmap(path))
            {
                int cols = src.Width / cellW;
                for (int i = 0; i < 256; i++)
                {
                    int startX = (i % cols) * cellW;
                    int startY = (i / cols) * cellH;
                    if (startY + cellH > src.Height) break;

                    Bitmap target = new Bitmap(cellW, cellH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    for (int y = 0; y < cellH; y++)
                    {
                        for (int x = 0; x < cellW; x++)
                        {
                            // 获取 BMP 原始像素
                            Color c = src.GetPixel(startX + x, startY + y);

                            // 逻辑翻转：如果原始是黑色（或暗色），设为 White（有字）
                            // 如果原始是白色，设为 Black（背景）
                            bool isText = c.R < 128;
                            target.SetPixel(x, y, isText ? Color.White : Color.Black);
                        }
                    }

                    AsciiSet[i].Glyph?.Dispose();
                    AsciiSet[i].Glyph = target;
                    AsciiSet[i].Width = CalculateWidth(target); // 自动测量
                    AsciiSet[i].IsManual = true;
                }
            }
        }

        // --- 6. 导出逻辑 ---
        public void SaveToBin(string path, int canvasW, int canvasH, FontRender renderer)
        {
            int rowStride = (canvasW + 7) / 8;
            int bpc = canvasH * rowStride;

            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                // 1. Header (16字节) - 严格对齐 Python 的 <BBH
                bw.Write(new char[] { 'F', 'O', 'N', 'T' });
                bw.Write((byte)canvasH);
                bw.Write((byte)canvasW);
                bw.Write((ushort)bpc); // < 代表小端序，BinaryWriter 默认就是小端
                bw.Write(new byte[8]);

                // 2. 宽度表 (256字节)
                for (int i = 0; i < 256; i++)
                {
                    bw.Write((byte)AsciiSet[i].Width);
                }

                // 3. 点阵数据 (核心修复)
                for (int i = 0; i < 256; i++)
                {
                    Bitmap bmp = AsciiSet[i].Glyph;
                    // 确保尺寸一致，如果不一致则在内存绘制一个匹配的
                    Bitmap targetBmp = bmp;
                    bool isTemp = false;
                    if (bmp.Width != canvasW || bmp.Height != canvasH)
                    {
                        targetBmp = new Bitmap(canvasW, canvasH);
                        using (Graphics g = Graphics.FromImage(targetBmp))
                        {
                            g.Clear(Color.Black);
                            g.DrawImage(bmp, 0, 0);
                        }
                        isTemp = true;
                    }

                    // --- 按照 Python 母本逻辑手动转码 ---
                    byte[] charData = new byte[bpc];
                    for (int y = 0; y < canvasH; y++)
                    {
                        for (int x = 0; x < canvasW; x++)
                        {
                            Color c = targetBmp.GetPixel(x, y);
                            // 如果是白色像素(有字)，则设为 1
                            if (c.R > 128)
                            {
                                int bytePos = y * rowStride + (x / 8);
                                int bitPos = 7 - (x % 8); // 关键：大端序排列 (MSB在左)
                                charData[bytePos] |= (byte)(1 << bitPos);
                            }
                        }
                    }

                    // 写入
                    for (int j = 0; j < charData.Length; j++)
                    {
                        bw.Write((byte)charData[j]);
                    }

                    if (isTemp)
                        targetBmp.Dispose();
                }
            }
        }

        // --- 辅助工具 ---
        public int CalculateWidth(Bitmap bmp)
        {
            if (bmp == null) return 8;

            // 获取位图当前的真实物理尺寸，严禁使用任何外部变量
            int realW = bmp.Width;
            int realH = bmp.Height;

            // 从右往左扫描
            for (int x = realW - 1; x >= 0; x--)
            {
                for (int y = 0; y < realH; y++)
                {
                    // 增加一层极致的保险检查
                    if (x >= 0 && x < realW && y >= 0 && y < realH)
                    {
                        Color c = bmp.GetPixel(x, y);
                        // 只要有像素（R通道判定），就返回当前宽度
                        if (c.R > 20) return x + 1;
                    }
                }
            }
            // 如果是全黑，默认返回一半宽度
            return realW / 2;
        }

        // 修正后的位转换函数：保持纯净，只做一次解密和一次位判定
        private Bitmap Convert1BppToBitmap(byte[] data, int w, int h, bool useXor, int xorKey)
        {
            // 强制使用 32bpp 避免 SetPixel 崩溃
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int stride = (w + 7) / 8; // 每行占用的字节数

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int byteIdx = y * stride + (x / 8);
                    if (byteIdx < data.Length)
                    {
                        byte b = (byte)data[byteIdx];
                        // 1. 仅在此处执行一次 XOR 解密
                        if (useXor)
                        {
                            b = (byte)(b ^ xorKey);
                        }

                        // 2. 根据大端序提取位：第0列对应 0x80, 第1列对应 0x40...
                        // 使用 (0x80 >> (x % 8)) 是最直观的 MSB 提取方式
                        bool isOn = (b & (0x80 >> (x % 8))) != 0;

                        bmp.SetPixel(x, y, isOn ? Color.White : Color.Black);
                    }
                }
            }
            return bmp;
        }

        public void Dispose()
        {
            foreach (var e in AsciiSet) e.Dispose();
            PreviewBitmap?.Dispose();
        }
    }
}