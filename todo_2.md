好的，现在我对整个项目的结构和命名空间分布有了完整的把握。下面是一份详细的重构路线图。

---

# 🏗️ 重构实施方案

## 当前项目结构

```
FC.Core/                    # 核心层
├── IEncodingProvider.cs     ← 需要升级 (ushort→uint)
├── FontRender.cs            ← 保持不变
├── GeneratorEngine.cs       ← 需要升级 (支持多配置+UTF-8)
└── AsciiManager.cs          ← 保持不变 (ASCII编辑器专用)

UI/
├── Form1.cs                 ← 需要修改 (菜单项改名)
├── UiFactory.cs             ← 保持不变
├── Controls/
│   ├── GbkGeneratorControl.cs  ← 废弃，由新控件替代
│   ├── AsciiGeneratorControl.cs ← 保持不变
│   ├── FontInspectorControl.cs  ← 保持不变
│   └── PixelEditorControl.cs    ← 保持不变
└── Forms/
    ├── FrmHelp.cs            ← 保持不变
    └── FrmProgress.cs        ← 保持不变
```

## 重构后目标结构

```
FC.Core/
├── IEncodingProvider.cs       ← 升级接口
├── FontRender.cs              ← 不变
├── GeneratorEngine.cs         ← 升级 (多配置 + uint + 渲染器缓存)
├── EncodingProviders/         ← 新建：所有编码提供者
│   ├── GbkCustomProvider.cs   ← 从原 IEncodingProvider.cs 拆出
│   ├── Gb2312Provider.cs      ← 从原 IEncodingProvider.cs 拆出
│   ├── Utf8BmpProvider.cs     ← 新增
│   └── Utf8RangeProvider.cs   ← 新增
├── Config/                    ← 新建：配置系统
│   ├── UnicodeRange.cs        ← 区间数据模型
│   ├── CharRangeConfig.cs     ← 单条规则
│   └── ConfigProfile.cs       ← 多规则配置 (JSON)
└── Formats/                   ← 新建：输出格式
    ├── IOutputFormatter.cs    ← 输出格式抽象接口
    └── UfntWriter.cs          ← .ufnt 格式写入器

UI/
├── Form1.cs                   ← 修改：菜单 "字库生成套件"
├── UiFactory.cs               ← 不变
├── Controls/
│   ├── (删除 GbkGeneratorControl.cs)
│   ├── AsciiGeneratorControl.cs ← 不变
│   ├── FontInspectorControl.cs  ← 不变
│   └── PixelEditorControl.cs    ← 不变
├── FontGenerators/            ← 新建：统一字库生成器
│   ├── FontGeneratorControl.cs   ← 主控件 (替代 GbkGeneratorControl)
│   └── FontGeneratorControl.Events.cs  ← 事件绑定 (partial class)
├── Forms/
│   ├── FrmHelp.cs              ← 不变
│   ├── FrmProgress.cs          ← 不变
│   └── FrmConfigRuleEditor.cs  ← 新增：配置规则编辑弹窗
└── Models/
    └── RangePresets.cs         ← 新增：Unicode 预设区间表
```

---

## 分步实施计划（共 14 步）

### 阶段一：数据模型层 (创建新文件，不依赖任何现有代码改动)

#### 步骤 1 — `Core/Config/UnicodeRange.cs`

```
目的：纯数据类，零依赖，可最先编写。
```

```csharp
namespace FC.Core.Config;

public class UnicodeRange
{
    public uint Start { get; set; }
    public uint End { get; set; }
    public string? Label { get; set; }

    public UnicodeRange() { }  // 用于反序列化
    public UnicodeRange(uint start, uint end, string? label = null)
    {
        Start = start; End = end; Label = label;
    }

    public bool Contains(uint cp) => cp >= Start && cp <= End;
    public int Length => (int)(End - Start + 1);
}
```

**可独立测试的点**：构造、Contains、Length

---

#### 步骤 2 — `Core/Config/CharRangeConfig.cs`

```
目的：单条规则，依赖 UnicodeRange。
```

