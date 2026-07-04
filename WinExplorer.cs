// ═══════════════════════════════════════════════════════════════════════════════
//  WINDOWS EXPLORER  –  Pixel-Perfect GDI+ Recreation
//  C# .NET Framework 4.8  |  Single File  |  Custom GDI+ Rendering
// ───────────────────────────────────────────────────────────────────────────────
//  ICONS  →  Place 24×24 PNG files in:  <exe-dir>\Explorer.exe\icons\Win10\
//
//  Required icon names (no extension):
//    backward_arrow   forward_arrow   up_arrow   search
//    change_view      more_options    preview_pane   help
//    quick_access     this_pc         network
//    desktop          downloads       documents      pictures
//    music            videos          3dobjects      drives
//    folder           file
//
//  Changes vs v1:
//    • Organize & New Folder buttons: text-only (no icon)
//    • All dropdown / context menus: no icons; image-margin kept for spacing
//    • Organize dropdown: guaranteed to fire (DoLayout forced in ctor)
//    • Column headers: white by default, #D9EBF9 on hover
//    • Column headers: fully resizable via drag (cursor changes at divider)
// ═══════════════════════════════════════════════════════════════════════════════
 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
 
namespace WinExplorer
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Entry Point
    // ──────────────────────────────────────────────────────────────────────────
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
 
    // ──────────────────────────────────────────────────────────────────────────
    //  Theme
    // ──────────────────────────────────────────────────────────────────────────
    static class Th
    {
        public static readonly Color Bg          = Color.FromArgb(240, 240, 240);
        public static readonly Color SelFill     = Color.FromArgb(204, 232, 255);  // #CCE8FF
        public static readonly Color SelBorder   = Color.FromArgb(153, 209, 255);  // #99D1FF
        public static readonly Color HoverFill   = Color.FromArgb(229, 241, 251);
        public static readonly Color HoverBord   = Color.FromArgb(0, 120, 215);
        public static readonly Color PressFill   = Color.FromArgb(204, 228, 247);
        public static readonly Color PressBord   = Color.FromArgb(0, 84, 153);
        public static readonly Color SepColor    = Color.FromArgb(208, 208, 208);
        public static readonly Color Border      = Color.FromArgb(180, 180, 180);
        public static readonly Color PaneSep     = Color.FromArgb(213, 213, 213);
        public static readonly Color HdrHover    = Color.FromArgb(217, 235, 249);  // #D9EBF9
        public static readonly Color TreeBg      = Color.White;
        public static readonly Color ContentBg   = Color.White;
        public static readonly Color TxtColor    = Color.FromArgb(0, 0, 0);
        public static readonly Color TxtDisabled = Color.FromArgb(130, 130, 130);
 
        public static readonly Font UiFont  = new Font("Segoe UI", 9f);
        public static readonly Font UiSmall = new Font("Segoe UI", 8f);
        public static readonly Font UiBold  = new Font("Segoe UI", 9f, FontStyle.Bold);
 
        public static void DrawHover(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(HoverFill)) g.FillRectangle(b, r);
            using (var p = new Pen(HoverBord))
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        public static void DrawPress(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(PressFill)) g.FillRectangle(b, r);
            using (var p = new Pen(PressBord))
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        public static void DrawSel(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(SelFill)) g.FillRectangle(b, r);
            using (var p = new Pen(SelBorder))
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
 
        /// Small solid downward triangle ▼
        public static void DrawDropArrow(Graphics g, int cx, int cy, Color col)
        {
            Point[] pts =
            {
                new Point(cx - 3, cy - 2),
                new Point(cx + 3, cy - 2),
                new Point(cx,     cy + 2)
            };
            using (var b = new SolidBrush(col)) g.FillPolygon(b, pts);
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  Icon Cache / Loader
    // ──────────────────────────────────────────────────────────────────────────
    static class Icons
    {
        static readonly string Dir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Explorer.exe", "icons", "Win10");
 
        static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
 
        public static Image Get(string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;
            string file = Path.Combine(Dir, name + ".png");
            Image img = null;
            if (File.Exists(file))
            {
                try
                {
                    var raw = Image.FromFile(file);
                    if (raw.Width == 24 && raw.Height == 24) { img = raw; }
                    else
                    {
                        var bmp = new Bitmap(24, 24);
                        using (var gg = Graphics.FromImage(bmp))
                        {
                            gg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            gg.DrawImage(raw, 0, 0, 24, 24);
                        }
                        raw.Dispose(); img = bmp;
                    }
                }
                catch { img = null; }
            }
            if (img == null) img = MakePlaceholder(name);
            Cache[name] = img;
            return img;
        }
 
        static Image MakePlaceholder(string name)
        {
            var bmp = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode    = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
 
                bool isFolder = IsFolder(name);
 
                if (isFolder)
                {
                    using (var bf = new SolidBrush(Color.FromArgb(255, 213, 84)))
                    { g.FillRectangle(bf, 2, 9, 20, 12); g.FillRectangle(bf, 2, 6, 8, 4); }
                    using (var pf = new Pen(Color.FromArgb(190, 150, 30), 1f))
                    { g.DrawRectangle(pf, 2, 9, 19, 11); g.DrawRectangle(pf, 2, 6, 7, 3); }
                }
                else if (name.Contains("backward") || name.Contains("back"))
                {
                    using (var p = new Pen(Color.FromArgb(70, 130, 180), 2f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
                    { g.DrawLine(p, 17, 12, 7, 12); g.DrawLine(p, 7, 12, 12, 7); g.DrawLine(p, 7, 12, 12, 17); }
                }
                else if (name.Contains("forward"))
                {
                    using (var p = new Pen(Color.FromArgb(70, 130, 180), 2f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
                    { g.DrawLine(p, 7, 12, 17, 12); g.DrawLine(p, 17, 12, 12, 7); g.DrawLine(p, 17, 12, 12, 17); }
                }
                else if (name.Contains("up"))
                {
                    using (var p = new Pen(Color.FromArgb(70, 130, 180), 2f) { EndCap = LineCap.Round, StartCap = LineCap.Round })
                    { g.DrawLine(p, 12, 17, 12, 7); g.DrawLine(p, 12, 7, 7, 12); g.DrawLine(p, 12, 7, 17, 12); }
                }
                else if (name == "search")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 2f))
                    { g.DrawEllipse(p, 4, 4, 12, 12); g.DrawLine(p, 14, 14, 19, 19); }
                }
                else if (name == "preview_pane")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    { g.DrawRectangle(p, 3, 4, 18, 16); g.DrawLine(p, 12, 4, 12, 20); }
                }
                else if (name == "help")
                {
                    using (var p = new Pen(Color.FromArgb(0, 102, 204), 2f)) g.DrawEllipse(p, 2, 2, 19, 19);
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("?", new Font("Segoe UI", 10f, FontStyle.Bold),
                            new SolidBrush(Color.FromArgb(0, 102, 204)), new RectangleF(0, 0, 24, 24), fmt);
                }
                else if (name == "network")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    {
                        g.DrawEllipse(p, 8, 3, 8, 6);
                        g.DrawEllipse(p, 2, 14, 6, 6);
                        g.DrawEllipse(p, 16, 14, 6, 6);
                        g.DrawLine(p, 12, 9, 5, 14); g.DrawLine(p, 12, 9, 19, 14);
                    }
                }
                else if (name == "this_pc")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    {
                        g.DrawRectangle(p, 3, 4, 18, 12);
                        g.DrawLine(p, 10, 16, 10, 19); g.DrawLine(p, 14, 16, 14, 19);
                        g.DrawLine(p, 7, 19, 17, 19);
                    }
                }
                else if (name == "change_view" || name == "more_options")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    {
                        g.DrawRectangle(p, 2, 3, 8, 7);   g.DrawRectangle(p, 14, 3, 8, 7);
                        g.DrawRectangle(p, 2, 14, 8, 7);  g.DrawRectangle(p, 14, 14, 8, 7);
                    }
                }
                else if (name == "quick_access")
                {
                    Point[] star = GetStarPoints(12, 12, 9, 4, 5);
                    using (var b = new SolidBrush(Color.FromArgb(255, 185, 0))) g.FillPolygon(b, star);
                }
                else if (name == "file")
                {
                    using (var p = new Pen(Color.FromArgb(80, 80, 80), 1.5f))
                    {
                        g.DrawPolygon(p, new[] { new Point(5,3), new Point(15,3), new Point(19,7), new Point(19,21), new Point(5,21) });
                        g.DrawLine(p, 15, 3, 15, 7); g.DrawLine(p, 15, 7, 19, 7);
                    }
                }
                else
                {
                    using (var p = new Pen(Color.FromArgb(130, 130, 130), 1f))
                        g.DrawRectangle(p, 3, 3, 17, 17);
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(name.Length > 0 ? name[0].ToString().ToUpper() : "?",
                            new Font("Segoe UI", 7f), Brushes.Gray, new RectangleF(0, 0, 24, 24), fmt);
                }
            }
            return bmp;
        }
 
        static bool IsFolder(string n) =>
            n == "folder" || n == "quick_access" || n == "desktop" || n == "downloads" ||
            n == "documents" || n == "pictures" || n == "music" || n == "videos" ||
            n == "3dobjects" || n == "drives" || n == "this_pc";
 
        static Point[] GetStarPoints(int cx, int cy, int outerR, int innerR, int np)
        {
            var pts = new Point[np * 2];
            double step = Math.PI / np;
            for (int i = 0; i < np * 2; i++)
            {
                double a = i * step - Math.PI / 2;
                int r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new Point((int)(cx + r * Math.Cos(a)), (int)(cy + r * Math.Sin(a)));
            }
            return pts;
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  Data Models
    // ──────────────────────────────────────────────────────────────────────────
    enum SortCol  { Name, Date, Type, Size }
    enum SortDir  { Asc, Desc }
    enum ViewMode { Details, LargeIcons, MediumIcons, SmallIcons, List,
                    ExtraLargeIcons, Tiles, Content }
 
    class TreeNode2
    {
        public string Label;
        public string Path;
        public string IconName;
        public bool   Expanded;
        public bool   IsVirtual;
        public bool   IsRoot;
        public int    Level;
        public List<TreeNode2> Children = new List<TreeNode2>();
        public TreeNode2 Parent;
        public bool HasChildren;
        public Rectangle Bounds;
    }
 
    class ContentItem
    {
        public string   Name;
        public string   FullPath;
        public DateTime DateModified;
        public string   ItemType;
        public long     Size;
        public bool     IsDirectory;
        public bool     Selected;
 
        public string SizeStr =>
            IsDirectory ? "" :
            Size < 1024       ? $"{Size} B" :
            Size < 1_048_576  ? $"{Size / 1024.0:F1} KB" :
                                $"{Size / 1_048_576.0:F1} MB";
 
        public string DateStr => DateModified == default ? "" :
            DateModified.ToString("M/d/yyyy h:mm tt");
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  Menu Renderer – no icons, image-margin column still visible for spacing
    // ──────────────────────────────────────────────────────────────────────────
    class ExplorerMenuRenderer : ToolStripProfessionalRenderer
    {
        public ExplorerMenuRenderer() : base(new ExplorerColorTable()) { }
 
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Available) return;
            if (e.Item.Selected && e.Item.Enabled)
            {
                e.Graphics.Clear(Color.White);
                var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height - 1);
                Th.DrawSel(e.Graphics, r);
            }
            else e.Graphics.Clear(Color.White);
        }
 
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Th.SepColor))
                e.Graphics.DrawLine(p, 28, y, e.Item.Width - 4, y);
        }
 
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Color.Black : Th.TxtDisabled;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            base.OnRenderItemText(e);
        }
 
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.Clear(Color.White);
 
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var p = new Pen(Th.Border))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
 
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? Color.Black : Th.TxtDisabled;
            base.OnRenderArrow(e);
        }
 
        // Keep image margin but draw nothing for items without images
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Draw the light grey image column to the left
            using (var b = new SolidBrush(Color.FromArgb(245, 245, 245)))
                e.Graphics.FillRectangle(b, new Rectangle(0, 0, 24, e.ToolStrip.Height));
        }
 
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            // Items intentionally have no images; nothing to draw
        }
    }
 
    class ExplorerColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder                    => Th.Border;
        public override Color MenuItemBorder                => Color.Transparent;
        public override Color MenuItemSelected              => Th.SelFill;
        public override Color MenuItemSelectedGradientBegin => Th.SelFill;
        public override Color MenuItemSelectedGradientEnd   => Th.SelFill;
        public override Color ToolStripDropDownBackground   => Color.White;
        public override Color ImageMarginGradientBegin      => Color.FromArgb(245, 245, 245);
        public override Color ImageMarginGradientMiddle     => Color.FromArgb(245, 245, 245);
        public override Color ImageMarginGradientEnd        => Color.FromArgb(245, 245, 245);
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  Helper: build a menu item (no image, but margin kept)
    // ──────────────────────────────────────────────────────────────────────────
    static class MenuHelper
    {
        static readonly ExplorerMenuRenderer Renderer = new ExplorerMenuRenderer();
 
        public static ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip { Renderer = Renderer };
            return m;
        }
 
        public static ToolStripMenuItem Item(string text, bool hasSub = false)
        {
            return new ToolStripMenuItem(text) { Font = Th.UiFont };
        }
 
        public static ToolStripMenuItem Sub(string text)
        {
            var m = new ToolStripMenuItem(text) { Font = Th.UiFont };
            return m;
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  TOP NAVIGATION BAR  (34 px tall)
    //  Back · Forward · ▼ · Up  |  [PathBox]  ▼  |  [12px] [SearchBox 202px] [12px]
    // ──────────────────────────────────────────────────────────────────────────
    class TopNavBar : Panel
    {
        public const int BAR_H = 34;
        const int BTN_Y  = 6;
        const int BTN_H  = 22;
        const int ICON   = 16;
 
        Rectangle _rBack, _rFwd, _rRecLoc, _rUp, _rRecFold;
 
        enum HBtn { None, Back, Fwd, RecLoc, Up, RecFold }
        HBtn _hov = HBtn.None, _prs = HBtn.None;
 
        bool _backEnabled, _fwdEnabled;
 
        TextBox _pathBox;
        Panel   _searchPanel;
        TextBox _searchBox;
 
        public event EventHandler BackClick;
        public event EventHandler ForwardClick;
        public event EventHandler UpClick;
        public event Action<string> Navigate;
        public event Action<string> SearchChanged;
 
        public string CurrentPath { get => _pathBox.Text; set => _pathBox.Text = value; }
        public bool BackEnabled    { get => _backEnabled; set { _backEnabled = value; Invalidate(); } }
        public bool ForwardEnabled { get => _fwdEnabled; set { _fwdEnabled = value; Invalidate(); } }
 
        public TopNavBar()
        {
            Height = BAR_H;
            Dock   = DockStyle.Top;
            BackColor = Th.Bg;
            DoubleBuffered = true;
 
            _pathBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font        = Th.UiFont,
                BackColor   = Color.White,
                ForeColor   = Th.TxtColor,
            };
            _pathBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Return) { Navigate?.Invoke(_pathBox.Text); e.Handled = true; }
            };
            Controls.Add(_pathBox);
 
            _searchPanel = new Panel { BackColor = Color.White };
            _searchPanel.Paint += DrawSearchPanel;
 
            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font        = Th.UiFont,
                BackColor   = Color.White,
                ForeColor   = Th.TxtDisabled,
                Text        = "Search",
            };
            _searchBox.GotFocus  += (s, e) =>
            {
                if (_searchBox.Text == "Search") { _searchBox.Text = ""; _searchBox.ForeColor = Th.TxtColor; }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (_searchBox.Text == "") { _searchBox.Text = "Search"; _searchBox.ForeColor = Th.TxtDisabled; }
            };
            _searchBox.TextChanged += (s, e) => SearchChanged?.Invoke(_searchBox.Text);
 
            _searchPanel.Controls.Add(_searchBox);
            Controls.Add(_searchPanel);
 
            MouseMove  += OnMM;
            MouseDown  += OnMD;
            MouseUp    += OnMU;
            MouseLeave += (s, e) => { _hov = HBtn.None; Invalidate(); };
            Resize     += (s, e) => DoLayout();
 
            DoLayout();
        }
 
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); DoLayout(); }
 
        void DrawSearchPanel(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            using (var p = new Pen(Th.PaneSep))
                e.Graphics.DrawRectangle(p, 0, 0, _searchPanel.Width - 1, _searchPanel.Height - 1);
            e.Graphics.DrawImage(Icons.Get("search"), _searchPanel.Width - 20, 3, 14, 14);
        }
 
        void DoLayout()
        {
            int x = 2;
            _rBack   = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28;
            _rFwd    = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28;
            _rRecLoc = new Rectangle(x, BTN_Y, 14, BTN_H); x += 14 + 2;
            _rUp     = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28 + 2;
 
            const int searchW = 202, padR = 12, recFW = 14;
            int pathRight = Width - padR - searchW - padR - recFW - 2;
            int pathW     = Math.Max(40, pathRight - x - 1);
 
            _pathBox.SetBounds(x + 3, BTN_Y + 3, pathW - 6, BTN_H - 6);
 
            x += pathW + 2;
            _rRecFold = new Rectangle(x, BTN_Y, recFW, BTN_H); x += recFW + 12;
 
            _searchPanel.SetBounds(x, BTN_Y, searchW, BTN_H);
            _searchBox.SetBounds(3, 3, searchW - 22, BTN_H - 6);
        }
 
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.Bg);
 
            using (var p = new Pen(Th.PaneSep)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
 
            DrawNavBtn(g, _rBack,   "backward_arrow", _backEnabled, HBtn.Back);
            DrawNavBtn(g, _rFwd,    "forward_arrow",  _fwdEnabled,  HBtn.Fwd);
            DrawDropArrowBtn(g, _rRecLoc, HBtn.RecLoc);
            DrawNavBtn(g, _rUp,     "up_arrow",       true,         HBtn.Up);
 
            // Path box border
            int pbX = _pathBox.Left - 3;
            using (var p = new Pen(Th.PaneSep))
                g.DrawRectangle(p, pbX, BTN_Y, _pathBox.Width + 5, BTN_H - 1);
 
            DrawDropArrowBtn(g, _rRecFold, HBtn.RecFold);
        }
 
        void DrawNavBtn(Graphics g, Rectangle r, string icon, bool enabled, HBtn btn)
        {
            if (_prs == btn && enabled) Th.DrawPress(g, r);
            else if (_hov == btn && enabled) Th.DrawHover(g, r);
 
            var img = Icons.Get(icon);
            int ix  = r.X + (r.Width  - ICON) / 2;
            int iy  = r.Y + (r.Height - ICON) / 2;
            if (enabled)
            {
                g.DrawImage(img, ix, iy, ICON, ICON);
            }
            else
            {
                var attrs = new System.Drawing.Imaging.ImageAttributes();
                var cm    = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.35f };
                attrs.SetColorMatrix(cm);
                g.DrawImage(img, new Rectangle(ix, iy, ICON, ICON), 0, 0, ICON, ICON, GraphicsUnit.Pixel, attrs);
            }
        }
 
        void DrawDropArrowBtn(Graphics g, Rectangle r, HBtn btn)
        {
            if (_prs == btn) Th.DrawPress(g, r);
            else if (_hov == btn) Th.DrawHover(g, r);
            Th.DrawDropArrow(g, r.X + r.Width / 2, r.Y + r.Height / 2, Th.TxtColor);
        }
 
        HBtn HitTest(Point pt)
        {
            if (_rBack.Contains(pt))    return HBtn.Back;
            if (_rFwd.Contains(pt))     return HBtn.Fwd;
            if (_rRecLoc.Contains(pt))  return HBtn.RecLoc;
            if (_rUp.Contains(pt))      return HBtn.Up;
            if (_rRecFold.Contains(pt)) return HBtn.RecFold;
            return HBtn.None;
        }
 
        void OnMM(object s, MouseEventArgs e) { var h = HitTest(e.Location); if (h != _hov) { _hov = h; Invalidate(); } }
        void OnMD(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _prs = HitTest(e.Location); Invalidate(); } }
        void OnMU(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = HitTest(e.Location); _prs = HBtn.None;
            if (hit == HBtn.Back && _backEnabled)   BackClick?.Invoke(this, EventArgs.Empty);
            else if (hit == HBtn.Fwd && _fwdEnabled) ForwardClick?.Invoke(this, EventArgs.Empty);
            else if (hit == HBtn.Up)                UpClick?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  COMMAND BAR  (31 px tall)
    //  [3] Organize(91×26) [2] NewFolder(88×26) [flex]
    //      ChangeView(27×26) MoreOptions(19×26) [10] Preview(28×26) [8] Help(28×26) [9]
    //
    //  Organize & New Folder: TEXT ONLY (no icon) – as in real Windows Explorer
    // ──────────────────────────────────────────────────────────────────────────
    class CommandBar : Panel
    {
        public const int BAR_H = 31;
        const int BTN_Y = 3;
        const int BTN_H = 26;
 
        enum HBtn { None, Organize, OrganizeDrop, NewFolder, ChangeView, MoreOptions, Preview, Help }
        HBtn _hov = HBtn.None, _prs = HBtn.None;
 
        Rectangle _rOrg, _rOrgDrop;
        Rectangle _rNF;
        Rectangle _rCV, _rMO;
        Rectangle _rPrev, _rHelp;
 
        ContextMenuStrip _organizeMenu;
        ContextMenuStrip _viewMenu;
 
        public event EventHandler NewFolderClick;
        public event EventHandler PreviewPaneClick;
        public event EventHandler HelpClick;
        public event Action<ViewMode> ViewChanged;
 
        public CommandBar()
        {
            Height = BAR_H;
            Dock   = DockStyle.Top;
            BackColor = Th.Bg;
            DoubleBuffered = true;
 
            // Build menus BEFORE layout so they're ready on first click
            BuildOrganizeMenu();
            BuildViewMenu();
 
            MouseMove  += OnMM;
            MouseDown  += OnMD;
            MouseUp    += OnMU;
            MouseLeave += (s, e) => { _hov = HBtn.None; Invalidate(); };
            Resize     += (s, e) => DoLayout();
 
            // Force initial layout immediately
            DoLayout();
        }
 
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); DoLayout(); }
 
        void DoLayout()
        {
            int x = 3;
            _rOrg     = new Rectangle(x,      BTN_Y, 75, BTN_H);   // main part
            _rOrgDrop = new Rectangle(x + 75, BTN_Y, 16, BTN_H);   // arrow part (total = 91)
            x += 93;  // 91 + 2 gap
 
            _rNF = new Rectangle(x, BTN_Y, 88, BTN_H);
            x += 90;
 
            // Right-side buttons: anchor from right edge
            int rx = Width - 9;
            rx -= 28; _rHelp = new Rectangle(rx, BTN_Y, 28, BTN_H);
            rx -= 8;
            rx -= 28; _rPrev = new Rectangle(rx, BTN_Y, 28, BTN_H);
            rx -= 10;
            rx -= 19; _rMO   = new Rectangle(rx, BTN_Y, 19, BTN_H);
            rx -= 27; _rCV   = new Rectangle(rx, BTN_Y, 27, BTN_H);
        }
 
        // ── Organize menu  (no icons; image-margin gives the indent) ──────────
        void BuildOrganizeMenu()
        {
            _organizeMenu = MenuHelper.NewMenu();
            _organizeMenu.Items.AddRange(new ToolStripItem[]
            {
                MenuHelper.Item("Cut"),
                MenuHelper.Item("Copy"),
                MenuHelper.Item("Paste"),
                MenuHelper.Item("Undo"),
                MenuHelper.Item("Redo"),
                new ToolStripSeparator(),
                MenuHelper.Item("Select All"),
                new ToolStripSeparator(),
                MenuHelper.Item("Layout"),
                MenuHelper.Item("Options"),
                new ToolStripSeparator(),
                MenuHelper.Item("Delete"),
                MenuHelper.Item("Rename"),
                MenuHelper.Item("Remove Properties"),
                MenuHelper.Item("Properties"),
                new ToolStripSeparator(),
                MenuHelper.Item("Close"),
            });
        }
 
        void BuildViewMenu()
        {
            _viewMenu = MenuHelper.NewMenu();
            var modes = new (string label, ViewMode vm)[]
            {
                ("Extra Large Icons", ViewMode.ExtraLargeIcons),
                ("Large Icons",       ViewMode.LargeIcons),
                ("Medium Icons",      ViewMode.MediumIcons),
                ("Small Icons",       ViewMode.SmallIcons),
                ("List",              ViewMode.List),
                ("Details",           ViewMode.Details),
                ("Tiles",             ViewMode.Tiles),
                ("Content",           ViewMode.Content),
            };
            foreach (var (label, vm) in modes)
            {
                var vm2  = vm;
                var item = MenuHelper.Item(label);
                item.Click += (s, e) => ViewChanged?.Invoke(vm2);
                _viewMenu.Items.Add(item);
            }
        }
 
        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.Bg);
 
            using (var p = new Pen(Th.PaneSep)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
 
            // Organize – split button (text only, no icon)
            DrawSplitTextBtn(g, _rOrg, _rOrgDrop, "Organize",
                _hov == HBtn.Organize, _hov == HBtn.OrganizeDrop,
                _prs == HBtn.Organize, _prs == HBtn.OrganizeDrop);
 
            // New Folder – plain text button (no icon)
            DrawTextBtn(g, _rNF, "New folder", _hov == HBtn.NewFolder, _prs == HBtn.NewFolder);
 
            // Change View – icon only
            DrawIconBtn(g, _rCV, "change_view", _hov == HBtn.ChangeView, _prs == HBtn.ChangeView);
 
            // More Options – tiny drop arrow only
            DrawDropOnlyBtn(g, _rMO, _hov == HBtn.MoreOptions, _prs == HBtn.MoreOptions);
 
            // Separator between ChangeView and MoreOptions
            using (var sp = new Pen(Th.SepColor))
                g.DrawLine(sp, _rMO.Left, _rMO.Top + 3, _rMO.Left, _rMO.Bottom - 3);
 
            // Preview pane
            DrawIconBtn(g, _rPrev, "preview_pane", _hov == HBtn.Preview, _prs == HBtn.Preview);
 
            // Help
            DrawIconBtn(g, _rHelp, "help", _hov == HBtn.Help, _prs == HBtn.Help);
        }
 
        // Text-only button (no icon)
        void DrawTextBtn(Graphics g, Rectangle r, string text, bool hover, bool press)
        {
            if (press)      Th.DrawPress(g, r);
            else if (hover) Th.DrawHover(g, r);
 
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, Th.UiFont, Brushes.Black, r, fmt);
        }
 
        // Split button – left=text, right=arrow; separator between them
        void DrawSplitTextBtn(Graphics g, Rectangle rMain, Rectangle rDrop, string text,
                              bool hovMain, bool hovDrop, bool prsMain, bool prsDrop)
        {
            if (prsMain)      Th.DrawPress(g, rMain);
            else if (hovMain) Th.DrawHover(g, rMain);
 
            if (prsDrop)      Th.DrawPress(g, rDrop);
            else if (hovDrop) Th.DrawHover(g, rDrop);
 
            // Draw outer border around the whole split button when either half is hot
            if (hovMain || hovDrop || prsMain || prsDrop)
            {
                var outer = Rectangle.Union(rMain, rDrop);
                using (var p = new Pen(hovMain || hovDrop ? Th.HoverBord : Th.PressBord))
                    g.DrawRectangle(p, outer.X, outer.Y, outer.Width - 1, outer.Height - 1);
            }
 
            // Label in main part
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, Th.UiFont, Brushes.Black, rMain, fmt);
 
            // Divider between main and drop
            using (var sp = new Pen(Th.SepColor))
                g.DrawLine(sp, rDrop.Left, rDrop.Top + 3, rDrop.Left, rDrop.Bottom - 3);
 
            // Arrow
            Th.DrawDropArrow(g, rDrop.X + rDrop.Width / 2, rDrop.Y + rDrop.Height / 2, Th.TxtColor);
        }
 
        void DrawIconBtn(Graphics g, Rectangle r, string icon, bool hover, bool press)
        {
            if (press)      Th.DrawPress(g, r);
            else if (hover) Th.DrawHover(g, r);
            var img = Icons.Get(icon);
            g.DrawImage(img, r.X + (r.Width - 16) / 2, r.Y + (r.Height - 16) / 2, 16, 16);
        }
 
        void DrawDropOnlyBtn(Graphics g, Rectangle r, bool hover, bool press)
        {
            if (press)      Th.DrawPress(g, r);
            else if (hover) Th.DrawHover(g, r);
            Th.DrawDropArrow(g, r.X + r.Width / 2, r.Y + r.Height / 2 + 1, Th.TxtColor);
        }
 
        // ── Hit-test & Mouse ─────────────────────────────────────────────────
        HBtn HitTest(Point pt)
        {
            if (_rOrg.Contains(pt))     return HBtn.Organize;
            if (_rOrgDrop.Contains(pt)) return HBtn.OrganizeDrop;
            if (_rNF.Contains(pt))      return HBtn.NewFolder;
            if (_rCV.Contains(pt))      return HBtn.ChangeView;
            if (_rMO.Contains(pt))      return HBtn.MoreOptions;
            if (_rPrev.Contains(pt))    return HBtn.Preview;
            if (_rHelp.Contains(pt))    return HBtn.Help;
            return HBtn.None;
        }
 
        void OnMM(object s, MouseEventArgs e) { var h = HitTest(e.Location); if (h != _hov) { _hov = h; Invalidate(); } }
        void OnMD(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _prs = HitTest(e.Location); Invalidate(); } }
 
        void OnMU(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = HitTest(e.Location);
            _prs = HBtn.None;
            Invalidate();
 
            switch (hit)
            {
                case HBtn.Organize:
                case HBtn.OrganizeDrop:
                    _organizeMenu.Show(this, new Point(_rOrg.Left, BAR_H));
                    break;
                case HBtn.NewFolder:
                    NewFolderClick?.Invoke(this, EventArgs.Empty);
                    break;
                case HBtn.ChangeView:
                case HBtn.MoreOptions:
                    _viewMenu.Show(this, new Point(_rCV.Left, BAR_H));
                    break;
                case HBtn.Preview:
                    PreviewPaneClick?.Invoke(this, EventArgs.Empty);
                    break;
                case HBtn.Help:
                    HelpClick?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  TREE PANE
    // ──────────────────────────────────────────────────────────────────────────
    class TreePane : Panel
    {
        const int ROW_H       = 20;
        const int ROOT_INDENT = 8;
        const int LVL_INDENT  = 16;
        const int ICON_SZ     = 16;
        const int ARROW_W     = 12;
 
        List<TreeNode2> _flat = new List<TreeNode2>();
        TreeNode2       _root;
        TreeNode2       _selected;
        int             _scrollY = 0, _totalH = 0;
 
        VScrollBar _vsb;
 
        public event Action<TreeNode2> NodeSelected;
 
        public TreePane()
        {
            BackColor = Th.TreeBg;
            DoubleBuffered = true;
 
            _vsb = new VScrollBar
            {
                Dock = DockStyle.Right, Minimum = 0, Maximum = 0,
                SmallChange = ROW_H, LargeChange = 100, Visible = false
            };
            _vsb.ValueChanged += (s, e) => { _scrollY = _vsb.Value; Invalidate(); };
            Controls.Add(_vsb);
 
            MouseDown  += OnMD;
            MouseWheel += OnMW;
            Resize     += (s, e) => { Rebuild(); Invalidate(); };
 
            BuildTree();
        }
 
        void BuildTree()
        {
            _root = new TreeNode2 { Label = "__root__", IsVirtual = true, Expanded = true };
 
            var qa = Add(_root, "Quick access", null, "quick_access", isVirtual: true, isRoot: true);
            qa.Expanded = true;
            Add(qa, "Desktop",   SpecialDir(Environment.SpecialFolder.DesktopDirectory), "desktop");
            Add(qa, "Downloads", GetDownloads(),                                          "downloads");
            Add(qa, "Documents", SpecialDir(Environment.SpecialFolder.MyDocuments),       "documents");
            Add(qa, "Pictures",  SpecialDir(Environment.SpecialFolder.MyPictures),        "pictures");
 
            var tpc = Add(_root, "This PC", null, "this_pc", isVirtual: true, isRoot: true);
            Add(tpc, "3D Objects", null,                                                   "3dobjects");
            Add(tpc, "Desktop",    SpecialDir(Environment.SpecialFolder.DesktopDirectory), "desktop");
            Add(tpc, "Documents",  SpecialDir(Environment.SpecialFolder.MyDocuments),      "documents");
            Add(tpc, "Downloads",  GetDownloads(),                                         "downloads");
            Add(tpc, "Music",      SpecialDir(Environment.SpecialFolder.MyMusic),          "music");
            Add(tpc, "Pictures",   SpecialDir(Environment.SpecialFolder.MyPictures),       "pictures");
            Add(tpc, "Videos",     SpecialDir(Environment.SpecialFolder.MyVideos),         "videos");
 
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    string lbl = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)
                        ? $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})"
                        : $"Local Disk ({drive.Name.TrimEnd('\\')})";
                    var d = Add(tpc, lbl, drive.RootDirectory.FullName, "drives");
                    d.HasChildren = true;
                }
                catch { }
            }
 
            Add(_root, "Network", null, "network", isVirtual: true, isRoot: true);
            Rebuild();
        }
 
        TreeNode2 Add(TreeNode2 parent, string label, string path, string icon,
                      bool isVirtual = false, bool isRoot = false)
        {
            var n = new TreeNode2
            {
                Label = label, Path = path, IconName = icon,
                IsVirtual = isVirtual, IsRoot = isRoot, Parent = parent,
                Level = parent == _root ? 0 : parent.Level + 1,
                HasChildren = !isVirtual && path != null,
            };
            parent.Children.Add(n); return n;
        }
 
        static string SpecialDir(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        static string GetDownloads() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
 
        void Rebuild()
        {
            _flat.Clear();
            Flatten(_root);
            _totalH = _flat.Count * ROW_H;
            UpdateScroll();
        }
 
        void Flatten(TreeNode2 n)
        {
            foreach (var c in n.Children)
            { _flat.Add(c); if (c.Expanded) Flatten(c); }
        }
 
        void UpdateScroll()
        {
            int vis = ClientSize.Height;
            if (_totalH > vis)
            {
                _vsb.Visible = true;
                _vsb.Maximum = Math.Max(0, _totalH - vis + _vsb.LargeChange);
                _scrollY     = Math.Min(_scrollY, Math.Max(0, _totalH - vis));
                _vsb.Value   = _scrollY;
            }
            else { _vsb.Visible = false; _scrollY = 0; }
        }
 
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.TreeBg);
 
            int listW = Width - (_vsb.Visible ? _vsb.Width : 0) - 2;
            int y0    = -_scrollY;
 
            for (int i = 0; i < _flat.Count; i++)
            {
                var n = _flat[i];
                int y = y0 + i * ROW_H;
                if (y + ROW_H < 0) continue;
                if (y > Height)    break;
 
                n.Bounds = new Rectangle(0, y, listW, ROW_H);
 
                if (n == _selected) Th.DrawSel(g, new Rectangle(0, y, listW, ROW_H - 1));
 
                int indent  = ROOT_INDENT + n.Level * LVL_INDENT;
                bool hasKids = n.Children.Count > 0 || n.HasChildren;
 
                if (hasKids) DrawExpandArrow(g, indent - 2, y + ROW_H / 2, n.Expanded);
 
                int iconX = indent + ARROW_W;
                g.DrawImage(Icons.Get(n.IconName ?? "folder"), iconX, y + (ROW_H - ICON_SZ) / 2, ICON_SZ, ICON_SZ);
 
                int labelX = iconX + ICON_SZ + 3;
                using (var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(n.Label, n.IsRoot ? Th.UiBold : Th.UiFont,
                        Brushes.Black, new RectangleF(labelX, y + 1, Math.Max(1, listW - labelX - 2), ROW_H - 2), fmt);
 
                if (n.IsRoot && i > 0)
                    using (var sp = new Pen(Th.SepColor)) g.DrawLine(sp, 0, y, listW, y);
            }
 
            using (var rp = new Pen(Th.PaneSep)) g.DrawLine(rp, listW, 0, listW, Height);
        }
 
        static void DrawExpandArrow(Graphics g, int cx, int cy, bool expanded)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Point[] pts = expanded
                ? new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) }
                : new[] { new Point(cx - 2, cy - 4), new Point(cx - 2, cy + 4), new Point(cx + 3, cy) };
            using (var b = new SolidBrush(Color.FromArgb(100, 100, 100))) g.FillPolygon(b, pts);
            g.SmoothingMode = SmoothingMode.Default;
        }
 
        void OnMD(object s, MouseEventArgs e)
        {
            int idx = (_scrollY + e.Y) / ROW_H;
            if (idx < 0 || idx >= _flat.Count) return;
            var n = _flat[idx];
 
            int indent  = ROOT_INDENT + n.Level * LVL_INDENT;
            bool hasKids = n.Children.Count > 0 || n.HasChildren;
            if (hasKids && e.X >= indent - 8 && e.X <= indent + ARROW_W)
            { ToggleExpand(n); return; }
 
            _selected = n;
            Invalidate();
            if (e.Button == MouseButtons.Left) NodeSelected?.Invoke(n);
        }
 
        void ToggleExpand(TreeNode2 n)
        {
            if (!n.Expanded)
            {
                if (n.Children.Count == 0 && n.Path != null && !n.IsVirtual)
                    LoadFsChildren(n);
                n.Expanded = true;
            }
            else n.Expanded = false;
            Rebuild(); Invalidate();
        }
 
        void LoadFsChildren(TreeNode2 n)
        {
            try
            {
                foreach (var d in Directory.GetDirectories(n.Path))
                {
                    n.Children.Add(new TreeNode2
                    {
                        Label = Path.GetFileName(d), Path = d, IconName = "folder",
                        Parent = n, Level = n.Level + 1, HasChildren = true,
                    });
                }
                n.HasChildren = false;
            }
            catch { }
        }
 
        void OnMW(object s, MouseEventArgs e)
        {
            _scrollY = Math.Max(0, Math.Min(_scrollY - e.Delta / 3, Math.Max(0, _totalH - ClientSize.Height)));
            if (_vsb.Visible) _vsb.Value = _scrollY;
            Invalidate();
        }
 
        public void SelectPath(string path)
        {
            foreach (var n in _flat)
                if (n.Path != null && n.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                { _selected = n; Invalidate(); return; }
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  CONTENT PANE
    //  Column headers: White default · #D9EBF9 on hover · fully resizable
    // ──────────────────────────────────────────────────────────────────────────
    class ContentPane : Panel
    {
        const int HDR_H  = 22;
        const int ROW_H  = 20;
        const int ICON_SZ = 16;
        const int RESIZE_ZONE = 4;   // px from divider to show resize cursor
 
        // Column widths (Name fills remaining when _wName == 0)
        int _wName = 0;   // 0 = auto-fill; set explicitly when user drags
        int _wDate = 160;
        int _wType = 100;
        int _wSize = 80;
 
        List<ContentItem> _items  = new List<ContentItem>();
        HashSet<int>      _selSet = new HashSet<int>();
        int               _lastSel = -1;
 
        SortCol _sortCol = SortCol.Name;
        SortDir _sortDir = SortDir.Asc;
        int     _scrollY = 0;
 
        // Column-header state
        int  _hdrHovCol  = -1;   // which col is hovered (-1 = none)
        bool _hdrDrag    = false;
        int  _hdrDragBnd  = -1;  // 0=Name|Date  1=Date|Type  2=Type|Size
        int  _hdrDragStartX, _hdrDragStartW;
 
        // Marquee
        bool  _marquee;
        Point _marqStart, _marqCur;
 
        int   _hovRow = -1;
 
        VScrollBar _vsb;
 
        ContextMenuStrip _bgMenu, _folderMenu, _fileMenu;
 
        public string CurrentPath { get; private set; } = "";
        public event Action<ContentItem> ItemActivated;
 
        public ContentPane()
        {
            BackColor = Th.ContentBg;
            DoubleBuffered = true;
 
            _vsb = new VScrollBar { Dock = DockStyle.Right, Minimum = 0, Visible = false };
            _vsb.ValueChanged += (s, e) => { _scrollY = _vsb.Value; Invalidate(); };
            Controls.Add(_vsb);
 
            BuildContextMenus();
 
            MouseDown   += OnMD;
            MouseMove   += OnMM;
            MouseUp     += OnMU;
            MouseWheel  += OnMW;
            MouseLeave  += (s, e) => { _hovRow = -1; _hdrHovCol = -1; Cursor = Cursors.Default; Invalidate(); };
            DoubleClick += OnDblClick;
            Resize      += (s, e) => UpdateScroll();
        }
 
        // ── Load ──────────────────────────────────────────────────────────────
        public void LoadPath(string path)
        {
            CurrentPath = path;
            _items.Clear(); _selSet.Clear(); _lastSel = -1; _scrollY = 0;
 
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(path))
                    {
                        try
                        {
                            var di = new DirectoryInfo(d);
                            _items.Add(new ContentItem { Name = di.Name, FullPath = di.FullName,
                                DateModified = di.LastWriteTime, ItemType = "File folder", IsDirectory = true });
                        }
                        catch { }
                    }
                    foreach (var f in Directory.GetFiles(path))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            _items.Add(new ContentItem { Name = fi.Name, FullPath = fi.FullName,
                                DateModified = fi.LastWriteTime, ItemType = GetTypeStr(fi.Extension),
                                Size = fi.Length, IsDirectory = false });
                        }
                        catch { }
                    }
                }
                catch { }
            }
            SortItems(); UpdateScroll(); Invalidate();
        }
 
        static string GetTypeStr(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "File";
            switch (ext.ToLowerInvariant())
            {
                case ".txt":  return "Text Document";
                case ".png": case ".jpg": case ".jpeg": case ".bmp": case ".gif": return "Image";
                case ".cs":   return "C# Source File";
                case ".exe":  return "Application";
                case ".dll":  return "Application Extension";
                case ".zip":  return "Compressed Folder";
                case ".pdf":  return "PDF Document";
                case ".mp3":  return "MP3 File";
                case ".mp4":  return "MP4 Video";
                default:      return ext.TrimStart('.').ToUpperInvariant() + " File";
            }
        }
 
        void SortItems()
        {
            IEnumerable<ContentItem> s;
            switch (_sortCol)
            {
                case SortCol.Name: s = _items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase); break;
                case SortCol.Date: s = _items.OrderBy(i => i.DateModified); break;
                case SortCol.Type: s = _items.OrderBy(i => i.ItemType, StringComparer.OrdinalIgnoreCase); break;
                case SortCol.Size: s = _items.OrderBy(i => i.Size); break;
                default:           s = _items; break;
            }
            if (_sortDir == SortDir.Desc) s = s.Reverse();
            _items = s.ToList();
        }
 
        void UpdateScroll()
        {
            int total = _items.Count * ROW_H, vis = Math.Max(1, ClientSize.Height - HDR_H);
            if (total > vis) { _vsb.Visible = true; _vsb.Maximum = total - vis + 100; _vsb.LargeChange = vis; _vsb.SmallChange = ROW_H; }
            else             { _vsb.Visible = false; _scrollY = 0; }
        }
 
        // ── Column helpers ─────────────────────────────────────────────────────
        int NameW => _wName > 0 ? _wName : Math.Max(80, ListWidth() - _wDate - _wType - _wSize);
        int ListWidth() => Width - (_vsb.Visible ? _vsb.Width : 0);
 
        // Returns (left-x, label, sortCol, width) for each column
        (int x, string lbl, SortCol col, int w)[] ColDefs()
        {
            int nw = NameW;
            return new[]
            {
                (0,                  "Name",          SortCol.Name, nw),
                (nw,                 "Date modified", SortCol.Date, _wDate),
                (nw + _wDate,        "Type",          SortCol.Type, _wType),
                (nw + _wDate+_wType, "Size",          SortCol.Size, _wSize),
            };
        }
 
        // Divider X positions: between col[i] and col[i+1]
        int[] DividerXs()
        {
            int nw = NameW;
            return new[] { nw, nw + _wDate, nw + _wDate + _wType };
        }
 
        int ColAtX(int x)
        {
            var cols = ColDefs();
            for (int i = 0; i < cols.Length; i++)
                if (x >= cols[i].x && x < cols[i].x + cols[i].w) return i;
            return -1;
        }
 
        bool NearDivider(int x, out int boundary)
        {
            var divs = DividerXs();
            for (int i = 0; i < divs.Length; i++)
                if (Math.Abs(x - divs[i]) <= RESIZE_ZONE) { boundary = i; return true; }
            boundary = -1; return false;
        }
 
        int GetDivStartWidth(int boundary)
        {
            switch (boundary)
            {
                case 0: return NameW;
                case 1: return _wDate;
                case 2: return _wType;
                default: return 0;
            }
        }
 
        void SetDivWidth(int boundary, int w)
        {
            w = Math.Max(40, w);
            switch (boundary)
            {
                case 0: _wName = w; break;
                case 1: _wDate = w; break;
                case 2: _wType = w; break;
            }
        }
 
        // ── Paint ──────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.ContentBg);
            DrawHeader(g);
            DrawItems(g);
            if (_marquee) DrawMarquee(g);
        }
 
        void DrawHeader(Graphics g)
        {
            int listW = ListWidth();
            var cols  = ColDefs();
 
            // White base
            g.FillRectangle(Brushes.White, 0, 0, listW, HDR_H);
 
            using (var hb    = new SolidBrush(Th.HdrHover))
            using (var sepP  = new Pen(Color.FromArgb(200, 200, 200)))
            using (var botP  = new Pen(Color.FromArgb(213, 213, 213)))
            using (var fmt   = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                foreach (var (x, lbl, col, w) in cols)
                {
                    // Hover highlight
                    int ci = Array.IndexOf(cols.Select(c => c.col).ToArray(), col);
                    if (ci == _hdrHovCol)
                        g.FillRectangle(hb, x, 0, w, HDR_H - 1);
 
                    // Label
                    g.DrawString(lbl, Th.UiFont, Brushes.Black, new RectangleF(x + 6, 0, w - 12, HDR_H), fmt);
 
                    // Sort indicator
                    if (col == _sortCol)
                    {
                        int ax = x + w - 14, ay = HDR_H / 2;
                        Point[] tri = _sortDir == SortDir.Asc
                            ? new[] { new Point(ax - 3, ay + 2), new Point(ax + 3, ay + 2), new Point(ax, ay - 2) }
                            : new[] { new Point(ax - 3, ay - 2), new Point(ax + 3, ay - 2), new Point(ax, ay + 2) };
                        g.FillPolygon(Brushes.Gray, tri);
                    }
 
                    // Column divider
                    if (x + w < listW)
                        g.DrawLine(sepP, x + w, 2, x + w, HDR_H - 3);
                }
 
                // Bottom border
                g.DrawLine(botP, 0, HDR_H - 1, listW, HDR_H - 1);
            }
        }
 
        void DrawItems(Graphics g)
        {
            int listW = ListWidth();
            var cols  = ColDefs();
            var rfmt  = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            var sfmt  = new StringFormat(rfmt) { Alignment = StringAlignment.Far };
 
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int y    = HDR_H + i * ROW_H - _scrollY;
                if (y + ROW_H < HDR_H) continue;
                if (y > Height)        break;
 
                bool sel = _selSet.Contains(i);
                bool hov = i == _hovRow;
 
                var rowR = new Rectangle(0, y, listW, ROW_H - 1);
                if (sel)      Th.DrawSel(g, rowR);
                else if (hov) Th.DrawHover(g, rowR);
 
                // Icon + Name
                g.DrawImage(Icons.Get(item.IsDirectory ? "folder" : "file"),
                    cols[0].x + 4, y + (ROW_H - ICON_SZ) / 2, ICON_SZ, ICON_SZ);
                g.DrawString(item.Name, Th.UiFont, Brushes.Black,
                    new RectangleF(cols[0].x + ICON_SZ + 8, y, cols[0].w - ICON_SZ - 12, ROW_H), rfmt);
 
                // Date
                g.DrawString(item.DateStr, Th.UiFont, Brushes.Black,
                    new RectangleF(cols[1].x + 4, y, cols[1].w - 8, ROW_H), rfmt);
 
                // Type
                g.DrawString(item.ItemType, Th.UiFont, Brushes.Black,
                    new RectangleF(cols[2].x + 4, y, cols[2].w - 8, ROW_H), rfmt);
 
                // Size (right-aligned)
                g.DrawString(item.SizeStr, Th.UiFont, Brushes.Black,
                    new RectangleF(cols[3].x + 4, y, cols[3].w - 8, ROW_H), sfmt);
 
                // Row separator
                using (var rp = new Pen(Color.FromArgb(242, 242, 242)))
                    g.DrawLine(rp, 0, y + ROW_H - 1, listW, y + ROW_H - 1);
            }
        }
 
        void DrawMarquee(Graphics g)
        {
            int x = Math.Min(_marqStart.X, _marqCur.X), y = Math.Min(_marqStart.Y, _marqCur.Y);
            int w = Math.Abs(_marqCur.X - _marqStart.X), h = Math.Abs(_marqCur.Y - _marqStart.Y);
            using (var b = new SolidBrush(Color.FromArgb(80, Th.SelFill))) g.FillRectangle(b, x, y, w, h);
            using (var p = new Pen(Th.SelBorder)) g.DrawRectangle(p, x, y, w - 1, h - 1);
        }
 
        // ── Mouse ──────────────────────────────────────────────────────────────
        void OnMD(object s, MouseEventArgs e)
        {
            Focus();
 
            if (e.Y < HDR_H)
            {
                // Start column resize drag?
                if (e.Button == MouseButtons.Left && NearDivider(e.X, out int bnd))
                {
                    _hdrDrag = true; _hdrDragBnd = bnd;
                    _hdrDragStartX = e.X; _hdrDragStartW = GetDivStartWidth(bnd);
                    Capture = true;
                    return;
                }
                // Sort click
                if (e.Button == MouseButtons.Left) HandleHeaderSortClick(e.X);
                return;
            }
 
            int idx = RowAt(e.Y);
 
            if (e.Button == MouseButtons.Left)
            {
                bool ctrl  = (Control.ModifierKeys & Keys.Control) != 0;
                bool shift = (Control.ModifierKeys & Keys.Shift)   != 0;
 
                if (idx >= 0 && idx < _items.Count)
                {
                    if (ctrl)
                    { if (_selSet.Contains(idx)) _selSet.Remove(idx); else _selSet.Add(idx); _lastSel = idx; }
                    else if (shift && _lastSel >= 0)
                    { _selSet.Clear(); for (int i = Math.Min(_lastSel,idx); i <= Math.Max(_lastSel,idx); i++) _selSet.Add(i); }
                    else
                    { _selSet.Clear(); _selSet.Add(idx); _lastSel = idx; }
                }
                else
                {
                    if (!ctrl && !shift) _selSet.Clear();
                    _lastSel = -1; _marquee = true; _marqStart = _marqCur = e.Location; Capture = true;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (idx >= 0 && idx < _items.Count)
                {
                    if (!_selSet.Contains(idx)) { _selSet.Clear(); _selSet.Add(idx); _lastSel = idx; }
                    Invalidate();
                    ((_items[idx].IsDirectory ? _folderMenu : _fileMenu)).Show(this, e.Location);
                }
                else { Invalidate(); _bgMenu.Show(this, e.Location); }
            }
            Invalidate();
        }
 
        void OnMM(object s, MouseEventArgs e)
        {
            // Column resize drag
            if (_hdrDrag)
            {
                SetDivWidth(_hdrDragBnd, _hdrDragStartW + e.X - _hdrDragStartX);
                Invalidate(); return;
            }
            if (_marquee)
            {
                _marqCur = e.Location; UpdateMarquee(); Invalidate(); return;
            }
 
            if (e.Y < HDR_H)
            {
                // Show resize cursor near dividers
                Cursor = NearDivider(e.X, out _) ? Cursors.VSplit : Cursors.Default;
 
                int col = ColAtX(e.X);
                if (col != _hdrHovCol) { _hdrHovCol = col; Invalidate(); }
                if (_hovRow != -1)     { _hovRow = -1; Invalidate(); }
            }
            else
            {
                if (Cursor != Cursors.Default) Cursor = Cursors.Default;
                if (_hdrHovCol != -1) { _hdrHovCol = -1; Invalidate(); }
                int row = RowAt(e.Y);
                if (row != _hovRow) { _hovRow = row; Invalidate(); }
            }
        }
 
        void OnMU(object s, MouseEventArgs e)
        {
            if (_hdrDrag)   { _hdrDrag = false; Capture = false; Cursor = Cursors.Default; }
            if (_marquee)   { _marquee = false; Capture = false; Invalidate(); }
        }
 
        void OnMW(object s, MouseEventArgs e)
        {
            int total = _items.Count * ROW_H, vis = Math.Max(1, ClientSize.Height - HDR_H);
            _scrollY = Math.Max(0, Math.Min(_scrollY - e.Delta / 3, Math.Max(0, total - vis)));
            if (_vsb.Visible) _vsb.Value = Math.Min(_scrollY, _vsb.Maximum);
            Invalidate();
        }
 
        void OnDblClick(object s, EventArgs e)
        {
            var mp  = PointToClient(Cursor.Position);
            int idx = RowAt(mp.Y);
            if (idx >= 0 && idx < _items.Count) ItemActivated?.Invoke(_items[idx]);
        }
 
        void UpdateMarquee()
        {
            int x1 = Math.Min(_marqStart.X, _marqCur.X), y1 = Math.Min(_marqStart.Y, _marqCur.Y);
            int x2 = Math.Max(_marqStart.X, _marqCur.X), y2 = Math.Max(_marqStart.Y, _marqCur.Y);
            _selSet.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                int ry1 = HDR_H + i * ROW_H - _scrollY, ry2 = ry1 + ROW_H;
                if (ry2 > y1 && ry1 < y2) _selSet.Add(i);
            }
        }
 
        void HandleHeaderSortClick(int mouseX)
        {
            foreach (var (x, _, col, w) in ColDefs())
            {
                if (mouseX >= x && mouseX < x + w)
                {
                    if (_sortCol == col) _sortDir = _sortDir == SortDir.Asc ? SortDir.Desc : SortDir.Asc;
                    else { _sortCol = col; _sortDir = SortDir.Asc; }
                    _selSet.Clear(); SortItems(); Invalidate(); return;
                }
            }
        }
 
        int RowAt(int y) => y < HDR_H ? -1 : (_scrollY + y - HDR_H) / ROW_H;
 
        // ── Context Menus (no icons; image-margin gives spacing) ───────────────
        void BuildContextMenus()
        {
            // ── Background menu ──────────────────────────────────────────────
            _bgMenu = MenuHelper.NewMenu();
 
            var viewSub  = MenuHelper.Sub("View");     AddViewSub(viewSub);
            var sortSub  = MenuHelper.Sub("Sort by");  AddSortSub(sortSub);
            var groupSub = MenuHelper.Sub("Group by"); AddGroupSub(groupSub);
            var giveSub  = MenuHelper.Sub("Give access to"); AddGiveAccessSub(giveSub);
            var newSub   = MenuHelper.Sub("New");      AddNewSub(newSub);
 
            _bgMenu.Items.AddRange(new ToolStripItem[]
            {
                viewSub, sortSub, groupSub,
                MenuHelper.Item("Refresh"),
                new ToolStripSeparator(),
                MenuHelper.Item("Paste"),
                MenuHelper.Item("Paste shortcut"),
                MenuHelper.Item("Undo Delete"),
                new ToolStripSeparator(),
                giveSub,
                new ToolStripSeparator(),
                newSub,
                new ToolStripSeparator(),
                MenuHelper.Item("Properties"),
            });
 
            // ── Folder menu ───────────────────────────────────────────────────
            _folderMenu = MenuHelper.NewMenu();
            var fGive = MenuHelper.Sub("Give access to"); AddGiveAccessSub(fGive);
            var fSend = MenuHelper.Sub("Send to");
 
            _folderMenu.Items.AddRange(new ToolStripItem[]
            {
                MenuHelper.Item("Open"),
                MenuHelper.Item("Open in new window"),
                MenuHelper.Item("Pin to Quick access"),
                MenuHelper.Item("Take Ownership"),
                new ToolStripSeparator(),
                fGive,
                MenuHelper.Item("Restore"),
                new ToolStripSeparator(),
                fSend,
                new ToolStripSeparator(),
                MenuHelper.Item("Cut"),
                MenuHelper.Item("Copy"),
                new ToolStripSeparator(),
                MenuHelper.Item("Create shortcut"),
                MenuHelper.Item("Delete"),
                MenuHelper.Item("Rename"),
                new ToolStripSeparator(),
                MenuHelper.Item("Properties"),
            });
 
            // ── File menu ─────────────────────────────────────────────────────
            _fileMenu = MenuHelper.NewMenu();
            var fiGive    = MenuHelper.Sub("Give access to"); AddGiveAccessSub(fiGive);
            var fiOpenWith = MenuHelper.Sub("Open with");
            var fiSend    = MenuHelper.Sub("Send to");
 
            _fileMenu.Items.AddRange(new ToolStripItem[]
            {
                MenuHelper.Item("Open"),
                MenuHelper.Item("Pin"),
                MenuHelper.Item("Edit"),
                MenuHelper.Item("Take Ownership"),
                fiOpenWith,
                new ToolStripSeparator(),
                fiGive,
                MenuHelper.Item("Restore previous version"),
                new ToolStripSeparator(),
                fiSend,
                MenuHelper.Item("Cut"),
                MenuHelper.Item("Copy"),
                new ToolStripSeparator(),
                MenuHelper.Item("Create shortcut"),
                MenuHelper.Item("Delete"),
                MenuHelper.Item("Rename"),
                new ToolStripSeparator(),
                MenuHelper.Item("Properties"),
            });
        }
 
        static void AddViewSub(ToolStripMenuItem m)
        {
            foreach (var s in new[] { "Extra Large Icons","Large Icons","Medium Icons","Small Icons","List","Details","Tiles","Content" })
                m.DropDownItems.Add(MenuHelper.Item(s));
        }
        static void AddSortSub(ToolStripMenuItem m)
        {
            m.DropDownItems.Add(MenuHelper.Item("Name"));
            m.DropDownItems.Add(MenuHelper.Item("Date modified"));
            m.DropDownItems.Add(MenuHelper.Item("Type"));
            m.DropDownItems.Add(MenuHelper.Item("Size"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("Ascending"));
            m.DropDownItems.Add(MenuHelper.Item("Descending"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("More..."));
        }
        static void AddGroupSub(ToolStripMenuItem m)
        {
            m.DropDownItems.Add(MenuHelper.Item("Name"));
            m.DropDownItems.Add(MenuHelper.Item("Date modified"));
            m.DropDownItems.Add(MenuHelper.Item("Type"));
            m.DropDownItems.Add(MenuHelper.Item("Size"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("Ascending"));
            m.DropDownItems.Add(MenuHelper.Item("Descending"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("More..."));
        }
        static void AddGiveAccessSub(ToolStripMenuItem m)
        {
            m.DropDownItems.Add(MenuHelper.Item("Remove access"));
            m.DropDownItems.Add(MenuHelper.Item("Homegroup (view)"));
            m.DropDownItems.Add(MenuHelper.Item("Homegroup (view and edit)"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("Specific people..."));
        }
        static void AddNewSub(ToolStripMenuItem m)
        {
            m.DropDownItems.Add(MenuHelper.Item("Folder"));
            m.DropDownItems.Add(MenuHelper.Item("Shortcut"));
            m.DropDownItems.Add(new ToolStripSeparator());
            m.DropDownItems.Add(MenuHelper.Item("Bitmap image"));
            m.DropDownItems.Add(MenuHelper.Item("Contact"));
            m.DropDownItems.Add(MenuHelper.Item("Rich Text Format"));
            m.DropDownItems.Add(MenuHelper.Item("Text Document"));
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  SPLITTER BAR  (4 px draggable divider)
    // ──────────────────────────────────────────────────────────────────────────
    class SplitterBar : Control
    {
        bool _drag; int _startX, _startW; Control _left;
 
        public SplitterBar(Control leftPanel)
        {
            _left  = leftPanel;
            Width  = 4;
            Cursor = Cursors.VSplit;
            BackColor = Th.PaneSep;
 
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                { _drag = true; _startX = Cursor.Position.X; _startW = _left.Width; Capture = true; }
            };
            MouseMove += (s, e) =>
            {
                if (!_drag) return;
                _left.Width = Math.Max(100, _startW + Cursor.Position.X - _startX);
                Parent?.PerformLayout();
            };
            MouseUp += (s, e) => { _drag = false; Capture = false; };
        }
 
        protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(Th.PaneSep);
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  STATUS BAR
    // ──────────────────────────────────────────────────────────────────────────
    class ExplorerStatusBar : Panel
    {
        Label _lbl;
 
        public new string Text { get => _lbl.Text; set => _lbl.Text = value; }
 
        public ExplorerStatusBar()
        {
            Height = 22; Dock = DockStyle.Bottom; BackColor = Th.Bg;
            _lbl = new Label { Dock = DockStyle.Fill, AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft, Font = Th.UiFont,
                Padding = new Padding(6, 0, 0, 0) };
            Controls.Add(_lbl);
        }
 
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Th.PaneSep)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }
 
    // ──────────────────────────────────────────────────────────────────────────
    //  MAIN FORM
    // ──────────────────────────────────────────────────────────────────────────
    class ExplorerForm : Form
    {
        TopNavBar         _nav;
        CommandBar        _cmd;
        TreePane          _tree;
        ContentPane       _content;
        SplitterBar       _splitter;
        ExplorerStatusBar _status;
        Panel             _mainArea;
 
        List<string> _history = new List<string>();
        int          _histIdx = -1;
 
        public ExplorerForm()
        {
            Text          = "File Explorer";
            MinimumSize   = new Size(700, 450);
            Size          = new Size(1100, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor     = Th.Bg;
            Font          = Th.UiFont;
 
            BuildLayout();
            WireEvents();
            Navigate("Quick access");
        }
 
        void BuildLayout()
        {
            SuspendLayout();
 
            _nav     = new TopNavBar();
            _cmd     = new CommandBar();
            _status  = new ExplorerStatusBar();
            _mainArea = new Panel { Dock = DockStyle.Fill };
 
            _tree     = new TreePane   { Dock = DockStyle.Left, Width = 220 };
            _splitter = new SplitterBar(_tree) { Dock = DockStyle.Left };
            _content  = new ContentPane { Dock = DockStyle.Fill };
 
            _mainArea.Controls.Add(_content);
            _mainArea.Controls.Add(_splitter);
            _mainArea.Controls.Add(_tree);
 
            Controls.Add(_mainArea);
            Controls.Add(_status);
            Controls.Add(_cmd);
            Controls.Add(_nav);
 
            ResumeLayout(false);
        }
 
        void WireEvents()
        {
            _nav.BackClick     += (s, e) => GoBack();
            _nav.ForwardClick  += (s, e) => GoForward();
            _nav.UpClick       += (s, e) => GoUp();
            _nav.Navigate      += path  => Navigate(path);
            _nav.SearchChanged += q     => _status.Text = string.IsNullOrEmpty(q) ? "" : $"Search: {q}";
 
            _cmd.NewFolderClick   += (s, e) => NewFolder();
            _cmd.HelpClick        += (s, e) => OpenHelp();
            _cmd.PreviewPaneClick += (s, e) => _status.Text = "Preview pane toggled";
            _cmd.ViewChanged      += vm    => _status.Text  = $"View: {vm}";
 
            _tree.NodeSelected += node =>
            {
                if (node.Path != null && Directory.Exists(node.Path)) Navigate(node.Path);
                else _status.Text = node.Label;
            };
 
            _content.ItemActivated += item =>
            {
                if (item.IsDirectory) Navigate(item.FullPath);
                else OpenFile(item.FullPath);
            };
        }
 
        // ── Navigation ────────────────────────────────────────────────────────
        void Navigate(string path)
        {
            if (_histIdx < _history.Count - 1)
                _history.RemoveRange(_histIdx + 1, _history.Count - _histIdx - 1);
            _history.Add(path); _histIdx = _history.Count - 1;
            ApplyNav(path);
        }
 
        void ApplyNav(string path)
        {
            _nav.CurrentPath    = path;
            _nav.BackEnabled    = _histIdx > 0;
            _nav.ForwardEnabled = _histIdx < _history.Count - 1;
 
            if (path == "Quick access")
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                _content.LoadPath(profile);
                Text = "Quick access – File Explorer";
            }
            else if (Directory.Exists(path))
            {
                _content.LoadPath(path);
                _tree.SelectPath(path);
                Text = (Path.GetFileName(path) ?? path) + " – File Explorer";
            }
            UpdateStatus();
        }
 
        void GoBack()    { if (_histIdx > 0) { _histIdx--; ApplyNav(_history[_histIdx]); } }
        void GoForward() { if (_histIdx < _history.Count - 1) { _histIdx++; ApplyNav(_history[_histIdx]); } }
        void GoUp()
        {
            string cur = _nav.CurrentPath;
            if (string.IsNullOrEmpty(cur) || cur == "Quick access") return;
            try { string p = Directory.GetParent(cur)?.FullName; if (p != null) Navigate(p); } catch { }
        }
 
        // ── Actions ───────────────────────────────────────────────────────────
        void NewFolder()
        {
            string cur = _content.CurrentPath;
            if (!Directory.Exists(cur)) return;
            string p = Path.Combine(cur, "New folder"); int i = 2;
            while (Directory.Exists(p)) p = Path.Combine(cur, $"New folder ({i++})");
            try { Directory.CreateDirectory(p); _content.LoadPath(cur); _status.Text = $"Created: {p}"; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
 
        void OpenHelp() =>
            Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/?LinkID=2004439") { UseShellExecute = true });
 
        void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
 
        void UpdateStatus()
        {
            string path = _nav.CurrentPath;
            if (path == "Quick access") { _status.Text = "Quick access"; return; }
            if (!Directory.Exists(path)) { _status.Text = path; return; }
            try
            {
                int total = Directory.GetDirectories(path).Length + Directory.GetFiles(path).Length;
                _status.Text = $"{total} item{(total != 1 ? "s" : "")}";
            }
            catch { _status.Text = path; }
        }
 
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if      (e.KeyCode == Keys.Back || (e.Alt && e.KeyCode == Keys.Left))  GoBack();
            else if (e.Alt && e.KeyCode == Keys.Right) GoForward();
            else if (e.Alt && e.KeyCode == Keys.Up)    GoUp();
            else if (e.KeyCode == Keys.F5)             _content.LoadPath(_content.CurrentPath);
        }
    }
}
 
