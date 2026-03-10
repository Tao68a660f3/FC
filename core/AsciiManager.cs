using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FC.core
{
    /// <summary>
    /// ASCII 字符数据实体
    /// </summary>
    public class AsciiCharEntry
    {
        public byte[] Data { get; set; } = new byte[0]; // 动态长度，由画布大小决定
        public int Width { get; set; } = 8;
        public bool IsManual { get; set; } = false; // 标记是否为手动编辑或已锁定的数据
    }

    public class AsciiManager
    {
        public AsciiCharEntry[] AsciiSet { get; } = new AsciiCharEntry[256];

        public AsciiManager()
        {
            for (int i = 0; i < 256; i++)
                AsciiSet[i] = new AsciiCharEntry();
        }

        // --- 1. 批量渲染逻辑 (从 UI 抽离) ---
        public void BatchRender(FontRender renderer, int canvasW, int canvasH)
        {
            for (int i = 0; i < 256; i++)
            {
                // 仅覆盖未锁定的字符
                if (!AsciiSet[i].IsManual)
                {
                    AsciiSet[i].Data = renderer.RenderChar(((char)i).ToString());
                    AsciiSet[i].Width = CalculateWidth(AsciiSet[i].Data, canvasW, canvasH);
                }
            }
        }

        // --- 2. 导入自定义 .bin 格式 (含 XOR 解密) ---
        public bool ImportFromBin(string path, out int canvasW, out int canvasH)
        {
            canvasW = 16; canvasH = 16;
            try
            {
                using (BinaryReader br = new BinaryReader(File.OpenRead(path)))
                {
                    // 检查文件头
                    if (new string(br.ReadChars(4)) != "FONT") return false;

                    canvasH = br.ReadByte();
                    canvasW = br.ReadByte();
                    int bpc = br.ReadUInt16(); // 每字符字节数
                    br.ReadBytes(8); // 跳过 Padding

                    // 读宽度表
                    for (int i = 0; i < 256; i++)
                        AsciiSet[i].Width = br.ReadByte();

                    // 读数据体并解密
                    for (int i = 0; i < 256; i++)
                    {
                        byte[] raw = br.ReadBytes(bpc);
                        // 执行 XOR i 解密
                        AsciiSet[i].Data = raw.Select(b => (byte)(b ^ i)).ToArray();
                        AsciiSet[i].IsManual = true; // 导入的二进制视为精修数据，锁定
                    }
                }
                return true;
            }
            catch { return false; }
        }

        // --- 3. 导入 .font 文本格式 ---
        public void ImportFromFontText(string path)
        {
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                if (!line.Contains(":")) continue;
                string[] parts = line.Split(':');
                if (int.TryParse(parts[0], out int idx) && idx >= 0 && idx < 256)
                {
                    AsciiSet[idx].Data = HexStringToByteArray(parts[1].Trim());
                    AsciiSet[idx].IsManual = true;
                }
            }
        }

        // --- 4. 从 BMP 网格导入 ---
        public void ImportFromBmp(string path, int cellW, int cellH, FontRender renderer)
        {
            using (Bitmap src = new Bitmap(path))
            {
                int cols = src.Width / cellW;
                for (int i = 0; i < 256; i++)
                {
                    int x = (i % cols) * cellW;
                    int y = (i / cols) * cellH;
                    if (y + cellH > src.Height) break;

                    using (Bitmap cut = src.Clone(new Rectangle(x, y, cellW, cellH), src.PixelFormat))
                    {
                        // 借用渲染器的转换逻辑
                        AsciiSet[i].Data = renderer.ConvertTo1Bpp(cut);
                        AsciiSet[i].Width = cellW;
                        AsciiSet[i].IsManual = true;
                    }
                }
            }
        }

        // --- 5. 导出 .bin (含 XOR 加密) ---
        public void SaveToBin(string path, int canvasW, int canvasH)
        {
            int bpc = canvasH * ((canvasW + 7) / 8);
            using (BinaryWriter bw = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                bw.Write(new char[] { 'F', 'O', 'N', 'T' });
                bw.Write((byte)canvasH);
                bw.Write((byte)canvasW);
                bw.Write((ushort)bpc);
                bw.Write(new byte[8]);

                for (int i = 0; i < 256; i++) bw.Write((byte)AsciiSet[i].Width);

                for (int i = 0; i < 256; i++)
                {
                    // 检查数据长度，防止崩溃
                    byte[] data = AsciiSet[i].Data;
                    if (data.Length != bpc) Array.Resize(ref data, bpc);

                    foreach (byte b in data)
                        bw.Write((byte)(b ^ i)); // XOR i 加密
                }
            }
        }

        // --- 辅助方法：自动计算有效宽度 ---
        public int CalculateWidth(byte[] data, int canvasW, int canvasH)
        {
            if (data == null || data.Length == 0) return canvasW / 2;
            int bytesPerRow = (canvasW + 7) / 8;
            for (int x = canvasW - 1; x >= 0; x--)
            {
                for (int y = 0; y < canvasH; y++)
                {
                    int byteIdx = y * bytesPerRow + (x / 8);
                    if (byteIdx < data.Length && (data[byteIdx] & (0x80 >> (x % 8))) != 0)
                        return x + 1;
                }
            }
            return canvasW / 2;
        }

        private byte[] HexStringToByteArray(string hex)
        {
            hex = hex.Replace(" ", "").Replace("0x", "").Replace(",", "");
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }
    }
}