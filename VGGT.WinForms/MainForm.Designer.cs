#nullable enable
using VGGT.WinForms.Controls;

namespace VGGT.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private SplitContainer rootSplit = null!;
    private SplitContainer rightSplit = null!;
    private TableLayoutPanel leftLayout = null!;
    private TableLayoutPanel pathsLayout = null!;
    private TableLayoutPanel optionsLayout = null!;
    private Panel imagePanel = null!;
    private Panel statusPanel = null!;
    private Panel viewHost = null!;
    private FlowLayoutPanel imageButtons = null!;
    private FlowLayoutPanel checksPanel = null!;
    private FlowLayoutPanel actionPanel = null!;
    private FlowLayoutPanel viewTools = null!;
    private Label titleLabel = null!;
    private Label lblPython = null!;
    private Label lblRepo = null!;
    private Label lblOutput = null!;
    private Label lblQuery = null!;
    private Label lblModel = null!;
    private Label lblConfidence = null!;
    private Label lblMaxPoints = null!;
    private Label lblMaxFaces = null!;
    private Label lblPointSize = null!;
    private TabControl resultTabs = null!;
    private TabPage depthTab = null!;
    private TabPage cameraTab = null!;
    private TabPage logTab = null!;

    private ListBox lstImages = null!;
    private Button btnAddImages = null!;
    private Button btnAddFolder = null!;
    private Button btnRemove = null!;
    private Button btnClear = null!;
    private TextBox txtPython = null!;
    private TextBox txtRepo = null!;
    private TextBox txtOutput = null!;
    private TextBox txtModel = null!;
    private TextBox txtQuery = null!;
    private Button btnPython = null!;
    private Button btnRepo = null!;
    private Button btnOutput = null!;
    private Button btnQuery = null!;
    private Button btnRun = null!;
    private Button btnCancel = null!;
    private Button btnOpenPly = null!;
    private Button btnOpenGlb = null!;
    private Button btnResetView = null!;
    private Button btnFlipVertical = null!;
    private NumericUpDown nudConfidence = null!;
    private NumericUpDown nudMaxPoints = null!;
    private NumericUpDown nudMaxFaces = null!;
    private NumericUpDown nudPointSize = null!;
    private CheckBox chkDense = null!;
    private CheckBox chkDepthCamera = null!;
    private CheckBox chkGenerateMesh = null!;
    private ProgressBar progress = null!;
    private Label lblStatus = null!;
    private Label lblInputHint = null!;
    private PointCloudViewport viewport = null!;
    private PictureBox picDepth = null!;
    private ListBox lstDepth = null!;
    private DataGridView gridCameras = null!;
    private TextBox txtLog = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        rootSplit = new SplitContainer();
        leftLayout = new TableLayoutPanel();
        titleLabel = new Label();
        imagePanel = new Panel();
        lstImages = new ListBox();
        imageButtons = new FlowLayoutPanel();
        btnAddImages = new Button();
        btnAddFolder = new Button();
        btnRemove = new Button();
        btnClear = new Button();
        lblInputHint = new Label();
        pathsLayout = new TableLayoutPanel();
        lblPython = new Label();
        txtPython = new TextBox();
        btnPython = new Button();
        lblRepo = new Label();
        txtRepo = new TextBox();
        btnRepo = new Button();
        lblOutput = new Label();
        txtOutput = new TextBox();
        btnOutput = new Button();
        lblQuery = new Label();
        txtQuery = new TextBox();
        btnQuery = new Button();
        optionsLayout = new TableLayoutPanel();
        lblModel = new Label();
        txtModel = new TextBox();
        lblConfidence = new Label();
        nudConfidence = new NumericUpDown();
        lblMaxPoints = new Label();
        nudMaxPoints = new NumericUpDown();
        lblMaxFaces = new Label();
        nudMaxFaces = new NumericUpDown();
        lblPointSize = new Label();
        nudPointSize = new NumericUpDown();
        checksPanel = new FlowLayoutPanel();
        chkDense = new CheckBox();
        chkDepthCamera = new CheckBox();
        chkGenerateMesh = new CheckBox();
        actionPanel = new FlowLayoutPanel();
        btnRun = new Button();
        btnCancel = new Button();
        btnOpenPly = new Button();
        btnOpenGlb = new Button();
        statusPanel = new Panel();
        lblStatus = new Label();
        progress = new ProgressBar();
        rightSplit = new SplitContainer();
        viewHost = new Panel();
        viewport = new PointCloudViewport();
        viewTools = new FlowLayoutPanel();
        btnResetView = new Button();
        btnFlipVertical = new Button();
        resultTabs = new TabControl();
        depthTab = new TabPage();
        picDepth = new PictureBox();
        lstDepth = new ListBox();
        cameraTab = new TabPage();
        gridCameras = new DataGridView();
        dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
        logTab = new TabPage();
        txtLog = new TextBox();
        ((System.ComponentModel.ISupportInitialize)rootSplit).BeginInit();
        rootSplit.Panel1.SuspendLayout();
        rootSplit.Panel2.SuspendLayout();
        rootSplit.SuspendLayout();
        leftLayout.SuspendLayout();
        imagePanel.SuspendLayout();
        imageButtons.SuspendLayout();
        pathsLayout.SuspendLayout();
        optionsLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)nudConfidence).BeginInit();
        ((System.ComponentModel.ISupportInitialize)nudMaxPoints).BeginInit();
        ((System.ComponentModel.ISupportInitialize)nudMaxFaces).BeginInit();
        ((System.ComponentModel.ISupportInitialize)nudPointSize).BeginInit();
        checksPanel.SuspendLayout();
        actionPanel.SuspendLayout();
        statusPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)rightSplit).BeginInit();
        rightSplit.Panel1.SuspendLayout();
        rightSplit.Panel2.SuspendLayout();
        rightSplit.SuspendLayout();
        viewHost.SuspendLayout();
        viewTools.SuspendLayout();
        resultTabs.SuspendLayout();
        depthTab.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picDepth).BeginInit();
        cameraTab.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridCameras).BeginInit();
        logTab.SuspendLayout();
        SuspendLayout();
        // 
        // rootSplit
        // 
        rootSplit.BackColor = Color.FromArgb(210, 220, 230);
        rootSplit.Dock = DockStyle.Fill;
        rootSplit.FixedPanel = FixedPanel.Panel1;
        rootSplit.Location = new Point(0, 0);
        rootSplit.Margin = new Padding(4);
        rootSplit.Name = "rootSplit";
        // 
        // rootSplit.Panel1
        // 
        rootSplit.Panel1.Controls.Add(leftLayout);
        rootSplit.Panel1.Padding = new Padding(18);
        // 
        // rootSplit.Panel2
        // 
        rootSplit.Panel2.Controls.Add(rightSplit);
        rootSplit.Panel2.Padding = new Padding(12);
        rootSplit.Size = new Size(2190, 1350);
        rootSplit.SplitterDistance = 630;
        rootSplit.SplitterWidth = 6;
        rootSplit.TabIndex = 0;
        // 
        // leftLayout
        // 
        leftLayout.BackColor = Color.White;
        leftLayout.ColumnCount = 1;
        leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        leftLayout.Controls.Add(titleLabel, 0, 0);
        leftLayout.Controls.Add(imagePanel, 0, 1);
        leftLayout.Controls.Add(lblInputHint, 0, 2);
        leftLayout.Controls.Add(pathsLayout, 0, 3);
        leftLayout.Controls.Add(optionsLayout, 0, 4);
        leftLayout.Controls.Add(checksPanel, 0, 5);
        leftLayout.Controls.Add(actionPanel, 0, 6);
        leftLayout.Controls.Add(statusPanel, 0, 7);
        leftLayout.Dock = DockStyle.Fill;
        leftLayout.Location = new Point(18, 18);
        leftLayout.Margin = new Padding(4);
        leftLayout.Name = "leftLayout";
        leftLayout.Padding = new Padding(15);
        leftLayout.RowCount = 8;
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 81F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 192F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 231F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 93F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 114F));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
        leftLayout.Size = new Size(594, 1314);
        leftLayout.TabIndex = 0;
        // 
        // titleLabel
        // 
        titleLabel.AutoSize = true;
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = new Font("Microsoft JhengHei UI", 17F, FontStyle.Bold);
        titleLabel.ForeColor = Color.FromArgb(16, 42, 67);
        titleLabel.Location = new Point(19, 15);
        titleLabel.Margin = new Padding(4, 0, 4, 0);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(556, 72);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "VGGT 推論控制台";
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // imagePanel
        // 
        imagePanel.Controls.Add(lstImages);
        imagePanel.Controls.Add(imageButtons);
        imagePanel.Dock = DockStyle.Fill;
        imagePanel.Location = new Point(19, 91);
        imagePanel.Margin = new Padding(4);
        imagePanel.Name = "imagePanel";
        imagePanel.Size = new Size(556, 397);
        imagePanel.TabIndex = 1;
        // 
        // lstImages
        // 
        lstImages.Dock = DockStyle.Fill;
        lstImages.HorizontalScrollbar = true;
        lstImages.IntegralHeight = false;
        lstImages.Location = new Point(0, 54);
        lstImages.Margin = new Padding(4);
        lstImages.Name = "lstImages";
        lstImages.SelectionMode = SelectionMode.MultiExtended;
        lstImages.Size = new Size(556, 343);
        lstImages.TabIndex = 1;
        // 
        // imageButtons
        // 
        imageButtons.Controls.Add(btnAddImages);
        imageButtons.Controls.Add(btnAddFolder);
        imageButtons.Controls.Add(btnRemove);
        imageButtons.Controls.Add(btnClear);
        imageButtons.Dock = DockStyle.Top;
        imageButtons.Location = new Point(0, 0);
        imageButtons.Margin = new Padding(4);
        imageButtons.Name = "imageButtons";
        imageButtons.Size = new Size(556, 54);
        imageButtons.TabIndex = 0;
        imageButtons.WrapContents = false;
        // 
        // btnAddImages
        // 
        btnAddImages.Location = new Point(4, 4);
        btnAddImages.Margin = new Padding(4);
        btnAddImages.Name = "btnAddImages";
        btnAddImages.Size = new Size(123, 44);
        btnAddImages.TabIndex = 0;
        btnAddImages.Text = "加入影像";
        btnAddImages.UseVisualStyleBackColor = true;
        btnAddImages.Click += btnAddImages_Click;
        // 
        // btnAddFolder
        // 
        btnAddFolder.Location = new Point(135, 4);
        btnAddFolder.Margin = new Padding(4);
        btnAddFolder.Name = "btnAddFolder";
        btnAddFolder.Size = new Size(144, 44);
        btnAddFolder.TabIndex = 1;
        btnAddFolder.Text = "加入資料夾";
        btnAddFolder.UseVisualStyleBackColor = true;
        btnAddFolder.Click += btnAddFolder_Click;
        // 
        // btnRemove
        // 
        btnRemove.Location = new Point(287, 4);
        btnRemove.Margin = new Padding(4);
        btnRemove.Name = "btnRemove";
        btnRemove.Size = new Size(99, 44);
        btnRemove.TabIndex = 2;
        btnRemove.Text = "移除";
        btnRemove.UseVisualStyleBackColor = true;
        btnRemove.Click += btnRemove_Click;
        // 
        // btnClear
        // 
        btnClear.Location = new Point(394, 4);
        btnClear.Margin = new Padding(4);
        btnClear.Name = "btnClear";
        btnClear.Size = new Size(99, 44);
        btnClear.TabIndex = 3;
        btnClear.Text = "清除";
        btnClear.UseVisualStyleBackColor = true;
        btnClear.Click += btnClear_Click;
        // 
        // lblInputHint
        // 
        lblInputHint.AutoEllipsis = true;
        lblInputHint.Dock = DockStyle.Fill;
        lblInputHint.ForeColor = Color.FromArgb(98, 125, 152);
        lblInputHint.Location = new Point(19, 492);
        lblInputHint.Margin = new Padding(4, 0, 4, 0);
        lblInputHint.Name = "lblInputHint";
        lblInputHint.Padding = new Padding(0, 9, 0, 0);
        lblInputHint.Size = new Size(556, 81);
        lblInputHint.TabIndex = 2;
        lblInputHint.Text = "輸入：同一靜態場景的 JPG/PNG。第一張是參考座標；建議視角間有重疊。";
        // 
        // pathsLayout
        // 
        pathsLayout.ColumnCount = 3;
        pathsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
        pathsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pathsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        pathsLayout.Controls.Add(lblPython, 0, 0);
        pathsLayout.Controls.Add(txtPython, 1, 0);
        pathsLayout.Controls.Add(btnPython, 2, 0);
        pathsLayout.Controls.Add(lblRepo, 0, 1);
        pathsLayout.Controls.Add(txtRepo, 1, 1);
        pathsLayout.Controls.Add(btnRepo, 2, 1);
        pathsLayout.Controls.Add(lblOutput, 0, 2);
        pathsLayout.Controls.Add(txtOutput, 1, 2);
        pathsLayout.Controls.Add(btnOutput, 2, 2);
        pathsLayout.Controls.Add(lblQuery, 0, 3);
        pathsLayout.Controls.Add(txtQuery, 1, 3);
        pathsLayout.Controls.Add(btnQuery, 2, 3);
        pathsLayout.Dock = DockStyle.Fill;
        pathsLayout.Location = new Point(19, 577);
        pathsLayout.Margin = new Padding(4);
        pathsLayout.Name = "pathsLayout";
        pathsLayout.RowCount = 4;
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        pathsLayout.Size = new Size(556, 184);
        pathsLayout.TabIndex = 3;
        // 
        // lblPython
        // 
        lblPython.Anchor = AnchorStyles.Left;
        lblPython.AutoSize = true;
        lblPython.Location = new Point(4, 11);
        lblPython.Margin = new Padding(4, 0, 4, 0);
        lblPython.Name = "lblPython";
        lblPython.Size = new Size(70, 23);
        lblPython.TabIndex = 0;
        lblPython.Text = "Python";
        // 
        // txtPython
        // 
        txtPython.Dock = DockStyle.Fill;
        txtPython.Location = new Point(112, 4);
        txtPython.Margin = new Padding(4);
        txtPython.Name = "txtPython";
        txtPython.Size = new Size(380, 30);
        txtPython.TabIndex = 1;
        txtPython.Text = "python";
        // 
        // btnPython
        // 
        btnPython.Dock = DockStyle.Fill;
        btnPython.Location = new Point(500, 4);
        btnPython.Margin = new Padding(4);
        btnPython.Name = "btnPython";
        btnPython.Size = new Size(52, 38);
        btnPython.TabIndex = 2;
        btnPython.Text = "…";
        btnPython.UseVisualStyleBackColor = true;
        btnPython.Click += btnPython_Click;
        // 
        // lblRepo
        // 
        lblRepo.Anchor = AnchorStyles.Left;
        lblRepo.AutoSize = true;
        lblRepo.Location = new Point(4, 46);
        lblRepo.Margin = new Padding(4, 0, 4, 0);
        lblRepo.Name = "lblRepo";
        lblRepo.Size = new Size(63, 46);
        lblRepo.TabIndex = 3;
        lblRepo.Text = "VGGT Repo";
        // 
        // txtRepo
        // 
        txtRepo.Dock = DockStyle.Fill;
        txtRepo.Location = new Point(112, 50);
        txtRepo.Margin = new Padding(4);
        txtRepo.Name = "txtRepo";
        txtRepo.Size = new Size(380, 30);
        txtRepo.TabIndex = 4;
        // 
        // btnRepo
        // 
        btnRepo.Dock = DockStyle.Fill;
        btnRepo.Location = new Point(500, 50);
        btnRepo.Margin = new Padding(4);
        btnRepo.Name = "btnRepo";
        btnRepo.Size = new Size(52, 38);
        btnRepo.TabIndex = 5;
        btnRepo.Text = "…";
        btnRepo.UseVisualStyleBackColor = true;
        btnRepo.Click += btnRepo_Click;
        // 
        // lblOutput
        // 
        lblOutput.Anchor = AnchorStyles.Left;
        lblOutput.AutoSize = true;
        lblOutput.Location = new Point(4, 103);
        lblOutput.Margin = new Padding(4, 0, 4, 0);
        lblOutput.Name = "lblOutput";
        lblOutput.Size = new Size(46, 23);
        lblOutput.TabIndex = 6;
        lblOutput.Text = "輸出";
        // 
        // txtOutput
        // 
        txtOutput.Dock = DockStyle.Fill;
        txtOutput.Location = new Point(112, 96);
        txtOutput.Margin = new Padding(4);
        txtOutput.Name = "txtOutput";
        txtOutput.Size = new Size(380, 30);
        txtOutput.TabIndex = 7;
        // 
        // btnOutput
        // 
        btnOutput.Dock = DockStyle.Fill;
        btnOutput.Location = new Point(500, 96);
        btnOutput.Margin = new Padding(4);
        btnOutput.Name = "btnOutput";
        btnOutput.Size = new Size(52, 38);
        btnOutput.TabIndex = 8;
        btnOutput.Text = "…";
        btnOutput.UseVisualStyleBackColor = true;
        btnOutput.Click += btnOutput_Click;
        // 
        // lblQuery
        // 
        lblQuery.Anchor = AnchorStyles.Left;
        lblQuery.AutoSize = true;
        lblQuery.Location = new Point(4, 149);
        lblQuery.Margin = new Padding(4, 0, 4, 0);
        lblQuery.Name = "lblQuery";
        lblQuery.Size = new Size(64, 23);
        lblQuery.TabIndex = 9;
        lblQuery.Text = "查詢點";
        // 
        // txtQuery
        // 
        txtQuery.Dock = DockStyle.Fill;
        txtQuery.Location = new Point(112, 142);
        txtQuery.Margin = new Padding(4);
        txtQuery.Name = "txtQuery";
        txtQuery.Size = new Size(380, 30);
        txtQuery.TabIndex = 10;
        // 
        // btnQuery
        // 
        btnQuery.Dock = DockStyle.Fill;
        btnQuery.Location = new Point(500, 142);
        btnQuery.Margin = new Padding(4);
        btnQuery.Name = "btnQuery";
        btnQuery.Size = new Size(52, 38);
        btnQuery.TabIndex = 11;
        btnQuery.Text = "…";
        btnQuery.UseVisualStyleBackColor = true;
        btnQuery.Click += btnQuery_Click;
        // 
        // optionsLayout
        // 
        optionsLayout.ColumnCount = 2;
        optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 189F));
        optionsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        optionsLayout.Controls.Add(lblModel, 0, 0);
        optionsLayout.Controls.Add(txtModel, 1, 0);
        optionsLayout.Controls.Add(lblConfidence, 0, 1);
        optionsLayout.Controls.Add(nudConfidence, 1, 1);
        optionsLayout.Controls.Add(lblMaxPoints, 0, 2);
        optionsLayout.Controls.Add(nudMaxPoints, 1, 2);
        optionsLayout.Controls.Add(lblMaxFaces, 0, 3);
        optionsLayout.Controls.Add(nudMaxFaces, 1, 3);
        optionsLayout.Controls.Add(lblPointSize, 0, 4);
        optionsLayout.Controls.Add(nudPointSize, 1, 4);
        optionsLayout.Dock = DockStyle.Fill;
        optionsLayout.Location = new Point(19, 769);
        optionsLayout.Margin = new Padding(4);
        optionsLayout.Name = "optionsLayout";
        optionsLayout.RowCount = 5;
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayout.Size = new Size(556, 223);
        optionsLayout.TabIndex = 4;
        // 
        // lblModel
        // 
        lblModel.Anchor = AnchorStyles.Left;
        lblModel.AutoSize = true;
        lblModel.Location = new Point(4, 10);
        lblModel.Margin = new Padding(4, 0, 4, 0);
        lblModel.Name = "lblModel";
        lblModel.Size = new Size(46, 23);
        lblModel.TabIndex = 0;
        lblModel.Text = "模型";
        // 
        // txtModel
        // 
        txtModel.Dock = DockStyle.Fill;
        txtModel.Location = new Point(193, 4);
        txtModel.Margin = new Padding(4);
        txtModel.Name = "txtModel";
        txtModel.Size = new Size(359, 30);
        txtModel.TabIndex = 1;
        txtModel.Text = "facebook/VGGT-1B";
        // 
        // lblConfidence
        // 
        lblConfidence.Anchor = AnchorStyles.Left;
        lblConfidence.AutoSize = true;
        lblConfidence.Location = new Point(4, 54);
        lblConfidence.Margin = new Padding(4, 0, 4, 0);
        lblConfidence.Name = "lblConfidence";
        lblConfidence.Size = new Size(133, 23);
        lblConfidence.TabIndex = 2;
        lblConfidence.Text = "濾除低信心 (%)";
        // 
        // nudConfidence
        // 
        nudConfidence.Dock = DockStyle.Fill;
        nudConfidence.Location = new Point(193, 48);
        nudConfidence.Margin = new Padding(4);
        nudConfidence.Maximum = new decimal(new int[] { 99, 0, 0, 0 });
        nudConfidence.Name = "nudConfidence";
        nudConfidence.Size = new Size(359, 30);
        nudConfidence.TabIndex = 3;
        nudConfidence.Value = new decimal(new int[] { 20, 0, 0, 0 });
        // 
        // lblMaxPoints
        // 
        lblMaxPoints.Anchor = AnchorStyles.Left;
        lblMaxPoints.AutoSize = true;
        lblMaxPoints.Location = new Point(4, 98);
        lblMaxPoints.Margin = new Padding(4, 0, 4, 0);
        lblMaxPoints.Name = "lblMaxPoints";
        lblMaxPoints.Size = new Size(118, 23);
        lblMaxPoints.TabIndex = 4;
        lblMaxPoints.Text = "預覽點數上限";
        // 
        // nudMaxPoints
        // 
        nudMaxPoints.Dock = DockStyle.Fill;
        nudMaxPoints.Increment = new decimal(new int[] { 10000, 0, 0, 0 });
        nudMaxPoints.Location = new Point(193, 92);
        nudMaxPoints.Margin = new Padding(4);
        nudMaxPoints.Maximum = new decimal(new int[] { 1000000, 0, 0, 0 });
        nudMaxPoints.Minimum = new decimal(new int[] { 1000, 0, 0, 0 });
        nudMaxPoints.Name = "nudMaxPoints";
        nudMaxPoints.Size = new Size(359, 30);
        nudMaxPoints.TabIndex = 5;
        nudMaxPoints.ThousandsSeparator = true;
        nudMaxPoints.Value = new decimal(new int[] { 150000, 0, 0, 0 });
        // 
        // lblMaxFaces
        // 
        lblMaxFaces.Anchor = AnchorStyles.Left;
        lblMaxFaces.AutoSize = true;
        lblMaxFaces.Location = new Point(4, 142);
        lblMaxFaces.Margin = new Padding(4, 0, 4, 0);
        lblMaxFaces.Name = "lblMaxFaces";
        lblMaxFaces.Size = new Size(133, 23);
        lblMaxFaces.TabIndex = 6;
        lblMaxFaces.Text = "Mesh 面數上限";
        // 
        // nudMaxFaces
        // 
        nudMaxFaces.Dock = DockStyle.Fill;
        nudMaxFaces.Increment = new decimal(new int[] { 25000, 0, 0, 0 });
        nudMaxFaces.Location = new Point(193, 136);
        nudMaxFaces.Margin = new Padding(4);
        nudMaxFaces.Maximum = new decimal(new int[] { 2000000, 0, 0, 0 });
        nudMaxFaces.Minimum = new decimal(new int[] { 1000, 0, 0, 0 });
        nudMaxFaces.Name = "nudMaxFaces";
        nudMaxFaces.Size = new Size(359, 30);
        nudMaxFaces.TabIndex = 7;
        nudMaxFaces.ThousandsSeparator = true;
        nudMaxFaces.Value = new decimal(new int[] { 400000, 0, 0, 0 });
        // 
        // lblPointSize
        // 
        lblPointSize.Anchor = AnchorStyles.Left;
        lblPointSize.AutoSize = true;
        lblPointSize.Location = new Point(4, 188);
        lblPointSize.Margin = new Padding(4, 0, 4, 0);
        lblPointSize.Name = "lblPointSize";
        lblPointSize.Size = new Size(64, 23);
        lblPointSize.TabIndex = 8;
        lblPointSize.Text = "點大小";
        // 
        // nudPointSize
        // 
        nudPointSize.Dock = DockStyle.Fill;
        nudPointSize.Location = new Point(193, 180);
        nudPointSize.Margin = new Padding(4);
        nudPointSize.Maximum = new decimal(new int[] { 6, 0, 0, 0 });
        nudPointSize.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        nudPointSize.Name = "nudPointSize";
        nudPointSize.Size = new Size(359, 30);
        nudPointSize.TabIndex = 9;
        nudPointSize.Value = new decimal(new int[] { 2, 0, 0, 0 });
        nudPointSize.ValueChanged += nudPointSize_ValueChanged;
        // 
        // checksPanel
        // 
        checksPanel.Controls.Add(chkDense);
        checksPanel.Controls.Add(chkDepthCamera);
        checksPanel.Controls.Add(chkGenerateMesh);
        checksPanel.Dock = DockStyle.Fill;
        checksPanel.Location = new Point(19, 1000);
        checksPanel.Margin = new Padding(4);
        checksPanel.Name = "checksPanel";
        checksPanel.Padding = new Padding(0, 10, 0, 0);
        checksPanel.Size = new Size(556, 85);
        checksPanel.TabIndex = 5;
        // 
        // chkDense
        // 
        chkDense.AutoSize = true;
        chkDense.Checked = true;
        chkDense.CheckState = CheckState.Checked;
        chkDense.Location = new Point(4, 14);
        chkDense.Margin = new Padding(4);
        chkDense.Name = "chkDense";
        chkDense.Size = new Size(169, 27);
        chkDense.TabIndex = 0;
        chkDense.Text = "儲存 dense NPZ";
        chkDense.UseVisualStyleBackColor = true;
        // 
        // chkDepthCamera
        // 
        chkDepthCamera.AutoSize = true;
        chkDepthCamera.Checked = true;
        chkDepthCamera.CheckState = CheckState.Checked;
        chkDepthCamera.Location = new Point(181, 14);
        chkDepthCamera.Margin = new Padding(4);
        chkDepthCamera.Name = "chkDepthCamera";
        chkDepthCamera.Size = new Size(273, 27);
        chkDepthCamera.TabIndex = 1;
        chkDepthCamera.Text = "以 Depth＋Camera 產生點雲";
        chkDepthCamera.UseVisualStyleBackColor = true;
        // 
        // chkGenerateMesh
        // 
        chkGenerateMesh.AutoSize = true;
        chkGenerateMesh.Checked = true;
        chkGenerateMesh.CheckState = CheckState.Checked;
        chkGenerateMesh.Location = new Point(4, 49);
        chkGenerateMesh.Margin = new Padding(4);
        chkGenerateMesh.Name = "chkGenerateMesh";
        chkGenerateMesh.Size = new Size(161, 27);
        chkGenerateMesh.TabIndex = 2;
        chkGenerateMesh.Text = "產生 GLB Mesh";
        chkGenerateMesh.UseVisualStyleBackColor = true;
        // 
        // actionPanel
        // 
        actionPanel.Controls.Add(btnRun);
        actionPanel.Controls.Add(btnCancel);
        actionPanel.Controls.Add(btnOpenPly);
        actionPanel.Controls.Add(btnOpenGlb);
        actionPanel.Dock = DockStyle.Fill;
        actionPanel.Location = new Point(19, 1093);
        actionPanel.Margin = new Padding(4);
        actionPanel.Name = "actionPanel";
        actionPanel.Padding = new Padding(0, 12, 0, 0);
        actionPanel.Size = new Size(556, 106);
        actionPanel.TabIndex = 6;
        // 
        // btnRun
        // 
        btnRun.BackColor = Color.FromArgb(37, 99, 166);
        btnRun.FlatStyle = FlatStyle.Flat;
        btnRun.ForeColor = Color.White;
        btnRun.Location = new Point(4, 16);
        btnRun.Margin = new Padding(4);
        btnRun.Name = "btnRun";
        btnRun.Size = new Size(162, 45);
        btnRun.TabIndex = 0;
        btnRun.Text = "開始推論";
        btnRun.UseVisualStyleBackColor = false;
        btnRun.Click += btnRun_Click;
        // 
        // btnCancel
        // 
        btnCancel.Enabled = false;
        btnCancel.Location = new Point(174, 16);
        btnCancel.Margin = new Padding(4);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(96, 45);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "取消";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += btnCancel_Click;
        // 
        // btnOpenPly
        // 
        btnOpenPly.Location = new Point(278, 16);
        btnOpenPly.Margin = new Padding(4);
        btnOpenPly.Name = "btnOpenPly";
        btnOpenPly.Size = new Size(120, 45);
        btnOpenPly.TabIndex = 2;
        btnOpenPly.Text = "開啟 PLY";
        btnOpenPly.UseVisualStyleBackColor = true;
        btnOpenPly.Click += btnOpenPly_Click;
        // 
        // btnOpenGlb
        // 
        btnOpenGlb.Enabled = false;
        btnOpenGlb.Location = new Point(406, 16);
        btnOpenGlb.Margin = new Padding(4);
        btnOpenGlb.Name = "btnOpenGlb";
        btnOpenGlb.Size = new Size(120, 45);
        btnOpenGlb.TabIndex = 3;
        btnOpenGlb.Text = "開啟 GLB";
        btnOpenGlb.UseVisualStyleBackColor = true;
        btnOpenGlb.Click += btnOpenGlb_Click;
        // 
        // statusPanel
        // 
        statusPanel.Controls.Add(lblStatus);
        statusPanel.Controls.Add(progress);
        statusPanel.Dock = DockStyle.Fill;
        statusPanel.Location = new Point(19, 1207);
        statusPanel.Margin = new Padding(4);
        statusPanel.Name = "statusPanel";
        statusPanel.Size = new Size(556, 88);
        statusPanel.TabIndex = 7;
        // 
        // lblStatus
        // 
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.ForeColor = Color.FromArgb(72, 101, 129);
        lblStatus.Location = new Point(0, 27);
        lblStatus.Margin = new Padding(4, 0, 4, 0);
        lblStatus.Name = "lblStatus";
        lblStatus.Padding = new Padding(0, 9, 0, 0);
        lblStatus.Size = new Size(556, 61);
        lblStatus.TabIndex = 0;
        lblStatus.Text = "就緒";
        // 
        // progress
        // 
        progress.Dock = DockStyle.Top;
        progress.Location = new Point(0, 0);
        progress.Margin = new Padding(4);
        progress.Name = "progress";
        progress.Size = new Size(556, 27);
        progress.TabIndex = 1;
        // 
        // rightSplit
        // 
        rightSplit.BackColor = Color.FromArgb(35, 54, 72);
        rightSplit.Dock = DockStyle.Fill;
        rightSplit.FixedPanel = FixedPanel.Panel2;
        rightSplit.Location = new Point(12, 12);
        rightSplit.Margin = new Padding(4);
        rightSplit.Name = "rightSplit";
        rightSplit.Orientation = Orientation.Horizontal;
        // 
        // rightSplit.Panel1
        // 
        rightSplit.Panel1.Controls.Add(viewHost);
        // 
        // rightSplit.Panel2
        // 
        rightSplit.Panel2.Controls.Add(resultTabs);
        rightSplit.Size = new Size(1530, 1326);
        rightSplit.SplitterDistance = 876;
        rightSplit.SplitterWidth = 6;
        rightSplit.TabIndex = 0;
        // 
        // viewHost
        // 
        viewHost.BackColor = Color.FromArgb(13, 23, 34);
        viewHost.Controls.Add(viewport);
        viewHost.Controls.Add(viewTools);
        viewHost.Dock = DockStyle.Fill;
        viewHost.Location = new Point(0, 0);
        viewHost.Margin = new Padding(4);
        viewHost.Name = "viewHost";
        viewHost.Size = new Size(1530, 876);
        viewHost.TabIndex = 0;
        // 
        // viewport
        // 
        viewport.BackColor = Color.FromArgb(13, 23, 34);
        viewport.Dock = DockStyle.Fill;
        viewport.ForeColor = Color.Gainsboro;
        viewport.Location = new Point(0, 57);
        viewport.Margin = new Padding(4);
        viewport.Name = "viewport";
        viewport.Size = new Size(1530, 819);
        viewport.TabIndex = 1;
        // 
        // viewTools
        // 
        viewTools.BackColor = Color.FromArgb(23, 42, 58);
        viewTools.Controls.Add(btnResetView);
        viewTools.Controls.Add(btnFlipVertical);
        viewTools.Dock = DockStyle.Top;
        viewTools.Location = new Point(0, 0);
        viewTools.Margin = new Padding(4);
        viewTools.Name = "viewTools";
        viewTools.Padding = new Padding(9, 6, 0, 0);
        viewTools.Size = new Size(1530, 57);
        viewTools.TabIndex = 0;
        // 
        // btnResetView
        // 
        btnResetView.BackColor = Color.FromArgb(48, 74, 94);
        btnResetView.ForeColor = Color.White;
        btnResetView.Location = new Point(13, 10);
        btnResetView.Margin = new Padding(4);
        btnResetView.Name = "btnResetView";
        btnResetView.Size = new Size(135, 44);
        btnResetView.TabIndex = 0;
        btnResetView.Text = "重設視角";
        btnResetView.UseVisualStyleBackColor = false;
        btnResetView.Click += btnResetView_Click;
        // 
        // btnFlipVertical
        // 
        btnFlipVertical.BackColor = Color.FromArgb(48, 74, 94);
        btnFlipVertical.ForeColor = Color.White;
        btnFlipVertical.Location = new Point(156, 10);
        btnFlipVertical.Margin = new Padding(4);
        btnFlipVertical.Name = "btnFlipVertical";
        btnFlipVertical.Size = new Size(135, 44);
        btnFlipVertical.TabIndex = 1;
        btnFlipVertical.Text = "上下翻轉";
        btnFlipVertical.UseVisualStyleBackColor = false;
        btnFlipVertical.Click += btnFlipVertical_Click;
        // 
        // resultTabs
        // 
        resultTabs.Controls.Add(depthTab);
        resultTabs.Controls.Add(cameraTab);
        resultTabs.Controls.Add(logTab);
        resultTabs.Dock = DockStyle.Fill;
        resultTabs.Location = new Point(0, 0);
        resultTabs.Margin = new Padding(4);
        resultTabs.Name = "resultTabs";
        resultTabs.SelectedIndex = 0;
        resultTabs.Size = new Size(1530, 444);
        resultTabs.TabIndex = 0;
        // 
        // depthTab
        // 
        depthTab.Controls.Add(picDepth);
        depthTab.Controls.Add(lstDepth);
        depthTab.Location = new Point(4, 32);
        depthTab.Margin = new Padding(4);
        depthTab.Name = "depthTab";
        depthTab.Padding = new Padding(4);
        depthTab.Size = new Size(1522, 408);
        depthTab.TabIndex = 0;
        depthTab.Text = "深度預覽";
        depthTab.UseVisualStyleBackColor = true;
        // 
        // picDepth
        // 
        picDepth.BackColor = Color.Black;
        picDepth.Dock = DockStyle.Fill;
        picDepth.Location = new Point(332, 4);
        picDepth.Margin = new Padding(4);
        picDepth.Name = "picDepth";
        picDepth.Size = new Size(1186, 400);
        picDepth.SizeMode = PictureBoxSizeMode.Zoom;
        picDepth.TabIndex = 0;
        picDepth.TabStop = false;
        // 
        // lstDepth
        // 
        lstDepth.Dock = DockStyle.Left;
        lstDepth.IntegralHeight = false;
        lstDepth.Location = new Point(4, 4);
        lstDepth.Margin = new Padding(4);
        lstDepth.Name = "lstDepth";
        lstDepth.Size = new Size(328, 400);
        lstDepth.TabIndex = 1;
        lstDepth.SelectedIndexChanged += lstDepth_SelectedIndexChanged;
        // 
        // cameraTab
        // 
        cameraTab.Controls.Add(gridCameras);
        cameraTab.Location = new Point(4, 32);
        cameraTab.Margin = new Padding(4);
        cameraTab.Name = "cameraTab";
        cameraTab.Padding = new Padding(4);
        cameraTab.Size = new Size(1522, 406);
        cameraTab.TabIndex = 1;
        cameraTab.Text = "相機參數";
        cameraTab.UseVisualStyleBackColor = true;
        // 
        // gridCameras
        // 
        gridCameras.AllowUserToAddRows = false;
        gridCameras.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridCameras.BackgroundColor = Color.White;
        gridCameras.ColumnHeadersHeight = 34;
        gridCameras.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4 });
        gridCameras.Dock = DockStyle.Fill;
        gridCameras.Location = new Point(4, 4);
        gridCameras.Margin = new Padding(4);
        gridCameras.Name = "gridCameras";
        gridCameras.ReadOnly = true;
        gridCameras.RowHeadersWidth = 62;
        gridCameras.Size = new Size(1514, 398);
        gridCameras.TabIndex = 0;
        // 
        // dataGridViewTextBoxColumn1
        // 
        dataGridViewTextBoxColumn1.HeaderText = "#";
        dataGridViewTextBoxColumn1.MinimumWidth = 8;
        dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
        dataGridViewTextBoxColumn1.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn2
        // 
        dataGridViewTextBoxColumn2.HeaderText = "影像";
        dataGridViewTextBoxColumn2.MinimumWidth = 8;
        dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
        dataGridViewTextBoxColumn2.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn3
        // 
        dataGridViewTextBoxColumn3.HeaderText = "fx / fy";
        dataGridViewTextBoxColumn3.MinimumWidth = 8;
        dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
        dataGridViewTextBoxColumn3.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn4
        // 
        dataGridViewTextBoxColumn4.HeaderText = "t (camera-from-world)";
        dataGridViewTextBoxColumn4.MinimumWidth = 8;
        dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
        dataGridViewTextBoxColumn4.ReadOnly = true;
        // 
        // logTab
        // 
        logTab.Controls.Add(txtLog);
        logTab.Location = new Point(4, 32);
        logTab.Margin = new Padding(4);
        logTab.Name = "logTab";
        logTab.Padding = new Padding(4);
        logTab.Size = new Size(1522, 406);
        logTab.TabIndex = 2;
        logTab.Text = "執行日誌";
        logTab.UseVisualStyleBackColor = true;
        // 
        // txtLog
        // 
        txtLog.BackColor = Color.FromArgb(18, 28, 38);
        txtLog.Dock = DockStyle.Fill;
        txtLog.Font = new Font("Consolas", 9F);
        txtLog.ForeColor = Color.Gainsboro;
        txtLog.Location = new Point(4, 4);
        txtLog.Margin = new Padding(4);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Both;
        txtLog.Size = new Size(1514, 398);
        txtLog.TabIndex = 0;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(144F, 144F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(242, 246, 250);
        ClientSize = new Size(2190, 1350);
        Controls.Add(rootSplit);
        Font = new Font("Microsoft JhengHei UI", 9F);
        Margin = new Padding(4);
        MinimumSize = new Size(1759, 1112);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "VGGT Studio - Visual Geometry Grounded Transformer";
        rootSplit.Panel1.ResumeLayout(false);
        rootSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)rootSplit).EndInit();
        rootSplit.ResumeLayout(false);
        leftLayout.ResumeLayout(false);
        leftLayout.PerformLayout();
        imagePanel.ResumeLayout(false);
        imageButtons.ResumeLayout(false);
        pathsLayout.ResumeLayout(false);
        pathsLayout.PerformLayout();
        optionsLayout.ResumeLayout(false);
        optionsLayout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)nudConfidence).EndInit();
        ((System.ComponentModel.ISupportInitialize)nudMaxPoints).EndInit();
        ((System.ComponentModel.ISupportInitialize)nudMaxFaces).EndInit();
        ((System.ComponentModel.ISupportInitialize)nudPointSize).EndInit();
        checksPanel.ResumeLayout(false);
        checksPanel.PerformLayout();
        actionPanel.ResumeLayout(false);
        statusPanel.ResumeLayout(false);
        rightSplit.Panel1.ResumeLayout(false);
        rightSplit.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)rightSplit).EndInit();
        rightSplit.ResumeLayout(false);
        viewHost.ResumeLayout(false);
        viewTools.ResumeLayout(false);
        resultTabs.ResumeLayout(false);
        depthTab.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)picDepth).EndInit();
        cameraTab.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)gridCameras).EndInit();
        logTab.ResumeLayout(false);
        logTab.PerformLayout();
        ResumeLayout(false);
    }

    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1 = null!;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2 = null!;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3 = null!;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4 = null!;
}
