using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ExplorerForm());
    }
}

// ----------------------- Main Form -----------------------
public class ExplorerForm : Form
{
    private TopToolbar topToolbar;
    private LowerToolbar lowerToolbar;
    private SplitContainer splitContainer;
    private FolderTree folderTree;
    private FileListView fileListView;
    private Stack<string> backStack = new Stack<string>();
    private Stack<string> forwardStack = new Stack<string>();
    private string currentPath;

    public ExplorerForm()
    {
        Text = "File Explorer";
        Size = new Size(1200, 800);
        DoubleBuffered = true;
        InitializeComponents();
        Load += (s, e) => NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
    }

    private void InitializeComponents()
    {
        topToolbar = new TopToolbar { Dock = DockStyle.Top, Height = 44 };
        lowerToolbar = new LowerToolbar { Dock = DockStyle.Top, Height = 31 };
        splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 4,
            BackColor = Color.FromArgb(240, 240, 240)
        };
        folderTree = new FolderTree { Dock = DockStyle.Fill };
        fileListView = new FileListView { Dock = DockStyle.Fill };

        splitContainer.Panel1.Controls.Add(folderTree);
        splitContainer.Panel2.Controls.Add(fileListView);

        Controls.Add(splitContainer);
        Controls.Add(lowerToolbar);
        Controls.Add(topToolbar);