```csharp
namespace FC.Core.Config;

public class CharRangeConfig
{
    public string Name { get; set; } = "未命名规则";
    public List<UnicodeRange> Ranges { get; set; } = new();
    public bool Enabled { get; set; } = true;

    // 字体与渲染参数
    public string FontPath { get; set; } = "";
    public float FontSize { get; set; } = 16;
    public int CanvasWidth { get; set; } = 16;
    public int CanvasHeight { get; set; } = 16;
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;
    public int ScaleX { get; set; } = 100;
    public int ScaleY { get; set; } = 100;
    public string ScanMode { get; set; } = "Horizontal";   // 存字符串，JSON友好
    public string BitOrder { get; set; } = "MSBFirst";

    [JsonIgnore]
    public int TotalChars => Ranges.Sum(r => r.Length);

    public bool ContainsCodePoint(uint cp) =>
        Enabled && Ranges.Any(r => r.Contains(cp));

    /// <summary>
    /// 将当前规则应用到 FontRender 实例
    /// </summary>
    public void ApplyToRender(FontRender renderer)
    {
        renderer.LoadFontFile(FontPath, FontSize);
        renderer.CanvasWidth = CanvasWidth;
        renderer.CanvasHeight = CanvasHeight;
        renderer.OffsetX = OffsetX;
        renderer.OffsetY = OffsetY;
        renderer.ScaleX = ScaleX;
        renderer.ScaleY = ScaleY;
        renderer.CurrentScanMode = Enum.Parse<ScanMode>(ScanMode);
        renderer.CurrentBitOrder = Enum.Parse<BitOrder>(BitOrder);
    }

    /// <summary>
    /// 生成缓存键，用于复用 FontRender 实例
    /// </summary>
    public string GetCacheKey() =>
        $"{FontPath}|{FontSize}|{CanvasWidth}x{CanvasHeight}|" +
        $"{OffsetX},{OffsetY}|{ScaleX},{ScaleY}|{ScanMode}|{BitOrder}";
}
```

**设计要点**：
- `ScanMode` / `BitOrder` 存为字符串而非枚举 → JSON 序列化更干净
- `GetCacheKey()` 用于渲染器缓存
- `ContainsCodePoint()` 供引擎查询

---

#### 步骤 3 — `Core/Config/ConfigProfile.cs`

```
目的：多规则集合 + JSON序列化。
```

```csharp
namespace FC.Core.Config;

public class ConfigProfile
{
    public string Description { get; set; } = "";
    public List<CharRangeConfig> Rules { get; set; } = new();

    /// <summary>按顺序查找首个匹配规则</summary>
    public CharRangeConfig? FindMatch(uint codePoint) =>
        Rules.FirstOrDefault(r => r.ContainsCodePoint(codePoint));

    /// <summary>保存到 JSON 文件</summary>
    public void SaveToFile(string path)
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };
        File.WriteAllText(path, JsonSerializer.Serialize(this, options));
    }

    /// <summary>从 JSON 文件加载</summary>
    public static ConfigProfile LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConfigProfile>(json) 
            ?? new ConfigProfile();
    }

    /// <summary>获取所有唯一的渲染配置键</summary>
    public IEnumerable<string> GetAllCacheKeys() =>
        Rules.Select(r => r.GetCacheKey()).Distinct();
}
```

**可独立测试的点**：序列化→反序列化一致性、FindMatch 优先级逻辑

---

#### 步骤 4 — `Core/Formats/IOutputFormatter.cs`

```
目的：为日后扩展不同输出格式预留抽象。
```

```csharp
namespace FC.Core.Formats;

/// <summary>
/// 输出格式抽象：不同格式实现不同的写入逻辑
/// </summary>
public interface IOutputFormatter
{
    string FormatName { get; }           // "Binary (.bin)", "Unicode (.ufnt)", ...
    string FileExtension { get; }        // ".bin", ".ufnt", ".c", ...
    
    /// <summary>
    /// 写入输出文件
    /// </summary>
    /// <param name="glyphs">已渲染好的点阵数据流 (code, data)</param>
    void Write(Stream stream, IEnumerable<(uint CodePoint, byte[] GlyphData)> glyphs,
               int canvasW, int canvasH, byte config);
}
```

**为什么现在做**：`GeneratorEngine` 的输出端需要改成写 `IOutputFormatter`，而不是直接 `FileStream.Write`。前期定义好接口，后续各格式分别实现。

---

### 阶段二：接口升级 + Provider 拆解

