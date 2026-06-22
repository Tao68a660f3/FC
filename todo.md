# 📝 ASCII 点阵字库工具升级计划 (V2)

## 1. 文件协议定义 (Binary Specification)

目标：在保持现有结构基础上，利用预留位实现跨硬件兼容。

| 偏移 (Offset) | 长度 | 名称 | 说明 |
| --- | --- | --- | --- |
| 0x00 | 4 Bytes | **Magic** | 固定为 `FONT` (0x46, 0x4F, 0x4E, 0x54) |
| 0x04 | 1 Byte | **CanvasH** | 画布高度 (像素) |
| 0x05 | 1 Byte | **CanvasW** | 画布宽度 (像素) |
| 0x06 | 2 Bytes | **BPC** | 每个字符占用的字节数 (小端序) |
| **0x08** | **1 Byte** | **Config** | **配置标志位 (核心升级)** |
| 0x09-0x0F | 7 Bytes | **Reserved** | 预留 (保持 16 字节对齐) |
| 0x10-0x10F | 256 Bytes | **Widths** | 宽度表：记录每个 ASCII 码的实际像素宽度 |
| 0x110-End | - | **Data** | 原始点阵数据 |
| **End+1** | **2 Bytes** | **Checksum** | **全文件累加校验和 (可选)** |

### 🛠 Config 字节位域规划 (3 Bits)

* **Bit 0: WidthMode (宽度解析模式)** (增加选项框 ✅字符已包含间距)

* `0`: **未勾选 -> 自动间距 (Auto-Spacing)** —— **（当前默认）** 渲染程序在宽度表基础上自动 **+n** 像素。
* `1`: **勾选 -> 绝对宽度 (Absolute)** —— 渲染程序完全信任宽度表，不额外加间距（适合像素级精确排版）。


* **Bit 1: ScanDirection (扫描方向)**
* `0`: **横向扫描 (Horizontal)** —— **（当前默认）** 逐行存储（y 增加，然后 x 增加）。
* `1`: **纵向扫描 (Vertical)** —— 逐列存储（针对特定屏幕驱动）。


* **Bit 2: BitOrder (位序)**
* `0`: **大端序 (MSB First)** —— **（当前默认）** 对应 `7 - (x % 8)`。
* `1`: **小端序 (LSB First)** —— 对应 `(x % 8)`。



---

## 2. 编辑器功能升级 (C# / WinForm)

目标：提高生产力，实现“编辑器重、单片机轻”。

* [ ] **自动裁边 (Auto-Crop)**：
* 扫描 Bitmap 列数据，寻找第一个和最后一个非空列。
* 一键更新 `AsciiSet[i].Width` 并左对齐图像。


* [ ] **自动居中 (Auto-Center)**：
* 在固定画布内，根据实际字符宽度自动计算水平偏移，并更新 `AsciiSet[i].Width` 。


* [ ] **校验和计算**：
* 在 `SaveToBin` 结束前，计算所有写入字节的 `uint16` 累加值。



---

## 3. 多平台适配任务 (Backlog)

### 💻 桌面模拟程序 (路牌 App)

* 适配 V2 协议读取，通过 `Config` 字节自动切换渲染逻辑。
* 支持加载多个 `.bin` 库进行对比。

### 嵌入式侧 (STM32 / ESP32)

* **解析层**：重写 `Draw_Char` 函数，读取 `Header[0x08]` 来决定渲染步进。
* **传输校验**：ESP32 通过网络下载字库后，比对末尾 Checksum，防止二进制损坏。

---

**💡 备注：**
这套方案只动了预留的 8 字节里的 3 个 Bit，对现有的文件长度没有破坏性改动（除了可选的末尾校验），是目前最稳妥的升级方式。

---

# 🔤 UTF-8 字库生成工具计划 (.ufnt)

## 0. 背景与问题

**GBK 方式（当前 `.bin`）的隐式索引对 UTF-8 不可行**，因为：

