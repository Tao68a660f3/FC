#nullable disable

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using FC.core;
using FC.ui;

namespace FC
{
    public partial class GbkGeneratorControl : UserControl
    {
        private FontRender _renderer = new FontRender();
        private GeneratorEngine _engine;

        // 成员变量声明 (修复之前的漏定义)
        private TextBox txtFontPath, txtPreviewInput;
        private NumericUpDown numFontSize, numCanvasW, numCanvasH, numOffsetX, numOffsetY, numScaleX, numScaleY;
        private ComboBox cmbScanMode, cmbBitOrder, cmbEncoding;
        private PictureBox picPreview;
        private Label lblStatus, lblFileSizeMsg;
        private Button btnGo; // 定义在这里

        public GbkGeneratorControl()
        {
            this.Size = new Size(1000, 700);

            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            _engine = new GeneratorEngine(_renderer);
            InitResponsiveLayout();

            this.Load += GbkGeneratorControl_Load;
        }

        private void GbkGeneratorControl_Load(object sender, EventArgs e)
        {
            string defaultFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "simsun.ttc");
            if (File.Exists(defaultFont)) txtFontPath.Text = defaultFont;

            BindEvents();
            UpdatePreview();
        }

        private void InitResponsiveLayout()
        {
            // --- 全局大网格 (1行2列) ---
            TableLayoutPanel mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // 左侧 35%
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); // 右侧 65%
            this.Controls.Add(mainTable);

