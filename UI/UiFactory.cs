#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FC.UI
{
    public static class UiFactory
    {
        // 配色定义，确保两个界面视觉统一
        public static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        public static readonly Color ControlBg = Color.FromArgb(45, 45, 45);
        public static readonly Color TextGray = Color.FromArgb(180, 180, 180);
        public static readonly Color AccentBlue = Color.FromArgb(0, 122, 204);

        public class PreciseNumericUpDown : NumericUpDown
        {
            public PreciseNumericUpDown()
            {
                this.TextAlign = HorizontalAlignment.Center;
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                // --- 核心修复：只有获得焦点时才响应滚轮 ---
                // 这样当你导入 BIN 时，焦点在按钮上，鼠标悬停在框框上误滚也不会触发变更
                if (!this.Focused)
                    return;

                // 1. 强制将事件标记为已处理
                HandledMouseEventArgs hme = e as HandledMouseEventArgs;
                if (hme != null)
                    hme.Handled = true;

                // 2. 根据 Delta 方向手动计算数值 (这里你写得很棒，解决了默认滚太快的问题)
                decimal newValue = this.Value + (e.Delta > 0 ? this.Increment : -this.Increment);

                // 3. 边界检查
                if (newValue >= this.Minimum && newValue <= this.Maximum)
                {
                    this.Value = newValue;
                }
            }
        }

        /// <summary>
        /// 创建统一风格的 GroupBox
        /// </summary>
        public static GroupBox CreateModernGroupBox(string title, int height)
        {
            return new GroupBox
            {
                Text = title,
                Height = height,
                //Width = 300, // 给一个默认宽度，适合左侧 330 宽的面板
                ForeColor = Color.LightSkyBlue,
                Margin = new Padding(0, 0, 0, 10),
                FlatStyle = FlatStyle.Flat,
                // Dock = DockStyle.Top // 删掉这一行！
                Anchor = AnchorStyles.Left | AnchorStyles.Right // 改用 Anchor 适配宽度
            };
        }

        /// <summary>
        /// 彻底抽离 Grid 容器初始化
        /// </summary>
        public static TableLayoutPanel CreateGridContainer(int rows, int cols)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = cols,
                RowCount = rows,
                Padding = new Padding(3)
            };
            // 均匀分配比例
            for (int i = 0; i < cols; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

            return grid;
        }

        /// <summary>
        /// 完整抽离 AddGridControl：处理 Label + PreciseNumericUpDown
        /// </summary>
        public static PreciseNumericUpDown AddGridControl(TableLayoutPanel grid, string labelText, int def, int row, int colGroup)
        {
            // 计算列位置：colGroup 0 对应 0,1 列；colGroup 1 对应 2,3 列
            int labelCol = colGroup * 2;
            int controlCol = colGroup * 2 + 1;

            Label lbl = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = TextGray,
                AutoSize = false
            };

            PreciseNumericUpDown num = new PreciseNumericUpDown
            {
                Minimum = -512, // 扩大范围兼容偏移和缩放
                Maximum = 1024,
                Value = def,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = ControlBg,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            grid.Controls.Add(lbl, labelCol, row);
            grid.Controls.Add(num, controlCol, row);
            return num;
        }

        /// <summary>
        /// 完整抽离 AddGridCombo：处理 Label + ComboBox
        /// </summary>
        public static ComboBox AddGridCombo(TableLayoutPanel grid, string labelText, Type enumType, int row)
        {
            Label lbl = new Label
            {
                Text = labelText,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.TopRight,
                ForeColor = TextGray,
                AutoSize = false
            };

            ComboBox cmb = new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ControlBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            if (enumType != null && enumType.IsEnum)
            {
                cmb.DataSource = Enum.GetValues(enumType);
            }

            grid.Controls.Add(lbl, 0, row);
            grid.Controls.Add(cmb, 1, row);
            return cmb;
        }

        /// <summary>
        /// 创建统一样式的现代按钮
        /// </summary>
        public static Button CreateStyledButton(string text, Color backColor, int height = 35)
        {
            return new Button
            {
                Text = text,
                Height = height,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 0),
                Cursor = Cursors.Hand
            };
        }
    }
}