Program · CS
// ============================================================
//  Classic Windows Task Manager Recreation
//  Single-file .NET 6.0 Windows Forms
//  Pixel-accurate to Windows 7 Task Manager (XP/7 Classic)
// ============================================================
 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
 
static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ClassicTaskManager());
    }
}
 
// --------------------------------------------------------------
//  Enums
// --------------------------------------------------------------
enum UpdateSpeed { High, Normal, Low, Paused }
 
// --------------------------------------------------------------
//  Graph History Control  – black bg, green line, grid
// --------------------------------------------------------------
sealed class GraphHistory : Control
{
    private readonly Queue<float> _main   = new();
    private readonly Queue<float> _kernel = new();
    public bool  ShowKernel { get; set; }
    public float Max        { get; set; } = 100f;
 
    public GraphHistory()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Black;
        TabStop   = false;
    }
 
    public void Push(float v, float k = 0f)
    {
        _main  .Enqueue(Math.Clamp(v, 0f, Max));
        _kernel.Enqueue(Math.Clamp(k, 0f, Max));
        int cap = Math.Max(Width, 4);
        while (_main  .Count > cap) _main  .Dequeue();
        while (_kernel.Count > cap) _kernel.Dequeue();
        Invalidate();
    }
 
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        DrawGrid(g);
        DrawLine(g, _main,   Color.Lime);
        if (ShowKernel) DrawLine(g, _kernel, Color.Red);
    }
 
    private void DrawGrid(Graphics g)
    {
        using var p = new Pen(Color.FromArgb(0, 68, 0));
        for (int x = 0; x < Width;  x += 12) g.DrawLine(p, x, 0, x, Height);
        for (int y = 0; y < Height; y += 12) g.DrawLine(p, 0, y, Width, y);
    }
 
    private void DrawLine(Graphics g, Queue<float> q, Color c)
    {
        var a = q.ToArray();
        if (a.Length < 2) return;
        using var pen = new Pen(c);
        int off = Width - a.Length;
        for (int i = 1; i < a.Length; i++)
        {
            int x1 = off + i - 1, x2 = off + i;
            int y1 = Height - 1 - (int)((a[i - 1] / Max) * (Height - 1));
            int y2 = Height - 1 - (int)((a[i    ] / Max) * (Height - 1));
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }
 
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        int cap = Math.Max(Width, 4);
        while (_main  .Count > cap) _main  .Dequeue();
        while (_kernel.Count > cap) _kernel.Dequeue();
    }
}
 
// --------------------------------------------------------------
//  Vertical Bar Gauge  – black bg, striped green fill
// --------------------------------------------------------------
sealed class BarGauge : Control
{
    public float  Value   { get; set; }
    public float  Max     { get; set; } = 100f;
    public string Footer  { get; set; } = "";
 
    public BarGauge()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Black;
        TabStop   = false;
    }
 
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
 
        // Grid
        using (var gp = new Pen(Color.FromArgb(0, 68, 0)))
        {
            for (int x = 0; x < Width;  x += 12) g.DrawLine(gp, x, 0, x, Height);
            for (int y = 0; y < Height; y += 12) g.DrawLine(gp, 0, y, Width, y);
        }
 
        // Filled stripes from bottom
        if (Max > 0f)
        {
            int fillH = (int)Math.Clamp((Value / Max) * Height, 0, Height);
            using var b = new SolidBrush(Color.Lime);
            const int sh = 2, gap = 1;
            for (int y = Height - sh; y > Height - fillH - sh; y -= (sh + gap))
            {
                int top = Math.Max(y, Height - fillH);
                int h2  = Math.Min(sh, (Height - fillH - top) + sh);
                if (h2 > 0) g.FillRectangle(b, 0, top, Width, h2);
            }
        }
 
        // Footer text
        if (!string.IsNullOrEmpty(Footer))
        {
            using var ft = new Font("Tahoma", 6.5f);
            using var tb = new SolidBrush(Color.Lime);
            var sz = g.MeasureString(Footer, ft);
            g.DrawString(Footer, ft, tb, (Width - sz.Width) / 2f, Height - sz.Height - 1);
        }
    }
}
 
// --------------------------------------------------------------
//  Run Dialog  (File ? New Task)
// --------------------------------------------------------------
sealed class RunDialog : Form
{
    public RunDialog()
    {
        Text            = "Create New Task";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = MinimizeBox = false;
        ShowInTaskbar   = false;
        ClientSize      = new Size(386, 134);
        Font            = new Font("Tahoma", 8f);
        StartPosition   = FormStartPosition.CenterParent;
 
        Controls.Add(new Label
        {
            Text = "Type the name of a program, folder, document, or\nInternet resource, and Windows will open it for you.",
            Left = 44, Top = 12, Width = 330, Height = 34, AutoSize = false
        });
        Controls.Add(new PictureBox
        {
            Left = 8, Top = 10, Width = 32, Height = 32,
            BackColor = Color.Transparent,
        });
 
        Controls.Add(new Label { Text = "Open:", Left = 10, Top = 58, AutoSize = true });
        var tb = new TextBox { Left = 56, Top = 55, Width = 244 };
        Controls.Add(tb);
 
        var bOk = new Button { Text = "OK",       Left = 220, Top = 96, Width = 72, Height = 23 };
        var bCc = new Button { Text = "Cancel",   Left = 300, Top = 96, Width = 72, Height = 23 };
        var bBr = new Button { Text = "Browse...", Left = 8,  Top = 96, Width = 80, Height = 23 };
 
        bOk.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(tb.Text))
                try { Process.Start(new ProcessStartInfo(tb.Text) { UseShellExecute = true }); }
                catch { MessageBox.Show("Windows cannot find '" + tb.Text + "'.", "Create New Task", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            Close();
        };
        bCc.Click += (_, _) => Close();
        bBr.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Title = "Browse", Filter = "Programs (*.exe)|*.exe|All Files (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK) tb.Text = ofd.FileName;
        };
        AcceptButton = bOk; CancelButton = bCc;
        Controls.Add(bOk); Controls.Add(bCc); Controls.Add(bBr);
    }
}
 