#### 步骤 5 — 升级 `Core/IEncodingProvider.cs`

```
核心改动：uint 化 + 新增 GetUnicodeCodePoint 方法。
```

**重构策略**：在同一文件中修改接口，同时保留旧 `GbkCustomProvider` 和 `Gb2312Provider` 的适配。

```csharp
namespace FC.Core;

public interface IEncodingProvider
{
    string Name { get; }
    string EncodingType { get; }       // "GBK" / "UTF-8"
    int TotalCount { get; }            // 覆盖字符总数

    /// <summary>
    /// 遍历编码值.
    /// GBK: uint 低16位存 GBK 双字节码 (如 0xA1A1)
    /// UTF-8: uint 直接存 Unicode 码点
    /// </summary>
    IEnumerable<uint> GetEncodingStream();

    /// <summary>编码值 → 显示字符串 (用于渲染)</summary>
    string GetString(uint code);

    /// <summary>编码值 → Unicode 码点 (用于配置匹配)</summary>
    uint GetUnicodeCodePoint(uint code);

    /// <summary>编码值 → 序号，-1表示不存在</summary>
    int GetIndexByCode(uint code);
}
```

**GBK 提供者的适配**（重点：原有 `ushort` 的升级兼容）：

```csharp
// 原有 GbkCustomProvider 需要改的地方：
// 1. IEnumerable<ushort> → IEnumerable<uint>
// 2. GetString(ushort)   → GetString(uint)   (强转低16位即可)
// 3. 新增 GetUnicodeCodePoint(uint code) — 核心逻辑:
//       string s = GetString(code);
//       return (uint)s[0];  // 取首个字符的 Unicode 码点
// 4. GetIndexByCode(ushort) → GetIndexByCode(uint)  (强转低16位)
```

**旧代码需要修改的具体位置**（以 `GbkCustomProvider` 为例）：

| 旧 | 新 |
|---|---|
| `IEnumerable<ushort> GetEncodingStream()` | `IEnumerable<uint> GetEncodingStream()` |
| `yield return (ushort)((h << 8) \| l);` | `yield return (uint)((h << 8) \| l);` |
| `string GetString(ushort code)` | `string GetString(uint code)` → `code = code & 0xFFFF` |
| 无此方法 | `uint GetUnicodeCodePoint(uint code)` → `return (uint)GetString(code)[0];` |
| `int GetIndexByCode(ushort code)` | `int GetIndexByCode(uint code)` → `code = code & 0xFFFF` |

**兼容性保障**：外部调用方（如 `GbkGeneratorControl`）使用的是 `IEncodingProvider` 接口，只要接口方法签名变了，就跟着改调用即可。改完之后原有的 GBK 生成功能必须完全可用。

---

#### 步骤 6 — 拆分 EncodingProviders 目录

```
Core/EncodingProviders/
├── GbkCustomProvider.cs      ← 从 IEncodingProvider.cs 中拆出
├── Gb2312Provider.cs         ← 从 IEncodingProvider.cs 中拆出
├── Utf8BmpProvider.cs        ← 新增
└── Utf8RangeProvider.cs      ← 新增
```

**`Utf8BmpProvider.cs`** — BMP 全字库 (U+0000 ~ U+FFFF)：

```csharp
namespace FC.Core.EncodingProviders;

public class Utf8BmpProvider : IEncodingProvider
{
    public string Name => "UTF-8 BMP (U+0000-U+FFFF)";
    public string EncodingType => "UTF-8";
    public int TotalCount => 0x10000;

    public IEnumerable<uint> GetEncodingStream()
    {
        for (uint cp = 0; cp <= 0xFFFF; cp++)
            yield return cp;
    }

    public string GetString(uint code) => char.ConvertFromUtf32((int)code);

    public uint GetUnicodeCodePoint(uint code) => code; // 自身就是Unicode

    public int GetIndexByCode(uint code) => code <= 0xFFFF ? (int)code : -1;
}
```

**`Utf8RangeProvider.cs`** — 自定义区间组合：

