#nullable disable
using FC.UI.Forms;
using FC.UI.Controls;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FC.UI
{
    public partial class Form1 : Form
    {
        // 核心容器：用于动态挂载不同的功能模块
        private Panel _moduleContainer;

        public Form1()
        {
            // --- DPI 适配逻辑开始 ---
            // 1. 定义 100% 缩放下的理想基准尺寸
            Size baseSize = new Size(1400, 960);

            // 2. 获取当前屏幕的缩放比例 (基于 96 DPI)
            using (Graphics g = this.CreateGraphics())
            {
                float scaleX = g.DpiX / 150f;
                float scaleY = g.DpiY / 150f;

                // 设置适配后的初始大小和最小大小
                this.Size = new Size((int)(baseSize.Width * scaleX), (int)(baseSize.Height * scaleY));
                this.MinimumSize = this.Size; // 锁定最小值，防止 UI 在小窗口下挤成一团
            }
            // --- DPI 适配逻辑结束 ---

            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.Text = "点阵字库工具 V2"; // 建议顺手改个标题
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. 建立一个全局表格布局
            TableLayoutPanel rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.FromArgb(30, 30, 30) // 建议背景色保持一致
            };

            // 清除默认样式并重新添加，确保比例准确
            rootLayout.RowStyles.Clear();
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // 第一行：菜单栏
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 第二行：内容区
            this.Controls.Add(rootLayout);

            // 2. 初始化菜单并放入第一行
            InitMenuBar();
            if (MainMenuStrip != null)
            {
                rootLayout.Controls.Add(MainMenuStrip, 0, 0);
            }

            // 3. 初始化模块容器并放入第二行
            _moduleContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            rootLayout.Controls.Add(_moduleContainer, 0, 1);

            SwitchModule(new AsciiGeneratorControl());
        }

        private void InitMenuBar()
        {
            MenuStrip menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Padding = new Padding(0, 2, 0, 2)
            };

            // --- 模式切换菜单 ---
            ToolStripMenuItem mnuMode = new ToolStripMenuItem("工作模式(&M)");

            var itemGbk = new ToolStripMenuItem("GBK/GB2312 生成器", null, (s, e) => SwitchModule(new GbkGeneratorControl()));
            var itemAscii = new ToolStripMenuItem("ASCII 标准字库", null, (s, e) => SwitchModule(new AsciiGeneratorControl()));
            var itemInspect = new ToolStripMenuItem("字库校验", null, (s, e) => SwitchModule(new FontInspectorControl()));

            mnuMode.DropDownItems.AddRange(new ToolStripItem[] { itemGbk, itemAscii, new ToolStripSeparator(), itemInspect });

            // --- 帮助菜单 ---
            ToolStripMenuItem mnuHelp = new ToolStripMenuItem("帮助(&H)");
            mnuHelp.DropDownItems.Add("关于", null, (s, e) => MessageBox.Show("FontFactory Pro v1.3\n专为嵌入式开发设计的字库工具\nBy:68a660f3", "关于"));
            mnuHelp.DropDownItems.Add("代码参考", null, (s, e) =>
            {
                using (var frm = new FrmHelp(Icon)) // 传入主窗体的紫色图标
                {
                    frm.ShowDialog(this);
                }
            });

            menuStrip.Items.Add(mnuMode);
            menuStrip.Items.Add(mnuHelp);

            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);
        }

        /// <summary>
        /// 切换当前显示的功能模块
        /// </summary>
        private void SwitchModule(UserControl module)
        {
            // 释放当前容器内的控件资源
            if (_moduleContainer.Controls.Count > 0)
            {
                var oldModule = _moduleContainer.Controls[0];
                _moduleContainer.Controls.Clear();
                oldModule.Dispose();
            }

            // 加载新模块
            module.Dock = DockStyle.Fill;
            _moduleContainer.Controls.Add(module);

            // 同步窗体标题（可选，增加仪式感）
            string modeName = module switch
            {
                GbkGeneratorControl => "GBK/GB2312 生成模式",
                AsciiGeneratorControl => "ASCII 标准字库模式",
                FontInspectorControl => "字库校验模式",
                _ => "未知模式"
            };
            Text = $"FontFactory Pro - {modeName}";
        }
    }
}