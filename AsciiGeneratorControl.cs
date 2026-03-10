#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using FC.core;

namespace FC
{
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiManager _mgr = new AsciiManager();
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65; // 默认 'A'

        // UI 控件
        private PixelEditorControl pixelEditor;
        private NumericUpDown numAsciiValue, numCanvasW, numCanvasH, numWidth;
        private NumericUpDown numFontSize, numScaleX, numScaleY, numOffsetX, numOffsetY, numBaseline;
        private TextBox txtChar;
        private GroupBox gbTTF;

        public AsciiGeneratorControl()
        {
            SetupResponsiveLayout();
            BindEvents();
            SyncUI();
        }

        private void SetupResponsiveLayout()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);

            // 主架构：左侧固定宽度 330px 参数区，右侧剩余空间给编辑器
            TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(mainLayout);

            // 左侧：垂直流式面板 (解决死坐标的核心)
            FlowLayoutPanel pnlParams = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10)
            };
            mainLayout.Controls.Add(pnlParams, 0, 0);

            // 1. 字符定位组
            GroupBox gbIndex = CreateGroupBox("字符定位 (0-255)", 80);
            TableLayoutPanel gridIndex = CreateInnerGrid(1, 3);
            numAsciiValue = AddGridNumeric(gridIndex, "Index", 0, 255, 65, 0, 0);
            txtChar = new TextBox { Width = 45, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White, TextAlign = HorizontalAlignment.Center, Margin = new Padding(5, 5, 0, 0) };
            gridIndex.Controls.Add(txtChar, 2, 0);
            gbIndex.Controls.Add(gridIndex);
            pnlParams.Controls.Add(gbIndex);

            // 2. 画布设置组
            GroupBox gbSize = CreateGroupBox("画布与变宽设置", 115);
            TableLayoutPanel gridSize = CreateInnerGrid(2, 2);
            numCanvasW = AddGridNumeric(gridSize, "画布W", 1, 128, 16, 0, 0);
            numCanvasH = AddGridNumeric(gridSize, "画布H", 1, 128, 16, 1, 0);
            numWidth = AddGridNumeric(gridSize, "有效宽", 1, 128, 8, 0, 1);
            gbSize.Controls.Add(gridSize);
            pnlParams.Controls.Add(gbSize);

            // 3. TTF 参数组 (3行2列)
            gbTTF = CreateGroupBox("TTF 渲染引擎 (矢量)", 160);
            TableLayoutPanel gridTTF = CreateInnerGrid(3, 2);
            numFontSize = AddGridNumeric(gridTTF, "字号", 1, 128, 16, 0, 0);
            numBaseline = AddGridNumeric(gridTTF, "基准Y", -64, 64, 0, 1, 0);
            numScaleX = AddGridNumeric(gridTTF, "比X%", 10, 500, 100, 0, 1);
            numScaleY = AddGridNumeric(gridTTF, "比Y%", 10, 500, 100, 1, 1);
            numOffsetX = AddGridNumeric(gridTTF, "移X", -64, 64, 0, 0, 2);
            numOffsetY = AddGridNumeric(gridTTF, "移Y", -64, 64, 0, 1, 2);
            gbTTF.Controls.Add(gridTTF);
            pnlParams.Controls.Add(gbTTF);

            // 4. 操作按钮
            pnlParams.Controls.Add(CreateStyledButton("导入 (TTF/BMP/FONT)", Color.DodgerBlue, OnImportClick));
            pnlParams.Controls.Add(CreateStyledButton("批量生成 (覆盖未锁定)", Color.Orange, OnBatchRender));
            pnlParams.Controls.Add(CreateStyledButton("导出 .BIN 字库", Color.ForestGreen, OnExportClick));

            // 中间编辑器
            pixelEditor = new PixelEditorControl { Dock = DockStyle.Fill, Margin = new Padding(20) };
            mainLayout.Controls.Add(pixelEditor, 1, 0);
        }

        #region UI 辅助方法
        private GroupBox CreateGroupBox(string t, int h) => new GroupBox { Text = t, Width = 300, Height = h, ForeColor = Color.White, Margin = new Padding(0, 0, 0, 10) };

        private TableLayoutPanel CreateInnerGrid(int rows, int cols)
        {
            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = rows, ColumnCount = cols };
            for (int i = 0; i < cols; i++) tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            for (int i = 0; i < rows; i++) tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 35F));
            return tl;
        }

        private NumericUpDown AddGridNumeric(TableLayoutPanel parent, string lab, int min, int max, int val, int col, int row)
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            p.Controls.Add(new Label { Text = lab, Location = new Point(0, 8), AutoSize = true, ForeColor = Color.Gray });
            var n = new NumericUpDown { Location = new Point(50, 5), Width = 55, Minimum = min, Maximum = max, Value = val, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            p.Controls.Add(n);
            parent.Controls.Add(p, col, row);
            return n;
        }

        private Button CreateStyledButton(string t, Color c, Action action)
        {
            Button b = new Button { Text = t, Width = 300, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Margin = new Padding(0, 5, 0, 0) };
            b.Click += (s, e) => action();
            return b;
        }
        #endregion

        private void BindEvents()
        {
            numAsciiValue.ValueChanged += (s, e) => {
                _currentIdx = (int)numAsciiValue.Value;
                txtChar.Text = ((char)_currentIdx).ToString();
                SyncUI();
            };

            numWidth.ValueChanged += (s, e) => {
                _mgr.AsciiSet[_currentIdx].Width = (int)numWidth.Value;
                pixelEditor.ActiveWidth = (int)numWidth.Value;
                pixelEditor.Invalidate();
            };

            // TTF 参数变动联动
            EventHandler ttfUpdate = (s, e) => {
                if (gbTTF.Enabled && !_mgr.AsciiSet[_currentIdx].IsManual)
                {
                    RenderCurrentChar();
                }
            };
            numFontSize.ValueChanged += ttfUpdate;
            numBaseline.ValueChanged += ttfUpdate;
            numOffsetX.ValueChanged += ttfUpdate;
            numOffsetY.ValueChanged += ttfUpdate;
        }

        private void RenderCurrentChar()
        {
            _fontRender.CanvasWidth = (int)numCanvasW.Value;
            _fontRender.CanvasHeight = (int)numCanvasH.Value;
            _fontRender.OffsetY = (int)numBaseline.Value;
            _fontRender.OffsetX = (int)numOffsetX.Value;
            _fontRender.ScaleX = (int)numScaleX.Value;
            _fontRender.ScaleY = (int)numScaleY.Value;

            _mgr.AsciiSet[_currentIdx].Data = _fontRender.RenderChar(((char)_currentIdx).ToString());
            _mgr.AsciiSet[_currentIdx].Width = _mgr.CalculateWidth(_mgr.AsciiSet[_currentIdx].Data, (int)numCanvasW.Value, (int)numCanvasH.Value);
            SyncUI();
        }

        private void SyncUI()
        {
            var entry = _mgr.AsciiSet[_currentIdx];
            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;
            pixelEditor.ActiveWidth = entry.Width;
            pixelEditor.CurrentData = entry.Data;
            numWidth.Value = entry.Width;
            pixelEditor.Invalidate();
        }

        private void OnImportClick()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "支持格式|*.ttf;*.otf;*.bmp;*.font" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string ext = Path.GetExtension(ofd.FileName).ToLower();
                    if (ext == ".bmp") _mgr.ImportFromBmp(ofd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, _fontRender);
                    else if (ext == ".font") _mgr.ImportFromFontText(ofd.FileName);
                    else _fontRender.LoadFontFile(ofd.FileName, (float)numFontSize.Value);
                    SyncUI();
                }
            }
        }

        private void OnBatchRender()
        {
            for (int i = 0; i < 256; i++)
            {
                if (!_mgr.AsciiSet[i].IsManual)
                {
                    _mgr.AsciiSet[i].Data = _fontRender.RenderChar(((char)i).ToString());
                    _mgr.AsciiSet[i].Width = _mgr.CalculateWidth(_mgr.AsciiSet[i].Data, (int)numCanvasW.Value, (int)numCanvasH.Value);
                }
            }
            SyncUI();
        }

        private void OnExportClick()
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "BIN文件|*.bin" };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                _mgr.SaveToBin(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value);
                MessageBox.Show("导出成功！");
            }
        }
    }

    // --- 补全 PixelEditorControl 控件 ---
    public class PixelEditorControl : UserControl
    {
        public int CanvasW { get; set; } = 16;
        public int CanvasH { get; set; } = 16;
        public int ActiveWidth { get; set; } = 8;
        public byte[] CurrentData { get; set; }

        public PixelEditorControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (CurrentData == null) return;
            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);

            // 绘制有效宽度背景
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 0, 120, 215)), 0, 0, ActiveWidth * cellSize, CanvasH * cellSize);

            using (Pen p = new Pen(Color.FromArgb(50, 50, 50)))
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
            HandleMouse(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.None) HandleMouse(e);
        }

        private void HandleMouse(MouseEventArgs e)
        {
            int cellSize = Math.Min(Width / CanvasW, Height / CanvasH);
            int x = e.X / cellSize, y = e.Y / cellSize;
            if (x >= 0 && x < CanvasW && y >= 0 && y < CanvasH)
            {
                int bytesPerRow = (CanvasW + 7) / 8;
                int byteIdx = y * bytesPerRow + (x / 8);
                if (e.Button == MouseButtons.Left) CurrentData[byteIdx] |= (byte)(0x80 >> (x % 8));
                else if (e.Button == MouseButtons.Right) CurrentData[byteIdx] &= (byte)~(0x80 >> (x % 8));
                this.Invalidate();
            }
        }
    }
}