```csharp
namespace FC.Core.EncodingProviders;

public class Utf8RangeProvider : IEncodingProvider
{
    private readonly List<UnicodeRange> _ranges;
    private readonly int _totalCount;

    public string Name => "UTF-8 自定义区间";
    public string EncodingType => "UTF-8";
    public int TotalCount => _totalCount;

    public Utf8RangeProvider(List<UnicodeRange> ranges)
    {
        _ranges = ranges.OrderBy(r => r.Start).ToList();
        _totalCount = _ranges.Sum(r => r.Length);
    }

    public IEnumerable<uint> GetEncodingStream()
    {
        foreach (var range in _ranges)
            for (uint cp = range.Start; cp <= range.End; cp++)
                yield return cp;
    }

    public string GetString(uint code) => char.ConvertFromUtf32((int)code);
    public uint GetUnicodeCodePoint(uint code) => code;

    public int GetIndexByCode(uint code)
    {
        int offset = 0;
        foreach (var r in _ranges)
        {
            if (code >= r.Start && code <= r.End)
                return offset + (int)(code - r.Start);
            offset += r.Length;
        }
        return -1;
    }
}
```

---

### 阶段三：引擎升级（最核心改动）

#### 步骤 7 — 升级 `Core/GeneratorEngine.cs`

```
核心改造：
1. 支持 IOutputFormatter 输出
2. 支持 ConfigProfile 多配置覆盖
3. 内置渲染器缓存
4. 保留原有单配置回退兼容
```

```csharp
namespace FC.Core;

public class GeneratorEngine : IDisposable
{
    private readonly FontRender _defaultRenderer;
    private readonly Dictionary<string, FontRender> _rendererCache = new();

    public GeneratorEngine(FontRender defaultRenderer)
    {
        _defaultRenderer = defaultRenderer;
    }

    /// <summary>
    /// 核心生成方法
    /// </summary>
    /// <param name="provider">编码提供者</param>
    /// <param name="formatter">输出格式器</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="configProfile">可选：多配置覆盖</param>
    /// <param name="progressCallback">进度回调</param>
    public async Task GenerateAsync(
        IEncodingProvider provider,
        IOutputFormatter formatter,
        string outputPath,
        ConfigProfile? configProfile,
        Action<int, int>? progressCallback)
    {
        await Task.Run(() =>
        {
            var encodingList = provider.GetEncodingStream().ToList();
            int total = encodingList.Count;
            int current = 0;

            // 预收集所有点阵数据（因为 .ufnt 需要先排序再写）
            var glyphCollector = new List<(uint CodePoint, byte[] GlyphData)>(total);

            foreach (var code in encodingList)
            {
                // 1. 获取 Unicode 码点 (用于配置匹配)
                uint unicode = provider.GetUnicodeCodePoint(code);

                // 2. 查配置 → 选渲染器
                FontRender renderer = ResolveRenderer(unicode, configProfile);

                // 3. 渲染
                string text = provider.GetString(code);
                byte[] glyphData = renderer.RenderChar(text);

                // 4. 收集 (UfntWriter 需要按码点排序后分段写入)
                glyphCollector.Add((unicode, glyphData));

                current++;
                if (current % 100 == 0 || current == total)
                    progressCallback?.Invoke(current, total);
            }

            // 5. 写入输出文件
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                formatter.Write(fs, glyphCollector,
                    _defaultRenderer.CanvasWidth,
                    _defaultRenderer.CanvasHeight,
                    MakeConfigByte(_defaultRenderer));
            }
        });
    }

    /// <summary>
    /// 解析渲染器：有匹配规则 → 从缓存取/新建；否则用默认
    /// </summary>
    private FontRender ResolveRenderer(uint unicode, ConfigProfile? profile)
    {
        if (profile == null) return _defaultRenderer;

        var rule = profile.FindMatch(unicode);
        if (rule == null) return _defaultRenderer;

        string key = rule.GetCacheKey();
        if (!_rendererCache.TryGetValue(key, out var renderer))
        {
            renderer = new FontRender();
            rule.ApplyToRender(renderer);
            _rendererCache[key] = renderer;
        }
        return renderer;
    }

    /// <summary>
    /// 保留向后兼容：无配置时的传统单配置生成
    /// </summary>
    public Task GenerateLegacyAsync(IEncodingProvider provider, string outputPath,
        Action<int, int> progressCallback)
    {
        var binFormatter = new BinFormatter(); // 现有 .bin 格式
        return GenerateAsync(provider, binFormatter, outputPath, null, progressCallback);
    }

    private static byte MakeConfigByte(FontRender r)
    {
        byte cfg = 0;
        // Bit 0: 保留 (宽度模式由 ASCII 编辑器专用，GBK 生成器不用)
        if (r.CurrentScanMode == ScanMode.Vertical)  cfg |= 0x02;
        if (r.CurrentBitOrder == BitOrder.LSBFirst)  cfg |= 0x04;
        return cfg;
    }

    public void Dispose()
    {
        _defaultRenderer?.Dispose();
        foreach (var r in _rendererCache.Values) r.Dispose();
        _rendererCache.Clear();
    }
}
```