- GBK/GB2312 编码空间填充率接近 100%，可以用数学公式 `index = (区-0xA1)*94 + (位-0xA1)` 直接算出偏移
- Unicode 空间布满空洞（如 U+007E→U+4E00 之间有约 19000 个空码点），扁平数组索引就要 4MB+
- 嵌入式 MCU 的 Flash 通常以 KB 计，必须用**压缩索引**

## 1. 文件格式：`.ufnt` (Unicode Font)

### 1.1 总体布局

```
┌─ 文件布局 ──────────────────────────────────────────────────┐
│                                                              │
│  [HEADER: 16 bytes]                                          │
│  [RANGE TABLE: N × 10 bytes]   (区间索引表，N ≤ 255)         │
│  [GLYPH DATA: TotalGlyphs × BPC bytes]                       │
│  [CHECKSUM: 2 bytes, 可选]                                   │
└──────────────────────────────────────────────────────────────┘
```

### 1.2 Header (16 字节)

| 偏移 | 大小 | 名称 | 说明 |
|------|------|------|------|
| 0x00 | 4 | **Magic** | 固定为 `UFNT` (0x55, 0x46, 0x4E, 0x54) |
| 0x04 | 1 | **CanvasH** | 画布高度 (像素) |
| 0x05 | 1 | **CanvasW** | 画布宽度 (像素) |
| 0x06 | 2 | **BPC** | 每个字符占用的字节数 (小端序) |
| 0x08 | 1 | **Config** | 配置标志位 (同 V2 协议) |
| 0x09 | 1 | **RangeCount** | 区间个数 N (0-255) |
| 0x0A | 2 | **TotalGlyphs** | 字符总数 (小端序) |
| 0x0C | 4 | **Reserved** | 预留 |

### 1.3 Range Table (N × 10 字节)

区间按下限升序排列，PC 端工具确保有序。

| 相对偏移 | 大小 | 名称 | 说明 |
|----------|------|------|------|
| +0x00 | 4 | **StartCode** | 区间起始 Unicode 码点 (uint32 LE) |
| +0x04 | 4 | **EndCode** | 区间终止 Unicode 码点 (uint32 LE) |
| +0x08 | 2 | **GlyphStartIndex** | 区间首个字符在 GLYPH DATA 中的序号 (uint16 LE) |

### 1.4 Glyph Data

按区间顺序连续存储，区间内按码点递增。每个字符固定 BPC 字节。

### 1.5 真实数据示例

```
假设字库含 ASCII(95字) + CJK(20992字) + 全角符号(240字)
BPC = 32 (16×16 横向扫描)

RangeCount = 3
Range[0]: Start=0x0020, End=0x007E, Base=0        → 95字符
Range[1]: Start=0x4E00, End=0x9FFF, Base=95       → 20992字符
Range[2]: Start=0xFF00, End=0xFFEF, Base=21087    → 240字符
TotalGlyphs = 21327

文件大小 = 16(header) + 30(range_table) + 21327×32(glyphs) + 2(checksum)
         ≈ 667 KB
索引开销 = 30 字节  ← 仅占 0.004%！
```

## 2. 嵌入式侧解码算法 (主推方案)

### 2.1 二分查找 (O(log N)，推荐方式)

```c
// 从 .ufnt 字库中查找字符 cp 的点阵数据
// 返回: >=0 为字形序号, -1 表示不在此字库中
int16_t font_get_glyph_index(const uint8_t* font, uint32_t cp) {
    uint8_t n = font[0x09];            // RangeCount
    const uint8_t* rt = font + 0x10;   // range table ptr

    int lo = 0, hi = n - 1;
    while (lo <= hi) {
        int mid = (lo + hi) >> 1;
        const uint8_t* r = rt + mid * 10;
        uint32_t start = le32(r);       // start code
        uint32_t end   = le32(r + 4);   // end code
        uint16_t base  = le16(r + 8);   // base index

        if (cp < start)      hi = mid - 1;
        else if (cp > end)   lo = mid + 1;
        else return (int16_t)(base + (cp - start));
    }
    return -1;
}

// 读取点阵数据到缓冲区
bool font_get_glyph(const uint8_t* font, uint32_t cp, uint8_t* out) {
    int16_t idx = font_get_glyph_index(font, cp);
    if (idx < 0) return false;

    uint16_t bpc   = le16(font + 0x06);
    uint8_t  n     = font[0x09];
    uint32_t offset = 0x10 + n * 10 + idx * bpc;

    memcpy(out, font + offset, bpc);
    return true;
}
```

