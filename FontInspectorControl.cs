#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;
using FC.core;

namespace FC
{
    public partial class FontInspectorControl : UserControl
    {
        private byte[] _fontData;
        private PictureBox picInspect;
        private TextBox txtInput, txtCode;
        private Label lblOffsetInfo;
        private NumericUpDown numW, numH;
        private ComboBox cmbScan, cmbBit, cmbEncoding;

        public FontInspectorControl()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(30, 30, 30);
            SetupCustomControls();
        }

        private void SetupCustomControls()
        {
            TableLayoutPanel main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // --- 左侧控制面板 ---
            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(15),
                AutoScroll = true
            };

            // 1. 文件加载
            Button btnLoad = new Button { Text = "📁 打开字库 (.bin)", Width = 280, Height = 45, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(60, 60, 60) };
            btnLoad.Click += (s, e) => LoadBin();
            panel.Controls.Add(btnLoad);

            // 2. 字库类型选择 (关键！)
            panel.Controls.Add(CreateLabel("字库编码类型:"));
            cmbEncoding = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEncoding.Items.AddRange(new string[] { "Custom GBK", "Standard GB2312" });
            cmbEncoding.SelectedIndex = 0;
            panel.Controls.Add(cmbEncoding);

            // 3. 规格参数
            panel.Controls.Add(CreateLabel("点阵规格 (宽 x 高):"));
            FlowLayoutPanel sizePanel = new FlowLayoutPanel { Width = 280, Height = 35 };
            numW = new NumericUpDown { Value = 16, Width = 80, Minimum = 8, Maximum = 128 };
            numH = new NumericUpDown { Value = 16, Width = 80, Minimum = 8, Maximum = 128 };
            sizePanel.Controls.Add(numW);
            sizePanel.Controls.Add(new Label { Text = "x", ForeColor = Color.White, AutoSize = true });
            sizePanel.Controls.Add(numH);
            panel.Controls.Add(sizePanel);

            // 4. 模式匹配 (必须与生成时一致)
            panel.Controls.Add(CreateLabel("取模设置:"));
            cmbScan = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbScan.Items.AddRange(new object[] { ScanMode.Horizontal, ScanMode.Vertical });
            cmbScan.SelectedIndex = 0;
            panel.Controls.Add(cmbScan);

            cmbBit = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBit.Items.AddRange(new object[] { BitOrder.MSBFirst, BitOrder.LSBFirst });
            cmbBit.SelectedIndex = 0;
            panel.Controls.Add(cmbBit);

            // 5. 查询输入
            panel.Controls.Add(new Label { Text = "────────────────", ForeColor = Color.DimGray, Width = 280 });
            panel.Controls.Add(CreateLabel("输入字符校验:", Color.Yellow));
            txtInput = new TextBox { Width = 280, Font = new Font("微软雅黑", 14), BackColor = Color.Black, ForeColor = Color.Lime };
            txtInput.TextChanged += (s, e) => RunInspect();
            panel.Controls.Add(txtInput);

            lblOffsetInfo = new Label { Text = "等待加载字库...", AutoSize = true, ForeColor = Color.Cyan, Font = new Font("Consolas", 9) };
            panel.Controls.Add(lblOffsetInfo);

            main.Controls.Add(panel, 0, 0);

