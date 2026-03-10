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
            // 按照说明书顺序依次吐出编码
            // 1区: 0xA1A1 - 0xA9FE (RowSize=94, 不跳过7F)
            foreach (var c in IterateRegion(0xA1A1, 0xA9FE, false)) yield return c;

            // 5区: 0xA840 - 0xA9A0 (RowSize=97, 跳过7F)
            foreach (var c in IterateRegion(0xA840, 0xA9A0, true)) yield return c;

            // 2区: 0xB0A1 - 0xF7FE (RowSize=94, 不跳过7F)
            foreach (var c in IterateRegion(0xB0A1, 0xF7FE, false)) yield return c;

            // 3区: 0x8140 - 0xA0FE (RowSize=191, 跳过7F)
            foreach (var c in IterateRegion(0x8140, 0xA0FE, true)) yield return c;

            // 4区: 0xAA40 - 0xFEA0 (RowSize=97, 跳过7F)
            foreach (var c in IterateRegion(0xAA40, 0xFEA0, true)) yield return c;

            // 4区补充: 0xFE40 - 0xFE4F
            foreach (var c in IterateRegion(0xFE40, 0xFE4F, false)) yield return c;
        }

        private IEnumerable<ushort> IterateRegion(ushort start, ushort end, bool skip7F)
        {
            byte startH = (byte)(start >> 8);
            byte endH = (byte)(end >> 8);

            for (byte h = startH; h <= endH; h++)
            {
                // 每行起始和结束的低位
                byte curStartL = (h == startH) ? (byte)(start & 0xFF) : (byte)0x40;
                byte curEndL = (h == endH) ? (byte)(end & 0xFF) : (byte)0xFE;

                for (int l = curStartL; l <= curEndL; l++)
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