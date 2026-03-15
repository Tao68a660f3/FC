#nullable disable
using FC.core;

namespace FC.ui
{
    public partial class AsciiGeneratorControl : UserControl
    {
        private AsciiManager _mgr = new AsciiManager();
        private FontRender _fontRender = new FontRender();
        private int _currentIdx = 65;
        private string _lastTtfPath = "";

        // 控件成员变量
        private NumericUpDown numCanvasW, numCanvasH, numActiveWidth;
        private NumericUpDown numFontSize, numFontScaleX, numFontScaleY, numFontOffsetX, numFontOffsetY;
        private NumericUpDown numShiftX, numShiftY, numAsciiIdx;
        private TextBox txtAsciiChar, txtFontPath;
        private CheckBox chkLocked;
        private Button btnLoadTTF, btnApplyVector, btnApplyShift, btnBatchRender, btnSaveBin, btnUnlockAll;
        private Button btnImportBin, btnImportBmp, btnImportFont;
        private PixelEditorControl pixelEditor;

        public AsciiGeneratorControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = UiFactory.BgColor;
            this.Dock = DockStyle.Fill;
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

            float scaleScaling = this.DeviceDpi / 150f;

            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (int)(420F * scaleScaling))); // 左侧控制
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // 右侧预览
            this.Controls.Add(mainTable);

            // --- 左侧：响应式容器 (4行) ---
            TableLayoutPanel leftGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 14F)); // 1. 画布
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 36F)); // 2. 矢量生成
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 18F)); // 3. 物理位移
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 32F)); // 4. 导航与执行
            mainTable.Controls.Add(leftGrid, 0, 0);

            // --- 1. 画布与协议 ---
            GroupBox gbCanvas = UiFactory.CreateModernGroupBox("画布与协议", 0);
            gbCanvas.Dock = DockStyle.Fill; // 确保填满第1行
            TableLayoutPanel canvasGrid = UiFactory.CreateGridContainer(2, 4);
            numCanvasW = UiFactory.AddGridControl(canvasGrid, "画布宽", 16, 0, 0);
            numCanvasH = UiFactory.AddGridControl(canvasGrid, "画布高", 16, 0, 1);
            numActiveWidth = UiFactory.AddGridControl(canvasGrid, "有效宽", 8, 1, 0);
            gbCanvas.Controls.Add(canvasGrid);
            leftGrid.Controls.Add(gbCanvas, 0, 0);

            // --- 2. 矢量生成设置 ---
            GroupBox gbVector = UiFactory.CreateModernGroupBox("矢量生成设置", 0);
            gbVector.Dock = DockStyle.Fill; // 确保填满第2行

            TableLayoutPanel filePickGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 32, ColumnCount = 2 };
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            txtFontPath = new TextBox { Dock = DockStyle.Fill, BackColor = UiFactory.ControlBg, ForeColor = Color.White, ReadOnly = true };
            btnLoadTTF = new Button { Text = "选择字体", Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            filePickGrid.Controls.Add(txtFontPath, 0, 0);
            filePickGrid.Controls.Add(btnLoadTTF, 1, 0);

            TableLayoutPanel vectorGrid = UiFactory.CreateGridContainer(3, 4);
            vectorGrid.Dock = DockStyle.Fill; // 让参数网格自适应
            numFontSize = UiFactory.AddGridControl(vectorGrid, "字号", 16, 0, 0);
            numFontOffsetX = UiFactory.AddGridControl(vectorGrid, "移X", 0, 1, 0);
            numFontOffsetY = UiFactory.AddGridControl(vectorGrid, "移Y", 0, 1, 1);
            numFontScaleX = UiFactory.AddGridControl(vectorGrid, "比X%", 100, 2, 0);
            numFontScaleY = UiFactory.AddGridControl(vectorGrid, "比Y%", 100, 2, 1);

            btnApplyVector = UiFactory.CreateStyledButton("矢量像素推到底稿", Color.FromArgb(60, 120, 60), 38);
            btnApplyVector.Dock = DockStyle.Bottom;

            gbVector.Controls.Add(vectorGrid); // 先加 Fill
            gbVector.Controls.Add(filePickGrid); // 再加 Top
            gbVector.Controls.Add(btnApplyVector); // 最后加 Bottom
            leftGrid.Controls.Add(gbVector, 0, 1);

            // --- 3. 物理像素位移 ---
            GroupBox gbShift = UiFactory.CreateModernGroupBox("物理像素位移", 0);
            gbShift.Dock = DockStyle.Fill; // 确保填满第3行
            TableLayoutPanel shiftGrid = UiFactory.CreateGridContainer(1, 4);
            numShiftX = UiFactory.AddGridControl(shiftGrid, "平移X", 0, 0, 0);
            numShiftY = UiFactory.AddGridControl(shiftGrid, "平移Y", 0, 0, 1);
            btnApplyShift = UiFactory.CreateStyledButton("应用物理位移", Color.FromArgb(120, 60, 60), 32);
            btnApplyShift.Dock = DockStyle.Bottom;
            gbShift.Controls.Add(shiftGrid);
            gbShift.Controls.Add(btnApplyShift);
            leftGrid.Controls.Add(gbShift, 0, 2);

            // --- 4. 导航与执行区 ---
            Panel runPanel = new Panel { Dock = DockStyle.Fill };

            // 导航行 (Top)
            TableLayoutPanel navGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 45, ColumnCount = 3, Padding = new Padding(5), BackColor = Color.FromArgb(45, 45, 48) };
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            navGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            numAsciiIdx = new NumericUpDown { Maximum = 255, Value = 65, Dock = DockStyle.Fill, BackColor = UiFactory.ControlBg, ForeColor = Color.Lime, Font = new Font("Consolas", 11F, FontStyle.Bold), TextAlign = HorizontalAlignment.Center };
            txtAsciiChar = new TextBox { MaxLength = 1, Dock = DockStyle.Fill, BackColor = UiFactory.ControlBg, ForeColor = Color.Orange, TextAlign = HorizontalAlignment.Center, Font = new Font("微软雅黑", 11F, FontStyle.Bold) };
            chkLocked = new CheckBox { Text = "锁定字符", ForeColor = Color.White, Dock = DockStyle.Fill, CheckAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            navGrid.Controls.Add(numAsciiIdx, 0, 0);
            navGrid.Controls.Add(txtAsciiChar, 1, 0);
            navGrid.Controls.Add(chkLocked, 2, 0);

            // 按钮网格 (Fill)
            TableLayoutPanel btnGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));

            btnUnlockAll = UiFactory.CreateStyledButton("🔓全部解锁", Color.FromArgb(80, 40, 40), 32);
            btnBatchRender = UiFactory.CreateStyledButton("批量矢量渲染", Color.FromArgb(70, 70, 70), 32);
            btnImportBin = UiFactory.CreateStyledButton("导入BIN", Color.FromArgb(50, 50, 50), 32);
            btnImportBmp = UiFactory.CreateStyledButton("导入BMP", Color.FromArgb(50, 50, 50), 32);
            btnImportFont = UiFactory.CreateStyledButton("导入FONT", Color.FromArgb(50, 50, 50), 32);
            btnSaveBin = UiFactory.CreateStyledButton("🚀导出.bin", UiFactory.AccentBlue, 42);

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
            btnLoadTTF.Click += (s, e) => {
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

            btnApplyVector.Click += (s, e) => {
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
            btnImportBin.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "BIN|*.bin" })
                    if (ofd.ShowDialog() == DialogResult.OK && _mgr.ImportFromBin(ofd.FileName, out int w, out int h))
                    {
                        numCanvasW.Value = w;
                        numCanvasH.Value = h;

                        pixelEditor.CanvasW = w;
                        pixelEditor.CanvasH = h;

                        OnIdxChanged();
                    }
            };

            btnImportBmp.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Bitmap|*.bmp" })
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        _mgr.ImportFromBmp(ofd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value);
                        OnIdxChanged();
                    }
            };

            btnImportFont.Click += (s, e) => {
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
            btnBatchRender.Click += (s, e) => {
                if (!string.IsNullOrEmpty(_lastTtfPath))
                {
                    _mgr.BatchRender(_fontRender, false);
                    OnIdxChanged();
                    MessageBox.Show("批量矢量渲染完成！");
                }
            };

            btnSaveBin.Click += (s, e) => {
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Binary|*.bin" })
                    if (sfd.ShowDialog() == DialogResult.OK)
                        _mgr.SaveToBin(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, _fontRender);
            };

            btnApplyShift.Click += (s, e) => {
                _mgr.ApplyShift(_currentIdx, -(int)numShiftX.Value, -(int)numShiftY.Value);
                numShiftX.Value = 0;
                numShiftY.Value = 0;
                SyncUI(false);
            };

            btnUnlockAll.Click += (s, e) => { _mgr.UnlockAll(); OnIdxChanged(); };

            // --- ASCII 联动 (防崩溃版本) ---
            numAsciiIdx.ValueChanged += (s, e) => {
                _currentIdx = (int)numAsciiIdx.Value;
                string newChar = ((char)_currentIdx).ToString();
                if (txtAsciiChar.Text != newChar)
                    txtAsciiChar.Text = newChar;
                OnIdxChanged();
            };

            txtAsciiChar.TextChanged += (s, e) => {
                if (!string.IsNullOrEmpty(txtAsciiChar.Text))
                {
                    int charVal = (int)txtAsciiChar.Text[0];
                    if (charVal >= (int)numAsciiIdx.Minimum && charVal <= (int)numAsciiIdx.Maximum)
                    {
                        if (numAsciiIdx.Value != charVal)
                            numAsciiIdx.Value = charVal;
                    }
                }
            };

            chkLocked.CheckedChanged += (s, e) => {
                if (chkLocked.Focused)
                    _mgr.AsciiSet[_currentIdx].IsManual = chkLocked.Checked;
            };

            // --- 画布响应 ---
            // --- 核心修复：画布尺寸变更监听 ---
            EventHandler canvasSizeTrigger = (s, e) => {
                int w = (int)numCanvasW.Value;
                int h = (int)numCanvasH.Value;

                // A. 更新编辑器控件的网格属性，确保 HandleMouse 坐标计算正确
                pixelEditor.CanvasW = w;
                pixelEditor.CanvasH = h;

                // B. 强制内存中的 256 个 Bitmap 重置尺寸并 Crop
                _mgr.ResizeAll(w, h);

                // C. 刷新 UI 渲染
                SyncUI(false);
            };

            numCanvasW.ValueChanged += canvasSizeTrigger;
            numCanvasH.ValueChanged += canvasSizeTrigger;

            numActiveWidth.ValueChanged += (s, e) => {
                _mgr.AsciiSet[_currentIdx].Width = (int)numActiveWidth.Value;
                pixelEditor.ActiveWidth = (int)numActiveWidth.Value;
                pixelEditor.Invalidate();
            };
            pixelEditor.DataChanged += () => { _mgr.AsciiSet[_currentIdx].IsManual = true; chkLocked.Checked = true; };
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