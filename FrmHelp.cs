#nullable disable
using FC.Resources; // 引用上面的命名空间
using System;
using System.Drawing;
using System.Net.Http;
using System.Windows.Forms;

namespace YourProjectName.UI
{
    public class FrmHelp : Form
    {
        private TextBox txtCode;
        private Label lblTitle;
        private Label lblFooter;

        public FrmHelp(Icon ownerIcon)
        {
            InitializeComponent(ownerIcon);
            // 在 FrmHelp 的构造函数中，或者 InitializeComponent 之后加这一句
            this.Load += (s, e) =>
            {
                txtCode.SelectionStart = 0;    // 光标移到开头
                txtCode.SelectionLength = 0;   // 选中长度设为0
                this.ActiveControl = null;     // 甚至可以直接取消掉窗体的当前活动焦点
            };
        }

        private void InitializeComponent(Icon ownerIcon)
        {
            // 窗体基础设置
            this.Text = "字库寻址算法参考";
            this.Size = new Size(620, 520);
            this.MinimumSize = new Size(400, 300); // 建议设置最小尺寸，防止缩太小
            this.FormBorderStyle = FormBorderStyle.Sizable; // 开启自由拉伸
            this.MaximizeBox = true;
            this.BackColor = Color.FromArgb(32, 32, 35);
            this.Icon = ownerIcon;

            // 顶部提示
            this.lblTitle = new Label
            {
                Text = "Binary 数据索引算法参考 (C#)",
                Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
                ForeColor = Color.MediumPurple,
                Location = new Point(15, 15),
                Size = new Size(400, 30)
            };

            // 代码展示 TextBox
            this.txtCode = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 22),
                ForeColor = Color.Thistle, // 浅紫色
                Font = new Font("Consolas", 10.5f), // 核心：等宽字体
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Both,
                Location = new Point(15, 55),
                Size = new Size(575, 380),
                Text = HelpContent.GbkAlgorithm + "\r\n\r\n" + HelpContent.Gb2312Algorithm,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 底部提示
            this.lblFooter = new Label
            {
                Text = "Note: DataOffset = index * (Width * Height / 8)",
                Font = new Font("Consolas", 9f, FontStyle.Italic),
                ForeColor = Color.DimGray,
                Location = new Point(15, 445),
                Size = new Size(500, 25)
            };

            this.Controls.Add(lblTitle);
            this.Controls.Add(txtCode);
            this.Controls.Add(lblFooter);
        }
    }
}