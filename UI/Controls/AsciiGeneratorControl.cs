#nullable disable
using FC;
using FC.Core;
using static FC.UI.UiFactory;

namespace FC.UI.Controls
{
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiManager _mgr = new AsciiManager();
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65;
        private string _lastTtfPath = "";

        // 控件成员变量
        private PreciseNumericUpDown numCanvasW, numCanvasH, numActiveWidth;
        private PreciseNumericUpDown numFontSize, numFontScaleX, numFontScaleY, numFontOffsetX, numFontOffsetY;
        private PreciseNumericUpDown numShiftX, numShiftY, numAsciiIdx;
        private TextBox txtAsciiChar, txtFontPath;
        private CheckBox chkLocked;
        private Button btnLoadTTF, btnApplyVector, btnApplyShift, btnBatchRender, btnSaveBin, btnUnlockAll;
        private Button btnImportBin, btnImportBmp, btnImportFont;
        private PixelEditorControl pixelEditor;
        // 协议配置
        private CheckBox chkWidthAbs;
        private ComboBox cmbScanDir, cmbBitOrder;
        // 快捷操作
        private Button btnAutoCrop, btnAutoCenter;

        public AsciiGeneratorControl()
        {
            DoubleBuffered = true;
            BackColor = BgColor;
            Dock = DockStyle.Fill;
            InitResponsiveLayout();
            BindEvents();

            // --- 核心修复：启动时强制同步 UI 状态 ---
            numAsciiIdx.Value = 65;
            txtAsciiChar.Text = "A";
            OnIdxChanged();
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

            float scaleScaling = DeviceDpi / 150f;

            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(420F * scaleScaling))); // 左侧控制
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // 右侧预览
            Controls.Add(mainTable);

            // --- 左侧：响应式容器 (4行) ---
            TableLayoutPanel leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F)); // 1. 画布
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 30F)); // 2. 矢量生成
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 18F)); // 3. 物理位移
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 32F)); // 4. 导航与执行
            mainTable.Controls.Add(leftGrid, 0, 0);

            // --- 1. 画布与协议 ---
            GroupBox gbCanvas = CreateModernGroupBox("画布与协议", 0);
            gbCanvas.Dock = DockStyle.Fill;
            TableLayoutPanel canvasGrid = CreateGridContainer(3, 4);

            numCanvasW = AddGridControl(canvasGrid, "画布宽", 16, 0, 0);
            numCanvasW.Minimum = 1;
            numCanvasH = AddGridControl(canvasGrid, "画布高", 16, 0, 1);
            numCanvasH.Minimum = 1;
            numActiveWidth = AddGridControl(canvasGrid, "有效宽", 8, 1, 0);
            numActiveWidth.Minimum = 0;

            // 新增：绝对宽度模式 CheckBox
            chkWidthAbs = new CheckBox
            {
                Text = "绝对宽度",
                ForeColor = TextGray,
                // 关键点 1：只 Anchor 到左侧，防止文字在窄列里强制换行
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(20, 0, 0, 0), // 左边给点留白，别挨数值框太紧
                CheckAlign = ContentAlignment.MiddleLeft
            };
            // 关键点 2：放在第 2 行（索引 1）、第 3 列（索引 2）
            canvasGrid.Controls.Add(chkWidthAbs, 2, 1);
            // 关键点 3：设置跨 2 列，把第 3、4 列的空间合并给它
            canvasGrid.SetColumnSpan(chkWidthAbs, 2);

            // 新增：扫描方向与位序选择
            cmbScanDir = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ControlBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbScanDir.Items.AddRange(new string[] { "横向 (H)", "纵向 (V)" });
            cmbScanDir.SelectedIndex = 0;
            canvasGrid.Controls.Add(new Label { Text = "方向", ForeColor = TextGray, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
            canvasGrid.Controls.Add(cmbScanDir, 1, 2);

            cmbBitOrder = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ControlBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            cmbBitOrder.Items.AddRange(new string[] { "MSB First", "LSB First" });
            cmbBitOrder.SelectedIndex = 0;
            canvasGrid.Controls.Add(new Label { Text = "位序", ForeColor = TextGray, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 2);
            canvasGrid.Controls.Add(cmbBitOrder, 3, 2);

            gbCanvas.Controls.Add(canvasGrid);
            leftGrid.Controls.Add(gbCanvas, 0, 0);

            // --- 2. 矢量生成设置 ---
            GroupBox gbVector = CreateModernGroupBox("矢量生成设置", 0);
            gbVector.Dock = DockStyle.Fill; // 确保填满第2行

            TableLayoutPanel filePickGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 2 };
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            txtFontPath = new TextBox { Dock = DockStyle.Fill, BackColor = ControlBg, ForeColor = Color.White, ReadOnly = true };
            btnLoadTTF = new Button { Text = "选择字体", Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            filePickGrid.Controls.Add(txtFontPath, 0, 0);
            filePickGrid.Controls.Add(btnLoadTTF, 1, 0);

            TableLayoutPanel vectorGrid = CreateGridContainer(3, 4);
            vectorGrid.Dock = DockStyle.Fill; // 让参数网格自适应
            numFontSize = AddGridControl(vectorGrid, "字号", 16, 0, 0);
            numFontSize.Minimum = 1;
            numFontOffsetX = AddGridControl(vectorGrid, "移X", 0, 1, 0);
            numFontOffsetY = AddGridControl(vectorGrid, "移Y", 0, 1, 1);
            numFontScaleX = AddGridControl(vectorGrid, "比X%", 100, 2, 0);
            numFontScaleX.Minimum = 10;
            numFontScaleX.Maximum = 500;
            numFontScaleY = AddGridControl(vectorGrid, "比Y%", 100, 2, 1);
            numFontScaleY.Minimum = 10;
            numFontScaleY.Maximum = 500;

            btnApplyVector = CreateStyledButton("矢量像素推到底稿", Color.FromArgb(60, 120, 60), 38);
            btnApplyVector.Dock = DockStyle.Bottom;

            gbVector.Controls.Add(vectorGrid); // 先加 Fill
            gbVector.Controls.Add(filePickGrid); // 再加 Top
            gbVector.Controls.Add(btnApplyVector); // 最后加 Bottom
            leftGrid.Controls.Add(gbVector, 0, 1);

            // --- 3. 物理像素位移 ---
            GroupBox gbShift = CreateModernGroupBox("物理像素位移", 0);
            gbShift.Dock = DockStyle.Fill; // 确保填满第3行
            TableLayoutPanel shiftGrid = CreateGridContainer(1, 4);
            numShiftX = AddGridControl(shiftGrid, "平移X", 0, 0, 0);
            numShiftY = AddGridControl(shiftGrid, "平移Y", 0, 0, 1);
            btnApplyShift = CreateStyledButton("应用物理位移", Color.FromArgb(120, 60, 60), 32);
            btnApplyShift.Dock = DockStyle.Bottom;

            btnAutoCrop = CreateStyledButton("水平裁边", Color.FromArgb(45, 85, 115), 32);
            btnAutoCenter = CreateStyledButton("自动居中", Color.FromArgb(45, 85, 115), 32);
            // 创建一个容器横向放置这两个按钮
            TableLayoutPanel autoBtnGrid = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 2 };
            autoBtnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            autoBtnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            autoBtnGrid.Controls.Add(btnAutoCrop, 0, 0);
            autoBtnGrid.Controls.Add(btnAutoCenter, 1, 0);

            gbShift.Controls.Add(shiftGrid);
            gbShift.Controls.Add(autoBtnGrid);
            gbShift.Controls.Add(btnApplyShift);
            leftGrid.Controls.Add(gbShift, 0, 2);

            // --- 4. 导航与执行区 ---
            Panel runPanel = new Panel { Dock = DockStyle.Fill };

            // 导航行 (Top)
            TableLayoutPanel navGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 45, ColumnCount = 3, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 48) };
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            numAsciiIdx = new PreciseNumericUpDown { Maximum = 255, Value = 65, Dock = DockStyle.Fill, BackColor = ControlBg, ForeColor = Color.Lime, Font = new Font("Consolas", 11F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            txtAsciiChar = new TextBox { MaxLength = 1, Dock = DockStyle.Fill, BackColor = ControlBg, ForeColor = Color.Orange, TextAlign = HorizontalAlignment.Center, Font = new Font("微软雅黑", 11F, FontStyle.Bold) };
            chkLocked = new CheckBox { Text = "锁定字符", ForeColor = Color.White, Dock = DockStyle.Fill, CheckAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            navGrid.Controls.Add(numAsciiIdx, 0, 0);
            navGrid.Controls.Add(txtAsciiChar, 1, 0);
            navGrid.Controls.Add(chkLocked, 2, 0);

            // 按钮网格 (Fill)
            TableLayoutPanel btnGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

            btnUnlockAll = CreateStyledButton("🔓全部解锁", Color.FromArgb(80, 40, 40), 32);
            btnBatchRender = CreateStyledButton("批量矢量渲染", Color.FromArgb(70, 70, 70), 32);
            btnImportBin = CreateStyledButton("导入BIN", Color.FromArgb(50, 50, 50), 32);
            btnImportBmp = CreateStyledButton("导入BMP", Color.FromArgb(50, 50, 50), 32);
            btnImportFont = CreateStyledButton("导入FONT", Color.FromArgb(50, 50, 50), 32);
            btnSaveBin = CreateStyledButton("🚀导出.bin", AccentBlue, 42);

            btnGrid.Controls.Add(btnUnlockAll, 0, 0);
            btnGrid.SetColumnSpan(btnUnlockAll, 3);
            btnGrid.Controls.Add(btnBatchRender, 0, 1);
            btnGrid.SetColumnSpan(btnBatchRender, 3);
            btnGrid.Controls.Add(btnImportBin, 0, 2);
            btnGrid.Controls.Add(btnImportBmp, 1, 2);
            btnGrid.Controls.Add(btnImportFont, 2, 2);
            btnGrid.Controls.Add(btnSaveBin, 0, 3);
            btnGrid.SetColumnSpan(btnSaveBin, 3);

            // 关键顺序：先加 Fill，再加 Top。WinForms 会让 Top 停靠在上方，Fill 占据剩余空间
            runPanel.Controls.Add(btnGrid);
            runPanel.Controls.Add(navGrid);

            leftGrid.Controls.Add(runPanel, 0, 3);

            pixelEditor = new PixelEditorControl { Dock = DockStyle.Fill };
            mainTable.Controls.Add(pixelEditor, 1, 0);
        }

        private void BindEvents()
        {
            // --- 字体与渲染逻辑 ---
            btnLoadTTF.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体|*.ttf;*.ttc;*.otf" })
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        txtFontPath.Text = ofd.FileName;
                        _lastTtfPath = ofd.FileName;
                        UpdateVectorPreview();
                    }
            };

            EventHandler vectorTrigger = (s, e) => UpdateVectorPreview();
            numFontSize.ValueChanged += vectorTrigger;
            numFontOffsetX.ValueChanged += vectorTrigger;
            numFontOffsetY.ValueChanged += vectorTrigger;
            numFontScaleX.ValueChanged += vectorTrigger;
            numFontScaleY.ValueChanged += vectorTrigger;

            btnApplyVector.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastTtfPath))
                {
                    _mgr.GenerateFromVector(_currentIdx, _fontRender);
                    OnIdxChanged(); // 重新加载底稿并刷新 UI
                }
            };

            // --- 响应式重绘修复 ---
            pixelEditor.Resize += (s, e) =>
            {
                // 当父容器（Window 或 TableLayoutPanel）大小改变导致控件缩放时
                // 强制控件重新计算内部网格并调用 OnPaint
                pixelEditor.Invalidate();
            };

            // --- 导入功能绑定 ---
            btnImportBin.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "BIN|*.bin" })
                {
                    // 增加 out byte config 参数
                    if (ofd.ShowDialog() == DialogResult.OK && _mgr.ImportFromBinV2(ofd.FileName, out int w, out int h, out byte config))
                    {
                        // 1. 同步画布和编辑器尺寸
                        numCanvasW.Value = w;
                        numCanvasH.Value = h;
                        pixelEditor.CanvasW = w;
                        pixelEditor.CanvasH = h;

                        // 2. 核心：同步 V2 协议 UI 状态
                        chkWidthAbs.Checked = (config & AsciiManager.CFG_WIDTH_ABS) != 0;
                        cmbScanDir.SelectedIndex = (config & AsciiManager.CFG_SCAN_VERT) != 0 ? 1 : 0;
                        cmbBitOrder.SelectedIndex = (config & AsciiManager.CFG_BIT_LSB) != 0 ? 1 : 0;

                        OnIdxChanged();
                    }
                }
            };

            btnImportBmp.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Bitmap|*.bmp" })
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _mgr.ImportFromBmp(ofd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value);
                        OnIdxChanged();
                    }
            };

            btnImportFont.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "FONT|*.font;*.txt" })
                    if (ofd.ShowDialog() == DialogResult.OK && _mgr.ImportFromFontText(ofd.FileName, out int w, out int h))
                    {
                        numCanvasW.Value = w;
                        numCanvasH.Value = h;

                        pixelEditor.CanvasW = w;
                        pixelEditor.CanvasH = h;
                        OnIdxChanged();
                    }
            };

            // --- 批量、平移、解锁 ---
            btnBatchRender.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastTtfPath))
                {
                    _mgr.BatchRender(_fontRender, false);
                    OnIdxChanged();
                    MessageBox.Show("批量矢量渲染完成！");
                }
            };

            btnSaveBin.Click += (s, e) =>
            {
                // 构造配置位
                byte config = 0;
                if (chkWidthAbs.Checked)
                    config |= AsciiManager.CFG_WIDTH_ABS;
                if (cmbScanDir.SelectedIndex == 1)
                    config |= AsciiManager.CFG_SCAN_VERT;
                if (cmbBitOrder.SelectedIndex == 1)
                    config |= AsciiManager.CFG_BIT_LSB;

                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Binary Font|*.bin" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        // 调用 V2 导出方法
                        _mgr.SaveToBinV2(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, config);
                        MessageBox.Show($"导出成功！\nConfig: 0x{config:X2} {numCanvasW.Value} {numCanvasH.Value}", "提示");
                    }
                }
            };

            btnApplyShift.Click += (s, e) =>
            {
                _mgr.ApplyShift(_currentIdx, -(int)numShiftX.Value, -(int)numShiftY.Value);
                numShiftX.Value = 0;
                numShiftY.Value = 0;
                SyncUI(false);
            };

            btnUnlockAll.Click += (s, e) => { _mgr.UnlockAll(); OnIdxChanged(); };

            // --- ASCII 联动 (防崩溃版本) ---
            numAsciiIdx.ValueChanged += (s, e) =>
            {
                _currentIdx = (int)numAsciiIdx.Value;
                string newChar = ((char)_currentIdx).ToString();
                if (txtAsciiChar.Text != newChar)
                    txtAsciiChar.Text = newChar;
                OnIdxChanged();
            };

            txtAsciiChar.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(txtAsciiChar.Text))
                {
                    int charVal = txtAsciiChar.Text[0];
                    if (charVal >= (int)numAsciiIdx.Minimum && charVal <= (int)numAsciiIdx.Maximum)
                    {
                        if (numAsciiIdx.Value != charVal)
                            numAsciiIdx.Value = charVal;
                    }
                }
            };

            chkLocked.CheckedChanged += (s, e) =>
            {
                if (chkLocked.Focused)
                    _mgr.AsciiSet[_currentIdx].IsManual = chkLocked.Checked;
            };

            // --- 画布响应 ---
            EventHandler canvasSizeTrigger = (s, e) =>
            {
                // --- 核心改进：非用户手动触发时不执行 Resize ---
                // 如果两个框都没有焦点，说明是代码在 Import 过程中赋值，直接跳过逻辑
                if (!numCanvasW.Focused && !numCanvasH.Focused)
                    return;

                int w = (int)numCanvasW.Value;
                int h = (int)numCanvasH.Value;

                // A. 更新编辑器控件尺寸属性
                pixelEditor.CanvasW = w;
                pixelEditor.CanvasH = h;

                // B. 手动缩放时，我们才调用 ResizeAll 这种会改动像素的操作
                _mgr.ResizeAll(w, h);

                // C. 刷新预览
                SyncUI(false);
            };

            numCanvasW.ValueChanged += canvasSizeTrigger;
            numCanvasH.ValueChanged += canvasSizeTrigger;

            numActiveWidth.ValueChanged += (s, e) =>
            {
                _mgr.AsciiSet[_currentIdx].Width = (int)numActiveWidth.Value;
                pixelEditor.ActiveWidth = (int)numActiveWidth.Value;
                pixelEditor.Invalidate();
            };
            pixelEditor.DataChanged += () => { _mgr.AsciiSet[_currentIdx].IsManual = true; chkLocked.Checked = true; };

            btnAutoCrop.Click += (s, e) =>
            {
                _mgr.AutoCropHorizontal(_currentIdx); // 调用新方法
                OnIdxChanged(); // 刷新 UI 同步宽度显示
            };

            btnAutoCenter.Click += (s, e) =>
            {
                _mgr.AutoCenter(_currentIdx); // 调用新方法
                OnIdxChanged(); // 刷新 UI 同步宽度显示
            };
        }

        private void OnIdxChanged()
        {
            var entry = _mgr.AsciiSet[_currentIdx];
            numActiveWidth.Value = entry.Width;
            chkLocked.Checked = entry.IsManual;
            // 确保红线位置同步
            pixelEditor.ActiveWidth = entry.Width;
            SyncPreview();
        }

        private void UpdateVectorPreview()
        {
            if (string.IsNullOrEmpty(_lastTtfPath))
                return;
            _fontRender.LoadFontFile(_lastTtfPath, (float)numFontSize.Value);
            // 修正变量名对齐
            _fontRender.CanvasWidth = (int)numCanvasW.Value;
            _fontRender.CanvasHeight = (int)numCanvasH.Value;
            _fontRender.OffsetX = -(int)numFontOffsetX.Value;
            _fontRender.OffsetY = -(int)numFontOffsetY.Value;
            _fontRender.ScaleX = (int)numFontScaleX.Value;
            _fontRender.ScaleY = (int)numFontScaleY.Value;
            _mgr.UpdateVectorPreview(_currentIdx, _fontRender);
            SyncUI(true);
        }

        private void SyncPreview()
        {
            _mgr.UpdateShiftPreview(_currentIdx, 0, 0);
            SyncUI(false);
        }

        private void SyncUI(bool isPreview)
        {
            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;
            pixelEditor.ActiveWidth = (int)numActiveWidth.Value;
            pixelEditor.CurrentBitmap = isPreview ? _mgr.PreviewBitmap : _mgr.AsciiSet[_currentIdx].Glyph;
            pixelEditor.Invalidate();
        }
    }
}