**关键设计说明**：

| 设计点 | 说明 |
|--------|------|
| **预收集再写入** | .ufnt 格式需要按码点排序写入，所以不能在遍历时边渲染边写。预收集 List 后由 `IOutputFormatter.Write` 统一处理 |
| **渲染器缓存** | `Dictionary<string, FontRender>` 按 `GetCacheKey()` 复用。10,000 个 CJK 字符命中的是同一个渲染器，只 `LoadFontFile` 一次 |
| **`ResolveRenderer`** | 封装了"有配置→查规则→缓存匹配→回退默认"的完整链路 |
| **`GenerateLegacyAsync`** | UI 侧不传配置时调此方法，与旧代码 100% 行为一致 |

---

#### 步骤 8 — `Core/Formats/UfntWriter.cs`

```
目的：.ufnt 格式写入器，实现 IOutputFormatter。
核心逻辑：排序 → 合并相邻区间 → 写入。
```

```csharp
namespace FC.Core.Formats;

public class UfntWriter : IOutputFormatter
{
    public string FormatName => "Unicode Font (.ufnt)";
    public string FileExtension => ".ufnt";

    public void Write(Stream stream, 
        IEnumerable<(uint CodePoint, byte[] GlyphData)> glyphs,
        int canvasW, int canvasH, byte config)
    {
        // 1. 排序（按码点升序）
        var sorted = glyphs.OrderBy(g => g.CodePoint).ToList();

        // 2. 合并相邻同 BPC 区间
        var ranges = new List<(uint Start, uint End, int BaseIndex)>();
        int idx = 0;
        while (idx < sorted.Count)
        {
            uint start = sorted[idx].CodePoint;
            uint end = start;
            int baseIdx = idx;
            while (idx + 1 < sorted.Count && sorted[idx + 1].CodePoint == end + 1)
            {
                end = sorted[idx + 1].CodePoint;
                idx++;
            }
            ranges.Add((start, end, baseIdx));
            idx++;
        }

        byte rangeCount = (byte)ranges.Count;
        ushort totalGlyphs = (ushort)sorted.Count;
        int bpc = sorted[0].GlyphData.Length;

        // 3. 写 Header (16 bytes)
        WriteHeader(stream, canvasW, canvasH, bpc, config, rangeCount, totalGlyphs);

        // 4. 写 Range Table (N × 10 bytes)
        foreach (var (start, end, baseIdx) in ranges)
        {
            WriteLe32(stream, start);
            WriteLe32(stream, end);
            WriteLe16(stream, (ushort)baseIdx);
        }

        // 5. 写 Glyph Data (TotalGlyphs × BPC)
        foreach (var (_, data) in sorted)
            stream.Write(data, 0, data.Length);

        // 6. 写 Checksum (2 bytes, 累加整个文件)
        // 需要先计算... 实现时先写文件再读回来追加校验和
    }

    private void WriteHeader(Stream s, int w, int h, int bpc, 
        byte config, byte rangeCount, ushort totalGlyphs)
    {
        s.Write(new byte[] { 0x55, 0x46, 0x4E, 0x54 }); // "UFNT"
        s.WriteByte((byte)h);
        s.WriteByte((byte)w);
        WriteLe16(s, (ushort)bpc);
        s.WriteByte(config);
        s.WriteByte(rangeCount);
        WriteLe16(s, totalGlyphs);
        s.Write(new byte[4]); // Reserved
    }

    private void WriteLe16(Stream s, ushort v) { s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8)); }
    private void WriteLe32(Stream s, uint v)  { s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 24)); }
}
```

---

### 阶段四：UI 层重构

#### 步骤 9 — `UI/Models/RangePresets.cs`

