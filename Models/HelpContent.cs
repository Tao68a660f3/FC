using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FC.Resources
{
    public static class HelpContent
    {
        // GBK 核心逻辑
        public const string GbkAlgorithm =
@"
// [GBK 索引计算参考]

public int GetIndexByCode(ushort code)
{
    byte h = (byte)(code >> 8);
    byte l = (byte)(code & 0xFF);

    // 1. 0xA1A1 - 0xA9FE (846字)
    if (h >= 0xA1 && h <= 0xA9 && l >= 0xA1 && l <= 0xFE)
        return (h - 0xA1) * 94 + (l - 0xA1);

    int baseIndex = 846;

    // 2. GBK扩充5区 (194字): 0xA840 - 0xA9A0 (每行97字)
    if (h >= 0xA8 && h <= 0xA9 && l >= 0x40 && l <= 0xA0)
    {
        return baseIndex + (h - 0xA8) * 97 + (l - 0x40);
    }

    baseIndex += 194;

    // 3. 0xB0A1 - 0xF7FE (6768字)
    if (h >= 0xB0 && h <= 0xF7 && l >= 0xA1 && l <= 0xFE)
        return baseIndex + (h - 0xB0) * 94 + (l - 0xA1);

    baseIndex += 6768;

    // 4. GBK扩充3区 (6112字): 0x8140 - 0xA0FE (每行191字)
    if (h >= 0x81 && h <= 0xA0 && l >= 0x40 && l <= 0xFE)
    {
        return baseIndex + (h - 0x81) * 191 + (l - 0x40);
    }

    baseIndex += 6112;

    // 5. GBK扩充4区 (8148字): 0xAA40 - 0xFDA0 (每行97字)
    if (h >= 0xAA && h <= 0xFD && l >= 0x40 && l <= 0xA0)
    {
        return baseIndex + (h - 0xAA) * 97 + (l - 0x40);
    }

    baseIndex += 8148;

    // 6. 补丁区 (16字): 0xFE40 - 0xFE4F
    if (h == 0xFE && l >= 0x40 && l <= 0x4F)
        return baseIndex + (l - 0x40);

    return -1;
}
";

        // GB2312 核心逻辑
        public const string Gb2312Algorithm =
@"
// [GB2312 标准分区参考]

public int GetIndexByCode(ushort code)
{
    byte h = (byte)(code >> 8);
    byte l = (byte)(code & 0xFF);

    // 标准 94x94 寻址，从 A1A1 开始到 FEFE
    if (h >= 0xA1 && h <= 0xFE && l >= 0xA1 && l <= 0xFE)
    {
        return (h - 0xA1) * 94 + (l - 0xA1);
    }
    return -1;
}
";

        // ASC Bin 核心逻辑
        public const string AscBinAlgorithm =
"""

// [BIN格式ASCII字库读取参考]

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
""";
    }
}