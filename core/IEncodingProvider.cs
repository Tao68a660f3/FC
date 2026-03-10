using System;
using System.Collections.Generic;
using System.Text;

namespace FC.core
{
    // 编码提供者接口：预留给未来多国语言
    public interface IEncodingProvider
    {
        string Name { get; }
        IEnumerable<ushort> GetEncodingStream();
        string GetString(ushort code);
    }

    // 复刻版 GBK 5区排列逻辑
    public class GbkCustomProvider : IEncodingProvider
    {
        public string Name => "GBK_Custom_5Zones";

        public IEnumerable<ushort> GetEncodingStream()
        {
            // 1. 0xA1A1 - 0xA9FE (846字)
            for (int h = 0xA1; h <= 0xA9; h++)
                for (int l = 0xA1; l <= 0xFE; l++) yield return (ushort)((h << 8) | l);

            // 2. GBK扩充5区 (194字): 0xA840 - 0xA9A1
            for (int h = 0xA8; h <= 0xA9; h++)
                for (int l = 0x40; l <= 0xA1; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 3. 0xB0A1 - 0xF7FE (6768字)
            for (int h = 0xB0; h <= 0xF7; h++)
                for (int l = 0xA1; l <= 0xFE; l++) yield return (ushort)((h << 8) | l);

            // 4. GBK扩充3区 (6112字): 0x8140 - 0xA0FF
            // 注意：低位扫到 0xFF 才能凑够 191 个有效字符
            for (int h = 0x81; h <= 0xA0; h++)
                for (int l = 0x40; l <= 0xFF; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 5. GBK扩充4区 (8148字): 0xAA40 - 0xFDA1
            for (int h = 0xAA; h <= 0xFD; h++)
                for (int l = 0x40; l <= 0xA1; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 6. 补丁区 (16字): 0xFE40 - 0xFE4F
            for (int l = 0x40; l <= 0x4F; l++) yield return (ushort)(0xFE00 | l);
        }

        public string GetString(ushort code)
        {
            return Encoding.GetEncoding("GBK").GetString(new byte[] { (byte)(code >> 8), (byte)(code & 0xFF) });
        }
    }

    // 标准 GB2312 逻辑 (用于生成标准 HZK 字库)
    public class Gb2312Provider : IEncodingProvider
    {
        public string Name => "GB2312_Standard";

        public IEnumerable<ushort> GetEncodingStream()
        {
            for (byte q = 0xA1; q <= 0xFE; q++) // 区
                for (byte w = 0xA1; w <= 0xFE; w++) // 位
                    yield return (ushort)((q << 8) | w);
        }

        public string GetString(ushort code) => Encoding.GetEncoding("GBK").GetString(new byte[] { (byte)(code >> 8), (byte)(code & 0xFF) });
    }
}