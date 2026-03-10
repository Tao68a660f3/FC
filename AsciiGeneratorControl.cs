using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FC.core;

namespace FC
{
    /// <summary>
    /// ASCII 字符数据实体
    /// </summary>
    public class AsciiCharEntry
    {
        public byte[] Data { get; set; } = new byte[0];
        public int Width { get; set; } = 8;
        public bool IsManual { get; set; } = false; // 标记是否被手动编辑过
    }

    /// <summary>
    /// 核心 ASCII 工作站控件
    /// </summary>
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiCharEntry[] _asciiSet = new AsciiCharEntry[256];
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65; // 默认 'A'
        private bool _isUpdatingUI = false; // 防止联动循环触发

        // UI 控件
        private PixelEditorControl pixelEditor;
        private NumericUpDown numAsciiValue, numCanvasW, numCanvasH, numWidth;
        private NumericUpDown numFontSize, numScaleX, numScaleY, numOffsetX, numOffsetY, numBaseline;
        private TextBox txtChar;
        private GroupBox gbTTF;
        private Button btnImport, btnExport, btnBatch;

        public AsciiGeneratorControl()
        {
            InitializeData();
            SetupLayout();
            ResetUIState();
            UpdateCurrentFromData();
        }

        private void InitializeData()
        {
            for (int i = 0; i < 256; i++)
                _asciiSet[i] = new AsciiCharEntry { Data = new byte[64], Width = 8 };
        }

