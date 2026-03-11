#nullable disable
using FC.ui;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FC
{
    public partial class Form1 : Form
    {
        // 核心容器：用于动态挂载不同的功能模块
        private Panel _moduleContainer;

        public Form1()
        {
            this.Size = new Size(1200, 800);

            // 1. 建立一个全局表格布局
            TableLayoutPanel rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 第一行随菜单高度
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // 第二行占满剩余空间
            this.Controls.Add(rootLayout);

            // 2. 初始化菜单并放入第一行
            InitMenuBar();
            rootLayout.Controls.Add(this.MainMenuStrip, 0, 0);

            // 3. 初始化模块容器并放入第二行
            _moduleContainer = new Panel { Dock = DockStyle.Fill };
            rootLayout.Controls.Add(_moduleContainer, 0, 1);

            SwitchModule(new GbkGeneratorControl());
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
            var itemAscii = new ToolStripMenuItem("ASCII 标准字库", null, (s, e) => {
                //MessageBox.Show("ASCII 模式正在开发中，即将上线！", "预告");
                 //待会儿我们要写的：
                 SwitchModule(new AsciiGeneratorControl());
            });
            var itemInspect = new ToolStripMenuItem("字库校验与参考代码", null, (s, e) => SwitchModule(new FontInspectorControl()));

            mnuMode.DropDownItems.AddRange(new ToolStripItem[] { itemGbk, itemAscii, new ToolStripSeparator(), itemInspect });

            // --- 帮助菜单 ---
            ToolStripMenuItem mnuHelp = new ToolStripMenuItem("帮助(&H)");
            mnuHelp.DropDownItems.Add("关于", null, (s, e) => MessageBox.Show("FontFactory Pro v1.0\n专为嵌入式开发设计的字库工具", "关于"));

            menuStrip.Items.Add(mnuMode);
            menuStrip.Items.Add(mnuHelp);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
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
            string modeName = module is GbkGeneratorControl ? "中文生成模式" : "字库检查模式";
            this.Text = $"FontFactory Pro - {modeName}";
        }
    }
}