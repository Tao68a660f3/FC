#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using FC.core;
using FC.ui;

namespace FC
{
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiManager _mgr = new AsciiManager();
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65; // 默认 'A'

        // 核心控件引用
        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel pnlParams;
        private PixelEditorControl pixelEditor;

        private NumericUpDown numAsciiValue, numCanvasW, numCanvasH, numWidth;
        private NumericUpDown numFontSize, numScaleX, numScaleY, numOffsetX, numOffsetY, numBaseline;
        private TextBox txtChar;

        public AsciiGeneratorControl()
        {
            this.Size = new Size(1000, 700);
            this.BackColor = UiFactory.BgColor;
            this.ForeColor = Color.White;
            this.Dock = DockStyle.Fill;

            // 1. 初始化物理布局
            InitIntegratedLayout();

            // 2. 绑定交互事件
            BindLogicEvents();

            // 3. 首次加载同步
            this.HandleCreated += (s, e) => SyncUI();
        }

        private void InitIntegratedLayout()
        {
            this.Controls.Clear();

            // 主容器：左参数 (330px) + 右预览 (剩余)
            mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // 左侧参数面板
            pnlParams = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(35, 35, 38)
            };

            // --- 组 1：字符选择 ---
            GroupBox gbIndex = CreateSafeGroup("字符定位 (0-255)", 85);
            TableLayoutPanel grid1 = UiFactory.CreateGridContainer(1, 4);
            numAsciiValue = UiFactory.AddGridControl(grid1, "Index", 65, 0, 0);
            numAsciiValue.Maximum = 255;
            txtChar = new TextBox
            {
                ReadOnly = true,
                BackColor = UiFactory.ControlBg,
                ForeColor = Color.Lime,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Margin = new Padding(5, 8, 5, 0),
                Text = "A",
                Font = new Font("Consolas", 10F, FontStyle.Bold)
            };
            grid1.Controls.Add(txtChar, 2, 0); // 占用第 3 列
            gbIndex.Controls.Add(grid1);
            pnlParams.Controls.Add(gbIndex);

            // --- 组 2：画布设置 ---
            GroupBox gbSize = CreateSafeGroup("画布与变宽", 125);
            TableLayoutPanel grid2 = UiFactory.CreateGridContainer(2, 4);
            numCanvasW = UiFactory.AddGridControl(grid2, "画布W", 16, 0, 0);
            numCanvasH = UiFactory.AddGridControl(grid2, "画布H", 16, 0, 1);
            numWidth = UiFactory.AddGridControl(grid2, "有效宽", 8, 1, 0);
            gbSize.Controls.Add(grid2);
            pnlParams.Controls.Add(gbSize);

            // --- 组 3：TTF 渲染参数 ---
            GroupBox gbTTF = CreateSafeGroup("TTF 渲染参数", 165);
            TableLayoutPanel grid3 = UiFactory.CreateGridContainer(3, 4);
            numFontSize = UiFactory.AddGridControl(grid3, "字号", 16, 0, 0);
            numBaseline = UiFactory.AddGridControl(grid3, "基准Y", 0, 0, 1);
            numScaleX = UiFactory.AddGridControl(grid3, "比X%", 100, 1, 0);
            numScaleY = UiFactory.AddGridControl(grid3, "比Y%", 100, 1, 1);
            numOffsetX = UiFactory.AddGridControl(grid3, "移X", 0, 2, 0);
            numOffsetY = UiFactory.AddGridControl(grid3, "移Y", 0, 2, 1);
            gbTTF.Controls.Add(grid3);
            pnlParams.Controls.Add(gbTTF);

            // --- 组 4：功能按钮 ---
            AddActionBtn("导入 (TTF/BMP/BIN)", UiFactory.AccentBlue, (s, e) => OnImportClick());
            AddActionBtn("批量渲染 (覆盖未锁定)", Color.DarkOrange, (s, e) => OnBatchRender());
            AddActionBtn("导出 .BIN (XOR加密)", Color.ForestGreen, (s, e) => OnExportClick());

