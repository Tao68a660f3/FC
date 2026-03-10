using System;
using System.Drawing;
using System.Windows.Forms;

namespace FC.ui
{
    public static class UiFactory
    {
        // 配色定义，确保两个界面视觉统一
        public static readonly Color BgColor = Color.FromArgb(30, 30, 30);
        public static readonly Color ControlBg = Color.FromArgb(45, 45, 45);
        public static readonly Color TextGray = Color.FromArgb(180, 180, 180);
        public static readonly Color AccentBlue = Color.FromArgb(0, 122, 204);

        /// <summary>
        /// 创建统一风格的 GroupBox
        /// </summary>
        public static GroupBox CreateModernGroupBox(string title, int height)
        {
            return new GroupBox
            {
                Text = title,
                Height = height,
                ForeColor = Color.LightSkyBlue,
                Margin = new Padding(0, 0, 0, 10),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Top // 默认在 FlowLayoutPanel 中顺序排列
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
        /// 完整抽离 AddGridControl：处理 Label + NumericUpDown
        /// </summary>
        public static NumericUpDown AddGridControl(TableLayoutPanel grid, string labelText, int def, int row, int colGroup)
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

            NumericUpDown num = new NumericUpDown
            {
                Value = def,
                Minimum = -512, // 扩大范围兼容偏移和缩放
                Maximum = 1024,
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
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = TextGray,
                AutoSize = false
            };

            ComboBox cmb = new ComboBox
            {
                Dock = DockStyle.Fill,
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