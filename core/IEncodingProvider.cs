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
        int GetIndexByCode(ushort code); //根据编码反查序号
    }

    // 复刻版 GBK 5区排列逻辑
    public class GbkCustomProvider : IEncodingProvider
    {
        public string Name => "GBK_Custom_5Zones";

        public IEnumerable<ushort> GetEncodingStream()
        {
            // 1. 0xA1A1 - 0xA9FE (846字)
            // 逻辑正确：(0xA9-0xA1+1) * 94 = 846
            for (int h = 0xA1; h <= 0xA9; h++)
                for (int l = 0xA1; l <= 0xFE; l++)
                    yield return (ushort)((h << 8) | l);

            // 2. GBK扩充5区 (194字): 0xA840 - 0xA9A0
            // 修正：终点改为 0xA0，不跳过 0x7F。每行 (0xA0-0x40+1) = 97字，两行共 194
            for (int h = 0xA8; h <= 0xA9; h++)
                for (int l = 0x40; l <= 0xA0; l++)
                    yield return (ushort)((h << 8) | l);

            // 3. 0xB0A1 - 0xF7FE (6768字)
            // 逻辑正确：(0xF7-0xB0+1) * 94 = 72 * 94 = 6768
            for (int h = 0xB0; h <= 0xF7; h++)
                for (int l = 0xA1; l <= 0xFE; l++)
                    yield return (ushort)((h << 8) | l);

            // 4. GBK扩充3区 (6112字): 0x8140 - 0xA0FE
            // 修正：终点改为 0xFE，不跳过 0x7F。每行 (0xFE-0x40+1) = 191字，32行共 6112
            for (int h = 0x81; h <= 0xA0; h++)
                for (int l = 0x40; l <= 0xFE; l++)
                    yield return (ushort)((h << 8) | l);

            // 5. GBK扩充4区 (8148字): 0xAA40 - 0xFEA0
            // 修正：将原有的 5 和 6 整合，终点改为 0xFE，低位到 0xA0。
            // 计算：(0xFE-0xAA+1) = 85行。每行 (0xA0-0x40+1) = 97字。85 * 97 = 8245? 
            // 不对，文档说扩充4区到 0xFE4F 结束。我们还是分两段写，确保总数 22084。

            // 5a. 0xAA40 - 0xFDA0 (8148字)
            // (0xFD-0xAA+1) = 84行。84 * 97 = 8148字。
            for (int h = 0xAA; h <= 0xFD; h++)
                for (int l = 0x40; l <= 0xA0; l++)
                    yield return (ushort)((h << 8) | l);

            // 6. 补丁区 (16字): 0xFE40 - 0xFE4F
            // (0x4F-0x40+1) = 16字。
            for (int l = 0x40; l <= 0x4F; l++)
                yield return (ushort)(0xFE00 | l);
        }

        public string GetString(ushort code)
        {
            return Encoding.GetEncoding("GBK").GetString(new byte[] { (byte)(code >> 8), (byte)(code & 0xFF) });
        }

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
    }
}