        // Wire events
        topToolbar.PathChanged += (s, path) => NavigateTo(path);
        topToolbar.BackRequested += (s, e) => NavigateBack();
        topToolbar.ForwardRequested += (s, e) => NavigateForward();
        topToolbar.UpRequested += (s, e) => NavigateUp();
        lowerToolbar.NewFolderRequested += (s, e) => CreateNewFolder();
        lowerToolbar.ViewChanged += (s, view) => fileListView.ViewMode = view;
        lowerToolbar.PreviewPaneToggled += (s, show) => { /* toggle preview */ };
        lowerToolbar.HelpRequested += (s, e) =>
            System.Diagnostics.Process.Start("https://go.microsoft.com/fwlink/?LinkID=2004439");
        folderTree.FolderSelected += (s, path) => NavigateTo(path);
        fileListView.ItemDoubleClicked += (s, path) => NavigateTo(path);
        fileListView.ItemRightClicked += (s, path) =>
        {
            bool isFolder = Directory.Exists(path);
            ContextMenuStrip menu = isFolder
                ? fileListView.FolderContextMenu
                : File.Exists(path)
                    ? fileListView.FileContextMenu
                    : fileListView.ListContextMenu;
            if (menu != null)
            {
                menu.Tag = path;
                menu.Show(fileListView, fileListView.PointToClient(Cursor.Position));
            }
        };
        fileListView.ListContextMenu = CreateListContextMenu();
        fileListView.FolderContextMenu = CreateFolderContextMenu();
        fileListView.FileContextMenu = CreateFileContextMenu();
    }

    private async void NavigateTo(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (path.Length == 2 && path[1] == ':')
            path += "\\";
        if (currentPath == path) return;
        if (currentPath != null) backStack.Push(currentPath);
        forwardStack.Clear();
        currentPath = path;
        topToolbar.SetPath(path);
        folderTree.ExpandToPath(path);
        await fileListView.LoadPathAsync(path);
    }

    private void NavigateBack() { if (backStack.Count > 0) { forwardStack.Push(currentPath); NavigateTo(backStack.Pop()); } }
    private void NavigateForward() { if (forwardStack.Count > 0) { backStack.Push(currentPath); NavigateTo(forwardStack.Pop()); } }
    private void NavigateUp()
    {
        var parent = Directory.GetParent(currentPath);
        if (parent != null) NavigateTo(parent.FullName);
    }

    private async void CreateNewFolder()
    {
        string name = "New folder", path = Path.Combine(currentPath, name);
        int i = 1;
        while (Directory.Exists(path)) path = Path.Combine(currentPath, $"{name} ({i++})");
        try
        {
            Directory.CreateDirectory(path);
            await fileListView.LoadPathAsync(currentPath);
        }
        catch { }
    }

    // --- Context menus (unchanged) ---
    private ContextMenuStrip CreateListContextMenu() { return BuildListMenu(); }
    private ContextMenuStrip CreateFolderContextMenu() { return BuildFolderMenu(); }
    private ContextMenuStrip CreateFileContextMenu() { return BuildFileMenu(); }

    private ContextMenuStrip BuildListMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.AddWithIcon("View", null, new ToolStripItem[] {
            new ToolStripMenuItem("Extra large icons"), new ToolStripMenuItem("Large icons"),
            new ToolStripMenuItem("Medium icons"), new ToolStripMenuItem("Small icons"),
            new ToolStripMenuItem("List"), new ToolStripMenuItem("Details"),
            new ToolStripMenuItem("Tiles"), new ToolStripMenuItem("Content") });
        menu.Items.AddWithIcon("Sort by", null, new ToolStripItem[] { new ToolStripMenuItem("Name"), new ToolStripMenuItem("Date modified"), new ToolStripMenuItem("Type"), new ToolStripMenuItem("Size"), new ToolStripSeparator(), new ToolStripMenuItem("Ascending"), new ToolStripMenuItem("Descending"), new ToolStripSeparator(), new ToolStripMenuItem("More...") });
        menu.Items.AddWithIcon("Group by", null, new ToolStripItem[] { new ToolStripMenuItem("Name"), new ToolStripMenuItem("Date modified"), new ToolStripMenuItem("Type"), new ToolStripMenuItem("Size"), new ToolStripSeparator(), new ToolStripMenuItem("Ascending"), new ToolStripMenuItem("Descending"), new ToolStripSeparator(), new ToolStripMenuItem("More...") });
        menu.Items.Add("Refresh");
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Paste");
        menu.Items.Add("Paste shortcut");
        menu.Items.Add("Undo Delete");
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.AddWithIcon("Give access to", null, new ToolStripItem[] { new ToolStripMenuItem("Remove access"), new ToolStripMenuItem("Homegroup (view)"), new ToolStripMenuItem("Homegroup (view and edit)"), new ToolStripSeparator(), new ToolStripMenuItem("Specific people...") });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.AddWithIcon("New", null, new ToolStripItem[] { new ToolStripMenuItem("Folder"), new ToolStripMenuItem("Shortcut"), new ToolStripSeparator(), new ToolStripMenuItem("Bitmap image"), new ToolStripMenuItem("Contact"), new ToolStripMenuItem("Rich Text Format"), new ToolStripMenuItem("Text Document") });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Properties");
        return menu;
    }

    private ContextMenuStrip BuildFolderMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add("Open"); m.Items.Add("Open in new window"); m.Items.Add("Pin to Quick Access");
        m.Items.Add("Take Ownership"); m.Items.Add("7-Zip"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Give access to"); m.Items.Add("Restore"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Send to"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Cut"); m.Items.Add("Copy"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Create shortcut"); m.Items.Add("Delete"); m.Items.Add("Rename");
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Properties");
        return m;
    }

    private ContextMenuStrip BuildFileMenu()
    {
        var m = new ContextMenuStrip();
        m.Items.Add("Open"); m.Items.Add("Pin"); m.Items.Add("Edit");
        m.Items.Add("Take Ownership"); m.Items.Add("7-Zip"); m.Items.Add("Open With");
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Give access to");
        m.Items.Add("Restore previous versions"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Send to"); m.Items.Add("Cut"); m.Items.Add("Copy");
        m.Items.Add(new ToolStripSeparator()); m.Items.Add("Create shortcut");
        m.Items.Add("Delete"); m.Items.Add("Rename"); m.Items.Add(new ToolStripSeparator());
        m.Items.Add("Properties");
        return m;
    }
}

// ----------------------- Top Toolbar -----------------------
public class TopToolbar : Control
{
    public event EventHandler BackRequested, ForwardRequested, UpRequested;
    public event EventHandler<string> PathChanged;
    private ToolTip toolTip = new ToolTip();
    private CustomButton btnBack, btnForward, btnRecent, btnUp, btnRecentFolders;
    private Panel pathPanel = new Panel { BackColor = Color.White, BorderStyle = BorderStyle.None };
    private TextBox searchBox = new TextBox();

    public TopToolbar()
    {
        Height = 44;
        DoubleBuffered = true;
        BackColor = Color.White;
        Initialize();
    }