            // 右侧：像素编辑器
            pixelEditor = new PixelEditorControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(15),
                BackColor = Color.Black
            };

            mainLayout.Controls.Add(pnlParams, 0, 0);
            mainLayout.Controls.Add(pixelEditor, 1, 0);
            this.Controls.Add(mainLayout);
        }

        // 辅助：创建不受 Dock.Top 干扰的容器
        private GroupBox CreateSafeGroup(string title, int h) => new GroupBox
        {
            Text = title,
            Height = h,
            Width = 300,
            ForeColor = Color.LightSkyBlue,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 10)
        };

        private void AddActionBtn(string text, Color c, EventHandler h)
        {
            Button btn = UiFactory.CreateStyledButton(text, c, 40);
            btn.Dock = DockStyle.None; // 关键：在 FlowLayoutPanel 中不要 Dock
            btn.Width = 300;
            btn.Click += h;
            pnlParams.Controls.Add(btn);
        }

        private void BindLogicEvents()
        {
            numAsciiValue.ValueChanged += (s, e) => {
                _currentIdx = (int)numAsciiValue.Value;
                txtChar.Text = ((char)_currentIdx).ToString();
                SyncUI();
            };

            // 联动渲染
            EventHandler renderTrigger = (s, e) => {
                if (!_mgr.AsciiSet[_currentIdx].IsManual) RenderCurrentChar();
            };
            numFontSize.ValueChanged += renderTrigger;
            numBaseline.ValueChanged += renderTrigger;
            numOffsetX.ValueChanged += renderTrigger;
            numOffsetY.ValueChanged += renderTrigger;
            numScaleX.ValueChanged += renderTrigger;
            numScaleY.ValueChanged += renderTrigger;
        }

        private void SyncUI()
        {
            if (pixelEditor == null) return;
            var entry = _mgr.AsciiSet[_currentIdx];

            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;

            // 确保数据有效性
            if (entry.Data == null) entry.Data = new byte[512];
            pixelEditor.CurrentData = entry.Data;
            pixelEditor.ActiveWidth = entry.Width;

            txtChar.ForeColor = entry.IsManual ? Color.Yellow : Color.Lime;
            pixelEditor.Invalidate();
        }

        private void RenderCurrentChar()
        {
            // 1. 同步画布与渲染参数
            _fontRender.CanvasWidth = (int)numCanvasW.Value;
            _fontRender.CanvasHeight = (int)numCanvasH.Value;
            _fontRender.OffsetY = (int)numBaseline.Value;
            _fontRender.OffsetX = (int)numOffsetX.Value;
            _fontRender.ScaleX = (int)numScaleX.Value;
            _fontRender.ScaleY = (int)numScaleY.Value;

            // 2. 核心：如果字号改变了，需要重新加载字体实例（针对你的底层实现）
            // 注意：如果你的 _fontRender 内部没有自动处理字号更新，
            // 可能需要在 Render 之前重新调用一次 LoadFontFile，或者你的类里有一个专门改 Size 的方法。

            // 3. 执行渲染并将结果存入管理器
            string charToRender = ((char)_currentIdx).ToString();
            _mgr.AsciiSet[_currentIdx].Data = _fontRender.RenderChar(charToRender);

            // 4. 自动计算有效宽度并刷新 UI
            _mgr.AsciiSet[_currentIdx].Width = _mgr.CalculateWidth(
                _mgr.AsciiSet[_currentIdx].Data,
                (int)numCanvasW.Value,
                (int)numCanvasH.Value
            );

            SyncUI();
        }

        /// <summary>
        /// 处理导入逻辑：支持 TTF 字体加载
        /// </summary>
        private void OnImportClick()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "字体文件|*.ttf;*.otf|所有文件|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 获取当前 UI 上的字号设定并传递给你的 LoadFontFile 函数
                        float fontSize = (float)numFontSize.Value;
                        _fontRender.LoadFontFile(ofd.FileName, fontSize);

                        RenderCurrentChar();
                        MessageBox.Show("字体加载成功！", "提示");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"字体加载失败: {ex.Message}", "错误");
                    }
                }
            }
        }

        /// <summary>
        /// 批量渲染：将当前 TTF 设置应用到所有 ASCII 字符 (32-126)
        /// </summary>
        private void OnBatchRender()
        {
            var result = MessageBox.Show("将根据当前 TTF 参数重新生成所有未手动修改的字符，是否继续？",
                                       "批量渲染确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // 遍历标准可见 ASCII 范围
                for (int i = 32; i < 127; i++)
                {
                    // 如果用户没有手动编辑过该字符，则自动重绘
                    if (!_mgr.AsciiSet[i].IsManual)
                    {
                        _fontRender.CanvasWidth = (int)numCanvasW.Value;
                        _fontRender.CanvasHeight = (int)numCanvasH.Value;
                        _fontRender.OffsetY = (int)numBaseline.Value;
                        _fontRender.OffsetX = (int)numOffsetX.Value;
                        _fontRender.ScaleX = (int)numScaleX.Value;
                        _fontRender.ScaleY = (int)numScaleY.Value;

                        _mgr.AsciiSet[i].Data = _fontRender.RenderChar(((char)i).ToString());
                        _mgr.AsciiSet[i].Width = _mgr.CalculateWidth(_mgr.AsciiSet[i].Data, (int)numCanvasW.Value, (int)numCanvasH.Value);
                    }
                }
                SyncUI();
                MessageBox.Show("批量渲染完成！", "提示");
            }
        }

        /// <summary>
        /// 导出逻辑：生成 .BIN 文件并执行 XOR 加密（针对嵌入式安全性）
        /// </summary>
        private void OnExportClick()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "二进制文件|*.bin";
                sfd.FileName = "ascii_font.bin";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                        {
                            // 遍历 0-255 (或根据需求选 32-126)
                            for (int i = 0; i < 256; i++)
                            {
                                var entry = _mgr.AsciiSet[i];
                                byte[] data = entry.Data;

                                // 如果数据为空，填充空白
                                if (data == null || data.Length == 0)
                                    data = new byte[(int)(numCanvasW.Value * numCanvasH.Value / 8)];

                                // 执行简单的 XOR 加密 (例如与 0x5A 异或)，增加一点破解门槛
                                byte[] encryptedData = data.Select(b => (byte)(b ^ 0x5A)).ToArray();

                                // 写入文件
                                fs.Write(encryptedData, 0, encryptedData.Length);
                            }
                        }
                        MessageBox.Show("导出成功！已应用 XOR (0x5A) 加密。", "提示");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导出失败: {ex.Message}", "错误");
                    }
                }
            }
        }
    }
}