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
            PreviewBitmap?.Dispose();
            // 直接调用渲染器生成“幻影”位图，不触碰本体 Glyph
            PreviewBitmap = renderer.RenderCharToBitmap(((char)idx).ToString());
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
            AsciiSet[idx].Glyph?.Dispose();
            // 正式抓取当前的矢量预览结果作为本体
            AsciiSet[idx].Glyph = renderer.RenderCharToBitmap(((char)idx).ToString());
            AsciiSet[idx].IsManual = true;
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
                    AsciiSet[i].Glyph?.Dispose();
                    // 批量生成时使用当前全局偏移
                    AsciiSet[i].Glyph = renderer.RenderCharToBitmap(((char)i).ToString());
                    AsciiSet[i].IsManual = false;
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

        // --- 5. 协议导入 (BIN / FONT / BMP) ---

        public bool ImportFromBin(string path, out int canvasW, out int canvasH)
        {
            canvasW = 16; canvasH = 16;
            using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(br.ReadChars(4)) != "FONT") return false;
                canvasH = br.ReadByte();
                canvasW = br.ReadByte();
                int bpc = br.ReadUInt16();
                br.ReadBytes(8);
                for (int i = 0; i < 256; i++) AsciiSet[i].Width = br.ReadByte();
                for (int i = 0; i < 256; i++)
                {
                    byte[] data = br.ReadBytes(bpc);
                    AsciiSet[i].Glyph?.Dispose();
                    AsciiSet[i].Glyph = Convert1BppToBitmap(data, canvasW, canvasH, i); // XOR i 解密
                    AsciiSet[i].IsManual = true;
                }
            }
            return true;
        }

        public void ImportFromFontText(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 3) return;
            int canvasW = int.Parse(lines[1].Split(',')[0]);
            int canvasH = int.Parse(lines[1].Split(',')[1]);
            for (int i = 2; i + 2 < lines.Length; i += 3)
            {
                int ascii = int.Parse(lines[i].Split(',')[0]);
                int w = int.Parse(lines[i + 1].Split(',')[0]);
                byte[] data = lines[i + 2].Trim().Split(',').Select(s => Convert.ToByte(s, 16)).ToArray();
                AsciiSet[ascii].Glyph?.Dispose();
                AsciiSet[ascii].Glyph = Convert1BppToBitmap(data, canvasW, canvasH, ascii); // XOR ascii 解密
                AsciiSet[ascii].Width = w;
                AsciiSet[ascii].IsManual = true;
            }
        }

        public void ImportFromBmp(string path, int cellW, int cellH)
        {
            using (Bitmap src = new Bitmap(path))
            {
                int cols = src.Width / cellW;
                for (int i = 0; i < 256; i++)
                {
                    int x = (i % cols) * cellW;
                    int y = (i / cols) * cellH;
                    if (y + cellH > src.Height) break;
                    Rectangle rect = new Rectangle(x, y, cellW, cellH);
                    AsciiSet[i].Glyph?.Dispose();
                    AsciiSet[i].Glyph = src.Clone(rect, src.PixelFormat);
                    AsciiSet[i].Width = CalculateWidth(AsciiSet[i].Glyph); // 自动测量
                    AsciiSet[i].IsManual = true;
                }
            }
        }

        // --- 6. 导出逻辑 ---
        public void SaveToBin(string path, int canvasW, int canvasH, FontRender renderer)
        {
            int bpc = canvasH * ((canvasW + 7) / 8);
            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                bw.Write(new char[] { 'F', 'O', 'N', 'T' });
                bw.Write((byte)canvasH); bw.Write((byte)canvasW); bw.Write((ushort)bpc);
                bw.Write(new byte[8]);
                for (int i = 0; i < 256; i++) bw.Write((byte)AsciiSet[i].Width);
                for (int i = 0; i < 256; i++)
                {
                    byte[] data = renderer.ConvertTo1Bpp(AsciiSet[i].Glyph);
                    foreach (byte b in data) bw.Write((byte)(b ^ i)); // XOR i 加密
                }
            }
        }

        // --- 辅助工具 ---
        public int CalculateWidth(Bitmap bmp)
        {
            for (int x = bmp.Width - 1; x >= 0; x--)
                for (int y = 0; y < bmp.Height; y++)
                    if (bmp.GetPixel(x, y).R > 20) return x + 1;
            return bmp.Width / 2;
        }

        private Bitmap Convert1BppToBitmap(byte[] data, int w, int h, int xorKey)
        {
            Bitmap bmp = new Bitmap(w, h);
            int stride = (w + 7) / 8;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int idx = y * stride + (x / 8);
                    if (idx < data.Length && (((data[idx] ^ xorKey) & (0x80 >> (x % 8))) != 0))
                        bmp.SetPixel(x, y, Color.White);
                    else
                        bmp.SetPixel(x, y, Color.Black);
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