            // --- 左侧：响应式容器 (按行比例分配) ---
            TableLayoutPanel leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(15)
            };
            // 分配行高比例
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 18F)); // 字体源
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 35F)); // 尺寸偏移
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25F)); // 输出格式
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 22F)); // 执行区
            mainTable.Controls.Add(leftGrid, 0, 0);

            // --- 1. 字体资源 (Dock.Fill) ---
            GroupBox gb1 = UiFactory.CreateModernGroupBox("字体资源", 0);
            gb1.Dock = DockStyle.Fill;
            TableLayoutPanel fontGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            fontGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
            fontGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            txtFontPath = new TextBox { Dock = DockStyle.Top, Margin = new Padding(5, 20, 5, 0), BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White };
            Button btnBrowse = new Button { Text = "浏览", Dock = DockStyle.Top, Height = 30, Margin = new Padding(5, 18, 5, 0), BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat };
            btnBrowse.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体|*.ttf;*.otf;*.ttc" })
                    if (ofd.ShowDialog() == DialogResult.OK) txtFontPath.Text = ofd.FileName;
            };
            fontGrid.Controls.Add(txtFontPath, 0, 0);
            fontGrid.Controls.Add(btnBrowse, 1, 0);
            gb1.Controls.Add(fontGrid);
            leftGrid.Controls.Add(gb1, 0, 0);

            // --- 2. 尺寸与偏移 (工业级 4 行布局) ---
            GroupBox gb2 = UiFactory.CreateModernGroupBox("尺寸与偏移", 0);
            gb2.Dock = DockStyle.Fill;

            TableLayoutPanel renderGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 4, // 增加到 4 行
                Padding = new Padding(5, 5, 5, 5)
            };

            renderGrid.ColumnStyles.Clear();
            renderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            renderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            renderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            renderGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            renderGrid.RowStyles.Clear();
            for (int i = 0; i < 4; i++)
            {
                renderGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            }

            // 第 0, 1, 2 行保持原样
            numFontSize = UiFactory.AddGridControl(renderGrid, "字号", 16, 0, 0);
            numCanvasW = UiFactory.AddGridControl(renderGrid, "宽", 16, 1, 0);
            numCanvasH = UiFactory.AddGridControl(renderGrid, "高", 16, 1, 1);
            numOffsetX = UiFactory.AddGridControl(renderGrid, "移X", 0, 2, 0);
            numOffsetY = UiFactory.AddGridControl(renderGrid, "移Y", 0, 2, 1);

            // --- 第 3 行：百分比缩放 ---
            // 默认值 100 代表 100%
            numScaleX = UiFactory.AddGridControl(renderGrid, "比X%", 100, 3, 0);
            numScaleY = UiFactory.AddGridControl(renderGrid, "比Y%", 100, 3, 1);

            // 设置一下范围，防止用户调成 0
            numScaleX.Minimum = 10;
            numScaleX.Maximum = 500;
            numScaleY.Minimum = 10;
            numScaleY.Maximum = 500;

            gb2.Controls.Add(renderGrid);
            lblFileSizeMsg = new Label { Text = "预计: 0 KB", Dock = DockStyle.Bottom, Height = 20, ForeColor = Color.Orange, TextAlign = ContentAlignment.MiddleRight };
            gb2.Controls.Add(lblFileSizeMsg);
            leftGrid.Controls.Add(gb2, 0, 1);

            // --- 3. 输出格式 (Dock.Fill) ---
            GroupBox gb3 = UiFactory.CreateModernGroupBox("输出格式", 0);
            gb3.Dock = DockStyle.Fill;
            TableLayoutPanel exportGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(10)
            };

            // 显式定义行高，让三行平分 GroupBox 的高度
            exportGrid.RowStyles.Clear();
            for (int i = 0; i < 3; i++) exportGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

            exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            exportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));

            cmbScanMode = UiFactory.AddGridCombo(exportGrid, "扫描", typeof(ScanMode), 0);
            cmbBitOrder = UiFactory.AddGridCombo(exportGrid, "位序", typeof(BitOrder), 1);
            cmbEncoding = UiFactory.AddGridCombo(exportGrid, "编码", null, 2);
            cmbEncoding.Items.AddRange(new string[] { "GBK_Custom_22084", "GB2312_Standard" });
            cmbEncoding.SelectedIndex = 0;
            gb3.Controls.Add(exportGrid);
            leftGrid.Controls.Add(gb3, 0, 2);

            // --- 4. 执行区 (下对齐) ---
            Panel runPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            btnGo = new Button { Text = "🚀 开始生成 (.bin)", Dock = DockStyle.Top, Height = 50, BackColor = Color.FromArgb(0, 122, 204), FlatStyle = FlatStyle.Flat, Font = new Font(this.Font, FontStyle.Bold) };
            btnGo.Click += BtnGo_Click;
            lblStatus = new Label { Text = "准备就绪", Dock = DockStyle.Bottom, Height = 25, ForeColor = Color.Gray };
            runPanel.Controls.Add(btnGo);
            runPanel.Controls.Add(lblStatus);
            leftGrid.Controls.Add(runPanel, 0, 3);

            // --- 右侧预览区 (保持比例适配) ---
            TableLayoutPanel rightTable = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.FromArgb(20, 20, 20), Padding = new Padding(20) };
            rightTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            rightTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainTable.Controls.Add(rightTable, 1, 0);

            txtPreviewInput = new TextBox { Dock = DockStyle.Fill, Text = "汉", Font = new Font("微软雅黑", 16), TextAlign = HorizontalAlignment.Center, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.Lime };
            rightTable.Controls.Add(txtPreviewInput, 0, 0);
            picPreview = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.Zoom };
            rightTable.Controls.Add(picPreview, 0, 1);
        }

        private void BindEvents()
        {
            Action update = () => { CalculateInfo(); UpdatePreview(); };
            numFontSize.ValueChanged += (s, e) => update();
            numCanvasW.ValueChanged += (s, e) => update();
            numCanvasH.ValueChanged += (s, e) => update();
            numOffsetX.ValueChanged += (s, e) => update();
            numOffsetY.ValueChanged += (s, e) => update(); // 此时 Y 变化也会触发预览
            numScaleX.ValueChanged += (s, e) => update();
            numScaleY.ValueChanged += (s, e) => update();
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

                _renderer.ScaleX = (int)numScaleX.Value;
                _renderer.ScaleY = (int)numScaleY.Value;

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
            // 动态获取当前编码下的字符总数，不再硬编码
            IEncodingProvider tempProvider = (cmbEncoding.SelectedIndex == 1)
                ? (IEncodingProvider)new Gb2312Provider()
                : (IEncodingProvider)new GbkCustomProvider();

            int charCount = 0;
            foreach (var _ in tempProvider.GetEncodingStream()) charCount++;

            // 计算单个字符占用的字节数
            // 横向扫描：每行字节数 * 行数；纵向扫描：每列字节数 * 列数
            int bytesPerChar;
            if (cmbScanMode.SelectedIndex == 0) // Horizontal
                bytesPerChar = ((int)numCanvasW.Value + 7) / 8 * (int)numCanvasH.Value;
            else // Vertical
                bytesPerChar = ((int)numCanvasH.Value + 7) / 8 * (int)numCanvasW.Value;

            long totalBytes = (long)charCount * bytesPerChar;
            lblFileSizeMsg.Text = $"预计: {totalBytes / 1024.0:F2} KB ({charCount} 字)";
        }

        private void BtnGo_Click(object sender, EventArgs e)
        {
            if (!File.Exists(txtFontPath.Text))
                return;

            // 1. 弹出保存对话框
            string savePath = "";
            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "字库文件|*.bin",
                FileName = (cmbEncoding.SelectedIndex == 0 ? "GBK_Custom" : "GB2312_Std") + ".bin"
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;
                savePath = sfd.FileName;
            }

            // 2. 准备 Provider 和参数
            IEncodingProvider provider = (cmbEncoding.SelectedIndex == 1)
                ? (IEncodingProvider)new Gb2312Provider()
                : (IEncodingProvider)new GbkCustomProvider();

            // 3. 实例化模态进度小窗
            FrmProgress frm = new FrmProgress();

            // 在小窗口显示后立即开始任务
            frm.Shown += async (s, ev) =>
            {
                try
                {
                    // 这里依然可以使用 await，因为模态窗体已经锁死了主界面
                    await _engine.GenerateAsync(provider, savePath, (cur, total) =>
                    {
                        // 通过 Invoke 更新小窗体上的进度
                        frm.Invoke(new Action(() =>
                        {
                            frm.ProgressBar.Maximum = total;
                            frm.ProgressBar.Value = cur;
                            frm.LabelStatus.Text = $"正在处理第 {cur} / {total} 个字符...";
                        }));
                    });

                    frm.Close(); // 任务完成后关闭小窗
                }
                catch (Exception ex)
                {
                    MessageBox.Show("生成失败: " + ex.Message);
                    frm.Close();
                }
            };

            // 4. 以模态方式启动小窗 (这一步会冻结主窗体)
            frm.ShowDialog(this);

            MessageBox.Show("字库生成成功！", "提示");
        }
    }
}