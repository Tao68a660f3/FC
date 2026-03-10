#nullable disable

using FC;

public partial class Form1 : Form
{
    private Panel _mainContainer;

    public Form1()
    {
        this.Text = "FontFactory Pro";
        this.Size = new Size(1100, 750);

        // 初始化主容器
        _mainContainer = new Panel { Dock = DockStyle.Fill };
        this.Controls.Add(_mainContainer);

        // 初始化菜单并加载默认模块
        SwitchToModule(new GbkGeneratorControl());
    }

    private void SwitchToModule(UserControl module)
    {
        _mainContainer.Controls.Clear();
        module.Dock = DockStyle.Fill;
        _mainContainer.Controls.Add(module);
    }
}