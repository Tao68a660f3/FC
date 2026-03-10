#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using FC.core;
using FC.ui;

namespace FC
{
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiManager _mgr = new AsciiManager();
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65; // 默认显示 'A'

        // 成员变量（提升作用域，确保引用不丢失）
        private TableLayoutPanel mainLayout;
        private FlowLayoutPanel pnlParams;
        private PixelEditorControl pixelEditor;
        private NumericUpDown numAsciiValue, numCanvasW, numCanvasH, numWidth;
        private NumericUpDown numFontSize, numScaleX, numScaleY, numOffsetX, numOffsetY, numBaseline;
        private TextBox txtChar;

        public AsciiGeneratorControl()
        {
            // 必须：对齐 GBK 模块的成功经验，显式设置初始大小
            this.Size = new Size(1000, 700);
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(32, 32, 32); // 硬编码颜色，避开静态引用风险

            SetupLayout();
            BindEvents();

            // 确保在句柄创建后刷新一次 UI
            this.Load += (s, e) => SyncUI();
        }

        private void SetupLayout()
        {
            this.Controls.Clear();

            // 1. 创建主布局容器
            mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F)); // 参数区固定宽度
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));  // 编辑区自适应
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 2. 创建左侧滚动参数面板
            pnlParams = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10),
                BackColor = Color.Transparent
            };

            // --- 填充参数：字符定位组 ---
            GroupBox gbIndex = UiFactory.CreateModernGroupBox("字符定位 (0-255)", 85);
            TableLayoutPanel gridIndex = UiFactory.CreateGridContainer(1, 4);
            numAsciiValue = UiFactory.AddGridControl(gridIndex, "Index", 65, 0, 0);
            txtChar = new TextBox
            {
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.Lime,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Center,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(5, 8, 5, 0),
                Text = "A"
            };
            gridIndex.Controls.Add(txtChar, 2, 0);
            gbIndex.Controls.Add(gridIndex);
            pnlParams.Controls.Add(gbIndex);

            // --- 填充参数：画布配置组 ---
            GroupBox gbSize = UiFactory.CreateModernGroupBox("画布与变宽", 120);
            TableLayoutPanel gridSize = UiFactory.CreateGridContainer(2, 4);
            numCanvasW = UiFactory.AddGridControl(gridSize, "画布W", 16, 0, 0);
            numCanvasH = UiFactory.AddGridControl(gridSize, "画布H", 16, 0, 1);
            numWidth = UiFactory.AddGridControl(gridSize, "有效宽", 8, 1, 0);
            gbSize.Controls.Add(gridSize);
            pnlParams.Controls.Add(gbSize);

            // --- 填充参数：TTF 渲染参数组 ---
            GroupBox gbTTF = UiFactory.CreateModernGroupBox("TTF 渲染参数", 160);
            TableLayoutPanel gridTTF = UiFactory.CreateGridContainer(3, 4);
            numFontSize = UiFactory.AddGridControl(gridTTF, "字号", 16, 0, 0);
            numBaseline = UiFactory.AddGridControl(gridTTF, "基准Y", 0, 0, 1);
            numScaleX = UiFactory.AddGridControl(gridTTF, "比X%", 100, 1, 0);
            numScaleY = UiFactory.AddGridControl(gridTTF, "比Y%", 100, 1, 1);
            numOffsetX = UiFactory.AddGridControl(gridTTF, "移X", 0, 2, 0);
            numOffsetY = UiFactory.AddGridControl(gridTTF, "移Y", 0, 2, 1);
            gbTTF.Controls.Add(gridTTF);
            pnlParams.Controls.Add(gbTTF);

            // --- 填充参数：操作按钮 ---
            Button btnImport = UiFactory.CreateStyledButton("导入 (TTF/BMP/FONT/BIN)", Color.FromArgb(0, 122, 204));
            btnImport.Click += (s, e) => OnImportClick();
            pnlParams.Controls.Add(btnImport);

            Button btnBatch = UiFactory.CreateStyledButton("批量渲染 (覆盖未锁定)", Color.DarkOrange);
            btnBatch.Click += (s, e) => OnBatchRender();
            pnlParams.Controls.Add(btnBatch);

            Button btnExport = UiFactory.CreateStyledButton("导出 .BIN (XOR加密)", Color.ForestGreen);
            btnExport.Click += (s, e) => OnExportClick();
            pnlParams.Controls.Add(btnExport);

            // 3. 创建右侧像素编辑器
            pixelEditor = new PixelEditorControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(20),
                BackColor = Color.Black
            };

            // 4. 组装并强制挂载
            mainLayout.Controls.Add(pnlParams, 0, 0);
            mainLayout.Controls.Add(pixelEditor, 1, 0);

            this.Controls.Add(mainLayout);

            // 5. 布局强制计算
            mainLayout.ResumeLayout(true);
            this.ResumeLayout(true);
            this.PerformLayout();
        }

        private void BindEvents()
        {
            // 索引改变
            numAsciiValue.ValueChanged += (s, e) => {
                _currentIdx = (int)numAsciiValue.Value;
                txtChar.Text = ((char)_currentIdx).ToString();
                SyncUI();
            };

            // 宽度改变（具名函数绑定，方便 SyncUI 解绑）
            numWidth.ValueChanged += numWidth_ValueChanged;

            // TTF 参数联动
            EventHandler ttfUpdate = (s, e) => {
                if (!_mgr.AsciiSet[_currentIdx].IsManual) RenderCurrentChar();
            };
            numFontSize.ValueChanged += ttfUpdate;
            numBaseline.ValueChanged += ttfUpdate;
            numOffsetX.ValueChanged += ttfUpdate;
            numOffsetY.ValueChanged += ttfUpdate;
            numScaleX.ValueChanged += ttfUpdate;
            numScaleY.ValueChanged += ttfUpdate;

            // 编辑器点击即锁定
            pixelEditor.MouseDown += (s, e) => {
                _mgr.AsciiSet[_currentIdx].IsManual = true;
                txtChar.ForeColor = Color.Yellow;
            };
        }

        private void numWidth_ValueChanged(object sender, EventArgs e)
        {
            _mgr.AsciiSet[_currentIdx].Width = (int)numWidth.Value;
            _mgr.AsciiSet[_currentIdx].IsManual = true;
            pixelEditor.ActiveWidth = (int)numWidth.Value;
            pixelEditor.Invalidate();
            txtChar.ForeColor = Color.Yellow;
        }

        private void SyncUI()
        {
            if (pixelEditor == null) return;
            var entry = _mgr.AsciiSet[_currentIdx];

            // 画布尺寸同步
            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;
            pixelEditor.CurrentData = entry.Data;

            // 宽度同步（暂时解绑，防止误触发锁定）
            numWidth.ValueChanged -= numWidth_ValueChanged;
            numWidth.Value = Math.Min(numWidth.Maximum, Math.Max(numWidth.Minimum, entry.Width));
            pixelEditor.ActiveWidth = (int)numWidth.Value;
            numWidth.ValueChanged += numWidth_ValueChanged;

            // 颜色状态反馈
            txtChar.ForeColor = entry.IsManual ? Color.Yellow : Color.Lime;

            pixelEditor.Invalidate();
        }

        private void RenderCurrentChar()
        {
            UpdateRendererConfig();
            _mgr.AsciiSet[_currentIdx].Data = _fontRender.RenderChar(((char)_currentIdx).ToString());
            _mgr.AsciiSet[_currentIdx].Width = _mgr.CalculateWidth(_mgr.AsciiSet[_currentIdx].Data, (int)numCanvasW.Value, (int)numCanvasH.Value);
            SyncUI();
        }

        private void UpdateRendererConfig()
        {
            _fontRender.CanvasWidth = (int)numCanvasW.Value;
            _fontRender.CanvasHeight = (int)numCanvasH.Value;
            _fontRender.OffsetY = (int)numBaseline.Value;
            _fontRender.OffsetX = (int)numOffsetX.Value;
            _fontRender.ScaleX = (int)numScaleX.Value;
            _fontRender.ScaleY = (int)numScaleY.Value;
        }

        private void OnImportClick()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "支持格式|*.ttf;*.otf;*.bmp;*.font;*.bin" })
            {
                if (ofd.ShowDialog() != DialogResult.OK) return;
                string ext = Path.GetExtension(ofd.FileName).ToLower();
                UpdateRendererConfig();

                if (ext == ".bin")
                {
                    if (_mgr.ImportFromBin(ofd.FileName, out int w, out int h))
                    {
                        numCanvasW.Value = w; numCanvasH.Value = h;
                    }
                }
                else if (ext == ".font") _mgr.ImportFromFontText(ofd.FileName);
                else if (ext == ".bmp") _mgr.ImportFromBmp(ofd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, _fontRender);
                else
                {
                    _fontRender.LoadFontFile(ofd.FileName, (float)numFontSize.Value);
                    RenderCurrentChar();
                }
                SyncUI();
            }
        }

        private void OnBatchRender()
        {
            UpdateRendererConfig();
            _mgr.BatchRender(_fontRender, (int)numCanvasW.Value, (int)numCanvasH.Value);
            SyncUI();
            MessageBox.Show("批量生成完成，已跳过锁定字符。");
        }

        private void OnExportClick()
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "BIN文件|*.bin" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _mgr.SaveToBin(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value);
                    MessageBox.Show("字库导出成功！");
                }
            }
        }
    }
}