// --------------------------------------------------------------
//  Main Form – Classic Task Manager
// --------------------------------------------------------------
sealed class ClassicTaskManager : Form
{
    // -- menu items we track ----------------------------------
    private ToolStripMenuItem _miAlwaysOnTop  = null!;
    private ToolStripMenuItem _miMinimizeOnUse = null!;
    private ToolStripMenuItem _miHideWhenMin  = null!;
    private ToolStripMenuItem _miSpeedHigh    = null!;
    private ToolStripMenuItem _miSpeedNormal  = null!;
    private ToolStripMenuItem _miSpeedLow     = null!;
    private ToolStripMenuItem _miSpeedPaused  = null!;
    private ToolStripMenuItem _miShowKernel   = null!;
 
    // -- tabs -------------------------------------------------
    private TabControl _tabs = null!;
 
    // -- Applications -----------------------------------------
    private ListView _lvApps = null!;
 
    // -- Processes --------------------------------------------
    private ListView _lvProcs = null!;
 
    // -- Services ---------------------------------------------
    private ListView _lvSvc = null!;
 
    // -- Performance ------------------------------------------
    private BarGauge     _gCpu  = null!;
    private BarGauge     _gMem  = null!;
    private GraphHistory[] _ghCores = null!;
    private GraphHistory _ghMem  = null!;
    private Label _lPmTotal = null!, _lPmCached = null!, _lPmAvail = null!, _lPmFree = null!;
    private Label _lKmPaged = null!, _lKmNP    = null!;
    private Label _lSysH    = null!, _lSysT    = null!, _lSysP    = null!;
    private Label _lUpTime  = null!, _lCommit  = null!;
 
    // -- Networking -------------------------------------------
    private GraphHistory[] _ghNet = null!;
    private ListView _lvNet = null!;
 
    // -- Users ------------------------------------------------
    private ListView _lvUsers = null!;
 
    // -- Status bar labels ------------------------------------
    private Label _sbProcs = null!, _sbCpu = null!, _sbMem = null!;
    private Panel _statusPanel = null!;
 
    // -- Simulation -------------------------------------------
    private UpdateSpeed _speed = UpdateSpeed.Normal;
    private bool _showKernel;
    private readonly Random _rng = new(42);
    private float _cpuPct  = 2f;
    private float _memMB   = 17_405f;   // ~71 % of 24505
    private readonly float[] _corePct = new float[6] { 1f, 2f, 3f, 1f, 2f, 1f };
    private readonly float[] _netPct  = new float[3];
    private int _procs = 233, _threads = 3842, _handles = 125084;
    private readonly DateTime _bootTime =
        DateTime.Now.AddHours(-2).AddMinutes(-23).AddSeconds(-8);
 
    private const float TotalMemMB  = 24505f;
    private const float CachedMemMB = 7718f;
 
    private System.Windows.Forms.Timer _timer = null!;
 
    // --------------------------------------------------------
    //  Constructor
    // --------------------------------------------------------
    public ClassicTaskManager()
    {
        SuspendLayout();
 
        Text        = "Windows Task Manager";
        // ClientSize: width=596, height=593
        // The "main white window" starts at x=6,y=28 from the client 0,0 (below the 20-px menu)
        // right margin = 8px, status bar = 23px ? outer panel padding drives this
        ClientSize  = new Size(596, 593);
        MinimumSize = new Size(320, 400);
        Font        = new Font("Tahoma", 8f);
        BackColor   = Color.FromArgb(0xF0, 0xF0, 0xF0);
        Icon        = BuildIcon();
 
        var menu   = BuildMenu();
        _statusPanel = BuildStatusBar();
        var tabArea  = BuildTabArea();
 
        // Dock order: menu top, status bottom, tab area fills remainder
        Controls.Add(tabArea);
        Controls.Add(menu);
        Controls.Add(_statusPanel);
 
        ResumeLayout(false);
        PerformLayout();
 
        // Populate static data
        PopulateApplications();
        PopulateProcesses();
        PopulateServices();
        PopulateUsers();
        PopulateNetwork();
 
        // Pre-fill graphs with historical data
        for (int i = 0; i < 300; i++) Simulate(false);
 
        // Start update timer
        _timer = new System.Windows.Forms.Timer();
        SetSpeed(UpdateSpeed.Normal);
        _timer.Tick += (_, _) => Simulate(true);
        _timer.Start();
 
        // Trigger initial layout of status labels
        LayoutStatusLabels();
    }
 
    // --------------------------------------------------------
    //  Icon  (simple pixel art approximation)
    // --------------------------------------------------------
    private static Icon BuildIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.FillRectangle(new SolidBrush(Color.FromArgb(40, 100, 200)), 1, 1, 14, 14);
            g.DrawRectangle(Pens.DarkBlue, 1, 1, 13, 13);
            using var lime = new SolidBrush(Color.Lime);
            g.FillRectangle(lime, 3, 11, 10, 2);
            g.FillRectangle(lime, 3,  8,  7, 2);
            g.FillRectangle(lime, 3,  5,  4, 2);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
 
