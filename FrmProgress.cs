public partial class FrmProgress : Form
{
    public ProgressBar ProgressBar => prg;
    public Label LabelStatus => lblStatus;

    public FrmProgress()
    {
        // 设置窗体样式：无边框或固定边框，确保用户无法缩放它
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.Text = "正在生成字库...";
        this.Size = new Size(360, 150);
        this.ControlBox = false; // 禁用关闭按钮，防止用户中途关掉

        // 简单的进度条和标签布局
        lblStatus = new Label { Location = new Point(20, 20), AutoSize = true, Text = "准备中..." };
        prg = new ProgressBar { Location = new Point(20, 50), Size = new Size(300, 25) };

        this.Controls.Add(lblStatus);
        this.Controls.Add(prg);
    }

    private Label lblStatus;
    private ProgressBar prg;
}