    private void Initialize()
    {
        btnBack = new CustomButton { Icon = SystemIconsEx.ArrowLeft, ToolTipText = "Back" };
        btnBack.Click += (s, e) => BackRequested?.Invoke(this, EventArgs.Empty);
        btnForward = new CustomButton { Icon = SystemIconsEx.ArrowRight, ToolTipText = "Forward" };
        btnForward.Click += (s, e) => ForwardRequested?.Invoke(this, EventArgs.Empty);
        btnRecent = new CustomButton { Icon = SystemIconsEx.Recent, ToolTipText = "Recent locations" };
        btnUp = new CustomButton { Icon = SystemIconsEx.UpFolder, ToolTipText = "Up" };
        btnUp.Click += (s, e) => UpRequested?.Invoke(this, EventArgs.Empty);
        btnRecentFolders = new CustomButton { Icon = SystemIconsEx.Folder, ToolTipText = "Recent folders" };
        searchBox.Text = "Search";
        searchBox.GotFocus += (s, e) => { if (searchBox.Text == "Search") searchBox.Text = ""; };
        searchBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(searchBox.Text)) searchBox.Text = "Search"; };

        Controls.Add(btnBack);
        Controls.Add(btnForward);
        Controls.Add(btnRecent);
        Controls.Add(btnUp);
        Controls.Add(pathPanel);
        Controls.Add(btnRecentFolders);
        Controls.Add(searchBox);

        LayoutControls();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutControls();
    }

    private void LayoutControls()
    {
        if (Width == 0) return;
        int y = (Height - 22) / 2;
        btnBack.Location = new Point(0, y); btnBack.Size = new Size(28, 22);
        btnForward.Location = new Point(28, y); btnForward.Size = new Size(28, 22);
        btnRecent.Location = new Point(56, y); btnRecent.Size = new Size(28, 22);
        btnUp.Location = new Point(84, y); btnUp.Size = new Size(28, 22);
        int right = Width - 12;
        searchBox.Size = new Size(202, 22);
        searchBox.Top = y;
        searchBox.Left = right - 202;
        btnRecentFolders.Size = new Size(28, 22);
        btnRecentFolders.Location = new Point(searchBox.Left - 40, y);
        pathPanel.Height = 22;
        pathPanel.Top = y;
        pathPanel.Left = 112;
        pathPanel.Width = Math.Max(50, btnRecentFolders.Left - 122);
    }

    public void SetPath(string path)
    {
        pathPanel.Controls.Clear();
        int x = 2;
        var icon = new PictureBox
        {
            Image = (SystemIconsEx.GetFolderIcon(false) ?? SystemIcons.Application).ToBitmap(),
            Size = new Size(16, 16),
            Location = new Point(x, 3),
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        pathPanel.Controls.Add(icon);
        x += 20;

        string root = Path.GetPathRoot(path);
        string remainder = path.Substring(root.Length);
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(root))
            parts.Add(root.TrimEnd(Path.DirectorySeparatorChar));
        parts.AddRange(remainder.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries));

        for (int i = 0; i < parts.Count; i++)
        {
            string part = parts[i];
            string fullPath;
            if (i == 0 && part.Length == 2 && part[1] == ':')
                fullPath = part + "\\";
            else
                fullPath = root + string.Join(Path.DirectorySeparatorChar.ToString(), parts.GetRange(0, i + 1));

            var btn = new CustomButton
            {
                Text = (i == 0 && part.Length == 2 && part[1] == ':') ? part + "\\" : part,
                Tag = fullPath,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Height = 22,
                Padding = new Padding(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 230, 255);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(150, 200, 255);
            btn.Click += (s, e) => PathChanged?.Invoke(this, ((Button)s).Tag.ToString());
            btn.Location = new Point(x, 0);
            using (Graphics g = btn.CreateGraphics())
            {
                SizeF sz = g.MeasureString(btn.Text, btn.Font);
                btn.Width = (int)sz.Width + btn.Padding.Horizontal + 2;
            }
            pathPanel.Controls.Add(btn);
            x += btn.Width;

            if (i < parts.Count - 1)
            {
                var sep = new Label { Text = ">", ForeColor = Color.Gray, AutoSize = true };
                sep.Location = new Point(x, 0);
                pathPanel.Controls.Add(sep);
                x += sep.Width;
            }
        }
    }
}

// ----------------------- Lower Toolbar -----------------------
public class LowerToolbar : Control
{
    public event EventHandler NewFolderRequested;
    public new event EventHandler HelpRequested;  // hides Control.HelpRequested
    public event EventHandler<bool> PreviewPaneToggled;
    public event EventHandler<string> ViewChanged;
    private CustomButton btnOrganize, btnNewFolder, btnChangeView, btnPreviewPane, btnHelp;
    private ContextMenuStrip organizeMenu, viewMenu;

