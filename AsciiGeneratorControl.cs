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
        private int _currentIdx = 65; // 默认选中 'A'

        // --- 自动生成的 UI 成员 (由构造函数初始化) ---
        private NumericUpDown numCanvasW, numCanvasH, numActiveWidth;
        private NumericUpDown numFontSize, numFontScaleX, numFontScaleY, numFontOffsetX, numFontOffsetY;
        private NumericUpDown numShiftX, numShiftY;
        private CheckBox chkLocked, chkForceOverwrite;
        private Button btnLoadTTF, btnApplyVector, btnApplyShift, btnBatchRender, btnSaveBin;
        private TrackBar trackBarIndex;
        private Label lblCharInfo;
        private PixelEditorControl pixelEditor;

        public AsciiGeneratorControl()
        {
            this.DoubleBuffered = true;
            this.BackColor = UiFactory.BgColor;
            BuildUI();
            BindEvents();

            // 初始化状态
            SyncPreview();
        }

        private void BuildUI()
        {
            // 1. 左侧控制面板
            FlowLayoutPanel leftPanel = new FlowLayoutPanel
            {
                Width = 330,
                Dock = DockStyle.Left,
                Padding = new Padding(10),
                AutoScroll = true,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            // 1.1 画布协议区
            var gpCanvas = UiFactory.CreateModernGroupBox("画布与协议 (Canvas Settings)", 110);
            var gridCanvas = UiFactory.CreateGridContainer(2, 2);
            numCanvasW = UiFactory.AddGridControl(gridCanvas, "画布宽", 16, 0, 0);
            numCanvasH = UiFactory.AddGridControl(gridCanvas, "画布高", 16, 0, 1);
            numActiveWidth = UiFactory.AddGridControl(gridCanvas, "有效宽", 8, 1, 0);
            gpCanvas.Controls.Add(gridCanvas);

            // 1.2 矢量辅助区 (Vector Generator)
            var gpVector = UiFactory.CreateModernGroupBox("矢量底稿 (Vector Generator)", 250);
            var gridVector = UiFactory.CreateGridContainer(4, 2);
            btnLoadTTF = UiFactory.CreateStyledButton("加载 TTF 字体", UiFactory.AccentBlue, 30);
            numFontSize = UiFactory.AddGridControl(gridVector, "字号", 12, 0, 0);
            numFontScaleX = UiFactory.AddGridControl(gridVector, "缩放X%", 100, 1, 0);
            numFontScaleY = UiFactory.AddGridControl(gridVector, "缩放Y%", 100, 1, 1);
            numFontOffsetX = UiFactory.AddGridControl(gridVector, "位置X", 0, 2, 0);
            numFontOffsetY = UiFactory.AddGridControl(gridVector, "位置Y", 0, 2, 1);
            btnApplyVector = UiFactory.CreateStyledButton("生成像素到底稿", Color.FromArgb(60, 120, 60), 30);
            gridVector.Dock = DockStyle.Top;
            gpVector.Controls.Add(gridVector);
            gpVector.Controls.Add(btnApplyVector);
            gpVector.Controls.Add(btnLoadTTF);

            // 1.3 位图大师区 (Bitmap Master)
            var gpBitmap = UiFactory.CreateModernGroupBox("位图大师 (Bitmap Master)", 125);
            var gridBitmap = UiFactory.CreateGridContainer(1, 2);
            numShiftX = UiFactory.AddGridControl(gridBitmap, "平移X", 0, 0, 0);
            numShiftY = UiFactory.AddGridControl(gridBitmap, "平移Y", 0, 0, 1);
            btnApplyShift = UiFactory.CreateStyledButton("应用物理位移 (Apply Shift)", Color.FromArgb(120, 60, 60), 35);
            gridBitmap.Dock = DockStyle.Top;
            gpBitmap.Controls.Add(gridBitmap);
            gpBitmap.Controls.Add(btnApplyShift);

            // 1.4 批量与状态
            var gpBatch = UiFactory.CreateModernGroupBox("批量与状态", 180);
            lblCharInfo = new Label { Text = "ASCII: 65 (A)", ForeColor = Color.White, Dock = DockStyle.Top, Height = 25, TextAlign = ContentAlignment.MiddleCenter };
            trackBarIndex = new TrackBar { Maximum = 255, Dock = DockStyle.Top, TickStyle = TickStyle.None, Height = 30 };
            chkLocked = new CheckBox { Text = "锁定当前字符 (IsManual)", ForeColor = Color.White, Dock = DockStyle.Top, Height = 25 };
            chkForceOverwrite = new CheckBox { Text = "强制覆盖锁定 (Batch)", ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 25 };
            btnBatchRender = UiFactory.CreateStyledButton("批量矢量渲染 (未锁定)", UiFactory.ControlBg, 30);
            btnSaveBin = UiFactory.CreateStyledButton("导出 BIN (加密宽度表)", Color.DarkOrange, 35);

            gpBatch.Controls.Add(btnSaveBin);
            gpBatch.Controls.Add(btnBatchRender);
            gpBatch.Controls.Add(chkForceOverwrite);
            gpBatch.Controls.Add(chkLocked);
            gpBatch.Controls.Add(trackBarIndex);
            gpBatch.Controls.Add(lblCharInfo);

            leftPanel.Controls.Add(gpCanvas);
            leftPanel.Controls.Add(gpVector);
            leftPanel.Controls.Add(gpBitmap);
            leftPanel.Controls.Add(gpBatch);

            // 2. 右侧编辑器
            pixelEditor = new PixelEditorControl { Dock = DockStyle.Fill };

            this.Controls.Add(pixelEditor);
            this.Controls.Add(leftPanel);
        }

        private void BindEvents()
        {
            // A. 矢量参数同步
            numFontSize.ValueChanged += (s, e) => UpdateVectorPreview();
            numFontScaleX.ValueChanged += (s, e) => UpdateVectorPreview();
            numFontScaleY.ValueChanged += (s, e) => UpdateVectorPreview();
            numFontOffsetX.ValueChanged += (s, e) => UpdateVectorPreview();
            numFontOffsetY.ValueChanged += (s, e) => UpdateVectorPreview();

            // B. 位图平移预览 (不影响本体)
            numShiftX.ValueChanged += (s, e) => UpdateShiftPreview();
            numShiftY.ValueChanged += (s, e) => UpdateShiftPreview();

            // C. 字符索引切换
            trackBarIndex.ValueChanged += (s, e) => {
                _currentIdx = trackBarIndex.Value;
                lblCharInfo.Text = $"ASCII: {_currentIdx} ({(char)_currentIdx})";
                numActiveWidth.Value = _mgr.AsciiSet[_currentIdx].Width;
                chkLocked.Checked = _mgr.AsciiSet[_currentIdx].IsManual;
                SyncPreview();
            };

            // D. 应用操作
            btnApplyVector.Click += (s, e) => {
                _mgr.GenerateFromVector(_currentIdx, _fontRender);
                SyncUI();
            };

            btnApplyShift.Click += (s, e) => {
                _mgr.ApplyShift(_currentIdx, (int)numShiftX.Value, (int)numShiftY.Value);
                numShiftX.Value = 0; numShiftY.Value = 0; // 物理操作后归零
                SyncUI();
            };

            btnBatchRender.Click += (s, e) => {
                _mgr.BatchRender(_fontRender, chkForceOverwrite.Checked);
                SyncUI();
            };

            // E. 画布逻辑
            numActiveWidth.ValueChanged += (s, e) => {
                _mgr.AsciiSet[_currentIdx].Width = (int)numActiveWidth.Value;
                pixelEditor.ActiveWidth = (int)numActiveWidth.Value;
                pixelEditor.Invalidate();
            };

            pixelEditor.DataChanged += () => {
                _mgr.AsciiSet[_currentIdx].IsManual = true;
                chkLocked.Checked = true;
            };

            btnSaveBin.Click += (s, e) => {
                SaveFileDialog sfd = new SaveFileDialog { Filter = "Binary Font|*.bin" };
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _mgr.SaveToBin(sfd.FileName, (int)numCanvasW.Value, (int)numCanvasH.Value, _fontRender);
                    MessageBox.Show("导出完成，已执行 XOR 加密。");
                }
            };
        }

        private void UpdateVectorPreview()
        {
            // 同步所有矢量参数到 FontRender
            _fontRender.FontSize = (float)numFontSize.Value;
            _fontRender.ScaleX = (int)numFontScaleX.Value; // 百分比转系数
            _fontRender.ScaleY = (int)numFontScaleY.Value;
            _fontRender.OffsetX = (int)numFontOffsetX.Value;
            _fontRender.OffsetY = (int)numFontOffsetY.Value;

            _mgr.UpdateVectorPreview(_currentIdx, _fontRender);
            SyncUI(true);
        }

        private void UpdateShiftPreview()
        {
            _mgr.UpdateShiftPreview(_currentIdx, (int)numShiftX.Value, (int)numShiftY.Value);
            SyncUI(true);
        }

        private void SyncPreview()
        {
            // 默认展示本体像素，不带偏移
            _mgr.UpdateShiftPreview(_currentIdx, 0, 0);
            SyncUI(false);
        }

        private void SyncUI(bool isPreview = true)
        {
            pixelEditor.CanvasW = (int)numCanvasW.Value;
            pixelEditor.CanvasH = (int)numCanvasH.Value;
            pixelEditor.ActiveWidth = (int)numActiveWidth.Value;

            // 如果是预览态，显示临时位图；否则显示本体
            pixelEditor.CurrentBitmap = isPreview ? _mgr.PreviewBitmap : _mgr.AsciiSet[_currentIdx].Glyph;
        }
    }
}