            // --- 右侧容器 (改为上下布局) ---
            TableLayoutPanel rightContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2
            };
            // 上方 75% 给点阵预览，下方 25% 给代码
            rightContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 75f));
            rightContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            // 1. 点阵预览
            picInspect = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.CenterImage
            };
            rightContainer.Controls.Add(picInspect, 0, 0);

            // 2. C 语言代码预览 (移到这里)
            txtCode = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 20),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical
            };

            // 给代码框加个简单的边距感
            Panel codeWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 10) };
            codeWrapper.Controls.Add(txtCode);
            rightContainer.Controls.Add(codeWrapper, 0, 1);

            main.Controls.Add(rightContainer, 1, 0);
            this.Controls.Add(main);
        }

        private Label CreateLabel(string text, Color? color = null) => new Label { Text = text, ForeColor = color ?? Color.Gray, AutoSize = true, Margin = new Padding(0, 10, 0, 3) };

        private void LoadBin()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字库文件|*.bin" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _fontData = File.ReadAllBytes(ofd.FileName);
                    lblOffsetInfo.Text = $"已加载: {Path.GetFileName(ofd.FileName)}\n大小: {_fontData.Length} 字节";
                    RunInspect();
                }
            }
        }

        private void RunInspect()
        {
            if (_fontData == null || string.IsNullOrEmpty(txtInput.Text)) return;

            // 1. 获取 GBK 编码
            byte[] bytes = Encoding.GetEncoding("GBK").GetBytes(txtInput.Text.Substring(0, 1));
            if (bytes.Length < 2) return;
            ushort code = (ushort)((bytes[0] << 8) | bytes[1]);

            // 2. 根据选择的 Provider 计算 Index
            IEncodingProvider provider = (cmbEncoding.SelectedIndex == 0)
                ? (IEncodingProvider)new GbkCustomProvider()
                : (IEncodingProvider)new Gb2312Provider();

            int index = provider.GetIndexByCode(code);
            int bytesPerChar = CalculateBytesSize();
            long offset = (long)index * bytesPerChar;

            if (index == -1 || offset + bytesPerChar > _fontData.Length)
            {
                lblOffsetInfo.Text = "错误: 字符不在字库范围内";
                picInspect.Image = null;
                return;
            }

            lblOffsetInfo.Text = $"编码: 0x{code:X4}\nIndex: {index}\nOffset: 0x{offset:X}";

            // 3. 提取数据并显示
            byte[] glyph = new byte[bytesPerChar];
            Array.Copy(_fontData, offset, glyph, 0, bytesPerChar);
            RenderToCanvas(glyph);

            // 4. 生成代码占位
            UpdateCodeSnippet(index, bytesPerChar, txtInput.Text.Substring(0, 1));
        }

        private int CalculateBytesSize()
        {
            int w = (int)numW.Value;
            int h = (int)numH.Value;
            if ((ScanMode)cmbScan.SelectedItem == ScanMode.Horizontal)
                return ((w + 7) / 8) * h;
            else
                return ((h + 7) / 8) * w;
        }

        private void RenderToCanvas(byte[] data)
        {
            int w = (int)numW.Value;
            int h = (int)numH.Value;
            // 为了看得清楚，我们创建一个放大的 Bitmap (每个点阵像素转为 20x20 的方块)
            int scale = 20;
            Bitmap bmp = new Bitmap(w * scale, h * scale);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(20, 20, 20));
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (GetBitFromData(data, x, y, w, h))
                        {
                            g.FillRectangle(Brushes.Lime, x * scale, y * scale, scale - 1, scale - 1);
                        }
                    }
                }
            }
            picInspect.Image = bmp;
        }

        // 复用你 Form1 里的 GetBitFromData 逻辑
        private bool GetBitFromData(byte[] data, int x, int y, int w, int h)
        {
            ScanMode mode = (ScanMode)cmbScan.SelectedItem;
            BitOrder order = (BitOrder)cmbBit.SelectedItem;

            if (mode == ScanMode.Horizontal)
            {
                int bpr = (w + 7) / 8;
                int byteIdx = y * bpr + (x / 8);
                int bit = x % 8;
                return order == BitOrder.MSBFirst ? (data[byteIdx] & (0x80 >> bit)) != 0 : (data[byteIdx] & (0x01 << bit)) != 0;
            }
            else
            {
                int bpc = (h + 7) / 8;
                int byteIdx = x * bpc + (y / 8);
                int bit = y % 8;
                return order == BitOrder.MSBFirst ? (data[byteIdx] & (0x80 >> bit)) != 0 : (data[byteIdx] & (0x01 << bit)) != 0;
            }
        }

        private void UpdateCodeSnippet(int index, int size, string c)
        {
            txtCode.Text = $"// 字符: {c}\r\n" +
                           $"// 地址 = 基地址 + (Index * 每个字符字节数)\r\n" +
                           $"uint32_t addr = FONT_BASE + ({index} * {size});\r\n" +
                           $"// 接下来调用 SPI_Read(addr, buffer, {size});";
        }
    }
}