    public LowerToolbar()
    {
        Height = 31;
        DoubleBuffered = true;
        BackColor = ColorTranslator.FromHtml("#F5F6F7");
        Initialize();
        LayoutButtons();
    }

    private void Initialize()
    {
        btnOrganize = new CustomButton { Text = "Organize", Size = new Size(91, 26), ToolTipText = "Organize" };
        organizeMenu = new ContextMenuStrip();
        organizeMenu.Items.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("Cut"), new ToolStripMenuItem("Copy"),
            new ToolStripMenuItem("Paste"), new ToolStripMenuItem("Undo"),
            new ToolStripMenuItem("Redo"), new ToolStripSeparator(),
            new ToolStripMenuItem("Select All"), new ToolStripSeparator(),
            new ToolStripMenuItem("Layout"), new ToolStripMenuItem("Options"),
            new ToolStripSeparator(), new ToolStripMenuItem("Delete"),
            new ToolStripMenuItem("Rename"), new ToolStripMenuItem("Remove Properties"),
            new ToolStripMenuItem("Properties"), new ToolStripSeparator(),
            new ToolStripMenuItem("Close")
        });
        btnOrganize.Click += (s, e) => organizeMenu.Show(btnOrganize, new Point(0, btnOrganize.Height));
        Controls.Add(btnOrganize);

        btnNewFolder = new CustomButton { Text = "New folder", Icon = SystemIconsEx.NewFolder, Size = new Size(88, 26), ToolTipText = "New folder" };
        btnNewFolder.Click += (s, e) => NewFolderRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(btnNewFolder);

        btnChangeView = new CustomButton { Icon = SystemIconsEx.ViewDetails, Size = new Size(27, 26), ToolTipText = "Change your view" };
        viewMenu = new ContextMenuStrip();
        viewMenu.Items.AddRange(new ToolStripItem[] {
            new ToolStripMenuItem("Extra large icons"), new ToolStripMenuItem("Large icons"),
            new ToolStripMenuItem("Medium icons"), new ToolStripMenuItem("Small icons"),
            new ToolStripMenuItem("List"), new ToolStripMenuItem("Details"),
            new ToolStripMenuItem("Tiles"), new ToolStripMenuItem("Content")
        });
        btnChangeView.Click += (s, e) => viewMenu.Show(btnChangeView, new Point(0, btnChangeView.Height));
        Controls.Add(btnChangeView);

        btnPreviewPane = new CustomButton { Icon = SystemIconsEx.PreviewPane, Size = new Size(28, 26), ToolTipText = "Show the preview pane" };
        Controls.Add(btnPreviewPane);

        btnHelp = new CustomButton { Icon = SystemIconsEx.Help, Size = new Size(28, 26), ToolTipText = "Get help" };
        btnHelp.Click += (s, e) => HelpRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(btnHelp);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutButtons();
    }

    private void LayoutButtons()
    {
        if (Width == 0) return;
        int x = 3;
        btnOrganize.Location = new Point(x, 3);
        btnNewFolder.Location = new Point(x + 91, 3);
        int right = Width - 10;
        btnHelp.Location = new Point(right - 28, 3);
        btnPreviewPane.Location = new Point(btnHelp.Left - 36, 3);
        btnChangeView.Location = new Point(btnPreviewPane.Left - 35, 3);
    }
}

// ----------------------- Folder Tree (Left Panel) -----------------------
public class FolderTree : Control
{
    public event EventHandler<string> FolderSelected;
    private TreeViewEx treeView;

    public FolderTree()
    {
        treeView = new TreeViewEx { Dock = DockStyle.Fill };
        Controls.Add(treeView);
        treeView.AfterSelect += (s, path) => FolderSelected?.Invoke(this, path);
        BuildStaticTree();
    }

