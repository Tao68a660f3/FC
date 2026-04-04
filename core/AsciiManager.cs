#nullable disable
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Drawing.Drawing2D;

namespace FC.Core
{
    public class AsciiCharEntry : IDisposable
    {
        public Bitmap Glyph
        {
            get; set;
        } // 本体像素数据
        public int Width { get; set; } = 8; // 导出到协议的宽度
        public bool IsManual { get; set; } = true; // 锁定标志：手动编辑或导入后为 true

        public void Dispose() => Glyph?.Dispose();
    }

    public class AsciiManager : IDisposable
    {
        // V2 协议配置常量
        public const byte CFG_WIDTH_ABS = 0x01;  // Bit 0: 绝对宽度模式
        public const byte CFG_SCAN_VERT = 0x02;  // Bit 1: 纵向扫描 (1) vs 横向 (0)
        public const byte CFG_BIT_LSB = 0x04;  // Bit 2: 小端位序 LSB (1) vs 大端 MSB (0)

        public AsciiCharEntry[] AsciiSet { get; } = new AsciiCharEntry[256];
        public Bitmap PreviewBitmap
        {
            get; private set;
        }

        public AsciiManager()
        {
            for (int i = 0; i < 256; i++)
                AsciiSet[i] = new AsciiCharEntry { Glyph = new Bitmap(16, 16) };
            PreviewBitmap = new Bitmap(16, 16);
        }

        // --- 0. 自动化处理算法 ---

        public void AutoCropHorizontal(int idx)
        {
            if (idx < 0 || idx > 255)
                return;
            Bitmap bmp = AsciiSet[idx].Glyph;
            int firstX = -1, lastX = -1;

            // 1. 扫描当前位图中的像素范围
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    if (bmp.GetPixel(x, y).R > 128)
                    {
                        if (firstX == -1)
                            firstX = x;
                        lastX = x;
                        break;
                    }
                }
            }

            if (firstX == -1)
                return; // 全空不处理

            // 2. 执行物理位移（左对齐）
            if (firstX > 0)
            {
                ApplyShift(idx, -firstX, 0);
            }

