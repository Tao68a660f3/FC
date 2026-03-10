using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using FC.core;

namespace FC
{
    public partial class Form1 : Form
    {
        // 逻辑组件
        private FontRender _renderer = new FontRender();
        private GeneratorEngine _engine;

        // UI 控件定义
        private TextBox txtFontPath, txtPreviewInput;
        private NumericUpDown numFontSize, numCanvasW, numCanvasH, numOffsetX, numOffsetY;
        private ComboBox cmbScanMode, cmbBitOrder, cmbEncoding;
        private PictureBox picPreview;
        private ProgressBar prgBus;
        private Label lblStatus;

        public Form1()
        {
            this.Text = "FontFactory - 高级字库生成器";
            this.Size = new Size(850, 550);
            this.StartPosition = FormStartPosition.CenterScreen;

            _engine = new GeneratorEngine(_renderer);
            InitLayout();
            BindEvents();
        }

        private void InitLayout()
        {
            // 主布局：左侧参数(350px)，右侧预览(剩余)
            TableLayoutPanel mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // --- 左侧参数面板 ---
            FlowLayoutPanel panelLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10), AutoScroll = true };

            // 字体选择
            panelLeft.Controls.Add(new Label { Text = "1. 字体源 (External TTF)", AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) });
            txtFontPath = new TextBox { Width = 220 };
            Button btnBrowse = new Button { Text = "浏览...", Width = 70 };
            btnBrowse.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体文件|*.ttf;*.otf;*.ttc" })
                    if (ofd.ShowDialog() == DialogResult.OK) txtFontPath.Text = ofd.FileName;
            };
            FlowLayoutPanel rowFont = new FlowLayoutPanel { Width = 320, Height = 30 };
            rowFont.Controls.AddRange(new Control[] { txtFontPath, btnBrowse });
            panelLeft.Controls.Add(rowFont);

            // 渲染尺寸控制
            panelLeft.Controls.Add(new Label { Text = "2. 渲染参数 (Pixel Size)", AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), Margin = new Padding(0, 10, 0, 0) });
            numFontSize = CreateNumPair(panelLeft, "字号 (Size):", 16);
            numCanvasW = CreateNumPair(panelLeft, "画布宽 (W):", 16);
            numCanvasH = CreateNumPair(panelLeft, "画布高 (H):", 16);
            numOffsetX = CreateNumPair(panelLeft, "偏移 X:", 0);
            numOffsetY = CreateNumPair(panelLeft, "偏移 Y:", 0);

            // 导出配置
            panelLeft.Controls.Add(new Label { Text = "3. 导出设置 (Output Config)", AutoSize = true, Font = new Font(this.Font, FontStyle.Bold), Margin = new Padding(0, 10, 0, 0) });
            cmbScanMode = CreateCombo(panelLeft, "扫描模式:", typeof(ScanMode));
            cmbBitOrder = CreateCombo(panelLeft, "位序(Bit):", typeof(BitOrder));
            cmbEncoding = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEncoding.Items.AddRange(new string[] { "GBK_Custom_22084", "GB2312_Standard" });
            cmbEncoding.SelectedIndex = 0;
            panelLeft.Controls.Add(new Label { Text = "编码模式:" });
            panelLeft.Controls.Add(cmbEncoding);

            // 生成按钮
            Button btnGo = new Button { Text = "开始生成全字库 (.bin)", Width = 300, Height = 40, Margin = new Padding(0, 20, 0, 0), BackColor = Color.LightSteelBlue };
            btnGo.Click += BtnGo_Click;
            panelLeft.Controls.Add(btnGo);

            prgBus = new ProgressBar { Width = 300, Height = 15, Margin = new Padding(0, 10, 0, 0) };
            panelLeft.Controls.Add(prgBus);
            lblStatus = new Label { Text = "就绪", Width = 300 };
            panelLeft.Controls.Add(lblStatus);

            // --- 右侧预览面板 ---
            FlowLayoutPanel panelRight = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(20), BackColor = Color.FromArgb(45, 45, 48) };
            panelRight.Controls.Add(new Label { Text = "实时点阵预览 (Real-time Preview)", ForeColor = Color.White, AutoSize = true });
            txtPreviewInput = new TextBox { Text = "我", Font = new Font("微软雅黑", 12), Width = 100 };
            panelRight.Controls.Add(txtPreviewInput);

            picPreview = new PictureBox { Width = 400, Height = 400, Margin = new Padding(0, 20, 0, 0), BorderStyle = BorderStyle.FixedSingle };
            panelRight.Controls.Add(picPreview);

            mainLayout.Controls.Add(panelLeft, 0, 0);
            mainLayout.Controls.Add(panelRight, 1, 0);
            this.Controls.Add(mainLayout);
        }

        private NumericUpDown CreateNumPair(Control parent, string text, int def)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true });
            var n = new NumericUpDown { Value = def, Minimum = -128, Maximum = 128, Width = 80 };
            parent.Controls.Add(n);
            return n;
        }

        private ComboBox CreateCombo(Control parent, string text, Type enumType)
        {
            parent.Controls.Add(new Label { Text = text, AutoSize = true });
            var c = new ComboBox { DataSource = Enum.GetValues(enumType), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            parent.Controls.Add(c);
            return c;
        }

        private void BindEvents()
        {
            // 只要参数变了，立刻更新预览
            EventHandler update = (s, e) => UpdatePreview();
            numFontSize.ValueChanged += update;
            numCanvasW.ValueChanged += update;
            numCanvasH.ValueChanged += update;
            numOffsetX.ValueChanged += update;
            numOffsetY.ValueChanged += update;
            txtPreviewInput.TextChanged += update;
            cmbScanMode.SelectedIndexChanged += update;
            cmbBitOrder.SelectedIndexChanged += update;

            // 初始加载
            this.Load += (s, e) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(txtFontPath.Text) || !File.Exists(txtFontPath.Text)) return;
            if (string.IsNullOrEmpty(txtPreviewInput.Text)) return;

            try
            {
                _renderer.LoadFontFile(txtFontPath.Text, (float)numFontSize.Value);
                _renderer.CanvasWidth = (int)numCanvasW.Value;
                _renderer.CanvasHeight = (int)numCanvasH.Value;
                _renderer.OffsetX = (int)numOffsetX.Value;
                _renderer.OffsetY = (int)numOffsetY.Value;
                _renderer.CurrentScanMode = (ScanMode)cmbScanMode.SelectedItem;
                _renderer.CurrentBitOrder = (BitOrder)cmbBitOrder.SelectedItem;

                byte[] data = _renderer.RenderChar(txtPreviewInput.Text.Substring(0, 1));
                DrawPixelGrid(data);
            }
            catch { /* 字体加载中暂忽略错误 */ }
        }

        private void DrawPixelGrid(byte[] data)
        {
            int w = (int)numCanvasW.Value;
            int h = (int)numCanvasH.Value;
            Bitmap bmp = new Bitmap(picPreview.Width, picPreview.Height);
            int blockSize = Math.Min(picPreview.Width / w, picPreview.Height / h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                int bytesPerRow = (w + 7) / 8;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // 这里逻辑要和渲染器 ConvertTo1Bpp 一致
                        bool isSet = false;
                        if (_renderer.CurrentScanMode == ScanMode.Horizontal)
                        {
                            int byteIdx = y * bytesPerRow + (x / 8);
                            int bitOffset = x % 8;
                            isSet = _renderer.CurrentBitOrder == BitOrder.MSBFirst ?
                                (data[byteIdx] & (0x80 >> bitOffset)) != 0 : (data[byteIdx] & (0x01 << bitOffset)) != 0;
                        }
                        else
                        {
                            int bytesPerCol = (h + 7) / 8;
                            int byteIdx = x * bytesPerCol + (y / 8);
                            int bitOffset = y % 8;
                            isSet = _renderer.CurrentBitOrder == BitOrder.MSBFirst ?
                                (data[byteIdx] & (0x80 >> bitOffset)) != 0 : (data[byteIdx] & (0x01 << bitOffset)) != 0;
                        }

                        Brush b = isSet ? Brushes.Lime : new SolidBrush(Color.FromArgb(40, 40, 40));
                        g.FillRectangle(b, x * blockSize, y * blockSize, blockSize - 1, blockSize - 1);
                    }
                }
            }
            picPreview.Image = bmp;
        }

        private async void BtnGo_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFontPath.Text)) return;

            IEncodingProvider provider = cmbEncoding.SelectedIndex == 0 ?
                (IEncodingProvider)new GbkCustomProvider() : new Gb2312Provider();

            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "font_out.bin");

            await _engine.GenerateAsync(provider, savePath, (cur, total) => {
                this.Invoke(new Action(() => {
                    prgBus.Maximum = total;
                    prgBus.Value = cur;
                    lblStatus.Text = $"生成中: {cur}/{total}";
                }));
            });

            MessageBox.Show($"字库已生成!\n路径: {savePath}\n大小: {new FileInfo(savePath).Length / 1024} KB");
        }
    }
}