    // --------------------------------------------------------
    //  Menu  (File | Options | View | Help)
    // --------------------------------------------------------
    private MenuStrip BuildMenu()
    {
        var ms = new MenuStrip { Dock = DockStyle.Top };
 
        // -- File ------------------------------------------
        var mFile = new ToolStripMenuItem("&File");
        mFile.DropDownItems.Add("New Task (Run...)", null,
            (_, _) => { using var d = new RunDialog(); d.ShowDialog(this); });
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add("Exit Task Manager", null, (_, _) => Close());
        ms.Items.Add(mFile);
 
        // -- Options ----------------------------------------
        var mOpt = new ToolStripMenuItem("&Options");
        _miAlwaysOnTop   = Checkable(mOpt, "Always On Top",
            () => TopMost = _miAlwaysOnTop.Checked);
        _miMinimizeOnUse = Checkable(mOpt, "Minimize On Use", null);
        _miHideWhenMin   = Checkable(mOpt, "Hide When Minimized", null);
        ms.Items.Add(mOpt);
 
        // -- View -------------------------------------------
        var mView = new ToolStripMenuItem("&View");
        mView.DropDownItems.Add("Refresh Now", null, (_, _) => Simulate(true));
 
        var mSpeed = new ToolStripMenuItem("Update Speed");
        _miSpeedHigh   = RadioItem(mSpeed, "High",   () => SetSpeed(UpdateSpeed.High));
        _miSpeedNormal = RadioItem(mSpeed, "Normal", () => SetSpeed(UpdateSpeed.Normal));
        _miSpeedLow    = RadioItem(mSpeed, "Low",    () => SetSpeed(UpdateSpeed.Low));
        _miSpeedPaused = RadioItem(mSpeed, "Paused", () => SetSpeed(UpdateSpeed.Paused));
        _miSpeedNormal.Checked = true;
        mView.DropDownItems.Add(mSpeed);
        mView.DropDownItems.Add(new ToolStripSeparator());
 
        var mCpuH = new ToolStripMenuItem("CPU History");
        var mAllCpu = new ToolStripMenuItem("One Graph, All CPUs")  { CheckOnClick = true };
        var mPerCpu = new ToolStripMenuItem("One Graph Per CPU") { CheckOnClick = true, Checked = true };
        mCpuH.DropDownItems.Add(mAllCpu);
        mCpuH.DropDownItems.Add(mPerCpu);
        mView.DropDownItems.Add(mCpuH);
 
        _miShowKernel = Checkable(mView, "Show Kernel Times", () =>
        {
            _showKernel = _miShowKernel.Checked;
            foreach (var g in _ghCores) g.ShowKernel = _showKernel;
        });
        ms.Items.Add(mView);
 
        // -- Help -------------------------------------------
        var mHelp = new ToolStripMenuItem("&Help");
        mHelp.DropDownItems.Add("Task Manager Help Topics", null, (_, _) =>
            Process.Start(new ProcessStartInfo(
                "https://go.microsoft.com/fwlink/?LinkId=517009") { UseShellExecute = true }));
        mHelp.DropDownItems.Add(new ToolStripSeparator());
        mHelp.DropDownItems.Add("About Task Manager", null, (_, _) =>
            MessageBox.Show(
                "Windows Task Manager\n\n.NET 6.0 Classic Recreation",
                "About Task Manager",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information));
        ms.Items.Add(mHelp);
 
        return ms;
    }
 
    private ToolStripMenuItem Checkable(ToolStripMenuItem parent, string text, Action? act)
    {
        var mi = new ToolStripMenuItem(text) { CheckOnClick = true };
        if (act != null) mi.Click += (_, _) => act();
        parent.DropDownItems.Add(mi);
        return mi;
    }
 
    private ToolStripMenuItem RadioItem(ToolStripMenuItem parent, string text, Action act)
    {
        var mi = new ToolStripMenuItem(text);
        mi.Click += (_, _) =>
        {
            foreach (ToolStripMenuItem s in parent.DropDownItems.OfType<ToolStripMenuItem>())
                s.Checked = s == mi;
            act();
        };
        parent.DropDownItems.Add(mi);
        return mi;
    }
 
    // --------------------------------------------------------
    //  Status bar  (23 px, 3 sections with dividers)
    // --------------------------------------------------------
    private Panel BuildStatusBar()
    {
        var pnl = new Panel { Dock = DockStyle.Bottom, Height = 23 };
 
        pnl.Paint += (s, e) =>
        {
            var p = (Panel)s!;
            // Top border line
            e.Graphics.DrawLine(SystemPens.ControlDark, 0, 0, p.Width, 0);
            // Section dividers
            int w3 = p.Width / 3;
            DrawSectionBorder(e.Graphics, w3,     1, p.Height - 2);
            DrawSectionBorder(e.Graphics, w3 * 2, 1, p.Height - 2);
        };
 
        _sbProcs = StatLabel("Processes: 233");
        _sbCpu   = StatLabel("CPU Usage: 2%");
        _sbMem   = StatLabel("Physical Memory: 71%");
        pnl.Controls.Add(_sbProcs);
        pnl.Controls.Add(_sbCpu);
        pnl.Controls.Add(_sbMem);
 
        pnl.Resize += (_, _) => LayoutStatusLabels();
        return pnl;
    }
 
    private static void DrawSectionBorder(Graphics g, int x, int top, int height)
    {
        g.DrawLine(SystemPens.ControlDark,  x - 1, top, x - 1, top + height);
        g.DrawLine(SystemPens.ControlLight, x,     top, x,     top + height);
    }
 
    private static Label StatLabel(string text) =>
        new()
        {
            Text      = text,
            AutoSize  = false,
            Height    = 23,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
        };
 
    private void LayoutStatusLabels()
    {
        if (_statusPanel == null) return;
        int w3 = _statusPanel.Width / 3;
        _sbProcs.Left = 4;         _sbProcs.Width = w3 - 4;
        _sbCpu  .Left = w3 + 4;   _sbCpu  .Width = w3 - 4;
        _sbMem  .Left = w3 * 2 + 4; _sbMem .Width = w3 - 4;
    }
 
