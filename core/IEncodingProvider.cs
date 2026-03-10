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
            // 区间 1 (符号区): 0xA1A1 - 0xA9FE (低位 A1-FE, 不含 7F)
            for (int h = 0xA1; h <= 0xA9; h++)
                for (int l = 0xA1; l <= 0xFE; l++) yield return (ushort)((h << 8) | l);

            // 区间 5 (扩充符号): 0xA840 - 0xA9A0 (低位 40-A0, 需跳过 7F)
            for (int h = 0xA8; h <= 0xA9; h++)
                for (int l = 0x40; l <= 0xA0; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 区间 2 (常用汉字): 0xB0A1 - 0xF7FE (低位 A1-FE, 不含 7F)
            for (int h = 0xB0; h <= 0xF7; h++)
                for (int l = 0xA1; l <= 0xFE; l++) yield return (ushort)((h << 8) | l);

            // 区间 3 (扩充 A 区): 0x8140 - 0xA0FE (低位 40-FE, 需跳过 7F)
            for (int h = 0x81; h <= 0xA0; h++)
                for (int l = 0x40; l <= 0xFE; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 区间 4 (扩充 B 区): 0xAA40 - 0xFEA0 (低位 40-A0, 需跳过 7F)
            for (int h = 0xAA; h <= 0xFE; h++)
                for (int l = 0x40; l <= 0xA0; l++) { if (l == 0x7F) continue; yield return (ushort)((h << 8) | l); }

            // 区间 4b (补丁): 0xFE40 - 0xFE4F
            for (int l = 0x40; l <= 0x4F; l++) yield return (ushort)(0xFE00 | l);
        }

        private IEnumerable<ushort> IterateRegion(ushort start, ushort end, bool skip7F)
        {
            byte startH = (byte)(start >> 8);
            byte startL = (byte)(start & 0xFF);
            byte endH = (byte)(end >> 8);
            byte endL = (byte)(end & 0xFF);

            for (int h = startH; h <= endH; h++)
            {
                // 关键点：除了首行和末行，中间行的低位都是从 0x40 (或 0xA1) 到 0xFE
                int sL = (h == startH) ? startL : (startL < 0xA1 ? 0x40 : 0xA1);
                int eL = (h == endH) ? endL : 0xFE;

                for (int l = sL; l <= eL; l++)
                {
                    if (skip7F && l == 0x7F) continue;
                    yield return (ushort)((h << 8) | (byte)l);
                }
            }
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