        #region UI 布局构建
        private void SetupLayout()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);

            TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainLayout);

            // --- 左侧：参数区 ---
            FlowLayoutPanel pnlParams = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            mainLayout.Controls.Add(pnlParams, 0, 0);

            // 1. 字符定位
            GroupBox gbIndex = CreateGroup("字符定位 (0-255)", 80);
            numAsciiValue = AddNumeric(gbIndex, "Index", 0, 255, 65, 10, 25);
            txtChar = new TextBox { Location = new Point(180, 25), Width = 40, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, TextAlign = HorizontalAlignment.Center };
            txtChar.TextChanged += (s, e) => {
                if (!_isUpdatingUI && txtChar.Text.Length > 0)
                {
                    numAsciiValue.Value = (int)txtChar.Text[0];
                }
            };
            numAsciiValue.ValueChanged += (s, e) => {
                _isUpdatingUI = true;
                _currentIdx = (int)numAsciiValue.Value;
                txtChar.Text = ((char)_currentIdx).ToString();
                UpdateCurrentFromData();
                _isUpdatingUI = false;
            };
            gbIndex.Controls.Add(txtChar);
            pnlParams.Controls.Add(gbIndex);

            // 2. 画布与宽度
            GroupBox gbSize = CreateGroup("画布与变宽设置", 110);
            numCanvasW = AddNumeric(gbSize, "画布W", 1, 128, 16, 10, 25);
            numCanvasH = AddNumeric(gbSize, "画布H", 1, 128, 16, 150, 25);
            numWidth = AddNumeric(gbSize, "有效宽", 1, 128, 8, 10, 65);
            numWidth.ValueChanged += (s, e) => {
                _asciiSet[_currentIdx].Width = (int)numWidth.Value;
                _asciiSet[_currentIdx].IsManual = true; // 手动改宽度也算精修
                pixelEditor.ActiveWidth = (int)numWidth.Value;
                pixelEditor.Invalidate();
            };
            pnlParams.Controls.Add(gbSize);

            // 3. TTF 参数
            gbTTF = CreateGroup("TTF 渲染引擎 (矢量)", 180);
            numFontSize = AddNumeric(gbTTF, "字号", 1, 100, 16, 10, 25);
            numBaseline = AddNumeric(gbTTF, "基准Y", -50, 50, 0, 150, 25);
            numScaleX = AddNumeric(gbTTF, "比X%", 10, 500, 100, 10, 65);
            numScaleY = AddNumeric(gbTTF, "比Y%", 10, 500, 100, 150, 65);
            numOffsetX = AddNumeric(gbTTF, "移X", -50, 50, 0, 10, 105);
            numOffsetY = AddNumeric(gbTTF, "移Y", -50, 50, 0, 150, 105);

            EventHandler ttfChange = (s, e) => { if (gbTTF.Enabled) RenderCurrentFromTTF(); };
            foreach (var n in new[] { numFontSize, numBaseline, numScaleX, numScaleY, numOffsetX, numOffsetY })
                n.ValueChanged += ttfChange;
            pnlParams.Controls.Add(gbTTF);

            // 4. 操作
            btnImport = CreateBtn("导入 (TTF/BMP/FONT)", Color.DodgerBlue);
            btnImport.Click += (s, e) => ShowImportDialog();
            pnlParams.Controls.Add(btnImport);

            btnBatch = CreateBtn("重渲染所有未锁定字符", Color.Orange);
            btnBatch.Click += (s, e) => BatchRender(false);
            pnlParams.Controls.Add(btnBatch);

            btnExport = CreateBtn("导出 .BIN (XOR加密)", Color.ForestGreen);
            btnExport.Click += (s, e) => SaveBinFile();
            pnlParams.Controls.Add(btnExport);

            // --- 中间：像素编辑器 ---
            pixelEditor = new PixelEditorControl { Dock = DockStyle.Fill, Margin = new Padding(20) };
            pixelEditor.PixelChanged += (data) => {
                _asciiSet[_currentIdx].Data = data;
                _asciiSet[_currentIdx].IsManual = true;
            };
            mainLayout.Controls.Add(pixelEditor, 1, 0);
        }
        #endregion

        #region 核心逻辑
        private void ResetUIState()
        {
            numCanvasW.Enabled = true;
            numCanvasH.Enabled = true;
            gbTTF.Enabled = true;
            numWidth.Enabled = true;
            numCanvasW.BackColor = Color.FromArgb(45, 45, 45);
        }

        private void RenderCurrentFromTTF()
        {
            var entry = _asciiSet[_currentIdx];
            if (entry.IsManual) return;

            _fontRender.CanvasWidth = (int)numCanvasW.Value;
            _fontRender.CanvasHeight = (int)numCanvasH.Value;
            _fontRender.ScaleX = (int)numScaleX.Value;
            _fontRender.ScaleY = (int)numScaleY.Value;
            _fontRender.OffsetX = (int)numOffsetX.Value;
            _fontRender.OffsetY = (int)numBaseline.Value;

            entry.Data = _fontRender.RenderChar(((char)_currentIdx).ToString());
            entry.Width = AutoCalculateWidth(entry.Data);
            UpdateCurrentFromData();
        }

        private int AutoCalculateWidth(byte[] data)
        {
            int w = (int)numCanvasW.Value;
            int h = (int)numCanvasH.Value;
            int bytesPerRow = (w + 7) / 8;
            for (int x = w - 1; x >= 0; x--)
            {
                for (int y = 0; y < h; y++)
                {
                    int byteIdx = y * bytesPerRow + (x / 8);
                    if ((data[byteIdx] & (0x80 >> (x % 8))) != 0) return x + 1;
                }
            }
            return w / 2;
        }

        private void UpdateCurrentFromData()
        {
            var entry = _asciiSet[_currentIdx];
            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;
            pixelEditor.ActiveWidth = entry.Width;
            pixelEditor.CurrentData = (byte[])entry.Data.Clone();
            numWidth.Value = entry.Width;
            pixelEditor.Invalidate();
        }

        private void ShowImportDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体文件|*.ttf;*.otf;*.ttc;*.bmp;*.font" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    ResetUIState();
                    string ext = Path.GetExtension(ofd.FileName).ToLower();
                    if (ext == ".bmp") ImportBmpGrid(ofd.FileName);
                    else if (ext == ".font") ImportFontText(ofd.FileName);
                    else _fontRender.LoadFontFile(ofd.FileName, (float)numFontSize.Value);

                    UpdateCurrentFromData();
                }
            }
        }

        private void ImportBmpGrid(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                int w = (int)numCanvasW.Value;
                int h = (int)numCanvasH.Value;
                // 简单的网格切割逻辑
                int cols = bmp.Width / w;
                for (int i = 0; i < 256; i++)
                {
                    int x = (i % cols) * w;
                    int y = (i / cols) * h;
                    if (y + h > bmp.Height) break;
                    // 此处应有具体的像素转 byte[] 逻辑
                }
            }
        }

        private void ImportFontText(string path)
        {
            // 解析逻辑参考
            numCanvasW.Enabled = numCanvasH.Enabled = gbTTF.Enabled = false;
            numCanvasW.BackColor = Color.DimGray;
        }

        private void SaveBinFile()
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "字库文件|*.bin" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                int h = (int)numCanvasH.Value;
                int maxW = (int)numCanvasW.Value;
                int bpc = h * ((maxW + 7) / 8);

                using (BinaryWriter bw = new BinaryWriter(File.Open(sfd.FileName, FileMode.Create)))
                {
                    bw.Write(new char[] { 'F', 'O', 'N', 'T' });
                    bw.Write((byte)h); bw.Write((byte)maxW);
                    bw.Write((ushort)bpc);
                    bw.Write(new byte[8]);
                    for (int i = 0; i < 256; i++) bw.Write((byte)_asciiSet[i].Width);
                    for (int i = 0; i < 256; i++)
                    {
                        foreach (byte b in _asciiSet[i].Data) bw.Write((byte)(b ^ i));
                    }
                }
                MessageBox.Show("导出成功！");
            }
        }

        private void BatchRender(bool force)
        {
            for (int i = 0; i < 256; i++)
            {
                if (!force && _asciiSet[i].IsManual) continue;
                // 循环渲染逻辑...
            }
            UpdateCurrentFromData();
        }
        #endregion

        #region UI 辅助
        private GroupBox CreateGroup(string t, int h) => new GroupBox { Text = t, Width = 300, Height = h, ForeColor = Color.White, Margin = new Padding(0, 0, 0, 10) };
        private NumericUpDown AddNumeric(Control p, string l, int min, int max, int v, int x, int y)
        {
            p.Controls.Add(new Label { Text = l, Location = new Point(x, y + 3), AutoSize = true, ForeColor = Color.DarkGray });
            var n = new NumericUpDown { Location = new Point(x + 45, y), Width = 55, Minimum = min, Maximum = max, Value = v, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            p.Controls.Add(n); return n;
        }
        private Button CreateBtn(string t, Color c) => new Button { Text = t, Width = 300, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Margin = new Padding(0, 5, 0, 0) };
        #endregion
    }

    /// <summary>
    /// 自定义像素网格控件
    /// </summary>
    public class PixelEditorControl : Control
    {
        public int CanvasW = 16, CanvasH = 16, ActiveWidth = 8;
        public byte[] CurrentData;
        public Action<byte[]> PixelChanged;

        public PixelEditorControl() { this.DoubleBuffered = true; }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (CurrentData == null) return;
            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);

            // 绘制有效区背景
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 0, 120, 215)), 0, 0, ActiveWidth * cellSize, CanvasH * cellSize);
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 50)), ActiveWidth * cellSize, 0, (CanvasW - ActiveWidth) * cellSize, CanvasH * cellSize);

            using (Pen p = new Pen(Color.FromArgb(60, 60, 60)))
            {
                int bytesPerRow = (CanvasW + 7) / 8;
                for (int y = 0; y < CanvasH; y++)
                {
                    for (int x = 0; x < CanvasW; x++)
                    {
                        e.Graphics.DrawRectangle(p, x * cellSize, y * cellSize, cellSize, cellSize);
                        int byteIdx = y * bytesPerRow + (x / 8);
                        if ((CurrentData[byteIdx] & (0x80 >> (x % 8))) != 0)
                            e.Graphics.FillRectangle(Brushes.White, x * cellSize + 1, y * cellSize + 1, cellSize - 1, cellSize - 1);
                    }
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);
            int x = e.X / cellSize, y = e.Y / cellSize;
            if (x >= 0 && x < CanvasW && y >= 0 && y < CanvasH)
            {
                int bytesPerRow = (CanvasW + 7) / 8;
                int byteIdx = y * bytesPerRow + (x / 8);
                if (e.Button == MouseButtons.Left) CurrentData[byteIdx] |= (byte)(0x80 >> (x % 8));
                else if (e.Button == MouseButtons.Right) CurrentData[byteIdx] &= (byte)~(0x80 >> (x % 8));

                PixelChanged?.Invoke(CurrentData);
                Invalidate();
            }
        }
    }
}