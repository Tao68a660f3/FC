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
            // 主容器：左右分栏
            SplitContainer splitMain = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 380, IsSplitterFixed = false };
            this.Controls.Add(splitMain);

            // --- 左侧：配置面板 ---
            FlowLayoutPanel leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true,
                Padding = new Padding(15),
                WrapContents = false
            };
            splitMain.Panel1.Controls.Add(leftFlow);

            // 1. 字体源
            GroupBox gb1 = CreateModernGroupBox("字体资源", 80);
            txtFontPath = new TextBox { Width = 240, Location = new Point(15, 35), BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
            Button btnBrowse = new Button { Text = "浏览", Location = new Point(265, 33), Width = 60, BackColor = Color.FromArgb(70, 70, 70) };
            btnBrowse.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体|*.ttf;*.otf;*.ttc" })
                    if (ofd.ShowDialog() == DialogResult.OK) txtFontPath.Text = ofd.FileName;
            };
            gb1.Controls.Add(txtFontPath); gb1.Controls.Add(btnBrowse);
            leftFlow.Controls.Add(gb1);

            // 2. 渲染核心 (网格布局)
            GroupBox gb2 = CreateModernGroupBox("尺寸与偏移", 160);
            TableLayoutPanel gridRender = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, Padding = new Padding(5, 20, 5, 5) };
            gb2.Controls.Add(gridRender);

            numFontSize = AddGridNum(gridRender, "字号:", 16, 0, 0);
            numCanvasW = AddGridNum(gridRender, "画布宽:", 16, 1, 0);
            numCanvasH = AddGridNum(gridRender, "画布高:", 16, 1, 1);
            numOffsetX = AddGridNum(gridRender, "偏移 X:", 0, 2, 0);
            numOffsetY = AddGridNum(gridRender, "偏移 Y:", 0, 2, 1); // 我们在逻辑里翻转它

            lblFileSizeMsg = new Label { Text = "预计: 0 KB", ForeColor = Color.Orange, AutoSize = true, Dock = DockStyle.Bottom };
            gb2.Controls.Add(lblFileSizeMsg);
            leftFlow.Controls.Add(gb2);

            // 3. 扫描参数
            GroupBox gb3 = CreateModernGroupBox("输出格式", 150);
            cmbScanMode = AddFlowCombo(gb3, "扫描模式:", typeof(ScanMode), 30);
            cmbBitOrder = AddFlowCombo(gb3, "位序控制:", typeof(BitOrder), 70);
            cmbEncoding = AddFlowCombo(gb3, "编码选择:", null, 110);
            cmbEncoding.Items.AddRange(new string[] { "GBK_Custom_22062", "GB2312_Standard" });
            cmbEncoding.SelectedIndex = 0;
            leftFlow.Controls.Add(gb3);

            // 4. 执行区
            btnGo = new Button { Text = "开始生成 (.bin)", Width = 340, Height = 50, BackColor = Color.FromArgb(0, 122, 204), FlatStyle = FlatStyle.Flat, Font = new Font(this.Font, FontStyle.Bold) };
            btnGo.Click += BtnGo_Click;
            leftFlow.Controls.Add(btnGo);

            prgBus = new ProgressBar { Width = 340, Height = 10, Margin = new Padding(0, 10, 0, 0) };
            leftFlow.Controls.Add(prgBus);
            lblStatus = new Label { Text = "准备就绪", AutoSize = true, ForeColor = Color.Gray };
            leftFlow.Controls.Add(lblStatus);

            // --- 右侧：预览面板 (响应式 PictureBox) ---
            Panel rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            splitMain.Panel2.Controls.Add(rightPanel);

            txtPreviewInput = new TextBox { Text = "汉", Font = new Font("微软雅黑", 14), Width = 100, Dock = DockStyle.Top, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, TextAlign = HorizontalAlignment.Center };
            rightPanel.Controls.Add(txtPreviewInput);

            picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 20, 0, 0),
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom // 自动缩放
            };
            rightPanel.Controls.Add(picPreview);
            // 解决 Dock 后顺序问题
            picPreview.BringToFront();
        }

        private GroupBox CreateModernGroupBox(string title, int height)
        {
            return new GroupBox { Text = title, Width = 345, Height = height, ForeColor = Color.LightSkyBlue, Margin = new Padding(0, 0, 0, 15) };
        }

        private NumericUpDown AddGridNum(TableLayoutPanel grid, string label, int def, int row, int col)
        {
            grid.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, col * 2, row);
            var n = new NumericUpDown { Value = def, Minimum = -128, Maximum = 128, Dock = DockStyle.Fill, BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            grid.Controls.Add(n, col * 2 + 1, row);
            return n;
        }

        private ComboBox AddFlowCombo(GroupBox gb, string text, Type enumType, int y)
        {
            gb.Controls.Add(new Label { Text = text, Location = new Point(15, y + 3), AutoSize = true });
            var c = new ComboBox { Location = new Point(100, y), Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White };
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