特点：
- ✅ 无动态内存分配
- ✅ ~25 行 C 代码，任何 MCU 都能跑
- ✅ N ≤ 16 时最快 4 次比较内定位

### 2.2 备选：线性扫描 (更适合 N < 8)

```c
int16_t font_get_glyph_index(const uint8_t* font, uint32_t cp) {
    uint8_t n = font[0x09];
    const uint8_t* rt = font + 0x10;
    for (int i = 0; i < n; i++) {
        uint32_t s = le32(rt + i*10);
        uint32_t e = le32(rt + i*10 + 4);
        if (cp >= s && cp <= e)
            return le16(rt + i*10 + 8) + (cp - s);
    }
    return -1;
}
```

区间极少（< 8 个）时代码更短，无需二分查找的展开逻辑。

### 2.3 备选：Bitmap 页加速（区间较多时）

在 Header 的 Reserved 中预留 32 字节做 bitmap，标记哪些 `0xTT__` 页有字符：

```c
// 第一级快速过滤
if (!(bitmap[cp >> 16] & (1 << ((cp >> 8) & 0xFF))))
    return -1;  // 整页无字符，跳过
// 第二级：页内二分查找
```

## 3. 与多配置生成引擎的关系

```
[PC端 生成过程]
                      ┌─ 规则1: ASCII → fontA, 8×16
主界面参数(默认) ─────┼─ 规则2: CJK   → fontB, 16×16
                      └─ 规则3: 符号  → fontC, 16×16
                             │
                             ▼
                   ConfigurableEngine
                             │
                   遍历编码流，对每个字符:
                      查配置 → 选渲染器 → 取模
                             │
                             ▼
                   自动合并相邻同配置区间
                   写入 .ufnt 文件

[MCU端 消费过程]
         .ufnt 字库文件 (固化在 Flash 中)
                    │
          font_get_glyph(cp, &buffer)
                    │
                    ▼
            直接渲染点阵到屏幕
```

**关键设计**：多配置机制（PC端）和索引格式（MCU端）完全解耦。PC 端工具在生成时自动完成区间合并，MCU 端只关心读取点阵。

## 4. 索引方案对比

| 方案 | 索引开销(10区间) | 查找速度 | 代码复杂度 | 推荐度 |
|------|:---:|:---:|:---:|:---:|
| **区间压缩(二分) ★** | ~100 字节 | O(log N) ~4次 | 极低 (~25行C) | **主推** |
| 区间压缩(线性) | ~100 字节 | O(N) | 极低 (~15行C) | 备选(N<8) |
| Bitmap 页加速 | +32 字节 | O(1)+O(log N) | 低 | 备选(区间多) |
| Block表 (256/块) | 17KB | O(1) | 极低 | 全覆盖字库 |
| 扁平指针数组 | 4MB+ | O(1) | 极低 | 不现实 |

## 5. PC 端实现要点

- [ ] 新增 `Core/Formats/UfntWriter.cs` — .ufnt 文件写入器
- [ ] 在 `FontGeneratorControl` 的输出格式选择中增加 "Unicode 字库 (.ufnt)"
- [ ] 生成时：遍历编码流 → 渲染取模 → 收集 "(字体参数键, code, 点阵)" → 排序 → 合并相邻区间 → 写入
- [ ] 区间合并规则：相邻码点且使用相同（字体+参数）则合并为一个区间
- [ ] 同时生成完整的 `.ufnt` 预览信息（总字数、区间数、大小等）
