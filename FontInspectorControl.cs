#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Collections.Generic;
using FC.core;

namespace FC
{
    public partial class FontInspectorControl : UserControl
    {
        private byte[] _fontData;
        private PictureBox picInspect;
        private TextBox txtInput, txtCode;
        private Label lblOffsetInfo;
        private NumericUpDown numW, numH, numZoom;
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

            // 2. 规格参数
            panel.Controls.Add(CreateLabel("点阵规格 (宽 x 高):"));
            FlowLayoutPanel sizePanel = new FlowLayoutPanel { Width = 280, Height = 35 };
            numW = new NumericUpDown { Value = 16, Width = 80, Minimum = 1, Maximum = 256 };
            numH = new NumericUpDown { Value = 16, Width = 80, Minimum = 1, Maximum = 256 };
            sizePanel.Controls.Add(numW);
            sizePanel.Controls.Add(new Label { Text = "x", ForeColor = Color.White, AutoSize = true });
            sizePanel.Controls.Add(numH);
            panel.Controls.Add(sizePanel);

            // 3. 显式缩放控制 (用户手动输入 1-30)
            panel.Controls.Add(CreateLabel("显示缩放倍率 (2-30):", Color.Orange));
            numZoom = new NumericUpDown { Value = 12, Minimum = 2, Maximum = 30, Width = 280 };
            panel.Controls.Add(numZoom);

            // 4. 取模设置 (完整保留)
            panel.Controls.Add(CreateLabel("扫描模式:"));
            cmbScan = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbScan.Items.AddRange(new object[] { ScanMode.Horizontal, ScanMode.Vertical });
            cmbScan.SelectedIndex = 0;
            panel.Controls.Add(cmbScan);

            panel.Controls.Add(CreateLabel("位序 (Bit Order):"));
            cmbBit = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBit.Items.AddRange(new object[] { BitOrder.MSBFirst, BitOrder.LSBFirst });
            cmbBit.SelectedIndex = 0;
            panel.Controls.Add(cmbBit);

            panel.Controls.Add(CreateLabel("字库编码类型:"));
            cmbEncoding = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbEncoding.Items.AddRange(new string[] { "Custom GBK", "Standard GB2312" });
            cmbEncoding.SelectedIndex = 0;
            panel.Controls.Add(cmbEncoding);

            // 5. 查询输入 (改为回车或失去焦点刷新)
            panel.Controls.Add(new Label { Text = "────────────────", ForeColor = Color.DimGray, Width = 280 });
            panel.Controls.Add(CreateLabel("输入字符校验 (回车应用):", Color.Yellow));
            txtInput = new TextBox { Width = 280, Font = new Font("微软雅黑", 14), BackColor = Color.Black, ForeColor = Color.Lime };

            // 优化响应：按回车键触发
            txtInput.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RunInspect(); } };
            // 优化响应：失去焦点触发
            txtInput.Leave += (s, e) => RunInspect();
            panel.Controls.Add(txtInput);

            lblOffsetInfo = new Label { Text = "就绪", AutoSize = true, ForeColor = Color.Cyan, Font = new Font("Consolas", 9) };
            panel.Controls.Add(lblOffsetInfo);

            main.Controls.Add(panel, 0, 0);

            // --- 右侧显示区 (增加多字符横向滚动支持) ---
            TableLayoutPanel rightContainer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            rightContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 75f));
            rightContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            Panel picWrapper = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Black };
            picInspect = new PictureBox { Location = new Point(0, 0), SizeMode = PictureBoxSizeMode.AutoSize };
            picWrapper.Controls.Add(picInspect);
            rightContainer.Controls.Add(picWrapper, 0, 0);

            txtCode = new TextBox { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.LightGray, Font = new Font("Consolas", 10), BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical };
            rightContainer.Controls.Add(txtCode, 0, 1);

            main.Controls.Add(rightContainer, 1, 0);
            this.Controls.Add(main);

            // 所有编辑框应用逻辑
            foreach (var n in new[] { numW, numH, numZoom })
                n.ValueChanged += (s, e) => RunInspect();
            foreach (var c in new[] { cmbScan, cmbBit, cmbEncoding })
                c.SelectedIndexChanged += (s, e) => RunInspect();
        }

        private Label CreateLabel(string text, Color? color = null) => new Label { Text = text, ForeColor = color ?? Color.Gray, AutoSize = true, Margin = new Padding(0, 10, 0, 3) };

        private void LoadBin()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字库文件|*.bin" })
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _fontData = File.ReadAllBytes(ofd.FileName);
                    lblOffsetInfo.Text = $"已加载: {Path.GetFileName(ofd.FileName)}\n大小: {_fontData.Length} 字节";
                    RunInspect();
                }
        }

        private void RunInspect()
        {
            if (_fontData == null || string.IsNullOrEmpty(txtInput.Text))
                return;

            int bytesPerChar = CalculateBytesSize();
            int w = (int)numW.Value;
            int h = (int)numH.Value;
            int zoom = (int)numZoom.Value;

            IEncodingProvider provider = (cmbEncoding.SelectedIndex == 0) ? (IEncodingProvider)new GbkCustomProvider() : (IEncodingProvider)new Gb2312Provider();

            List<byte[]> glyphs = new List<byte[]>();
            StringBuilder sbCode = new StringBuilder();

            // 多字符处理
            foreach (char c in txtInput.Text)
            {
                byte[] bytes = Encoding.GetEncoding("GBK").GetBytes(c.ToString());
                ushort code = (bytes.Length >= 2) ? (ushort)((bytes[0] << 8) | bytes[1]) : (ushort)bytes[0];

                int index = provider.GetIndexByCode(code);
                long offset = (long)index * bytesPerChar;

                if (index != -1 && offset + bytesPerChar <= _fontData.Length)
                {
                    byte[] g = new byte[bytesPerChar];
                    Array.Copy(_fontData, offset, g, 0, bytesPerChar);
                    glyphs.Add(g);
                    sbCode.AppendLine($"// '{c}' Code:0x{code:X4} Index:{index} Offset:0x{offset:X}");
                }
            }

            if (glyphs.Count > 0)
            {
                RenderMultiToCanvas(glyphs, w, h, zoom);
                txtCode.Text = sbCode.ToString();
                lblOffsetInfo.Text = $"已显示 {glyphs.Count} 个字符";
            }
        }

        private void RenderMultiToCanvas(List<byte[]> glyphs, int w, int h, int zoom)
        {
            int spacing = zoom * 0; // 字符间距设为 0 个 Zoom 单位
            int totalW = glyphs.Count * (w * zoom + spacing);
            int totalH = h * zoom;

            Bitmap bmp = new Bitmap(totalW, totalH);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(30, 30, 30));
                for (int i = 0; i < glyphs.Count; i++)
                {
                    int charOffsetX = i * (w * zoom + spacing);
                    byte[] data = glyphs[i];

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (GetBitFromData(data, x, y, w, h))
                            {
                                g.FillRectangle(Brushes.Lime, charOffsetX + x * zoom, y * zoom, zoom - 1, zoom - 1);
                            }
                        }
                    }
                }
            }
            var old = picInspect.Image;
            picInspect.Image = bmp;
            old?.Dispose();
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
    }
}