            // 3. 重要：裁边后的“有效宽度”就是内容的物理宽度
            AsciiSet[idx].Width = (lastX - firstX) + 1;
            AsciiSet[idx].IsManual = true;
        }

        public void AutoCenter(int idx)
        {
            if (idx < 0 || idx > 255)
                return;

            // --- 第一步：扫描边界 (直接访问当前最新的 Glyph) ---
            int curFirstX = -1, curLastX = -1;

            // 注意：这里直接用 AsciiSet[idx].Glyph.Width，不缓存局部变量
            for (int x = 0; x < AsciiSet[idx].Glyph.Width; x++)
            {
                for (int y = 0; y < AsciiSet[idx].Glyph.Height; y++)
                {
                    if (AsciiSet[idx].Glyph.GetPixel(x, y).R > 128)
                    {
                        if (curFirstX == -1)
                            curFirstX = x;
                        curLastX = x;
                        break;
                    }
                }
            }

            if (curFirstX == -1)
                return;

            // --- 第二步：计算并应用位移 ---
            int contentWidth = (curLastX - curFirstX) + 1;
            int targetX = (AsciiSet[idx].Glyph.Width - contentWidth) / 2;
            int dx = targetX - curFirstX;

            if (dx != 0)
            {
                ApplyShift(idx, dx, 0); // ApplyShift 内部会 Dispose 旧的，生成新的
            }

            // --- 第三步：设置宽度 ---
            AsciiSet[idx].Width = AsciiSet[idx].Glyph.Width;
        }

        // --- 1. 矢量预览接口 (仅反映 FontRender 的参数效果) ---
        public void UpdateVectorPreview(int idx, FontRender renderer)
        {
            if (idx < 0 || idx > 255)
                return;

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
            if (idx < 0 || idx > 255)
                return;
            PreviewBitmap?.Dispose();
            // 基于本体 Glyph 生成平移后的预览图，不触碰本体 Glyph
            PreviewBitmap = ShiftBitmap(AsciiSet[idx].Glyph, dx, dy);
        }

        // --- 3. 物理移位确认 (Master级操作：将位移写死到本体) ---
        public void ApplyShift(int idx, int dx, int dy)
        {
            if (idx < 0 || idx > 255)
                return;

            Bitmap oldBmp = AsciiSet[idx].Glyph;
            int w = oldBmp.Width;
            int h = oldBmp.Height;

            Bitmap newBmp = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                // 关键：清空新位图背景
                g.Clear(Color.Black);
                // 绘制偏移后的旧位图
                g.DrawImage(oldBmp, dx, dy);
            }

            // 核心修复：先替换引用，再销毁旧的，防止悬空指针
            AsciiSet[idx].Glyph = newBmp;
            oldBmp.Dispose();

            AsciiSet[idx].IsManual = true;
        }

        // --- 4. 矢量生成确认 (将矢量底稿写死到本体) ---
        public void GenerateFromVector(int idx, FontRender renderer)
        {
            if (idx < 0 || idx > 255)
                return;

            using (Bitmap raw = renderer.RenderCharToBitmap(((char)idx).ToString()))
            {
                if (raw == null)
                    return;

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
            if (PreviewBitmap == null)
                return;
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
            foreach (var e in AsciiSet)
                e.IsManual = false;
        }

        public void NewBlank(int w, int h)
        {
            for (int i = 0; i < 256; i++)
            {
                AsciiSet[i].Glyph?.Dispose();
                AsciiSet[i].Glyph = new Bitmap(w, h);
                using (Graphics g = Graphics.FromImage(AsciiSet[i].Glyph))
                    g.Clear(Color.Black);
                AsciiSet[i].Width = w / 2;
                AsciiSet[i].IsManual = true; // 纯手工模式起始
            }
        }

        public void ResizeAll(int newW, int newH)
        {
            // 1. 更新所有字符底稿
            for (int i = 0; i < 256; i++)
            {
                Bitmap oldBmp = AsciiSet[i].Glyph;
                // 创建新尺寸的位图，显式指定 32bpp 保证 SetPixel 兼容性
                Bitmap newBmp = new Bitmap(newW, newH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.Black); // 默认底色
                    if (oldBmp != null)
                    {
                        // 实现左上角对齐的 Crop 逻辑：旧图画到新图上，多出的裁掉，缺的补黑
                        g.DrawImage(oldBmp, 0, 0);
                    }
                }

                AsciiSet[i].Glyph = newBmp;
                oldBmp?.Dispose(); // 释放旧内存
            }

            // 2. 关键：同步更新预览图尺寸，否则 UpdateVectorPreview 会报错
            PreviewBitmap?.Dispose();
            PreviewBitmap = new Bitmap(newW, newH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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
        public bool ImportFromBinV2(string path, out int canvasW, out int canvasH, out byte config)
        {
            canvasW = 16;
            canvasH = 16;
            config = 0;
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    if (new string(br.ReadChars(4)) != "FONT")
                        return false;

                    canvasH = br.ReadByte();
                    canvasW = br.ReadByte();
                    int bpc = br.ReadUInt16();


                    config = br.ReadByte(); // 读取 V2 配置字节 (偏移 0x08)
                    br.ReadBytes(7);        // 跳过剩余的 7 字节保留位

                    // 同步扫描模式参数
                    bool isVert = (config & CFG_SCAN_VERT) != 0;
                    bool isLsb = (config & CFG_BIT_LSB) != 0;
                    int stride = isVert ? (canvasH + 7) / 8 : (canvasW + 7) / 8;

                    for (int i = 0; i < 256; i++)
                        AsciiSet[i].Width = br.ReadByte();

                    for (int i = 0; i < 256; i++)
                    {
                        byte[] data = br.ReadBytes(bpc);
                        AsciiSet[i].Glyph?.Dispose();
                        // 使用通用的 EncodeGlyph 的逆向逻辑：DecodeGlyph
                        AsciiSet[i].Glyph = DecodeGlyph(data, canvasW, canvasH, isVert, isLsb, stride);
                        AsciiSet[i].IsManual = true;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private Bitmap DecodeGlyph(byte[] data, int w, int h, bool vert, bool lsb, int stride)
        {
            // 显式创建指定尺寸的位图
            Bitmap bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // 初始化背景为黑色
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
            }

            int mainLimit = vert ? w : h; // 纵向扫描主轴是宽(x)，横向是高(y)
            int subLimit = vert ? h : w;  // 纵向扫描副轴是高(y)，横向是宽(x)

            for (int m = 0; m < mainLimit; m++)
            {
                for (int s = 0; s < subLimit; s++)
                {
                    int bytePos = m * stride + (s / 8);
                    int bitPos = lsb ? (s % 8) : (7 - (s % 8));

                    if (bytePos < data.Length)
                    {
                        bool isOn = (data[bytePos] & (1 << bitPos)) != 0;
                        if (isOn)
                        {
                            // 还原回 X, Y 坐标
                            int resX = vert ? m : s;
                            int resY = vert ? s : m;

                            if (resX < w && resY < h)
                                bmp.SetPixel(resX, resY, Color.White);
                        }
                    }
                }
            }
            return bmp;
        }

        // 增加 out 参数，确保尺寸能带回 UI
        public bool ImportFromFontText(string path, out int canvasW, out int canvasH)
        {
            canvasW = 16;
            canvasH = 16; // 默认值
            string[] lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length < 3)
                return false;

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
                    if (startY + cellH > src.Height)
                        break;

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

        // --- 6. 增强型导出逻辑 (V2) ---

        public void SaveToBinV2(string path, int canvasW, int canvasH, byte config)
        {
            bool isVert = (config & CFG_SCAN_VERT) != 0;
            bool isLsb = (config & CFG_BIT_LSB) != 0;

            int stride = isVert ? (canvasH + 7) / 8 : (canvasW + 7) / 8;
            int bpc = isVert ? canvasW * stride : canvasH * stride;

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                // Header (16字节)
                bw.Write(new char[] { 'F', 'O', 'N', 'T' });
                bw.Write((byte)canvasH);
                bw.Write((byte)canvasW);
                bw.Write((ushort)bpc);
                bw.Write(config);
                bw.Write(new byte[7]); // Reserved

                // Widths (256字节)
                for (int i = 0; i < 256; i++)
                    bw.Write((byte)AsciiSet[i].Width);

                // Data 编码
                for (int i = 0; i < 256; i++)
                {
                    byte[] data = EncodeGlyph(AsciiSet[i].Glyph, canvasW, canvasH, isVert, isLsb, stride, bpc);
                    bw.Write(data);
                }

                // 计算 Checksum (uint16 累加)
                byte[] rawPayload = ms.ToArray();
                ushort checksum = 0;
                foreach (byte b in rawPayload)
                    checksum += b;

                // 写入文件并追加校验和
                File.WriteAllBytes(path, rawPayload);
                using (FileStream fs = new FileStream(path, FileMode.Append))
                using (BinaryWriter cw = new BinaryWriter(fs))
                {
                    cw.Write(checksum);
                }
            }
        }

        private byte[] EncodeGlyph(Bitmap bmp, int w, int h, bool vert, bool lsb, int stride, int bpc)
        {
            byte[] res = new byte[bpc];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (bmp.GetPixel(x, y).R > 128)
                    {
                        int mainIdx = vert ? x : y;
                        int subIdx = vert ? y : x;

                        int bytePos = mainIdx * stride + (subIdx / 8);
                        int bitPos = lsb ? (subIdx % 8) : (7 - (subIdx % 8));
                        res[bytePos] |= (byte)(1 << bitPos);
                    }
                }
            }
            return res;
        }

        // --- 辅助工具 ---
        public int CalculateWidth(Bitmap bmp)
        {
            if (bmp == null)
                return 8;

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
                        if (c.R > 20)
                            return x + 1;
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
            foreach (var e in AsciiSet)
                e.Dispose();
            PreviewBitmap?.Dispose();
        }
    }
}