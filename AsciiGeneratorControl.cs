#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
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
        private Button btnLoadTTF, btnApplyVector, btnApplyShift, btnBatchRender, btnSaveBin;
        private Button btnImportBin, btnImportBmp, btnImportFont;
        private PixelEditorControl pixelEditor;

        public AsciiGeneratorControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = UiFactory.BgColor;
            this.Dock = DockStyle.Fill;
            InitResponsiveLayout();
            BindEvents();
            SyncPreview();
        }
        // 需确保类成员变量包含以下定义：
        // private Button btnBatchRender, btnImportBin, btnImportBmp, btnImportFont, btnSaveBin;

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
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); // 左侧控制
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F)); // 右侧预览
            this.Controls.Add(mainTable);

            // --- 左侧：响应式容器 (4行比例分配) ---
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
            gbCanvas.Dock = DockStyle.Fill;
            TableLayoutPanel canvasGrid = UiFactory.CreateGridContainer(2, 4);
            numCanvasW = UiFactory.AddGridControl(canvasGrid, "画布宽", 16, 0, 0);
            numCanvasH = UiFactory.AddGridControl(canvasGrid, "画布高", 16, 0, 1);
            numActiveWidth = UiFactory.AddGridControl(canvasGrid, "有效宽", 8, 1, 0);
            gbCanvas.Controls.Add(canvasGrid);
            leftGrid.Controls.Add(gbCanvas, 0, 0);

            // --- 2. 矢量生成设置 (整合文件选择) ---
            GroupBox gbVector = UiFactory.CreateModernGroupBox("矢量生成设置", 0);
            gbVector.Dock = DockStyle.Fill;

            // 顶部文件选择
            TableLayoutPanel filePickGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 32, ColumnCount = 2 };
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            filePickGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            txtFontPath = new TextBox { Dock = DockStyle.Fill, BackColor = UiFactory.ControlBg, ForeColor = Color.White, ReadOnly = true };
            btnLoadTTF = new Button { Text = "选择字体", Dock = DockStyle.Fill, BackColor = Color.FromArgb(60, 60, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            filePickGrid.Controls.Add(txtFontPath, 0, 0);
            filePickGrid.Controls.Add(btnLoadTTF, 1, 0);

            // 参数网格
            TableLayoutPanel vectorGrid = UiFactory.CreateGridContainer(3, 4);
            numFontSize = UiFactory.AddGridControl(vectorGrid, "字号", 16, 0, 0);
            numFontOffsetX = UiFactory.AddGridControl(vectorGrid, "移X", 0, 1, 0);
            numFontOffsetY = UiFactory.AddGridControl(vectorGrid, "移Y", 0, 1, 1);
            numFontScaleX = UiFactory.AddGridControl(vectorGrid, "比X%", 100, 2, 0);
            numFontScaleY = UiFactory.AddGridControl(vectorGrid, "比Y%", 100, 2, 1);

            btnApplyVector = UiFactory.CreateStyledButton("矢量像素推到底稿", Color.FromArgb(60, 120, 60), 38);
            btnApplyVector.Dock = DockStyle.Bottom;

            gbVector.Controls.Add(vectorGrid);
            gbVector.Controls.Add(filePickGrid);
            gbVector.Controls.Add(btnApplyVector);
            leftGrid.Controls.Add(gbVector, 0, 1);

            // --- 3. 物理像素位移 ---
            GroupBox gbShift = UiFactory.CreateModernGroupBox("物理像素位移", 0);
            gbShift.Dock = DockStyle.Fill;
            TableLayoutPanel shiftGrid = UiFactory.CreateGridContainer(1, 4);
            numShiftX = UiFactory.AddGridControl(shiftGrid, "平移X", 0, 0, 0);
            numShiftY = UiFactory.AddGridControl(shiftGrid, "平移Y", 0, 0, 1);
            btnApplyShift = UiFactory.CreateStyledButton("应用物理位移", Color.FromArgb(120, 60, 60), 32);
            btnApplyShift.Dock = DockStyle.Bottom;
            gbShift.Controls.Add(shiftGrid);
            gbShift.Controls.Add(btnApplyShift);
            leftGrid.Controls.Add(gbShift, 0, 2);

            // --- 4. 导航与执行区 (2x3 按钮矩阵布局) ---
            Panel runPanel = new Panel { Dock = DockStyle.Fill };

            // 导航行
            Panel navRow = new Panel { Dock = DockStyle.Top, Height = 40 };
            numAsciiIdx = new NumericUpDown { Maximum = 255, Value = 65, Width = 55, Left = 5, Top = 8, BackColor = UiFactory.ControlBg, ForeColor = Color.Lime };
            txtAsciiChar = new TextBox { MaxLength = 1, Width = 35, Left = 65, Top = 8, BackColor = UiFactory.ControlBg, ForeColor = Color.Orange, TextAlign = HorizontalAlignment.Center };
            chkLocked = new CheckBox { Text = "锁定字符", ForeColor = Color.White, Left = 110, Top = 10, AutoSize = true };
            navRow.Controls.AddRange(new Control[] { numAsciiIdx, txtAsciiChar, chkLocked });

            // 按钮网格 (导入三剑客并列)
            TableLayoutPanel btnGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            btnGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            btnBatchRender = UiFactory.CreateStyledButton("批量矢量渲染", Color.FromArgb(70, 70, 70), 32);
            btnImportBin = UiFactory.CreateStyledButton("导入 BIN", Color.FromArgb(50, 50, 50), 32);
            btnImportBmp = UiFactory.CreateStyledButton("导入 BMP", Color.FromArgb(50, 50, 50), 32);
            btnImportFont = UiFactory.CreateStyledButton("导入 FONT", Color.FromArgb(50, 50, 50), 32);
            btnSaveBin = UiFactory.CreateStyledButton("🚀 导出加密 .bin", UiFactory.AccentBlue, 42);

            // 第一行：批量渲染
            btnGrid.Controls.Add(btnBatchRender, 0, 0);
            btnGrid.SetColumnSpan(btnBatchRender, 2);
            // 第二行：导入功能 (左 Bin, 右 Bmp/Font 组合)
            btnGrid.Controls.Add(btnImportBin, 0, 1);

            // 嵌套一个小网格把 Bmp 和 Font 放在右边一列
            TableLayoutPanel subImportGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            subImportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            subImportGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            subImportGrid.Controls.Add(btnImportBmp, 0, 0);
            subImportGrid.Controls.Add(btnImportFont, 1, 0);
            btnGrid.Controls.Add(subImportGrid, 1, 1);

            // 第三行：导出
            btnGrid.Controls.Add(btnSaveBin, 0, 2);
            btnGrid.SetColumnSpan(btnSaveBin, 2);

            runPanel.Controls.Add(btnGrid);
            runPanel.Controls.Add(navRow);
            leftGrid.Controls.Add(runPanel, 0, 3);

            pixelEditor = new PixelEditorControl { Dock = DockStyle.Fill };
            mainTable.Controls.Add(pixelEditor, 1, 0);
        }

        private void BindEvents()
        {
            // --- 字体与矢量预览 ---
            btnLoadTTF.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "字体|*.ttf;*.otf" })
                    if (ofd.ShowDialog() == DialogResult.OK) { txtFontPath.Text = ofd.FileName; _lastTtfPath = ofd.FileName; UpdateVectorPreview(); }
            };

            // 数值改变即触发预览更新
            EventHandler vectorTrigger = (s, e) => UpdateVectorPreview();
            numFontSize.ValueChanged += vectorTrigger;
            numFontOffsetX.ValueChanged += vectorTrigger;
            numFontOffsetY.ValueChanged += vectorTrigger;
            numFontScaleX.ValueChanged += vectorTrigger;
            numFontScaleY.ValueChanged += vectorTrigger;

            btnApplyVector.Click += (s, e) => {
                if (!string.IsNullOrEmpty(_lastTtfPath))
                {
                    _mgr.GenerateFromVector(_currentIdx, _fontRender); //
                    SyncUI(false);
                }
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
                        _mgr.ImportFromBmp(ofd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value); //
                        OnIdxChanged();
                    }
            };

            btnImportFont.Click += (s, e) => {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "FONT|*.font;*.txt" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        // 获取文件中的实际尺寸并同步到 UI
                        if (_mgr.ImportFromFontText(ofd.FileName, out int w, out int h))
                        {
                            numCanvasW.Value = w;
                            numCanvasH.Value = h;

                            // 必须同步更新编辑器的画布尺寸，否则重绘会越界
                            pixelEditor.CanvasW = w;
                            pixelEditor.CanvasH = h;

                            OnIdxChanged();
                            MessageBox.Show($"解析成功！当前字库尺寸已切换为: {w}x{h}");
                        }
                    }
                }
            };

            // --- 批量与导出 ---
            btnBatchRender.Click += (s, e) => {
                if (!string.IsNullOrEmpty(_lastTtfPath))
                {
                    _mgr.BatchRender(_fontRender, false); //
                    SyncUI(false);
                    MessageBox.Show("批量矢量渲染完成！");
                }
            };

            btnSaveBin.Click += (s, e) => {
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "Binary|*.bin" })
                    if (sfd.ShowDialog() == DialogResult.OK) _mgr.SaveToBin(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, _fontRender); //
            };

            // --- 导航与平移 ---
            btnApplyShift.Click += (s, e) => {
                _mgr.ApplyShift(_currentIdx, (int)numShiftX.Value, (int)numShiftY.Value); //
                numShiftX.Value = 0; numShiftY.Value = 0; SyncUI(false);
            };

            numAsciiIdx.ValueChanged += (s, e) => { _currentIdx = (int)numAsciiIdx.Value; txtAsciiChar.Text = ((char)_currentIdx).ToString(); OnIdxChanged(); };
            txtAsciiChar.TextChanged += (s, e) => { if (txtAsciiChar.Text.Length > 0) numAsciiIdx.Value = (int)txtAsciiChar.Text[0]; };

            // --- 画布与编辑响应 ---
            numCanvasW.ValueChanged += (s, e) => { pixelEditor.CanvasW = (int)numCanvasW.Value; SyncUI(false); };
            numCanvasH.ValueChanged += (s, e) => { pixelEditor.CanvasH = (int)numCanvasH.Value; SyncUI(false); };
            numActiveWidth.ValueChanged += (s, e) => { _mgr.AsciiSet[_currentIdx].Width = (int)numActiveWidth.Value; pixelEditor.ActiveWidth = (int)numActiveWidth.Value; pixelEditor.Invalidate(); };
            pixelEditor.DataChanged += () => { _mgr.AsciiSet[_currentIdx].IsManual = true; chkLocked.Checked = true; }; //
        }

        private void OnIdxChanged() { numActiveWidth.Value = _mgr.AsciiSet[_currentIdx].Width; chkLocked.Checked = _mgr.AsciiSet[_currentIdx].IsManual; SyncPreview(); }
        private void UpdateVectorPreview() { if (string.IsNullOrEmpty(_lastTtfPath)) return; _fontRender.LoadFontFile(_lastTtfPath, (float)numFontSize.Value); _fontRender.OffsetX = (int)numFontOffsetX.Value; _fontRender.OffsetY = (int)numFontOffsetY.Value; _fontRender.ScaleX = (int)numFontScaleX.Value; _fontRender.ScaleY = (int)numFontScaleY.Value; _mgr.UpdateVectorPreview(_currentIdx, _fontRender); SyncUI(true); }
        private void SyncPreview() { _mgr.UpdateShiftPreview(_currentIdx, 0, 0); SyncUI(false); }
        private void SyncUI(bool isPreview) { pixelEditor.CanvasW = (int)numCanvasW.Value; pixelEditor.CanvasH = (int)numCanvasH.Value; pixelEditor.ActiveWidth = (int)numActiveWidth.Value; pixelEditor.CurrentBitmap = isPreview ? _mgr.PreviewBitmap : _mgr.AsciiSet[_currentIdx].Glyph; }
    }
}