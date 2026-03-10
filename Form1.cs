using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using FC.core;

namespace FC
{
    public partial class Form1 : Form
    {
        private FontRender _renderer = new FontRender();
        private GeneratorEngine _engine;

        // 成员变量声明 (修复之前的漏定义)
        private TextBox txtFontPath, txtPreviewInput;
        private NumericUpDown numFontSize, numCanvasW, numCanvasH, numOffsetX, numOffsetY;
        private ComboBox cmbScanMode, cmbBitOrder, cmbEncoding;
        private PictureBox picPreview;
        private ProgressBar prgBus;
        private Label lblStatus, lblFileSizeMsg;
        private Button btnGo; // 定义在这里

        public Form1()
        {
            this.Text = "FontFactory Pro - 响应式字库工作站";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            _engine = new GeneratorEngine(_renderer);
            InitResponsiveLayout();

            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string defaultFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simsun.ttc");
            if (File.Exists(defaultFont)) txtFontPath.Text = defaultFont;

            BindEvents();
            UpdatePreview();
        }

        private void InitResponsiveLayout()
        {
            // 1. 全局大网格 (1行2列)
            TableLayoutPanel mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F)); // 左侧控制区 38%
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F)); // 右侧预览区 62%
            this.Controls.Add(mainTable);

            // --- 左侧：控制容器 ---
            FlowLayoutPanel leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(15),
                AutoScroll = true,
                WrapContents = false
            };
            mainTable.Controls.Add(leftFlow, 0, 0);

            // --- 组 1: 字体资源 (比例对齐) ---
            GroupBox gb1 = CreateModernGroupBox("字体资源", 100);
            TableLayoutPanel fontGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            fontGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F)); // 文本框占 80%
            fontGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F)); // 按钮占 20%

            txtFontPath = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(5, 15, 5, 5), BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            Button btnBrowse = new Button { Text = "...", Dock = DockStyle.Fill, Margin = new Padding(5, 12, 5, 5), BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat };
            btnBrowse.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体|*.ttf;*.otf;*.ttc" })
                    if (ofd.ShowDialog() == DialogResult.OK) txtFontPath.Text = ofd.FileName;
            };
            fontGrid.Controls.Add(txtFontPath, 0, 0);
            fontGrid.Controls.Add(btnBrowse, 1, 0);
            gb1.Controls.Add(fontGrid);
            leftFlow.Controls.Add(gb1);

            // --- 组 2: 尺寸与偏移 (均匀网格) ---
            GroupBox gb2 = CreateModernGroupBox("尺寸与偏移", 180);
            TableLayoutPanel renderGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(5, 15, 5, 5) };
            // 四列平分：25% 25% 25% 25%
            for (int i = 0; i < 4; i++) renderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            numFontSize = AddGridControl(renderGrid, "字号", 16, 0, 0);
            numCanvasW = AddGridControl(renderGrid, "宽", 16, 1, 0);
            numCanvasH = AddGridControl(renderGrid, "高", 16, 1, 1);
            numOffsetX = AddGridControl(renderGrid, "移X", 0, 2, 0);
            numOffsetY = AddGridControl(renderGrid, "移Y", 0, 2, 1);
            gb2.Controls.Add(renderGrid);

            lblFileSizeMsg = new Label { Text = "预计: 0 KB", Dock = DockStyle.Bottom, ForeColor = Color.Orange, TextAlign = ContentAlignment.MiddleRight };
            gb2.Controls.Add(lblFileSizeMsg);
            leftFlow.Controls.Add(gb2);

            // --- 组 3: 输出格式 ---
            GroupBox gb3 = CreateModernGroupBox("输出格式", 150);
            TableLayoutPanel exportGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(5, 15, 5, 5) };
            exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));

            cmbScanMode = AddGridCombo(exportGrid, "扫描", typeof(ScanMode), 0);
            cmbBitOrder = AddGridCombo(exportGrid, "位序", typeof(BitOrder), 1);
            cmbEncoding = AddGridCombo(exportGrid, "编码", null, 2);
            cmbEncoding.Items.AddRange(new string[] { "GBK_Custom_22062", "GB2312_Standard" });
            cmbEncoding.SelectedIndex = 0;
            gb3.Controls.Add(exportGrid);
            leftFlow.Controls.Add(gb3);

            // --- 执行区 ---
            btnGo = new Button { Text = "🚀 开始生成 (.bin)", Width = 340, Height = 60, BackColor = Color.FromArgb(0, 122, 204), FlatStyle = FlatStyle.Flat, Font = new Font(this.Font, FontStyle.Bold), Margin = new Padding(0, 20, 0, 0) };
            btnGo.Click += BtnGo_Click;
            leftFlow.Controls.Add(btnGo);

            prgBus = new ProgressBar { Width = 340, Height = 10, Margin = new Padding(0, 10, 0, 0) };
            leftFlow.Controls.Add(prgBus);
            lblStatus = new Label { Text = "准备就绪", AutoSize = true, ForeColor = Color.Gray };
            leftFlow.Controls.Add(lblStatus);

            // --- 右侧：预览区 (比例适配) ---
            TableLayoutPanel rightTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(20) };
            rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // 顶部输入框固定高度
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 预览图占满剩余
            mainTable.Controls.Add(rightTable, 1, 0);

            txtPreviewInput = new TextBox { Dock = DockStyle.Fill, Text = "汉", Font = new Font("微软雅黑", 16), TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.Lime };
            rightTable.Controls.Add(txtPreviewInput, 0, 0);

            picPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };
            rightTable.Controls.Add(picPreview, 0, 1);
        }

        // 辅助：支持 Grid 的下拉框添加
        private ComboBox AddGridCombo(TableLayoutPanel grid, string text, Type enumType, int row)
        {
            grid.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var c = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            if (enumType != null) c.DataSource = Enum.GetValues(enumType);
            grid.Controls.Add(c, 1, row);
            return c;
        }

        private GroupBox CreateModernGroupBox(string title, int height)
        {
            return new GroupBox
            {
                Text = title,
                Width = 350, // 这里的宽度在 FlowLayoutPanel 中起作用，但在 TableLayoutPanel 中会被 Dock 覆盖
                Height = height,
                ForeColor = Color.LightSkyBlue,
                Margin = new Padding(0, 0, 0, 15),
                FlatStyle = FlatStyle.Flat // 让边框看起来更简洁
            };
        }

        // 辅助方法：精准网格添加
        private NumericUpDown AddGridControl(TableLayoutPanel grid, string label, int def, int row, int colGroup)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, Margin = new Padding(0, 0, 0, 5) }, colGroup * 2, row);
            var n = new NumericUpDown { Value = def, Minimum = -128, Maximum = 128, Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            grid.Controls.Add(n, colGroup * 2 + 1, row);
            return n;
        }

        // 辅助方法：下拉框
        private ComboBox AddFlowCombo(GroupBox gb, string text, Type enumType, int y)
        {
            gb.Controls.Add(new Label { Text = text, Location = new Point(15, y + 3), AutoSize = true });
            var c = new ComboBox { Location = new Point(100, y), Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            if (enumType != null) c.DataSource = Enum.GetValues(enumType);
            gb.Controls.Add(c);
            return c;
        }

        private void BindEvents()
        {
            Action update = () => { CalculateInfo(); UpdatePreview(); };
            numFontSize.ValueChanged += (s, e) => update();
            numCanvasW.ValueChanged += (s, e) => update();
            numCanvasH.ValueChanged += (s, e) => update();
            numOffsetX.ValueChanged += (s, e) => update();
            numOffsetY.ValueChanged += (s, e) => update(); // 此时 Y 变化也会触发预览
            txtFontPath.TextChanged += (s, e) => update();
            txtPreviewInput.TextChanged += (s, e) => update();
            cmbScanMode.SelectedIndexChanged += (s, e) => update();
            cmbBitOrder.SelectedIndexChanged += (s, e) => update();
            this.Resize += (s, e) => UpdatePreview(); // 窗口缩放时重绘预览图
        }

        private void UpdatePreview()
        {
            if (!File.Exists(txtFontPath.Text) || string.IsNullOrEmpty(txtPreviewInput.Text)) return;

            try
            {
                _renderer.LoadFontFile(txtFontPath.Text, (float)numFontSize.Value);
                _renderer.CanvasWidth = (int)numCanvasW.Value;
                _renderer.CanvasHeight = (int)numCanvasH.Value;

                // 修正偏移方向：让用户觉得 OffsetY 变大是“向上移”
                _renderer.OffsetX = (int)numOffsetX.Value;
                _renderer.OffsetY = -(int)numOffsetY.Value;

                _renderer.CurrentScanMode = (ScanMode)cmbScanMode.SelectedItem;
                _renderer.CurrentBitOrder = (BitOrder)cmbBitOrder.SelectedItem;

                byte[] data = _renderer.RenderChar(txtPreviewInput.Text.Substring(0, 1));

                // 绘制预览图
                int w = _renderer.CanvasWidth;
                int h = _renderer.CanvasHeight;
                Bitmap bmp = new Bitmap(512, 512); // 使用固定大尺寸画布，靠 SizeMode.Zoom 适配
                int blockSize = 512 / Math.Max(w, h);

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            bool isSet = GetBitFromData(data, x, y, w, h);
                            if (isSet) g.FillRectangle(Brushes.Lime, x * blockSize, y * blockSize, blockSize - 1, blockSize - 1);
                            else g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 30)), x * blockSize, y * blockSize, blockSize - 1, blockSize - 1);
                        }
                    }
                }
                picPreview.Image = bmp;
            }
            catch { }
        }

        // 这里的逻辑复用你之前的 Bit 判断
        private bool GetBitFromData(byte[] data, int x, int y, int w, int h)
        {
            if (cmbScanMode.SelectedIndex == 0)
            { // Horizontal
                int bpr = (w + 7) / 8;
                int byteIdx = y * bpr + (x / 8);
                int bit = x % 8;
                return cmbBitOrder.SelectedIndex == 0 ? (data[byteIdx] & (0x80 >> bit)) != 0 : (data[byteIdx] & (0x01 << bit)) != 0;
            }
            else
            { // Vertical
                int bpc = (h + 7) / 8;
                int byteIdx = x * bpc + (y / 8);
                int bit = y % 8;
                return cmbBitOrder.SelectedIndex == 0 ? (data[byteIdx] & (0x80 >> bit)) != 0 : (data[byteIdx] & (0x01 << bit)) != 0;
            }
        }

        private void CalculateInfo()
        {
            int charCount = (cmbEncoding.SelectedIndex == 0) ? 22062 : 6768;
            int bytesPerChar = ((int)numCanvasW.Value + 7) / 8 * (int)numCanvasH.Value;
            lblFileSizeMsg.Text = $"预计: {charCount * bytesPerChar / 1024} KB ({charCount} 字)";
        }

        private async void BtnGo_Click(object sender, EventArgs e)
        {
            if (!File.Exists(txtFontPath.Text)) return;
            btnGo.Enabled = false;
            btnGo.Text = "正在输出...";

            await _engine.GenerateAsync(new GbkCustomProvider(), "output.bin", (cur, total) => {
                this.Invoke(new Action(() => {
                    prgBus.Maximum = total;
                    prgBus.Value = cur;
                    lblStatus.Text = $"处理中: {cur}/{total}";
                }));
            });

            btnGo.Enabled = true;
            btnGo.Text = "开始生成 (.bin)";
            MessageBox.Show("字库生成成功！");
        }
    }
}