    private void BuildStaticTree()
    {
        // Quick Access
        var quickAccess = new TreeNodeEx("Quick Access", null, true);
        quickAccess.IsExpanded = true;
        quickAccess.Children.Add(new TreeNodeEx("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), true));
        quickAccess.Children.Add(new TreeNodeEx("Downloads", KnownFolders.GetPath(KnownFolderIds.Downloads), true));
        quickAccess.Children.Add(new TreeNodeEx("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), true));
        quickAccess.Children.Add(new TreeNodeEx("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), true));
        treeView.Nodes.Add(quickAccess);

        // This PC
        var thisPC = new TreeNodeEx("This PC", null, true);
        thisPC.IsExpanded = true;
        thisPC.Children.Add(new TreeNodeEx("3D Objects", KnownFolders.GetPath(KnownFolderIds.Objects3D), true));
        thisPC.Children.Add(new TreeNodeEx("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), true));
        thisPC.Children.Add(new TreeNodeEx("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), true));
        thisPC.Children.Add(new TreeNodeEx("Downloads", KnownFolders.GetPath(KnownFolderIds.Downloads), true));
        thisPC.Children.Add(new TreeNodeEx("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), true));
        thisPC.Children.Add(new TreeNodeEx("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), true));
        thisPC.Children.Add(new TreeNodeEx("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), true));
        // Add drives
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                thisPC.Children.Add(new TreeNodeEx(drive.Name, drive.RootDirectory.FullName, true));
        }
        catch { }
        treeView.Nodes.Add(thisPC);
    }

    public void ExpandToPath(string path) => treeView.ExpandToPath(path);
}

// ----------------------- TreeViewEx (GDI+ drawn, virtualized) -----------------------
public class TreeViewEx : Control
{
    public List<TreeNodeEx> Nodes = new List<TreeNodeEx>();
    public event EventHandler<string> AfterSelect;
    private VScrollBar vScroll = new VScrollBar();
    private int itemHeight = 20;
    private int scrollOffset;
    private TreeNodeEx selectedNode;
    private int maxY;
    private ConcurrentDictionary<string, Icon> iconCache = new ConcurrentDictionary<string, Icon>();

    public TreeViewEx()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        vScroll.Dock = DockStyle.Right;
        vScroll.ValueChanged += (s, e) => { scrollOffset = vScroll.Value; Invalidate(); };
        Controls.Add(vScroll);
    }

    public void ExpandToPath(string path)
    {
        foreach (var node in Nodes)
            if (path.StartsWith(node.FullPath ?? "", StringComparison.OrdinalIgnoreCase))
                ExpandNode(node, path);
        Invalidate();
    }

    private bool ExpandNode(TreeNodeEx node, string path)
    {
        if (node.FullPath != null && node.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            node.IsExpanded = true;
            if (!node.ChildrenLoaded && node.IsFolder)
                LoadChildrenAsync(node);
            return true;
        }
        if (!node.ChildrenLoaded && node.IsFolder)
            LoadChildrenAsync(node);
        node.IsExpanded = true;
        foreach (var child in node.Children)
            if (ExpandNode(child, path))
                return true;
        node.IsExpanded = false;
        return false;
    }

    private async void LoadChildrenAsync(TreeNodeEx node)
    {
        node.Children.Clear();
        try
        {
            await Task.Run(() =>
            {
                var dirs = Directory.GetDirectories(node.FullPath);
                foreach (var dir in dirs)
                    node.Children.Add(new TreeNodeEx(Path.GetFileName(dir), dir, true));
            });
        }
        catch { }
        node.ChildrenLoaded = true;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int y = -scrollOffset;
        maxY = 0;
        DrawNodes(g, Nodes, ref y, 0);
        maxY += scrollOffset;
        vScroll.Maximum = Math.Max(0, maxY - Height);
        vScroll.LargeChange = Height;
        vScroll.Visible = vScroll.Maximum > 0;
    }

    private void DrawNodes(Graphics g, List<TreeNodeEx> nodes, ref int y, int indent)
    {
        foreach (var node in nodes)
        {
            if (y + itemHeight < 0) { y += itemHeight; continue; }
            if (y > Height) break;
            Rectangle rect = new Rectangle(0, y, Width - vScroll.Width, itemHeight);
            if (node == selectedNode)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 215)), rect);
                g.DrawString(node.Text, Font, Brushes.White, indent + 40, y + 2);
            }
            else
            {
                g.DrawString(node.Text, Font, Brushes.Black, indent + 40, y + 2);
            }

            if (node.IsFolder)
            {
                Rectangle arrowRect = new Rectangle(indent + 24 - 12, y + 2, 16, 16);
                g.DrawString(node.IsExpanded ? "?" : "?", Font, Brushes.Black, arrowRect);
            }

            Icon icon = node.IsFolder ? GetCachedFolderIcon(false) : null;
            if (icon != null)
                g.DrawIcon(icon, new Rectangle(indent + 24, y + 2, 16, 16));

            y += itemHeight;
            if (node.IsExpanded)
                DrawNodes(g, node.Children, ref y, indent + 16);
        }
        maxY = Math.Max(maxY, y);
    }

    private Icon GetCachedFolderIcon(bool small)
    {
        string key = "folder_" + small;
        if (!iconCache.TryGetValue(key, out var icon))
        {
            icon = SystemIconsEx.GetFolderIcon(small) ?? SystemIcons.Application;
            iconCache[key] = icon;
        }
        return icon;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int y = -scrollOffset;
        TreeNodeEx hit = HitTest(Nodes, ref y, 0, e.Location);
        if (hit != null)
        {
            if (hit.IsFolder && hit.FullPath != null)
            {
                if (!hit.ChildrenLoaded) LoadChildrenAsync(hit);
                hit.IsExpanded = !hit.IsExpanded;
            }
            selectedNode = hit;
            AfterSelect?.Invoke(this, hit.FullPath);
            Invalidate();
        }
    }

    private TreeNodeEx HitTest(List<TreeNodeEx> nodes, ref int y, int indent, Point point)
    {
        foreach (var node in nodes)
        {
            Rectangle rect = new Rectangle(0, y, Width, itemHeight);
            if (rect.Contains(point)) return node;
            y += itemHeight;
            if (node.IsExpanded)
            {
                var child = HitTest(node.Children, ref y, indent + 16, point);
                if (child != null) return child;
            }
        }
        return null;
    }
}

public class TreeNodeEx
{
    public string Text, FullPath;
    public bool IsFolder, IsExpanded, ChildrenLoaded;
    public List<TreeNodeEx> Children = new List<TreeNodeEx>();
    public TreeNodeEx(string text, string fullPath, bool isFolder)
    {
        Text = text; FullPath = fullPath; IsFolder = isFolder;
    }
}

// ----------------------- File ListView (Center) -----------------------
public class FileListView : Control
{
    public string ViewMode { get; set; } = "Details";
    public event EventHandler<string> ItemDoubleClicked, ItemRightClicked;
    public ContextMenuStrip ListContextMenu, FolderContextMenu, FileContextMenu;
    private List<FileSystemItem> items = new List<FileSystemItem>();
    private VScrollBar vScroll = new VScrollBar();
    private int itemHeight = 24;
    private int scrollOffset;
    private int[] columnWidths = new int[] { 300, 180, 120, 100 };
    private string[] columnHeaders = new string[] { "Name", "Date modified", "Type", "Size" };
    private int headerHeight = 24;
    private int selectedIndex = -1;
    private bool isResizing;
    private int resizingColumn = -1;
    private int resizeStartX;

    public FileListView()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        vScroll.Dock = DockStyle.Right;
        vScroll.ValueChanged += (s, e) => { scrollOffset = vScroll.Value; Invalidate(); };
        Controls.Add(vScroll);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public async Task LoadPathAsync(string path)
    {
        items.Clear();
        await Task.Run(() =>
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(path))
                    items.Add(new FileSystemItem(dir, true));
                foreach (var file in Directory.EnumerateFiles(path))
                    items.Add(new FileSystemItem(file, false));
            }
            catch { }
        });
        selectedIndex = -1;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Header background white
        g.FillRectangle(Brushes.White, 0, 0, Width, headerHeight);
        int cumulativeX = 0;
        for (int i = 0; i < columnWidths.Length; i++)
        {
            Rectangle headerRect = new Rectangle(cumulativeX, 0, columnWidths[i], headerHeight);
            g.DrawString(columnHeaders[i], Font, Brushes.Black, headerRect.Left + 4, 4);
            int separatorX = cumulativeX + columnWidths[i];
            g.DrawLine(Pens.Gray, separatorX, 0, separatorX, headerHeight - 1);
            cumulativeX += columnWidths[i];
        }

        int y = headerHeight - scrollOffset;
        int index = 0;
        foreach (var item in items)
        {
            if (y + itemHeight < headerHeight) { y += itemHeight; index++; continue; }
            if (y > Height) break;
            Rectangle rect = new Rectangle(0, y, Width - vScroll.Width, itemHeight);
            if (index == selectedIndex)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 120, 215)), rect);
                DrawItemText(g, item, y, true);
            }
            else
            {
                g.FillRectangle(index % 2 == 0 ? Brushes.White : Brushes.WhiteSmoke, rect);
                DrawItemText(g, item, y, false);
            }
            y += itemHeight;
            index++;
        }

        vScroll.Maximum = Math.Max(0, items.Count * itemHeight - (Height - headerHeight));
        vScroll.LargeChange = Math.Max(1, Height - headerHeight);
        vScroll.Visible = vScroll.Maximum > 0;
    }

    private void DrawItemText(Graphics g, FileSystemItem item, int y, bool selected)
    {
        Brush textBrush = selected ? Brushes.White : Brushes.Black;
        int cumulativeX = 0;
        Icon icon = item.IconSmall ?? SystemIconsEx.GetFolderIcon(true) ?? SystemIcons.Application;
        if (icon != null)
            g.DrawIcon(icon, new Rectangle(cumulativeX + 2, y + 4, 16, 16));
        g.DrawString(item.Name, Font, textBrush, cumulativeX + 22, y + 4);
        cumulativeX += columnWidths[0];
        g.DrawString(item.LastWriteTime.ToString("g"), Font, textBrush, cumulativeX + 4, y + 4);
        cumulativeX += columnWidths[1];
        g.DrawString(item.Type, Font, textBrush, cumulativeX + 4, y + 4);
        cumulativeX += columnWidths[2];
        if (!item.IsFolder)
            g.DrawString(FormatSize(item.Size), Font, textBrush, cumulativeX + 4, y + 4);
    }

    private string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB";
        double gb = mb / 1024.0;
        return $"{gb:F1} GB";
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Y < headerHeight)
        {
            int cumulativeX = 0;
            for (int i = 0; i < columnWidths.Length - 1; i++)
            {
                cumulativeX += columnWidths[i];
                if (Math.Abs(e.X - cumulativeX) <= 3)
                {
                    isResizing = true;
                    resizingColumn = i;
                    resizeStartX = e.X;
                    return;
                }
            }
        }
        else
        {
            int index = (e.Y - headerHeight + scrollOffset) / itemHeight;
            if (index >= 0 && index < items.Count)
            {
                selectedIndex = index;
                Invalidate();
                if (e.Button == MouseButtons.Right)
                    ItemRightClicked?.Invoke(this, items[index].FullPath);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (isResizing && resizingColumn >= 0)
        {
            int delta = e.X - resizeStartX;
            int newWidth = columnWidths[resizingColumn] + delta;
            if (newWidth > 50)
            {
                columnWidths[resizingColumn] = newWidth;
                resizeStartX = e.X;
                Invalidate();
            }
        }
        else
        {
            Cursor = Cursors.Default;
            if (e.Y < headerHeight)
            {
                int cumulativeX = 0;
                for (int i = 0; i < columnWidths.Length - 1; i++)
                {
                    cumulativeX += columnWidths[i];
                    if (Math.Abs(e.X - cumulativeX) <= 3)
                    {
                        Cursor = Cursors.VSplit;
                        break;
                    }
                }
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        isResizing = false;
        resizingColumn = -1;
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);
        if (selectedIndex >= 0 && selectedIndex < items.Count)
            ItemDoubleClicked?.Invoke(this, items[selectedIndex].FullPath);
    }
}

public class FileSystemItem
{
    public string FullPath;
    public bool IsFolder;
    public string Name => Path.GetFileName(FullPath);
    public DateTime LastWriteTime => IsFolder ? Directory.GetLastWriteTime(FullPath) : File.GetLastWriteTime(FullPath);
    public string Type => IsFolder ? "File folder" : Path.GetExtension(FullPath).TrimStart('.').ToUpper() + " File";
    public long Size => IsFolder ? 0 : new FileInfo(FullPath).Length;
    private Icon iconSmall;
    public Icon IconSmall
    {
        get
        {
            if (iconSmall == null)
                iconSmall = SystemIconsEx.GetIconForPath(FullPath, true);
            return iconSmall;
        }
    }
    public FileSystemItem(string path, bool isFolder) { FullPath = path; IsFolder = isFolder; }
}

// ----------------------- Custom Button (GDI+, improved) -----------------------
public class CustomButton : Button
{
    public string ToolTipText { get; set; }
    private ToolTip tip = new ToolTip();
    public Icon Icon { get; set; }

    public CustomButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 230, 255);
        FlatAppearance.MouseDownBackColor = Color.FromArgb(150, 200, 255);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (Icon != null)
            g.DrawIcon(Icon, new Rectangle(4, (Height - 16) / 2, 16, 16));
        Rectangle textRect = ClientRectangle;
        if (Icon != null)
        {
            textRect.X += 22;
            textRect.Width -= 22;
        }
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!string.IsNullOrEmpty(ToolTipText))
            tip.SetToolTip(this, ToolTipText);
    }
}

// ----------------------- System Icons Helper (with caching) -----------------------
public static class SystemIconsEx
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "SHGetStockIconInfo")]
    private static extern int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

    [StructLayout(LayoutKind.Sequential)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint SHGSI_ICON = 0x100;

    private const uint SIID_ARROWLEFT = 83, SIID_ARROWRIGHT = 84;
    private const uint SIID_RECENT = 0x65, SIID_UPFOLDER = 0x66;
    private const uint SIID_FOLDER = 3, SIID_NEWFOLDER = 0x72;
    private const uint SIID_VIEWDETAILS = 0x81, SIID_PREVIEWPANE = 0x80;
    private const uint SIID_HELP = 0x17;

    private static ConcurrentDictionary<string, Icon> iconCache = new ConcurrentDictionary<string, Icon>();

    public static Icon ArrowLeft => GetStockIcon(SIID_ARROWLEFT);
    public static Icon ArrowRight => GetStockIcon(SIID_ARROWRIGHT);
    public static Icon Recent => GetStockIcon(SIID_RECENT);
    public static Icon UpFolder => GetStockIcon(SIID_UPFOLDER);
    public static Icon Folder => GetStockIcon(SIID_FOLDER);
    public static Icon NewFolder => GetStockIcon(SIID_NEWFOLDER);
    public static Icon ViewDetails => GetStockIcon(SIID_VIEWDETAILS);
    public static Icon PreviewPane => GetStockIcon(SIID_PREVIEWPANE);
    public static Icon Help => GetStockIcon(SIID_HELP);

    private static Icon GetStockIcon(uint id)
    {
        string key = "stock_" + id;
        if (iconCache.TryGetValue(key, out var icon))
            return icon;
        SHSTOCKICONINFO info = new SHSTOCKICONINFO();
        info.cbSize = (uint)Marshal.SizeOf(info);
        if (SHGetStockIconInfo(id, SHGSI_ICON, ref info) == 0 && info.hIcon != IntPtr.Zero)
        {
            icon = Icon.FromHandle(info.hIcon).Clone() as Icon;
            iconCache[key] = icon;
            return icon;
        }
        return null;
    }

    public static Icon GetIconForPath(string path, bool small)
    {
        string key = "path_" + small + "_" + path;
        if (iconCache.TryGetValue(key, out var cached))
            return cached;
        try
        {
            SHFILEINFO info = new SHFILEINFO();
            uint flags = SHGFI_ICON | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            IntPtr res = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), flags);
            if (res != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                var icon = Icon.FromHandle(info.hIcon).Clone() as Icon;
                iconCache[key] = icon;
                return icon;
            }
        }
        catch { }
        return null;
    }

    public static Icon GetFolderIcon(bool small = false)
    {
        string key = "folder_" + small;
        if (iconCache.TryGetValue(key, out var cached))
            return cached;
        try
        {
            SHFILEINFO info = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (small ? SHGFI_SMALLICON : SHGFI_LARGEICON);
            SHGetFileInfo(@"C:\", FILE_ATTRIBUTE_DIRECTORY, ref info, (uint)Marshal.SizeOf(info), flags);
            if (info.hIcon != IntPtr.Zero)
            {
                var icon = Icon.FromHandle(info.hIcon).Clone() as Icon;
                iconCache[key] = icon;
                return icon;
            }
        }
        catch { }
        return SystemIcons.Application;
    }
}

// ----------- Known Folders (fixed with static class) -----------
public static class KnownFolderIds
{
    public static readonly Guid Downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
    public static readonly Guid Objects3D = new Guid("31C0DD25-9439-4F12-BF41-7FF4EDA38722");
}

public static class KnownFolders
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

    public static string GetPath(Guid folderId)
    {
        IntPtr outPath;
        if (SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out outPath) == 0)
        {
            string path = Marshal.PtrToStringUni(outPath);
            Marshal.FreeCoTaskMem(outPath);
            return path;
        }
        return string.Empty;
    }
}

// ----------- Extension for adding submenu with icon -----------
public static class ToolStripExtensions
{
    public static ToolStripMenuItem AddWithIcon(this ToolStripItemCollection items, string text, Image image, ToolStripItem[] children)
    {
        var item = new ToolStripMenuItem(text, image);
        item.DropDownItems.AddRange(children);
        items.Add(item);
        return item;
    }
}