```
目的：全局预设区间表，供 UI 下拉选择使用。
```

```csharp
namespace FC.UI.Models;

public static class RangePresets
{
    public record RangePreset(string Name, List<UnicodeRange> Ranges);

    public static readonly List<RangePreset> All = new()
    {
        new("ASCII (U+0020-U+007E)", new() { new(0x0020, 0x007E, "ASCII") }),
        new("CJK统一汉字 (U+4E00-U+9FFF)", new() { new(0x4E00, 0x9FFF, "CJK_Unified") }),
        new("CJK扩展A (U+3400-U+4DBF)", new() { new(0x3400, 0x4DBF, "CJK_ExtA") }),
        new("CJK符号和标点 (U+3000-U+303F)", new() { new(0x3000, 0x303F, "CJK_Symbols") }),
        new("全角ASCII/标点 (U+FF00-U+FFEF)", new() { new(0xFF00, 0xFFEF, "Fullwidth") }),
        new("平假名 (U+3040-U+309F)", new() { new(0x3040, 0x309F, "Hiragana") }),
        new("片假名 (U+30A0-U+30FF)", new() { new(0x30A0, 0x30FF, "Katakana") }),
        new("BMP全字库 (U+0000-U+FFFF)", new() { new(0x0000, 0xFFFF, "BMP") }),
        new("自定义...", new() { new(0, 0, "Custom") }),
    };
}
```

---

#### 步骤 10 — `UI/Forms/FrmConfigRuleEditor.cs`

```
目的：配置规则编辑器弹窗。
可以从主界面导入当前参数，也可以独立填写。
```

**UI 布局**（~500×550 的弹窗）：

```
┌─ 编辑配置规则 ───────────────────────────────────┐
│                                                    │
│  规则名称: [________________________]              │
│                                                    │
│  ── 编码区间 ──────────────────────────────────    │
│  预设: ▾ [CJK统一汉字]                              │
│  或自定义: [U+4E00-U+9FFF] [添加]                  │
│                                                    │
│  已选区间:                                          │
│  ┌────────────────────────────────────────┐        │
│  │ U+4E00 - U+9FFF  (CJK统一汉字)    [X] │        │
│  │ U+3400 - U+4DBF  (CJK扩展A)      [X]  │        │
│  └────────────────────────────────────────┘        │
│                                                    │
│  ── 字体与参数 ────────────────────────────────    │
│  字体: [C:\...\simsun.ttc]      [浏览]            │
│  字号: [16]   画布: [16] × [16]                    │
│  偏移: X:[0]  Y:[2]  缩放: X:[100]%  Y:[100]%    │
│  扫描: ▾ [横向]   位序: ▾ [MSB First]             │
│                                                    │
│  [从主界面导入当前参数]                             │
│                                                    │
│  [确认添加]                          [取消]       │
└────────────────────────────────────────────────────┘
```

**代码结构要点**：

```csharp
namespace FC.UI.Forms;

public partial class FrmConfigRuleEditor : Form
{
    // 属性：编辑完成后外部读取
    public CharRangeConfig? Result { get; private set; }

    // 从主界面导入参数
    public void ImportFromMainUI(FontRender renderer, string fontPath) { ... }
}
```

---

#### 步骤 11 — `UI/FontGenerators/FontGeneratorControl.cs`（主控件）

```
目的：统一字库生成器，替代 GbkGeneratorControl。
```

**类设计**（使用 `partial class` 拆分文件）：

| 文件 | 内容 |
|------|------|
| `FontGeneratorControl.cs` | 主文件：构造函数、控件成员声明 |
| `FontGeneratorControl.Layout.cs` | `InitResponsiveLayout()` — 布局代码 |

**主文件结构**：

