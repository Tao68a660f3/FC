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
    }
}