    // --------------------------------------------------------
    //  Tab area (fills between menu and status bar)
    //  Main white panel: left=6, top=8 (from below menu),
    //                    right margin=8
    // --------------------------------------------------------
    private Panel BuildTabArea()
    {
        // Outer panel: left=6, right=8 from form edge ? Padding(6,8,8,0)
        var outer = new Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(6, 8, 8, 0),
        };
 
        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.TabPages.Add(BuildApplicationsTab());
        _tabs.TabPages.Add(BuildProcessesTab());
        _tabs.TabPages.Add(BuildServicesTab());
        _tabs.TabPages.Add(BuildPerformanceTab());
        _tabs.TabPages.Add(BuildNetworkingTab());
        _tabs.TabPages.Add(BuildUsersTab());
 
        outer.Controls.Add(_tabs);
        return outer;
    }
 
    // --------------------------------------------------------
    //  Tab: Applications
    // --------------------------------------------------------
    private TabPage BuildApplicationsTab()
    {
        var tp = new TabPage("Applications");
 
        _lvApps = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect   = true,
        };
        _lvApps.Columns.Add("Task",   340, HorizontalAlignment.Left);
        _lvApps.Columns.Add("Status", 100, HorizontalAlignment.Left);
 
        // Button bar – 40 px tall, #F0F0F0 background
        var bar = MakeButtonBar();
        var bEnd    = MakeBtn("End Task");
        var bSwitch = MakeBtn("Switch To");
        var bNew    = MakeBtn("New Task...");
 
        bNew.Click    += (_, _) => { using var d = new RunDialog(); d.ShowDialog(this); };
        bSwitch.Click += (_, _) =>
            MessageBox.Show("No window selected.", "Task Manager",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        bEnd.Click += (_, _) =>
        {
            foreach (ListViewItem it in _lvApps.SelectedItems.Cast<ListViewItem>().ToList())
                _lvApps.Items.Remove(it);
        };
 
        // Order in array: [0]=rightmost…[n-1]=leftmost
        bar.Controls.AddRange(new Control[] { bEnd, bSwitch, bNew });
        bar.Resize += (_, _) => AlignRight(bar, new[] { bEnd, bSwitch, bNew });
 
        tp.Controls.Add(_lvApps);
        tp.Controls.Add(bar);
        return tp;
    }
 
    // --------------------------------------------------------
    //  Tab: Processes
    // --------------------------------------------------------
    private TabPage BuildProcessesTab()
    {
        var tp = new TabPage("Processes");
 
        _lvProcs = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
        };
        _lvProcs.Columns.Add("Image Name",   142, HorizontalAlignment.Left);
        _lvProcs.Columns.Add("User Name",     78, HorizontalAlignment.Left);
        _lvProcs.Columns.Add("CPU",           40, HorizontalAlignment.Right);
        _lvProcs.Columns.Add("Memory (...)",  90, HorizontalAlignment.Right);
        _lvProcs.Columns.Add("Description",  145, HorizontalAlignment.Left);
 
        var bar  = MakeButtonBar();
        var bEnd = MakeBtn("End Process");
 
        // "Show processes from all users" – left aligned, 21 px from left
        var bShow = new Button
        {
            Text   = "Show processes from all users",
            Height = 23,
            Width  = 192,
            Left   = 21,
            Top    = 9,
        };
 
        bEnd.Click += (_, _) =>
        {
            foreach (ListViewItem it in _lvProcs.SelectedItems.Cast<ListViewItem>().ToList())
                _lvProcs.Items.Remove(it);
        };
 
        bar.Controls.Add(bEnd);
        bar.Controls.Add(bShow);
        bar.Resize += (_, _) =>
        {
            AlignRight(bar, new[] { bEnd });
            bShow.Left = 21;
            bShow.Top  = 9;
        };
 
        tp.Controls.Add(_lvProcs);
        tp.Controls.Add(bar);
        return tp;
    }
 
    // --------------------------------------------------------
    //  Tab: Services
    // --------------------------------------------------------
    private TabPage BuildServicesTab()
    {
        var tp = new TabPage("Services");
 
        _lvSvc = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
        };
        _lvSvc.Columns.Add("Name",        120, HorizontalAlignment.Left);
        _lvSvc.Columns.Add("PID",          44, HorizontalAlignment.Right);
        _lvSvc.Columns.Add("Description", 173, HorizontalAlignment.Left);
        _lvSvc.Columns.Add("Status",       58, HorizontalAlignment.Left);
        _lvSvc.Columns.Add("Group",        90, HorizontalAlignment.Left);
 
        var bar  = MakeButtonBar();
        var bSvc = MakeBtn("Services...");
        bar.Controls.Add(bSvc);
        bar.Resize += (_, _) => AlignRight(bar, new[] { bSvc });
 
        tp.Controls.Add(_lvSvc);
        tp.Controls.Add(bar);
        return tp;
    }
 
    // --------------------------------------------------------
    //  Tab: Performance
    // --------------------------------------------------------
    private TabPage BuildPerformanceTab()
    {
        var tp = new TabPage("Performance")
        {
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
        };
 
        var outer = new Panel
        {
            Dock      = DockStyle.Fill,
            Padding   = new Padding(4, 2, 4, 4),
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
        };
 
        // -- CPU row -----------------------------------------
        var cpuRow = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 152,
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
        };
 
        _gCpu = new BarGauge { Left = 0, Top = 18, Width = 56, Height = 108 };
        cpuRow.Controls.Add(TinyLabel("CPU Usage", 0, 4, 60));
        cpuRow.Controls.Add(_gCpu);
 
        // Per-core graph area
        int cores = 6;
        _ghCores = new GraphHistory[cores];
        var pCores = new Panel
        {
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        pCores.Controls.Add(TinyLabel("CPU Usage History", 0, 4, 220));
        for (int i = 0; i < cores; i++)
        {
            _ghCores[i] = new GraphHistory();
            pCores.Controls.Add(_ghCores[i]);
        }
        pCores.Resize += (_, _) => LayoutCoreGraphs(pCores, cores);
        cpuRow.Controls.Add(pCores);
        cpuRow.Resize += (_, _) =>
        {
            pCores.Left   = 64;
            pCores.Top    = 0;
            pCores.Width  = cpuRow.Width - 68;
            pCores.Height = cpuRow.Height;
        };
        outer.Controls.Add(cpuRow);
 
        // -- Memory row --------------------------------------
        var memRow = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 152,
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
        };
 
        _gMem = new BarGauge { Left = 0, Top = 18, Width = 56, Height = 108, Footer = "17.2 GB" };
        memRow.Controls.Add(TinyLabel("Memory", 0, 4, 60));
        memRow.Controls.Add(_gMem);
 
        _ghMem = new GraphHistory();
        var pMemH = new Panel
        {
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
            Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        pMemH.Controls.Add(TinyLabel("Physical Memory Usage History", 0, 4, 260));
        pMemH.Controls.Add(_ghMem);
        pMemH.Resize += (_, _) =>
        {
            _ghMem.Left   = 0;
            _ghMem.Top    = 20;
            _ghMem.Width  = pMemH.Width;
            _ghMem.Height = pMemH.Height - 22;
        };
        memRow.Controls.Add(pMemH);
        memRow.Resize += (_, _) =>
        {
            pMemH.Left   = 64;
            pMemH.Top    = 0;
            pMemH.Width  = memRow.Width - 68;
            pMemH.Height = memRow.Height;
        };
        outer.Controls.Add(memRow);
 
        // -- Stats panel -------------------------------------
        var pStats = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
        };
        BuildStatsPanel(pStats);
        outer.Controls.Add(pStats);
 
        tp.Controls.Add(outer);
        return tp;
    }
 
    private void LayoutCoreGraphs(Panel p, int n)
    {
        int labelH = 20;
        int cols   = 3;
        int rows   = (n + cols - 1) / cols;
        int cW     = p.Width  / cols;
        int cH     = (p.Height - labelH) / rows;
        const int gap = 2;
        for (int i = 0; i < n && i < _ghCores.Length; i++)
        {
            int col = i % cols, row = i / cols;
            _ghCores[i].Left   = col * cW + gap;
            _ghCores[i].Top    = labelH + row * cH + gap;
            _ghCores[i].Width  = cW - gap * 2;
            _ghCores[i].Height = Math.Max(cH - gap * 2, 4);
        }
    }
 
    private void BuildStatsPanel(Panel p)
    {
        // Left column: Physical Memory + Kernel Memory
        int y1 = 4;
        p.Controls.Add(GroupHeader("Physical Memory (MB)", 0, y1)); y1 += 16;
        p.Controls.Add(KeyLabel("Total",     4, y1)); _lPmTotal  = ValLabel(p, 92, y1); y1 += 14;
        p.Controls.Add(KeyLabel("Cached",    4, y1)); _lPmCached = ValLabel(p, 92, y1); y1 += 14;
        p.Controls.Add(KeyLabel("Available", 4, y1)); _lPmAvail  = ValLabel(p, 92, y1); y1 += 14;
        p.Controls.Add(KeyLabel("Free",      4, y1)); _lPmFree   = ValLabel(p, 92, y1); y1 += 20;
 
        p.Controls.Add(GroupHeader("Kernel Memory (MB)", 0, y1)); y1 += 16;
        p.Controls.Add(KeyLabel("Paged",    4, y1)); _lKmPaged = ValLabel(p, 92, y1); y1 += 14;
        p.Controls.Add(KeyLabel("Nonpaged", 4, y1)); _lKmNP    = ValLabel(p, 92, y1);
 
        // Right column: System
        int x2 = 200, y2 = 4;
        p.Controls.Add(GroupHeader("System", x2, y2)); y2 += 16;
        p.Controls.Add(KeyLabel("Handles",    x2 + 4, y2)); _lSysH   = ValLabel(p, x2 + 92, y2); y2 += 14;
        p.Controls.Add(KeyLabel("Threads",    x2 + 4, y2)); _lSysT   = ValLabel(p, x2 + 92, y2); y2 += 14;
        p.Controls.Add(KeyLabel("Processes",  x2 + 4, y2)); _lSysP   = ValLabel(p, x2 + 92, y2); y2 += 14;
        p.Controls.Add(KeyLabel("Up Time",    x2 + 4, y2)); _lUpTime = ValLabel(p, x2 + 92, y2); y2 += 14;
        p.Controls.Add(KeyLabel("Commit (GB)", x2 + 4, y2)); _lCommit = ValLabel(p, x2 + 92, y2);
 
        // Resource Monitor button (bottom-right, 16 px from right, 17 px from bottom)
        var bRM = new Button
        {
            Text   = "Resource Monitor...",
            Width  = 140,
            Height = 23,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        bRM.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("perfmon.exe", "/res") { UseShellExecute = true }); }
            catch { }
        };
        p.Controls.Add(bRM);
        p.Resize += (_, _) =>
        {
            bRM.Left = p.Width  - bRM.Width  - 16;
            bRM.Top  = p.Height - bRM.Height - 17;
        };
    }
 
    private static Label GroupHeader(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y, Width = 190, Height = 14,
                Font = new Font("Tahoma", 8f), BackColor = Color.Transparent };
 
    private static Label KeyLabel(string t, int x, int y) =>
        new() { Text = t, Left = x, Top = y, Width = 86, Height = 14, BackColor = Color.Transparent };
 
    private static Label ValLabel(Panel p, int x, int y)
    {
        var l = new Label
        {
            Text      = "0",
            Left      = x,
            Top       = y,
            Width     = 80,
            Height    = 14,
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent,
        };
        p.Controls.Add(l);
        return l;
    }
 
    private static Label TinyLabel(string t, int x, int y, int w) =>
        new() { Text = t, Left = x, Top = y, Width = w, Height = 16, BackColor = Color.Transparent };
 
    // --------------------------------------------------------
    //  Tab: Networking
    // --------------------------------------------------------
    private TabPage BuildNetworkingTab()
    {
        var tp = new TabPage("Networking");
 
        var outer = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0),
            Padding   = new Padding(4, 4, 4, 0),
        };
 
        // Table at bottom
        _lvNet = new ListView
        {
            Dock          = DockStyle.Bottom,
            Height        = 88,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
        };
        _lvNet.Columns.Add("Adapter Name",        140, HorizontalAlignment.Left);
        _lvNet.Columns.Add("Network Utiliza...",   100, HorizontalAlignment.Right);
        _lvNet.Columns.Add("Link Sp...",            80, HorizontalAlignment.Right);
        _lvNet.Columns.Add("State",                 80, HorizontalAlignment.Left);
 
        // Graph rows (fill area above table)
        string[] adapterNames = { "Ethernet 3", "Local Area Connection", "VMware Network Adapter VMnet1" };
        int n = adapterNames.Length;
        _ghNet = new GraphHistory[n];
 
        var pGraphs = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0) };
        for (int i = 0; i < n; i++)
        {
            int idx  = i;
            string lbl = adapterNames[i];
            var pRow = new Panel { BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0) };
            pRow.Controls.Add(TinyLabel($"{lbl}", 0, 2, 340));
            _ghNet[i] = new GraphHistory();
            pRow.Controls.Add(_ghNet[i]);
            pRow.Resize += (_, _) =>
            {
                _ghNet[idx].Left   = 0;
                _ghNet[idx].Top    = 18;
                _ghNet[idx].Width  = pRow.Width;
                _ghNet[idx].Height = Math.Max(pRow.Height - 20, 4);
            };
            pGraphs.Controls.Add(pRow);
        }
        pGraphs.Resize += (_, _) =>
        {
            int h = pGraphs.Height / n;
            for (int i = 0; i < pGraphs.Controls.Count && i < n; i++)
            {
                var c = pGraphs.Controls[i];
                c.Left   = 0;
                c.Top    = i * h;
                c.Width  = pGraphs.Width;
                c.Height = h - 2;
            }
        };
 
        outer.Controls.Add(pGraphs);
        outer.Controls.Add(_lvNet);
        tp.Controls.Add(outer);
        return tp;
    }
 
    // --------------------------------------------------------
    //  Tab: Users
    // --------------------------------------------------------
    private TabPage BuildUsersTab()
    {
        var tp = new TabPage("Users");
 
        _lvUsers = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
        };
        _lvUsers.Columns.Add("User",        80,  HorizontalAlignment.Left);
        _lvUsers.Columns.Add("ID",          40,  HorizontalAlignment.Right);
        _lvUsers.Columns.Add("Status",      70,  HorizontalAlignment.Left);
        _lvUsers.Columns.Add("Client Name", 100, HorizontalAlignment.Left);
        _lvUsers.Columns.Add("Session",     80,  HorizontalAlignment.Left);
 
        var bar  = MakeButtonBar();
        var bMsg  = MakeBtn("Send Message...");
        var bLog  = MakeBtn("Logoff");
        var bDisc = MakeBtn("Disconnect");
 
        bar.Controls.AddRange(new Control[] { bDisc, bLog, bMsg });
        bar.Resize += (_, _) => AlignRight(bar, new[] { bDisc, bLog, bMsg });
 
        tp.Controls.Add(_lvUsers);
        tp.Controls.Add(bar);
        return tp;
    }
 
    // --------------------------------------------------------
    //  Layout helpers
    // --------------------------------------------------------
 
    /// <summary>
    /// Creates the 40 px button bar panel used at the bottom of each tab.
    /// </summary>
    private static Panel MakeButtonBar() =>
        new() { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0) };
 
    /// <summary>
    /// Creates a standard push button sized to its text.
    /// </summary>
    private static Button MakeBtn(string text) =>
        new()
        {
            Text   = text,
            Height = 23,
            Width  = Math.Max(75,
                TextRenderer.MeasureText(text, SystemFonts.DefaultFont).Width + 18),
        };
 
    /// <summary>
    /// Positions <paramref name="buttons"/> flush to the right of <paramref name="bar"/>:
    ///   • right edge of rightmost button = bar.Width - 16 px
    ///   • top of buttons = 9 px (giving 17 px from bottom for a 23 px button)
    ///   • 6 px gap between buttons
    /// </summary>
    private static void AlignRight(Panel bar, Button[] buttons)
    {
        const int rightPad = 16;
        const int topOff   = 9;
        const int gap      = 6;
 
        int x = bar.Width - rightPad;
        foreach (var b in buttons)          // array[0] = rightmost
        {
            x -= b.Width;
            b.Left = x;
            b.Top  = topOff;
            x -= gap;
        }
    }
 
    // --------------------------------------------------------
    //  Data population
    // --------------------------------------------------------
    private void PopulateApplications()
    {
        string[] apps =
        {
            "[TMEP].pptx - PowerPoint",
            "Admin",
            "bat_dragged.png - Pinta",
            "Black 3D Viewer",
            "bootia32.efi - Notepad",
            "CFF Explorer VII - [WinExplorer.dll]",
            "CFF Explorer VII - [WinExplorer.dll]",
            "ClassicTaskmgr",
            "Downloads",
            "Downloads – File Explorer",
            "Event Viewer",
            "File Explorer",
            "FileExplorer",
            "FileExplorer.zip",
            "Google Chrome Help - Google Chrome",
            "Just Color Picker",
            "l.bimage - Notepad",
            "net6.0-windows",
            "Noesis",
            "Preferences - Directory Opus",
            "readme.txt - Notepad",
            "Resource Hacker - tm.exe",
            "Services",
            "Task Manager",
            "Win10",
            "Winaero - At the edge of tweaking - Thorium",
            "Windows 7 Games for Windows 11, Windows 10...",
            "Windows Explorer recreation in C# .NET - Claud...",
        };
        foreach (var a in apps)
        {
            var it = new ListViewItem(a);
            it.SubItems.Add("Running");
            _lvApps.Items.Add(it);
        }
        if (_lvApps.Items.Count > 0) _lvApps.Items[0].Selected = true;
    }
 
    private void PopulateProcesses()
    {
        (string img, string user, string cpu, string mem, string desc)[] rows =
        {
            ("ApplicationFra...", "Admin", "00", "2,240 K",   "Applicatio..."),
            ("Black3DViewe...",   "Admin", "00", "39,528 K",  "Black3DVi..."),
            ("CFF Explorer....",  "Admin", "00", "4,104 K",   "Common ..."),
            ("CFF Explorer....",  "Admin", "00", "4,100 K",   "Common ..."),
            ("chrome.exe",        "Admin", "00", "397,516 K", "Google C..."),
            ("chrome.exe",        "Admin", "00", "14,900 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "5,432 K",   "Google C..."),
            ("chrome.exe",        "Admin", "00", "192,572 K", "Google C..."),
            ("chrome.exe",        "Admin", "00", "27,500 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "143,548 K", "Google C..."),
            ("chrome.exe",        "Admin", "00", "247,308 K", "Google C..."),
            ("chrome.exe",        "Admin", "00", "45,468 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "18,828 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "21,768 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "83,344 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "3,964 K",   "Google C..."),
            ("chrome.exe",        "Admin", "00", "20,240 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "29,568 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "68,488 K",  "Google C..."),
            ("chrome.exe",        "Admin", "00", "312,712 K", "Google C..."),
            ("chrome.exe",        "Admin", "00", "7,600 K",   "Google C..."),
            ("csrss.exe",         "",      "01", "1,300 K",   ""),
            ("ctfmon.exe",        "Admin", "00", "69,504 K",  "CTF Loader"),
            ("dopus.exe",         "Admin", "01", "101,236 K", "Directory ..."),
            ("dopusrt.exe",       "Admin", "00", "1,092 K",   "Directory ..."),
            ("dopusrt.exe",       "Admin", "00", "1,276 K",   "Directory ..."),
            ("dwm.exe",           "",      "02", "105,744 K", ""),
            ("explorer.exe",      "Admin", "03", "132,624 K", "Windows ..."),
        };
        foreach (var (img, user, cpu, mem, desc) in rows)
        {
            var it = new ListViewItem(img);
            it.SubItems.Add(user); it.SubItems.Add(cpu);
            it.SubItems.Add(mem);  it.SubItems.Add(desc);
            _lvProcs.Items.Add(it);
        }
    }
 
    private void PopulateServices()
    {
        (string name, string pid, string desc, string status, string grp)[] rows =
        {
            ("VaultSvc",      "884",  "Credential ...", "Runn...", ""),
            ("SamSs",         "884",  "Security Ac...", "Runn...", ""),
            ("NetTcpPort...", "884",  "Net.Tcp Po...",  "Runn...", ""),
            ("Netlogon",      "",     "Netlogon",       "Stop...", ""),
            ("KeyIso",        "884",  "CNG Key Is...",  "Runn...", ""),
            ("EFS",           "884",  "Encrypting ...", "Runn...", ""),
            ("WalletService", "",     "WalletService",  "Stop...", "appmodel"),
            ("StateRepos...", "2512", "State Repo...",  "Stop...", "appmodel"),
            ("EntAppSvc",     "",     "Enterprise ...", "Stop...", "appmodel"),
            ("camsvc",        "",     "Capability ...", "Stop...", "appmodel"),
            ("AppReadiness",  "",     "App Readi...",   "Stop...", "AppReadin..."),
            ("AssignedAc...", "",     "AssignedA...",   "Stop...", "AssignedA..."),
            ("AxInstSV",      "",     "ActiveX Ins...", "Stop...", "AxInstSVG..."),
            ("BcastDVRU...",  "",     "GameDVR ...",    "Stop...", "BcastDVRU..."),
            ("BluetoothU...", "",     "Bluetooth ...",  "Stop...", "BthAppGroup"),
            ("FrameServer",   "",     "Windows C...",   "Stop...", "Camera"),
            ("cbdhsvc_57...", "",     "Clipboard ...",  "Stop...", "ClipboardS..."),
            ("SystemEve...",  "92",   "System Ev...",   "Runn...", "DcomLaunch"),
            ("Power",         "92",   "Power",          "Runn...", "DcomLaunch"),
            ("PlugPlay",      "92",   "Plug and Play",  "Runn...", "DcomLaunch"),
            ("LSM",           "92",   "Local Sessi...", "Runn...", "DcomLaunch"),
            ("DeviceInstall", "92",   "Device Inst...", "Stop...", "DcomLaunch"),
            ("DcomLaunch",    "92",   "DCOM Ser...",    "Runn...", "DcomLaunch"),
            ("BrokerInfra...", "92",  "Backgroun...",   "Runn...", "DcomLaunch"),
            ("DevicesFlo...", "",     "DevicesFlo...",  "Stop...", "DevicesFlow"),
            ("DevicesPick...", "",    "DevicesPick...", "Stop...", "DevicesFlow"),
            ("ConsentUx...",  "",     "ConsentUX...",   "Stop...", "DevicesFlow"),
            ("diagsvc",       "",     "Diagnostic ...", "Stop...", "diagnostics"),
        };
        foreach (var (name, pid, desc, status, grp) in rows)
        {
            var it = new ListViewItem(name);
            it.SubItems.Add(pid);  it.SubItems.Add(desc);
            it.SubItems.Add(status); it.SubItems.Add(grp);
            _lvSvc.Items.Add(it);
        }
        if (_lvSvc.Items.Count > 0)
        {
            _lvSvc.Items[0].Selected   = true;
            _lvSvc.Items[0].ForeColor  = Color.Blue;
        }
    }
 
    private void PopulateUsers()
    {
        var it = new ListViewItem("Admin");
        it.SubItems.Add("1");
        it.SubItems.Add("Active");
        it.SubItems.Add("");        // Client Name (blank for console)
        it.SubItems.Add("Console");
        _lvUsers.Items.Add(it);
    }
 
    private void PopulateNetwork()
    {
        (string name, string util, string link, string state)[] rows =
        {
            ("Ethernet 2",        "0 %", "100 Mbps", "Connected"),
            ("Ethernet 3",        "0 %", "1 Gbps",   "Connected"),
            ("Local Area Con...", "0 %", "-",         "Disconnected"),
            ("VMware Networ...",  "0 %", "100 Mbps",  "Connected"),
            ("VMware Networ...",  "0 %", "100 Mbps",  "Connected"),
        };
        foreach (var (name, util, link, state) in rows)
        {
            var it = new ListViewItem(name);
            it.SubItems.Add(util); it.SubItems.Add(link); it.SubItems.Add(state);
            _lvNet.Items.Add(it);
        }
    }
 
    // --------------------------------------------------------
    //  Simulation
    // --------------------------------------------------------
    private void SetSpeed(UpdateSpeed s)
    {
        _speed = s;
        if (s == UpdateSpeed.Paused)
        {
            _timer?.Stop();
            return;
        }
        _timer.Interval = s switch
        {
            UpdateSpeed.High   =>  500,
            UpdateSpeed.Normal => 1000,
            UpdateSpeed.Low    => 4000,
            _                  => 1000,
        };
        _timer.Start();
    }
 
    private void Simulate(bool updateUI)
    {
        // CPU
        _cpuPct = Drift(_cpuPct, 0.5f, 15f, 0f, 100f);
        for (int i = 0; i < _corePct.Length; i++)
            _corePct[i] = Drift(_corePct[i], 0.5f, 20f, 0f, 100f);
 
        // Memory (slow drift)
        _memMB = Drift(_memMB, 0.5f, 50f, 0f, TotalMemMB);
 
        // Network (near zero)
        for (int i = 0; i < _netPct.Length; i++)
            _netPct[i] = Math.Clamp(_netPct[i] + (_rng.NextSingle() - 0.5f) * 0.05f, 0f, 1f);
 
        // Push to graph controls
        for (int i = 0; i < _ghCores.Length; i++)
            _ghCores[i].Push(_corePct[i], _corePct[i] * 0.12f);
 
        float memPct = _memMB / TotalMemMB * 100f;
        _ghMem.Push(memPct);
 
        for (int i = 0; i < _ghNet.Length; i++)
            _ghNet[i].Push(_netPct[i]);
 
        if (!updateUI) return;
 
        // Update gauges
        _gCpu.Value   = _cpuPct;
        _gCpu.Footer  = $"{(int)_cpuPct} %";
        _gCpu.Invalidate();
 
        float memGB = _memMB / 1024f;
        _gMem.Value   = memPct;
        _gMem.Footer  = $"{memGB:F1} GB";
        _gMem.Invalidate();
 
        // Stats labels
        float avail = TotalMemMB - _memMB;
        float free  = avail * 0.02f;
 
        Set(_lPmTotal,  $"{TotalMemMB:N0}");
        Set(_lPmCached, $"{CachedMemMB:N0}");
        Set(_lPmAvail,  $"{avail:N0}");
        Set(_lPmFree,   $"{free:N0}");
        Set(_lKmPaged,  "454");
        Set(_lKmNP,     "230");
 
        _handles += _rng.Next(-10, 11);
        _threads += _rng.Next(-2, 3);
        Set(_lSysH,   $"{_handles:N0}");
        Set(_lSysT,   $"{_threads:N0}");
        Set(_lSysP,   $"{_procs:N0}");
        Set(_lUpTime, (DateTime.Now - _bootTime).ToString(@"h\:mm\:ss"));
        Set(_lCommit, $"{(int)(_memMB * 1.2f / 1024f)} / {(int)(TotalMemMB / 1024f)}");
 
        // Status bar
        _sbProcs.Text = $"Processes: {_procs}";
        _sbCpu  .Text = $"CPU Usage: {(int)_cpuPct}%";
        _sbMem  .Text = $"Physical Memory: {(int)memPct}%";
    }
 
    private float Drift(float v, float bias, float step, float min, float max)
    {
        float d = (_rng.NextSingle() - bias) * step;
        return Math.Clamp(v + d, min, max);
    }
 
    private static void Set(Label? l, string text)
    {
        if (l != null) l.Text = text;
    }
 
    // --------------------------------------------------------
    //  WndProc – hide on minimise when option checked
    // --------------------------------------------------------
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE   = 0xF020;
 
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SYSCOMMAND &&
            (m.WParam.ToInt32() & 0xFFF0) == SC_MINIMIZE &&
            _miHideWhenMin?.Checked == true)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
            return;
        }
        if (_miMinimizeOnUse?.Checked == true && m.Msg == WM_SYSCOMMAND)
        {
            // Minimise on task switch – basic implementation
        }
        base.WndProc(ref m);
    }
}
 