```csharp
namespace FC.UI.FontGenerators;

public partial class FontGeneratorControl : UserControl
{
    // === 核心对象 ===
    private FontRender _defaultRenderer;
    private GeneratorEngine _engine;
    private ConfigProfile? _currentProfile;

    // === 控件成员 ===
    // 第1行: 编码模式选择
    private ComboBox cmbEncoding;
    // 第2行: 字体资源
    private TextBox txtFontPath;
    // 第3行: 尺寸与偏移 (同现有)
    // 第4行: 输出格式
    private ComboBox cmbScanMode, cmbBitOrder, cmbOutputFormat;
    // 第5行: 配置管理
    private ListBox lstConfigRules;
    private Button btnLoadConfig, btnSaveConfig, btnExportRule, btnAddRule;
    // 第6行: 执行区
    private Button btnGenerate;
    private Label lblStatus;

    // === 事件绑定 ===
    private void BindEvents() { ... }

    // === 核心业务 ===
    private void OnExportRule() { /* 弹出 FrmConfigRuleEditor，预填当前参数 */ }
    private void OnLoadConfig() { /* 打开 JSON → ConfigProfile.LoadFromFile */ }
    private void OnGenerate()   { /* 构建 provider + 调 engine.GenerateAsync */ }
}
```

**布局改动重点**（与旧 `GbkGeneratorControl` 的区别）：

| 旧 | 新 |
|---|---|
| 4行左侧面板 | 6行左侧面板 |
| 编码模式只有 GBK/GB2312 | 增加 UTF-8 BMP、UTF-8 自定义区间等 |
| 无输出格式选择 | `cmbOutputFormat`：.bin / .ufnt |
| 无配置管理 | 完整的配置管理区 |
| 硬编码开始事件 | 动态选择 formatter + provider |

---

#### 步骤 12 — 注册到 `Form1.cs` 菜单

```csharp
// 修改 InitMenuBar() 中的菜单项
var itemFontGen = new ToolStripMenuItem("字库生成套件(&G)", null, 
    (s, e) => SwitchModule(new FontGeneratorControl()));

// 替换旧的 itemGbk
```

---

### 阶段五：清理

#### 步骤 13 — 删除废弃文件

| 删除 | 原因 |
|------|------|
| `UI/Controls/GbkGeneratorControl.cs` | 由 `FontGeneratorControl` 替代 |
| `UI/Controls/GbkGeneratorControl.resx` | 同上 |

#### 步骤 14 — 回归测试

| 测试用例 | 预期结果 |
|----------|----------|
| GBK_Custom 生成 (无配置) | 与旧版本输出一致 |
| GB2312 生成 (无配置) | 与旧版本输出一致 |
| GBK_Custom + .json 配置 | 命中规则的字符按规则参数渲染 |
| UTF-8 BMP 全字库 (.ufnt) | 文件格式正确，嵌入端可解码 |
| UTF-8 自定义区间 (.ufnt) | 只有目标区间的字符 |
| 导出当前设置为规则 | 生成完整规则，可加载回来 |
| 加载/保存配置 (.json) | 配置文件完整可用 |

---

### 执行依赖图

```
步骤1  UnicodeRange       (无依赖)
步骤2  CharRangeConfig    (依赖步骤1)
步骤3  ConfigProfile      (依赖步骤2)
步骤4  IOutputFormatter   (无依赖)
步骤5  IEncodingProvider 升级 (无依赖)
步骤6  Utf8*Provider      (依赖步骤5)
步骤7  GeneratorEngine     (依赖步骤3,4,5)
步骤8  UfntWriter          (依赖步骤4)
步骤9  RangePresets        (依赖步骤1)
步骤10 FrmConfigRuleEditor (依赖步骤2,9)
步骤11 FontGeneratorControl (依赖步骤5,6,7,8,10)
步骤12 Form1.cs 修改       (依赖步骤11)
步骤13 删除旧文件           (依赖步骤11)
步骤14 回归测试             (全部)
```

**并行执行建议**：
- 阶段一（步骤1-4）：可以一起做，全是独立新文件
- 阶段二（步骤5-6）：串行，接口先升级再写实现
- 阶段三（步骤7-8）：可以并行，引擎和新格式同时写
- 阶段四（步骤9-12）：串行，UI 依赖下层接口
- 阶段五（步骤13-14）：全做完了再清理

---

这就是完整的重构计划。它保证了**每一步都可在不破坏现有功能的前提下增量进行**，核心原则是：

1. **旧代码不动**（除了 `IEncodingProvider` 的接口签名升级和对应的调用方适配）
2. **新加的都是新文件**（容易做版本管理，不会冲突）
3. **`GbkGeneratorControl` 到最后才删**（之前的步骤中它还在正常工作）

你觉得这个方案可行吗？有没有哪个步骤想先讨论得更细一些？