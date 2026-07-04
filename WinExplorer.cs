// ═══════════════════════════════════════════════════════════════════════════════
//  WINDOWS EXPLORER  –  Pixel-Perfect GDI+ Recreation  (v2)
//  C# .NET Framework 4.8  |  Single File  |  Custom GDI+ Rendering
// ───────────────────────────────────────────────────────────────────────────────
//  ICONS  →  Place 16×16 PNG files in:  <exe-dir>\icons\Win10\
//
//  Required icon names (filename without .png, all 16×16 px):
//    backward_arrow  forward_arrow  previous_small_arrow  up_arrow
//    reload  search  organize  new_folder  change_view  more_options
//    preview_pane  help  quick_access  this_pc  network
//    desktop  downloads  documents  pictures  music  videos
//    3dobjects  drives  folder  file
//    cut  copy  paste  delete  rename  shortcut  undo  redo
//    selectall  properties  sendto  openwith  options  ascending  descending
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
    //  Theme  – every pixel colour / font in one place
    // ──────────────────────────────────────────────────────────────────────────
    static class Th
    {
        // ── Selection / hover ────────────────────────────────────────────────
        public static readonly Color SelFill        = Color.FromArgb(204, 232, 255); // #CCE8FF active selection fill
        public static readonly Color SelBorder      = Color.FromArgb(153, 209, 255); // #99D1FF active selection border
        public static readonly Color ItemHover      = Color.FromArgb(229, 227, 255); // #E5E3FF row hover (borderless)
        public static readonly Color InactiveDirFill= Color.FromArgb(217, 217, 217); // #D9D9D9 inactive folder selection
        // inactive file selection = border-only using SelBorder

        // ── Header ───────────────────────────────────────────────────────────
        public static readonly Color HdrBg         = Color.White;
        public static readonly Color HdrHover       = Color.FromArgb(217, 235, 249); // #D9EBF9 column header hover
        public static readonly Color HdrBorder      = Color.FromArgb(213, 213, 213);

        // ── Toolbar / chrome ─────────────────────────────────────────────────
        public static readonly Color Bg            = Color.FromArgb(240, 240, 240);
        public static readonly Color BtnHoverFill  = Color.FromArgb(229, 241, 251);
        public static readonly Color BtnHoverBord  = Color.FromArgb(0, 120, 215);
        public static readonly Color BtnPressFill  = Color.FromArgb(204, 228, 247);
        public static readonly Color BtnPressBord  = Color.FromArgb(0, 84, 153);
        public static readonly Color SepColor      = Color.FromArgb(208, 208, 208);
        public static readonly Color PaneSep       = Color.FromArgb(180, 180, 180);
        public static readonly Color TxtColor      = Color.Black;
        public static readonly Color TxtDisabled   = Color.FromArgb(160, 160, 160);
        public static readonly Color TreeBg        = Color.White;
        public static readonly Color ContentBg     = Color.White;

        // ── Fonts ────────────────────────────────────────────────────────────
        public static readonly Font UiFont  = new Font("Segoe UI", 9f);
        public static readonly Font UiBold  = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font UiSmall = new Font("Segoe UI", 8f);

        // ── Draw helpers ─────────────────────────────────────────────────────
        public static void FillSel(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(SelFill))   g.FillRectangle(b, r);
            using (var p = new Pen(SelBorder))         g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        public static void FillBtnHover(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(BtnHoverFill)) g.FillRectangle(b, r);
            using (var p = new Pen(BtnHoverBord))         g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        public static void FillBtnPress(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(BtnPressFill)) g.FillRectangle(b, r);
            using (var p = new Pen(BtnPressBord))         g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        public static void DropArrow(Graphics g, int cx, int cy, Color c)
        {
            Point[] pts = { new Point(cx - 3, cy - 2), new Point(cx + 3, cy - 2), new Point(cx, cy + 2) };
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Icon Cache – loads 16×16 PNGs from <exe-dir>\icons\Win10\
    // ──────────────────────────────────────────────────────────────────────────
    static class Icons
    {
        const int SZ = 16;

        static readonly string Dir = Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath) ?? AppDomain.CurrentDomain.BaseDirectory,
            "icons", "Win10");

        static readonly Dictionary<string, Image> Cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public static Image Get(string name)
        {
            if (Cache.TryGetValue(name, out var hit)) return hit;
            string path = Path.Combine(Dir, name + ".png");
            Image img = null;
            if (File.Exists(path))
            {
                try
                {
                    using (var raw = Image.FromFile(path))
                    {
                        var bmp = new Bitmap(SZ, SZ);
                        using (var g = Graphics.FromImage(bmp))
                        { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(raw, 0, 0, SZ, SZ); }
                        img = bmp;
                    }
                }
                catch { img = null; }
            }
            img = img ?? Placeholder(name);
            Cache[name] = img;
            return img;
        }

        // ── Placeholder generator ─────────────────────────────────────────────
        static Image Placeholder(string name)
        {
            var bmp = new Bitmap(SZ, SZ);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                DrawIcon(g, name);
            }
            return bmp;
        }

        static void DrawIcon(Graphics g, string n)
        {
            var ink = new SolidBrush(Color.FromArgb(50, 50, 50));
            var pen = new Pen(Color.FromArgb(50, 50, 50), 1.2f);

            switch (n)
            {
                // ── Navigation arrows (black) ─────────────────────────────────
                case "backward_arrow":
                    g.DrawLine(pen, 12, 8, 4, 8); g.DrawLine(pen, 4, 8, 8, 4); g.DrawLine(pen, 4, 8, 8, 12); break;
                case "forward_arrow":
                    g.DrawLine(pen, 4, 8, 12, 8); g.DrawLine(pen, 12, 8, 8, 4); g.DrawLine(pen, 12, 8, 8, 12); break;
                case "up_arrow":
                    g.DrawLine(pen, 8, 12, 8, 4); g.DrawLine(pen, 8, 4, 4, 8); g.DrawLine(pen, 8, 4, 12, 8); break;
                case "previous_small_arrow":
                    Th.DropArrow(g, 8, 9, Color.FromArgb(50, 50, 50)); break;

                // ── Toolbar icons ─────────────────────────────────────────────
                case "search":
                    g.DrawEllipse(pen, 2, 2, 9, 9); g.DrawLine(pen, 10, 10, 13, 13); break;
                case "reload":
                    g.DrawArc(pen, 2, 2, 11, 11, -30, 270);
                    g.DrawLine(pen, 12, 4, 9, 1); g.DrawLine(pen, 12, 4, 14, 7); break;
                case "change_view":
                    g.FillRectangle(ink, 1, 1, 5, 5); g.FillRectangle(ink, 9, 1, 5, 5);
                    g.FillRectangle(ink, 1, 9, 5, 5); g.FillRectangle(ink, 9, 9, 5, 5); break;
                case "more_options":
                    Th.DropArrow(g, 8, 10, Color.FromArgb(50, 50, 50)); break;
                case "preview_pane":
                    g.DrawRectangle(pen, 1, 2, 13, 11);
                    g.DrawLine(pen, 8, 2, 8, 13); break;
                case "help":
                    g.DrawEllipse(pen, 1, 1, 13, 13);
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("?", new Font("Segoe UI", 7.5f, FontStyle.Bold), ink, new RectangleF(0, 0, 16, 16), fmt); break;
                case "organize":
                    g.DrawLine(pen, 2, 4, 14, 4); g.DrawLine(pen, 2, 8, 14, 8); g.DrawLine(pen, 2, 12, 14, 12); break;
                case "new_folder":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 200, 50)), 1, 5, 12, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 200, 50)), 1, 3, 6, 3);
                    g.DrawRectangle(pen, 1, 5, 11, 8); g.DrawRectangle(pen, 1, 3, 5, 2);
                    pen.Color = Color.FromArgb(0, 140, 0); pen.Width = 1.5f;
                    g.DrawLine(pen, 12, 10, 15, 10); g.DrawLine(pen, 13, 8, 13, 13); break;

                // ── Folder / location icons ───────────────────────────────────
                case "folder":
                case "3dobjects":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawRectangle(pen, 1, 6, 13, 8); g.DrawRectangle(pen, 1, 4, 6, 2); break;
                case "quick_access":
                    var star = StarPts(8, 8, 7, 3, 5);
                    g.FillPolygon(new SolidBrush(Color.FromArgb(255, 185, 0)), star); break;
                case "desktop":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawLine(new Pen(Color.FromArgb(0, 120, 215), 1.5f), 7, 8, 13, 8); break;
                case "downloads":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawLine(new Pen(Color.FromArgb(0, 150, 30), 1.5f), 9, 7, 9, 12);
                    g.DrawLine(new Pen(Color.FromArgb(0, 150, 30), 1.5f), 6, 10, 9, 13); g.DrawLine(new Pen(Color.FromArgb(0, 150, 30), 1.5f), 12, 10, 9, 13); break;
                case "documents":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawLine(pen, 5, 9, 11, 9); g.DrawLine(pen, 5, 11, 10, 11); break;
                case "pictures":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawEllipse(new Pen(Color.FromArgb(0, 120, 215)), 5, 8, 3, 3); break;
                case "music":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.DrawLine(pen, 7, 7, 11, 6); g.DrawLine(pen, 7, 7, 7, 12); g.DrawEllipse(pen, 5, 10, 3, 3); break;
                case "videos":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 6, 14, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(255, 196, 42)), 1, 4, 7, 3);
                    g.FillPolygon(ink, new[] { new Point(6, 7), new Point(6, 13), new Point(11, 10) }); break;
                case "this_pc":
                    g.DrawRectangle(pen, 2, 3, 11, 8);
                    g.DrawLine(pen, 6, 11, 6, 13); g.DrawLine(pen, 10, 11, 10, 13); g.DrawLine(pen, 4, 13, 12, 13); break;
                case "network":
                    g.DrawEllipse(pen, 5, 1, 5, 4); g.DrawEllipse(pen, 1, 9, 4, 4); g.DrawEllipse(pen, 11, 9, 4, 4);
                    g.DrawLine(pen, 8, 5, 3, 9); g.DrawLine(pen, 8, 5, 13, 9); break;

                // ── Drive icon ────────────────────────────────────────────────
                case "drives":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(220, 220, 220)), 1, 4, 14, 9);
                    g.DrawRectangle(pen, 1, 4, 13, 8);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(0, 120, 215)), 9, 9, 3, 3);
                    g.DrawLine(pen, 2, 7, 7, 7); break;

                // ── File icon ─────────────────────────────────────────────────
                case "file":
                    g.DrawPolygon(pen, new[] {
                        new Point(3,1), new Point(10,1), new Point(13,4),
                        new Point(13,15), new Point(3,15) });
                    g.DrawLine(pen, 10, 1, 10, 4); g.DrawLine(pen, 10, 4, 13, 4); break;

                // ── Edit actions ──────────────────────────────────────────────
                case "cut":
                    g.DrawLine(pen, 5, 1, 11, 13); g.DrawLine(pen, 11, 1, 5, 13);
                    g.DrawEllipse(pen, 2, 11, 4, 4); g.DrawEllipse(pen, 10, 11, 4, 4); break;
                case "copy":
                    g.DrawRectangle(pen, 4, 4, 9, 10); g.DrawRectangle(pen, 2, 2, 9, 10);
                    g.FillRectangle(new SolidBrush(Th.ContentBg), 3, 3, 8, 9); g.DrawRectangle(pen, 2, 2, 9, 10); break;
                case "paste":
                    g.DrawRectangle(pen, 3, 5, 10, 10); g.FillRectangle(new SolidBrush(Color.White), 4, 6, 9, 9);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(210, 210, 210)), 5, 3, 6, 4);
                    g.DrawRectangle(pen, 5, 3, 5, 3); break;
                case "undo":
                    g.DrawArc(pen, 2, 3, 10, 9, 180, 180);
                    g.DrawLine(pen, 2, 8, 2, 3); g.DrawLine(pen, 2, 3, 5, 5); g.DrawLine(pen, 2, 3, 4, 6); break;
                case "redo":
                    g.DrawArc(pen, 4, 3, 10, 9, 0, 180);
                    g.DrawLine(pen, 14, 8, 14, 3); g.DrawLine(pen, 14, 3, 11, 5); g.DrawLine(pen, 14, 3, 12, 6); break;
                case "delete":
                    g.FillRectangle(new SolidBrush(Color.FromArgb(210, 210, 210)), 3, 5, 10, 10);
                    g.DrawRectangle(pen, 3, 5, 9, 9);
                    g.DrawLine(pen, 1, 3, 15, 3); g.DrawLine(pen, 6, 1, 10, 1); g.DrawLine(pen, 6, 3, 6, 1); g.DrawLine(pen, 10, 3, 10, 1);
                    g.DrawLine(pen, 6, 7, 6, 12); g.DrawLine(pen, 10, 7, 10, 12); break;
                case "rename":
                    g.DrawLine(pen, 3, 13, 12, 4); g.DrawLine(pen, 12, 4, 14, 2);
                    g.DrawLine(pen, 14, 2, 12, 4); g.DrawLine(pen, 1, 13, 3, 13); g.DrawLine(pen, 1, 13, 2, 15); break;
                case "shortcut":
                    g.DrawPolygon(pen, new[] { new Point(1,1), new Point(8,1), new Point(11,4), new Point(11,11), new Point(1,11) });
                    var arw = new SolidBrush(Color.Black);
                    g.FillPolygon(arw, new[] { new Point(8, 9), new Point(12, 13), new Point(13, 10), new Point(15, 15), new Point(10, 13), new Point(13, 12) }); break;
                case "selectall":
                    g.DrawRectangle(pen, 1, 1, 7, 7); g.DrawRectangle(pen, 1, 8, 7, 7);
                    g.DrawLine(pen, 10, 4, 15, 4); g.DrawLine(pen, 10, 11, 15, 11); break;
                case "properties":
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                    g.DrawLine(pen, 8, 5, 8, 9); g.FillEllipse(ink, 7, 10, 2, 2); break;
                case "sendto":
                    g.DrawRectangle(pen, 1, 4, 9, 9);
                    g.DrawLine(pen, 10, 8, 15, 8); g.DrawLine(pen, 12, 5, 15, 8); g.DrawLine(pen, 12, 11, 15, 8); break;
                case "openwith":
                    g.DrawRectangle(pen, 1, 3, 11, 9);
                    g.DrawLine(pen, 12, 7, 15, 7); g.DrawLine(pen, 13, 5, 15, 7); g.DrawLine(pen, 13, 9, 15, 7); break;
                case "options":
                    g.DrawEllipse(pen, 4, 4, 8, 8);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        int x1 = (int)(8 + 5 * Math.Cos(a)), y1 = (int)(8 + 5 * Math.Sin(a));
                        int x2 = (int)(8 + 7 * Math.Cos(a)), y2 = (int)(8 + 7 * Math.Sin(a));
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    break;
                case "ascending":
                    g.DrawLine(pen, 2, 13, 2, 3); g.DrawLine(pen, 2, 3, 5, 6); g.DrawLine(pen, 2, 3, -1, 6);
                    g.DrawLine(pen, 6, 5, 14, 5); g.DrawLine(pen, 6, 9, 12, 9); g.DrawLine(pen, 6, 13, 9, 13); break;
                case "descending":
                    g.DrawLine(pen, 2, 3, 2, 13); g.DrawLine(pen, 2, 13, 5, 10); g.DrawLine(pen, 2, 13, -1, 10);
                    g.DrawLine(pen, 6, 5, 14, 5); g.DrawLine(pen, 6, 9, 12, 9); g.DrawLine(pen, 6, 13, 9, 13); break;

                default:
                    // Unknown: letter box
                    g.DrawRectangle(pen, 2, 2, 11, 11);
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(n.Length > 0 ? n[0].ToString().ToUpper() : "?",
                            new Font("Segoe UI", 6f), ink, new RectangleF(0, 0, 16, 16), fmt);
                    break;
            }
            ink.Dispose(); pen.Dispose();
        }

        static Point[] StarPts(int cx, int cy, int outerR, int innerR, int n)
        {
            var pts = new Point[n * 2];
            double step = Math.PI / n;
            for (int i = 0; i < n * 2; i++)
            {
                double a = i * step - Math.PI / 2;
                int r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new Point(cx + (int)(r * Math.Cos(a)), cy + (int)(r * Math.Sin(a)));
            }
            return pts;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Data models
    // ──────────────────────────────────────────────────────────────────────────
    enum SortCol  { Name, Date, Type, Size }
    enum SortDir  { Asc, Desc }
    enum ViewMode { Details, LargeIcons, MediumIcons, SmallIcons,
                    List, ExtraLargeIcons, Tiles, Content }

    class TreeNode2
    {
        public string Label, Path, IconName;
        public bool   Expanded, IsVirtual, IsRoot;
        public int    Level;
        public List<TreeNode2> Children = new List<TreeNode2>();
        public TreeNode2 Parent;
        public bool HasChildren;
        public Rectangle Bounds;
    }

    class ContentItem
    {
        public string   Name, FullPath, ItemType;
        public DateTime DateModified;
        public long     Size;
        public bool     IsDirectory, Selected;

        public string SizeStr =>
            IsDirectory ? "" :
            Size < 1024       ? $"{Size} B" :
            Size < 1_048_576  ? $"{Size / 1024.0:F1} KB" :
                                $"{Size / 1_048_576.0:F1} MB";
        public string DateStr => DateModified == default ? "" :
            DateModified.ToString("M/d/yyyy h:mm tt");
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Custom menu renderer (Win-10 look)
    // ──────────────────────────────────────────────────────────────────────────
    class ExMenuRenderer : ToolStripProfessionalRenderer
    {
        public ExMenuRenderer() : base(new ExColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            if (e.Item.Selected && e.Item.Enabled)
                Th.FillSel(e.Graphics, new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height - 1));
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Th.SepColor)) e.Graphics.DrawLine(p, 30, y, e.Item.Width - 4, y);
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
            using (var p = new Pen(Th.PaneSep))
                e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            if (e.Image == null) return;
            var r = e.ImageRectangle;
            e.Graphics.DrawImage(e.Image, r.X + (r.Width - 16) / 2, r.Y + (r.Height - 16) / 2, 16, 16);
        }
    }
    class ExColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder                    => Th.PaneSep;
        public override Color MenuItemBorder               => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Th.SelFill;
        public override Color MenuItemSelectedGradientEnd   => Th.SelFill;
        public override Color ToolStripDropDownBackground  => Color.White;
        public override Color ImageMarginGradientBegin     => Color.White;
        public override Color ImageMarginGradientMiddle    => Color.White;
        public override Color ImageMarginGradientEnd       => Color.White;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Helper: build ToolStripMenuItem with 16×16 icon
    // ──────────────────────────────────────────────────────────────────────────
    static class MI
    {
        public static ToolStripMenuItem Item(string text, string icon = null, EventHandler click = null)
        {
            var m = new ToolStripMenuItem(text) { Font = Th.UiFont };
            if (icon != null) m.Image = Icons.Get(icon);
            if (click != null) m.Click += click;
            return m;
        }
        public static ToolStripMenuItem Sub(string text, string icon = null)
        {
            var m = new ToolStripMenuItem(text) { Font = Th.UiFont };
            if (icon != null) m.Image = Icons.Get(icon);
            return m;
        }
        public static ToolStripSeparator Sep() => new ToolStripSeparator();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TOP NAV BAR  (34 px)
    //  Back · Forward · RecentLoc▾ · Up · [PathBox] · RecentFold▾ · [12] ·
    //  [SearchBox 202] · [12]
    // ══════════════════════════════════════════════════════════════════════════
    class TopNavBar : Panel
    {
        public const int H = 34;
        const int BTN_H = 22, BTN_Y = 6, ICO = 16, SM_W = 14;

        enum Hit { None, Back, Fwd, RecLoc, Up, RecFold }
        Hit _hov, _prs;

        Rectangle _rBack, _rFwd, _rRecLoc, _rUp, _rRecFold;

        TextBox _pathBox;
        Panel   _searchWrap;
        TextBox _searchBox;

        bool _backOn, _fwdOn;

        public string  CurrentPath { get => _pathBox.Text; set => _pathBox.Text = value; }
        public bool    BackEnabled   { set { _backOn = value; Invalidate(); } }
        public bool    ForwardEnabled{ set { _fwdOn  = value; Invalidate(); } }

        public event EventHandler BackClick, ForwardClick, UpClick;
        public event Action<string> Navigate, SearchChanged;

        public TopNavBar()
        {
            Height = H; Dock = DockStyle.Top; BackColor = Th.Bg;
            DoubleBuffered = true; Padding = Padding.Empty;

            _pathBox = new TextBox
            {
                BorderStyle = BorderStyle.None, Font = Th.UiFont,
                BackColor = Color.White, ForeColor = Th.TxtColor,
            };
            _pathBox.KeyDown += (s, e) =>
            { if (e.KeyCode == Keys.Return) { Navigate?.Invoke(_pathBox.Text); e.SuppressKeyPress = true; } };
            Controls.Add(_pathBox);

            _searchWrap = new Panel { BackColor = Color.White };
            _searchWrap.Paint += PaintSearchWrap;

            _searchBox = new TextBox
            {
                BorderStyle = BorderStyle.None, Font = Th.UiFont,
                BackColor = Color.White, ForeColor = Th.TxtDisabled, Text = "Search"
            };
            _searchBox.GotFocus  += (s, e) => { if (_searchBox.Text == "Search") { _searchBox.Text = ""; _searchBox.ForeColor = Th.TxtColor; } };
            _searchBox.LostFocus += (s, e) => { if (_searchBox.Text == "") { _searchBox.Text = "Search"; _searchBox.ForeColor = Th.TxtDisabled; } };
            _searchBox.TextChanged += (s, e) => SearchChanged?.Invoke(_searchBox.Text == "Search" ? "" : _searchBox.Text);

            _searchWrap.Controls.Add(_searchBox);
            Controls.Add(_searchWrap);

            MouseMove   += (s, e) => { var h = HitAt(e.Location); if (h != _hov) { _hov = h; Invalidate(); } };
            MouseLeave  += (s, e) => { _hov = Hit.None; Invalidate(); };
            MouseDown   += (s, e) => { if (e.Button == MouseButtons.Left) { _prs = HitAt(e.Location); Invalidate(); } };
            MouseUp     += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                var h = HitAt(e.Location); _prs = Hit.None; Invalidate();
                if (h == Hit.Back  && _backOn) BackClick?.Invoke(this, EventArgs.Empty);
                if (h == Hit.Fwd   && _fwdOn)  ForwardClick?.Invoke(this, EventArgs.Empty);
                if (h == Hit.Up)                UpClick?.Invoke(this, EventArgs.Empty);
            };
            Resize += (s, e) => Layout2();
        }

        void PaintSearchWrap(object s, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            using (var p = new Pen(Th.PaneSep))
                e.Graphics.DrawRectangle(p, 0, 0, _searchWrap.Width - 1, _searchWrap.Height - 1);
            e.Graphics.DrawImage(Icons.Get("search"), _searchWrap.Width - 19, 3, 14, 14);
        }

        void Layout2()
        {
            int x = 2;
            _rBack   = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28;
            _rFwd    = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28;
            _rRecLoc = new Rectangle(x, BTN_Y, SM_W, BTN_H); x += SM_W + 2;
            _rUp     = new Rectangle(x, BTN_Y, 28, BTN_H); x += 28 + 2;

            const int searchW = 202, gap = 12, rfW = SM_W;
            int pathRight = Width - gap - searchW - gap - rfW - 2;
            int pathW     = Math.Max(60, pathRight - x - 1);
            _pathBox.SetBounds(x + 3, BTN_Y + 3, pathW - 6, BTN_H - 6);
            x += pathW + 2;
            _rRecFold = new Rectangle(x, BTN_Y, rfW, BTN_H); x += rfW + gap;
            _searchWrap.SetBounds(x, BTN_Y, searchW, BTN_H);
            _searchBox.SetBounds(3, 3, searchW - 22, BTN_H - 6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.Bg);
            using (var p = new Pen(Th.PaneSep)) g.DrawLine(p, 0, H - 1, Width, H - 1);

            DrawNavBtn(g, _rBack,   "backward_arrow", _backOn, Hit.Back);
            DrawNavBtn(g, _rFwd,    "forward_arrow",  _fwdOn,  Hit.Fwd);
            DrawArrowBtn(g, _rRecLoc, Hit.RecLoc);
            DrawNavBtn(g, _rUp,     "up_arrow",       true,    Hit.Up);

            // Path box border
            int px = _pathBox.Left - 3, py = BTN_Y;
            int pw = _pathBox.Width + 6;
            using (var p = new Pen(Th.PaneSep)) g.DrawRectangle(p, px, py, pw - 1, BTN_H - 1);

            DrawArrowBtn(g, _rRecFold, Hit.RecFold);
        }

        void DrawNavBtn(Graphics g, Rectangle r, string ico, bool en, Hit btn)
        {
            if (_prs == btn && en) Th.FillBtnPress(g, r);
            else if (_hov == btn && en) Th.FillBtnHover(g, r);

            var img = Icons.Get(ico);
            int ix  = r.X + (r.Width  - ICO) / 2;
            int iy  = r.Y + (r.Height - ICO) / 2;
            if (en)
            {
                g.DrawImage(img, ix, iy, ICO, ICO);
            }
            else
            {
                // Greyed-out: use colour matrix, draw at SAME size (no scaling bug)
                var ia = new System.Drawing.Imaging.ImageAttributes();
                var cm = new System.Drawing.Imaging.ColorMatrix();
                cm.Matrix00 = cm.Matrix11 = cm.Matrix22 = 0.4f;
                cm.Matrix33 = 0.5f;
                ia.SetColorMatrix(cm);
                g.DrawImage(img,
                    new Rectangle(ix, iy, ICO, ICO),
                    0, 0, img.Width, img.Height,
                    GraphicsUnit.Pixel, ia);
            }
        }

        void DrawArrowBtn(Graphics g, Rectangle r, Hit btn)
        {
            if (_prs == btn) Th.FillBtnPress(g, r);
            else if (_hov == btn) Th.FillBtnHover(g, r);
            Th.DropArrow(g, r.X + r.Width / 2, r.Y + r.Height / 2, Th.TxtColor);
        }

        Hit HitAt(Point p)
        {
            if (_rBack.Contains(p))    return Hit.Back;
            if (_rFwd.Contains(p))     return Hit.Fwd;
            if (_rRecLoc.Contains(p))  return Hit.RecLoc;
            if (_rUp.Contains(p))      return Hit.Up;
            if (_rRecFold.Contains(p)) return Hit.RecFold;
            return Hit.None;
        }
        protected override void OnCreateControl() { base.OnCreateControl(); Layout2(); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COMMAND BAR  (31 px)
    //  [3] Organize(91) [2] NewFolder(88) [flex]
    //  ChangeView(27) MoreOpts(19) [10] Preview(28) [8] Help(28) [9]
    // ══════════════════════════════════════════════════════════════════════════
    class CommandBar : Panel
    {
        public const int H = 31;
        const int BTN_Y = 3, BTN_H = 26;

        enum Hit { None, Org, NF, CV, MO, Prev, Help }
        Hit _hov, _prs;

        Rectangle _rOrg, _rNF, _rCV, _rMO, _rPrev, _rHelp;

        ContextMenuStrip _orgMenu, _viewMenu;

        // Events for ExplorerForm to wire up
        public event EventHandler      NewFolderClick, PreviewClick, HelpClick;
        public event Action<ViewMode>  ViewChanged;
        // Organize actions
        public event EventHandler OrgCut, OrgCopy, OrgPaste, OrgUndo, OrgRedo,
                                   OrgSelectAll, OrgDelete, OrgRename, OrgProperties, OrgClose;

        public CommandBar()
        {
            Height = H; Dock = DockStyle.Top; BackColor = Th.Bg;
            DoubleBuffered = true; Padding = Padding.Empty;
            BuildOrganizeMenu(); BuildViewMenu();

            MouseMove   += (s, e) => { var h = HitAt(e.Location); if (h != _hov) { _hov = h; Invalidate(); } };
            MouseLeave  += (s, e) => { _hov = Hit.None; Invalidate(); };
            MouseDown   += (s, e) => { if (e.Button == MouseButtons.Left) { _prs = HitAt(e.Location); Invalidate(); } };
            MouseUp     += OnMU;
            Resize      += (s, e) => DoLayout();
        }

        void DoLayout()
        {
            int x = 3;
            _rOrg = new Rectangle(x, BTN_Y, 91, BTN_H); x += 93;
            _rNF  = new Rectangle(x, BTN_Y, 88, BTN_H); x += 88;

            int rx = Width - 9;
            rx -= 28; _rHelp = new Rectangle(rx, BTN_Y, 28, BTN_H);
            rx -= 8;
            rx -= 28; _rPrev = new Rectangle(rx, BTN_Y, 28, BTN_H);
            rx -= 10;
            rx -= 19; _rMO   = new Rectangle(rx, BTN_Y, 19, BTN_H);
            rx -= 27; _rCV   = new Rectangle(rx, BTN_Y, 27, BTN_H);
        }

        void BuildOrganizeMenu()
        {
            _orgMenu = new ContextMenuStrip { Renderer = new ExMenuRenderer(), ShowImageMargin = true };
            _orgMenu.Items.AddRange(new ToolStripItem[]
            {
                MI.Item("Cut",               "cut",       (s,e)=>OrgCut?.Invoke(this,EventArgs.Empty)),
                MI.Item("Copy",              "copy",      (s,e)=>OrgCopy?.Invoke(this,EventArgs.Empty)),
                MI.Item("Paste",             "paste",     (s,e)=>OrgPaste?.Invoke(this,EventArgs.Empty)),
                MI.Item("Undo",              "undo",      (s,e)=>OrgUndo?.Invoke(this,EventArgs.Empty)),
                MI.Item("Redo",              "redo",      (s,e)=>OrgRedo?.Invoke(this,EventArgs.Empty)),
                MI.Sep(),
                MI.Item("Select All",        "selectall", (s,e)=>OrgSelectAll?.Invoke(this,EventArgs.Empty)),
                MI.Sep(),
                MI.Item("Layout",            "change_view"),
                MI.Item("Options",           "options"),
                MI.Sep(),
                MI.Item("Delete",            "delete",    (s,e)=>OrgDelete?.Invoke(this,EventArgs.Empty)),
                MI.Item("Rename",            "rename",    (s,e)=>OrgRename?.Invoke(this,EventArgs.Empty)),
                MI.Item("Remove Properties", "properties"),
                MI.Item("Properties",        "properties",(s,e)=>OrgProperties?.Invoke(this,EventArgs.Empty)),
                MI.Sep(),
                MI.Item("Close",             "file",      (s,e)=>OrgClose?.Invoke(this,EventArgs.Empty)),
            });
        }

        void BuildViewMenu()
        {
            _viewMenu = new ContextMenuStrip { Renderer = new ExMenuRenderer(), ShowImageMargin = true };
            var modes = new (string, ViewMode)[]
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
            foreach (var (lbl, vm) in modes)
            {
                var vm2 = vm;
                _viewMenu.Items.Add(MI.Item(lbl, "change_view", (s,e)=>ViewChanged?.Invoke(vm2)));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.Bg);
            using (var p = new Pen(Th.PaneSep)) g.DrawLine(p, 0, H - 1, Width, H - 1);

            // Organize – text only (no icon on button face)
            DrawTxtBtn(g, _rOrg, "Organize", Hit.Org, dropArrow: true);

            // New folder – text only (no icon on button face)
            DrawTxtBtn(g, _rNF, "New folder", Hit.NF, dropArrow: false);

            // ChangeView icon-only + adjacent drop arrow
            DrawIcoBtn(g, _rCV, "change_view", Hit.CV);
            DrawDropBtn(g, _rMO, Hit.MO);
            using (var sp = new Pen(Th.SepColor))
                g.DrawLine(sp, _rMO.Left, _rMO.Top + 3, _rMO.Left, _rMO.Bottom - 3);

            DrawIcoBtn(g, _rPrev, "preview_pane", Hit.Prev);
            DrawIcoBtn(g, _rHelp, "help",         Hit.Help);
        }

        void DrawTxtBtn(Graphics g, Rectangle r, string text, Hit btn, bool dropArrow)
        {
            if (_prs == btn) Th.FillBtnPress(g, r);
            else if (_hov == btn) Th.FillBtnHover(g, r);

            int textW = dropArrow ? r.Width - 14 : r.Width - 4;
            var rf = new RectangleF(r.X + 4, r.Y, textW, r.Height);
            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(text, Th.UiFont, Brushes.Black, rf, sf);
            if (dropArrow) Th.DropArrow(g, r.Right - 8, r.Y + r.Height / 2, Th.TxtColor);
        }

        void DrawIcoBtn(Graphics g, Rectangle r, string ico, Hit btn)
        {
            if (_prs == btn) Th.FillBtnPress(g, r);
            else if (_hov == btn) Th.FillBtnHover(g, r);
            g.DrawImage(Icons.Get(ico), r.X + (r.Width - 16) / 2, r.Y + (r.Height - 16) / 2, 16, 16);
        }

        void DrawDropBtn(Graphics g, Rectangle r, Hit btn)
        {
            if (_prs == btn) Th.FillBtnPress(g, r);
            else if (_hov == btn) Th.FillBtnHover(g, r);
            Th.DropArrow(g, r.X + r.Width / 2, r.Y + r.Height / 2 + 1, Th.TxtColor);
        }

        Hit HitAt(Point p)
        {
            if (_rOrg.Contains(p)) return Hit.Org;
            if (_rNF.Contains(p))  return Hit.NF;
            if (_rCV.Contains(p))  return Hit.CV;
            if (_rMO.Contains(p))  return Hit.MO;
            if (_rPrev.Contains(p))return Hit.Prev;
            if (_rHelp.Contains(p))return Hit.Help;
            return Hit.None;
        }

        void OnMU(object s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var h = HitAt(e.Location); _prs = Hit.None; Invalidate();
            switch (h)
            {
                case Hit.Org:  _orgMenu.Show(this, new Point(_rOrg.Left, H)); break;
                case Hit.NF:   NewFolderClick?.Invoke(this, EventArgs.Empty); break;
                case Hit.CV:
                case Hit.MO:   _viewMenu.Show(this, new Point(_rCV.Left, H)); break;
                case Hit.Prev: PreviewClick?.Invoke(this, EventArgs.Empty); break;
                case Hit.Help: HelpClick?.Invoke(this, EventArgs.Empty); break;
            }
        }
        protected override void OnCreateControl() { base.OnCreateControl(); DoLayout(); }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TREE PANE  (left, resizable)
    //  Quick Access → Desktop, Downloads, Documents, Pictures
    //  This PC → 3D Objects, Desktop, Documents, Downloads, Music, Pictures,
    //            Videos, [drives]
    //  Network
    //  Root indent 8 px ; +16 px per level ; row height 22 px
    //  Vertical scroll only ; 2 px right margin
    // ══════════════════════════════════════════════════════════════════════════
    class TreePane : Panel
    {
        const int ROW_H = 22, ROOT_X = 8, LVL = 16, ICO = 16, ARW = 12;

        List<TreeNode2> _flat = new List<TreeNode2>();
        TreeNode2 _root, _selected;
        int _scrollY, _totalH;
        VScrollBar _vsb;

        public event Action<TreeNode2> NodeSelected;

        public TreePane()
        {
            BackColor = Th.TreeBg; DoubleBuffered = true; Padding = Padding.Empty;
            _vsb = new VScrollBar { Dock = DockStyle.Right, Minimum = 0, Visible = false };
            _vsb.ValueChanged += (s, e) => { _scrollY = _vsb.Value; Invalidate(); };
            Controls.Add(_vsb);

            MouseDown  += OnMD;
            MouseWheel += OnMW;
            Resize     += (s, e) => UpdateScroll();

            Build();
        }

        void Build()
        {
            _root = V("__root__", null, null, virtual_: true);
            _root.Expanded = true;

            var qa = Child(_root, "Quick access", null, "quick_access", virt: true, root: true);
            qa.Expanded = true;
            Child(qa, "Desktop",   SF(Environment.SpecialFolder.DesktopDirectory), "desktop");
            Child(qa, "Downloads", Downloads(), "downloads");
            Child(qa, "Documents", SF(Environment.SpecialFolder.MyDocuments), "documents");
            Child(qa, "Pictures",  SF(Environment.SpecialFolder.MyPictures),  "pictures");

            var tpc = Child(_root, "This PC", null, "this_pc", virt: true, root: true);
            Child(tpc, "3D Objects", null, "3dobjects");
            Child(tpc, "Desktop",    SF(Environment.SpecialFolder.DesktopDirectory), "desktop");
            Child(tpc, "Documents",  SF(Environment.SpecialFolder.MyDocuments), "documents");
            Child(tpc, "Downloads",  Downloads(), "downloads");
            Child(tpc, "Music",      SF(Environment.SpecialFolder.MyMusic),    "music");
            Child(tpc, "Pictures",   SF(Environment.SpecialFolder.MyPictures), "pictures");
            Child(tpc, "Videos",     SF(Environment.SpecialFolder.MyVideos),   "videos");
            foreach (var drv in DriveInfo.GetDrives())
            {
                try
                {
                    string lbl = drv.IsReady && drv.VolumeLabel.Length > 0
                        ? $"{drv.VolumeLabel} ({drv.Name.TrimEnd('\\')})"
                        : $"Local Disk ({drv.Name.TrimEnd('\\')})";
                    var d = Child(tpc, lbl, drv.RootDirectory.FullName, "drives");
                    d.HasChildren = true;
                }
                catch { }
            }

            Child(_root, "Network", null, "network", virt: true, root: true);

            Rebuild();
        }

        static string SF(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        static string Downloads() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        static TreeNode2 V(string lbl, string path, string ico, bool virtual_ = false)
            => new TreeNode2 { Label = lbl, Path = path, IconName = ico, IsVirtual = virtual_ };

        TreeNode2 Child(TreeNode2 par, string lbl, string path, string ico,
                        bool virt = false, bool root = false)
        {
            var n = new TreeNode2
            {
                Label = lbl, Path = path, IconName = ico,
                IsVirtual = virt, IsRoot = root,
                Parent = par, Level = par == _root ? 0 : par.Level + 1,
                HasChildren = !virt && path != null,
            };
            par.Children.Add(n); return n;
        }

        void Rebuild()
        {
            _flat.Clear();
            Flatten(_root);
            _totalH = _flat.Count * ROW_H;
            UpdateScroll();
            Invalidate();
        }

        void Flatten(TreeNode2 n)
        {
            foreach (var c in n.Children) { _flat.Add(c); if (c.Expanded) Flatten(c); }
        }

        void UpdateScroll()
        {
            int vis = ClientSize.Height;
            _vsb.Visible = _totalH > vis;
            if (_vsb.Visible)
            {
                _vsb.Maximum     = Math.Max(0, _totalH - vis + 100);
                _vsb.SmallChange = ROW_H; _vsb.LargeChange = 100;
                _scrollY = Math.Min(_scrollY, Math.Max(0, _totalH - vis));
                _vsb.Value = _scrollY;
            }
            else _scrollY = 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.TreeBg);
            int lw = Width - (_vsb.Visible ? _vsb.Width : 0) - 2;

            for (int i = 0; i < _flat.Count; i++)
            {
                var n = _flat[i];
                int y = -_scrollY + i * ROW_H;
                if (y + ROW_H < 0) continue;
                if (y > Height) break;
                n.Bounds = new Rectangle(0, y, lw, ROW_H);

                if (n == _selected) Th.FillSel(g, new Rectangle(0, y, lw, ROW_H - 1));
                if (n.IsRoot && i > 0)
                    using (var sp = new Pen(Th.SepColor)) g.DrawLine(sp, 0, y, lw, y);

                int indent = ROOT_X + n.Level * LVL;
                bool hasKids = n.Children.Count > 0 || n.HasChildren;
                if (hasKids)
                {
                    int ax = indent, ay = y + ROW_H / 2;
                    DrawTriangle(g, ax, ay, n.Expanded);
                }
                g.DrawImage(Icons.Get(n.IconName ?? "folder"),
                    indent + ARW, y + (ROW_H - ICO) / 2, ICO, ICO);

                var rf = new RectangleF(indent + ARW + ICO + 3, y + 1, lw - indent - ARW - ICO - 5, ROW_H - 2);
                var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(n.Label, n.IsRoot ? Th.UiBold : Th.UiFont, Brushes.Black, rf, sf);
            }
            using (var p = new Pen(Th.PaneSep)) g.DrawLine(p, lw, 0, lw, Height);
        }

        static void DrawTriangle(Graphics g, int cx, int cy, bool open)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Point[] pts = open
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
            int indent = ROOT_X + n.Level * LVL;
            bool hasKids = n.Children.Count > 0 || n.HasChildren;
            if (hasKids && e.X >= indent - 4 && e.X <= indent + ARW + 2)
            { Toggle(n); return; }
            _selected = n; Invalidate();
            if (e.Button == MouseButtons.Left) NodeSelected?.Invoke(n);
        }

        void Toggle(TreeNode2 n)
        {
            if (!n.Expanded)
            {
                if (n.Children.Count == 0 && n.Path != null && !n.IsVirtual) LoadFs(n);
                n.Expanded = true;
            }
            else n.Expanded = false;
            Rebuild();
        }

        void LoadFs(TreeNode2 n)
        {
            try
            {
                foreach (var d in Directory.GetDirectories(n.Path))
                {
                    try
                    {
                        n.Children.Add(new TreeNode2
                        {
                            Label = Path.GetFileName(d), Path = d, IconName = "folder",
                            Parent = n, Level = n.Level + 1, HasChildren = true,
                        });
                    }
                    catch { }
                }
                n.HasChildren = false;
            }
            catch { }
        }

        void OnMW(object s, MouseEventArgs e)
        {
            _scrollY = Math.Max(0, _scrollY - e.Delta / 3);
            if (_vsb.Visible) { _scrollY = Math.Min(_scrollY, _vsb.Maximum - _vsb.LargeChange + 1); _vsb.Value = _scrollY; }
            Invalidate();
        }

        public void SelectPath(string path)
        {
            if (path == null) { _selected = null; Invalidate(); return; }
            foreach (var n in _flat)
                if (n.Path != null && n.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                { _selected = n; Invalidate(); return; }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CONTENT PANE  (right side)
    //  Column header: white bg, 25 px tall, #D9EBF9 on hover, columns resizable
    //  Default col widths: Name=272  Date=120  Type=120  Size=80
    //  Row height: 22 px
    //  Hover: #E5E3FF (no border)
    //  Selected active:   #CCE8FF fill + #99D1FF border
    //  Selected inactive: folder=#D9D9D9 fill | file=#99D1FF border only
    // ══════════════════════════════════════════════════════════════════════════
    class ContentPane : Panel
    {
        const int HDR_H = 25, ROW_H = 22, ICO = 16, DIV_HIT = 4;

        // Column widths (user-resizable)
        int _wN = 272, _wD = 120, _wT = 120, _wS = 80;

        List<ContentItem> _items    = new List<ContentItem>();
        HashSet<int>      _sel      = new HashSet<int>();
        int               _lastSel  = -1;
        bool              _focused;

        SortCol _sortCol = SortCol.Name;
        SortDir _sortDir = SortDir.Asc;
        int     _scrollY;

        // Column resize state
        int  _resizeCol   = -1;   // 0=Name 1=Date 2=Type  (-1=none)
        int  _resizeStartX, _resizeStartW;
        int  _hdrHovCol   = -1;   // header column hover  (-1=none)
        int  _hdrHovDiv   = -1;   // divider hover        (-1=none)

        // Row hover
        int  _hovRow = -1;

        // Marquee
        bool  _marquee;
        Point _marqA, _marqB;

        VScrollBar _vsb;
        ContextMenuStrip _bgMenu, _folderMenu, _fileMenu;

        public string CurrentPath { get; private set; } = "";
        public event Action<ContentItem> ItemActivated;

        // Clipboard-style cut tracking
        HashSet<string> _cutPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ContentPane()
        {
            BackColor = Th.ContentBg; DoubleBuffered = true; Padding = Padding.Empty;
            SetStyle(ControlStyles.Selectable, true); TabStop = true;

            _vsb = new VScrollBar { Dock = DockStyle.Right, Minimum = 0, Visible = false };
            _vsb.ValueChanged += (s, e) => { _scrollY = _vsb.Value; Invalidate(); };
            Controls.Add(_vsb);

            GotFocus  += (s, e) => { _focused = true;  Invalidate(); };
            LostFocus += (s, e) => { _focused = false; Invalidate(); };

            MouseDown   += OnMD;
            MouseMove   += OnMM;
            MouseUp     += OnMU;
            MouseWheel  += OnMW;
            MouseLeave  += (s, e) => { _hovRow = -1; _hdrHovCol = -1; _hdrHovDiv = -1; Cursor = Cursors.Default; Invalidate(); };
            DoubleClick += OnDbl;
            Resize      += (s, e) => UpdateScroll();

            BuildMenus();
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void LoadPath(string path)
        {
            CurrentPath = path; _items.Clear(); _sel.Clear(); _lastSel = -1; _scrollY = 0;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) { Invalidate(); return; }
            try
            {
                foreach (var d in Directory.GetDirectories(path))
                    try
                    {
                        var di = new DirectoryInfo(d);
                        _items.Add(new ContentItem { Name = di.Name, FullPath = di.FullName,
                            DateModified = di.LastWriteTime, ItemType = "File folder", IsDirectory = true });
                    }
                    catch { }
                foreach (var f in Directory.GetFiles(path))
                    try
                    {
                        var fi = new FileInfo(f);
                        _items.Add(new ContentItem { Name = fi.Name, FullPath = fi.FullName,
                            DateModified = fi.LastWriteTime, ItemType = TypeStr(fi.Extension),
                            Size = fi.Length, IsDirectory = false });
                    }
                    catch { }
            }
            catch { }
            SortItems(); UpdateScroll(); Invalidate();
        }

        public void SelectAll()
        { for (int i = 0; i < _items.Count; i++) _sel.Add(i); Invalidate(); }

        public void CutSelected()
        {
            _cutPaths.Clear();
            foreach (int i in _sel) _cutPaths.Add(_items[i].FullPath);
            CopyToClipboard(cut: true);
        }

        public void CopySelected() => CopyToClipboard(cut: false);

        public void PasteFromClipboard()
        {
            if (!Clipboard.ContainsFileDropList() || !Directory.Exists(CurrentPath)) return;
            var files = Clipboard.GetFileDropList();
            foreach (string src in files)
            {
                try
                {
                    string name = Path.GetFileName(src);
                    string dest = Path.Combine(CurrentPath, name);
                    if (File.Exists(src))  File.Copy(src, dest, false);
                    else if (Directory.Exists(src)) CopyDir(src, dest);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Paste Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            LoadPath(CurrentPath);
        }

        public void DeleteSelected()
        {
            if (_sel.Count == 0) return;
            var names = _sel.Select(i => _items[i].Name).Take(5).ToList();
            string msg = _sel.Count == 1
                ? $"Are you sure you want to delete '{names[0]}'?"
                : $"Are you sure you want to delete these {_sel.Count} items?";
            if (MessageBox.Show(msg, "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            foreach (int i in _sel.OrderByDescending(x => x))
            {
                var item = _items[i];
                try
                {
                    if (item.IsDirectory) Directory.Delete(item.FullPath, true);
                    else File.Delete(item.FullPath);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
            LoadPath(CurrentPath);
        }

        public void RenameSelected()
        {
            if (_sel.Count != 1) return;
            var item = _items[_sel.First()];
            string newName = Microsoft.VisualBasic.Interaction.InputBox("Enter new name:", "Rename", item.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;
            try
            {
                string newPath = Path.Combine(Path.GetDirectoryName(item.FullPath) ?? "", newName);
                if (item.IsDirectory) Directory.Move(item.FullPath, newPath);
                else File.Move(item.FullPath, newPath);
                LoadPath(CurrentPath);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── Layout helpers ────────────────────────────────────────────────────
        int ListW() => Width - (_vsb.Visible ? _vsb.Width : 0);
        int TotalW() => _wN + _wD + _wT + _wS;

        // Column left-x positions
        int X0() => 0;
        int X1() => _wN;
        int X2() => _wN + _wD;
        int X3() => _wN + _wD + _wT;

        // Divider X positions (right edge of each col except last)
        int Div0() => _wN;
        int Div1() => _wN + _wD;
        int Div2() => _wN + _wD + _wT;

        bool NearDiv(int x, int div) => Math.Abs(x - div) <= DIV_HIT;

        int ColAt(int x) // which column header the mouse is over (-1 if none)
        {
            if (x < 0) return -1;
            if (x < _wN)          return 0;
            if (x < _wN + _wD)    return 1;
            if (x < _wN + _wD + _wT) return 2;
            if (x < TotalW())     return 3;
            return -1;
        }

        int DivAt(int x)
        {
            if (NearDiv(x, Div0())) return 0;
            if (NearDiv(x, Div1())) return 1;
            if (NearDiv(x, Div2())) return 2;
            return -1;
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Th.ContentBg);
            DrawHeader(g);
            DrawRows(g);
            if (_marquee) DrawMarquee(g);
        }

        void DrawHeader(Graphics g)
        {
            int tw = Math.Min(TotalW(), ListW());
            g.FillRectangle(Brushes.White, 0, 0, ListW(), HDR_H);
            using (var hbord = new Pen(Th.HdrBorder)) g.DrawLine(hbord, 0, HDR_H - 1, ListW(), HDR_H - 1);

            var cols = new (int x, int w, string lbl, SortCol col)[]
            {
                (X0(), _wN, "Name",          SortCol.Name),
                (X1(), _wD, "Date modified", SortCol.Date),
                (X2(), _wT, "Type",          SortCol.Type),
                (X3(), _wS, "Size",          SortCol.Size),
            };

            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

            for (int i = 0; i < cols.Length; i++)
            {
                var (x, w, lbl, col) = cols[i];
                if (x >= ListW()) break;
                int clippedW = Math.Min(w, ListW() - x);

                // Hover highlight
                if (_hdrHovCol == i && _hdrHovDiv == -1)
                    g.FillRectangle(new SolidBrush(Th.HdrHover), x, 0, clippedW, HDR_H - 1);

                // Label
                g.DrawString(lbl, Th.UiFont, Brushes.Black,
                    new RectangleF(x + 4, 0, clippedW - 14, HDR_H), fmt);

                // Sort indicator
                if (col == _sortCol)
                {
                    int ax = x + clippedW - 10, ay = HDR_H / 2;
                    if (_sortDir == SortDir.Asc)
                    { g.FillPolygon(Brushes.DarkGray, new[] { new Point(ax - 3, ay + 2), new Point(ax + 3, ay + 2), new Point(ax, ay - 2) }); }
                    else
                    { g.FillPolygon(Brushes.DarkGray, new[] { new Point(ax - 3, ay - 2), new Point(ax + 3, ay - 2), new Point(ax, ay + 2) }); }
                }

                // Divider (all except last)
                if (i < 3 && x + w < ListW())
                    using (var sp = new Pen(Th.HdrBorder))
                        g.DrawLine(sp, x + w - 1, 3, x + w - 1, HDR_H - 3);
            }
        }

        void DrawRows(Graphics g)
        {
            int lw = ListW();
            int tw = Math.Min(TotalW(), lw); // row highlight ends here
            var ellipsis = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            var rightFmt = new StringFormat(ellipsis) { Alignment = StringAlignment.Far };

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int y = HDR_H + i * ROW_H - _scrollY;
                if (y + ROW_H < HDR_H) continue;
                if (y > Height) break;

                bool sel = _sel.Contains(i);
                bool hov = i == _hovRow;
                var rowR = new Rectangle(0, y, tw, ROW_H - 1);

                if (sel)
                {
                    if (_focused)
                    {
                        Th.FillSel(g, rowR);
                    }
                    else if (item.IsDirectory)
                    {
                        using (var b = new SolidBrush(Th.InactiveDirFill)) g.FillRectangle(b, rowR);
                    }
                    else
                    {
                        using (var p = new Pen(Th.SelBorder)) g.DrawRectangle(p, rowR.X, rowR.Y, rowR.Width - 1, rowR.Height - 1);
                    }
                }
                else if (hov && !sel)
                {
                    using (var b = new SolidBrush(Th.ItemHover)) g.FillRectangle(b, rowR); // borderless
                }

                // Icon
                string ico = item.IsDirectory ? "folder" : "file";
                g.DrawImage(Icons.Get(ico), X0() + 2, y + (ROW_H - ICO) / 2, ICO, ICO);

                // Name (with "..." truncation)
                var nR = new RectangleF(X0() + ICO + 6, y, _wN - ICO - 10, ROW_H);
                g.DrawString(item.Name, Th.UiFont, Brushes.Black, nR, ellipsis);

                // Date
                if (X1() < lw)
                {
                    var dR = new RectangleF(X1() + 4, y, _wD - 8, ROW_H);
                    g.DrawString(item.DateStr, Th.UiFont, Brushes.Black, dR, ellipsis);
                }
                // Type
                if (X2() < lw)
                {
                    var tR = new RectangleF(X2() + 4, y, _wT - 8, ROW_H);
                    g.DrawString(item.ItemType, Th.UiFont, Brushes.Black, tR, ellipsis);
                }
                // Size (right-aligned)
                if (X3() < lw)
                {
                    var sR = new RectangleF(X3() + 2, y, _wS - 4, ROW_H);
                    g.DrawString(item.SizeStr, Th.UiFont, Brushes.Black, sR, rightFmt);
                }

                // Row divider (very faint)
                if (i % 2 == 0)
                    using (var rp = new Pen(Color.FromArgb(245, 245, 245)))
                        g.DrawLine(rp, 0, y + ROW_H - 1, tw, y + ROW_H - 1);
            }
        }

        void DrawMarquee(Graphics g)
        {
            int x = Math.Min(_marqA.X, _marqB.X), y = Math.Min(_marqA.Y, _marqB.Y);
            int w = Math.Abs(_marqB.X - _marqA.X), h = Math.Abs(_marqB.Y - _marqA.Y);
            using (var b = new SolidBrush(Color.FromArgb(60, Th.SelFill))) g.FillRectangle(b, x, y, w, h);
            using (var p = new Pen(Th.SelBorder)) g.DrawRectangle(p, x, y, w - 1, h - 1);
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        void OnMD(object s, MouseEventArgs e)
        {
            Focus();
            if (e.Y < HDR_H)        { HandleHdrClick(e); return; }

            int idx = RowAt(e.Y);

            if (e.Button == MouseButtons.Left)
            {
                bool ctrl  = (ModifierKeys & Keys.Control) != 0;
                bool shift = (ModifierKeys & Keys.Shift)   != 0;

                if (idx >= 0 && idx < _items.Count)
                {
                    if (ctrl)
                    { if (_sel.Contains(idx)) _sel.Remove(idx); else _sel.Add(idx); _lastSel = idx; }
                    else if (shift && _lastSel >= 0)
                    { _sel.Clear(); for (int i = Math.Min(_lastSel, idx); i <= Math.Max(_lastSel, idx); i++) _sel.Add(i); }
                    else
                    { _sel.Clear(); _sel.Add(idx); _lastSel = idx; }
                }
                else
                {
                    if (!ctrl && !shift) _sel.Clear();
                    _lastSel = -1; _marquee = true; _marqA = _marqB = e.Location; Capture = true;
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (idx >= 0 && idx < _items.Count)
                {
                    if (!_sel.Contains(idx)) { _sel.Clear(); _sel.Add(idx); _lastSel = idx; }
                    Invalidate();
                    (_items[idx].IsDirectory ? _folderMenu : _fileMenu).Show(this, e.Location);
                }
                else { Invalidate(); _bgMenu.Show(this, e.Location); }
            }
            Invalidate();
        }

        void OnMM(object s, MouseEventArgs e)
        {
            if (_resizeCol >= 0)
            {
                // Column resizing
                int dx = e.X - _resizeStartX;
                int newW = Math.Max(40, _resizeStartW + dx);
                switch (_resizeCol) { case 0: _wN = newW; break; case 1: _wD = newW; break; case 2: _wT = newW; break; }
                Invalidate(); return;
            }

            if (_marquee) { _marqB = e.Location; UpdateMarqueeSel(); Invalidate(); return; }

            if (e.Y < HDR_H)
            {
                int div = DivAt(e.X);
                int col = div >= 0 ? -1 : ColAt(e.X);
                if (div != _hdrHovDiv || col != _hdrHovCol) { _hdrHovDiv = div; _hdrHovCol = col; Invalidate(); }
                Cursor = div >= 0 ? Cursors.VSplit : Cursors.Default;
                _hovRow = -1;
            }
            else
            {
                if (_hdrHovDiv != -1 || _hdrHovCol != -1) { _hdrHovDiv = -1; _hdrHovCol = -1; Invalidate(); }
                Cursor = Cursors.Default;
                int idx = RowAt(e.Y);
                if (idx != _hovRow) { _hovRow = idx; Invalidate(); }
            }
        }

        void OnMU(object s, MouseEventArgs e)
        {
            if (_resizeCol >= 0) { _resizeCol = -1; Capture = false; Cursor = Cursors.Default; Invalidate(); return; }
            if (_marquee) { _marquee = false; Capture = false; Invalidate(); }
        }

        void HandleHdrClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int div = DivAt(e.X);
                if (div >= 0)
                {
                    // Start column resize
                    _resizeCol = div;
                    _resizeStartX = e.X;
                    switch (div) { case 0: _resizeStartW = _wN; break; case 1: _resizeStartW = _wD; break; case 2: _resizeStartW = _wT; break; }
                    Capture = true; return;
                }
                // Sort click
                var col = ColAt(e.X);
                SortCol sc = col == 0 ? SortCol.Name : col == 1 ? SortCol.Date : col == 2 ? SortCol.Type : SortCol.Size;
                if (col >= 0)
                {
                    if (_sortCol == sc) _sortDir = _sortDir == SortDir.Asc ? SortDir.Desc : SortDir.Asc;
                    else { _sortCol = sc; _sortDir = SortDir.Asc; }
                    SortItems(); _sel.Clear(); Invalidate();
                }
            }
        }

        void UpdateMarqueeSel()
        {
            int y1 = Math.Min(_marqA.Y, _marqB.Y), y2 = Math.Max(_marqA.Y, _marqB.Y);
            _sel.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                int ry = HDR_H + i * ROW_H - _scrollY;
                if (ry + ROW_H > y1 && ry < y2) _sel.Add(i);
            }
        }

        void OnMW(object s, MouseEventArgs e)
        {
            _scrollY = Math.Max(0, _scrollY - e.Delta / 3);
            if (_vsb.Visible) { _scrollY = Math.Min(_scrollY, Math.Max(0, _vsb.Maximum - _vsb.LargeChange)); _vsb.Value = _scrollY; }
            Invalidate();
        }

        void OnDbl(object s, EventArgs e)
        {
            var mp = PointToClient(Cursor.Position);
            int idx = RowAt(mp.Y);
            if (idx >= 0 && idx < _items.Count) ItemActivated?.Invoke(_items[idx]);
        }

        int RowAt(int y) => y < HDR_H ? -1 : (_scrollY + y - HDR_H) / ROW_H;

        void UpdateScroll()
        {
            int total = _items.Count * ROW_H, vis = Math.Max(1, ClientSize.Height - HDR_H);
            _vsb.Visible = total > vis;
            if (_vsb.Visible)
            { _vsb.Maximum = Math.Max(0, total - vis + 100); _vsb.SmallChange = ROW_H; _vsb.LargeChange = 100; }
            else _scrollY = 0;
        }

        void SortItems()
        {
            IEnumerable<ContentItem> s = _sortCol switch
            {
                SortCol.Name => _items.OrderBy(i => !i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase),
                SortCol.Date => _items.OrderBy(i => i.DateModified),
                SortCol.Type => _items.OrderBy(i => i.ItemType, StringComparer.OrdinalIgnoreCase),
                SortCol.Size => _items.OrderBy(i => i.Size),
                _ => _items,
            };
            if (_sortDir == SortDir.Desc) s = s.Reverse();
            _items = s.ToList();
        }

        static string TypeStr(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "File";
            return ext.ToLower() switch
            {
                ".txt"  => "Text Document",
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "Image",
                ".cs"   => "C# Source File",
                ".exe"  => "Application",
                ".dll"  => "Application Extension",
                ".zip"  => "Compressed Folder",
                ".pdf"  => "PDF Document",
                ".mp3"  => "MP3 Audio",
                ".mp4"  => "MP4 Video",
                ".html" or ".htm" => "HTML Document",
                ".xml"  => "XML Document",
                ".json" => "JSON File",
                _       => ext.TrimStart('.').ToUpper() + " File",
            };
        }

        void CopyToClipboard(bool cut)
        {
            if (_sel.Count == 0 || !Directory.Exists(CurrentPath)) return;
            var sc = new StringCollection();
            foreach (int i in _sel) sc.Add(_items[i].FullPath);
            var data = new DataObject();
            data.SetFileDropList(sc);
            if (cut)
            {
                // Mark as cut via DropEffect
                var dropEffect = new MemoryStream(4);
                dropEffect.Write(BitConverter.GetBytes((int)System.Windows.Forms.DragDropEffects.Move), 0, 4);
                data.SetData("Preferred DropEffect", dropEffect);
            }
            Clipboard.SetDataObject(data, true);
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)));
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        // ── Context menus ─────────────────────────────────────────────────────
        void BuildMenus()
        {
            var rend = new ExMenuRenderer();

            // ── Background (empty space) ─────────────────────────────────────
            _bgMenu = new ContextMenuStrip { Renderer = rend };
            var viewSub  = ViewSubMenu();
            var sortSub  = SortSubMenu();
            var grpSub   = GroupSubMenu();
            var giveSub  = GiveAccessSubMenu();
            var newSub   = NewSubMenu();
            _bgMenu.Items.AddRange(new ToolStripItem[]
            {
                viewSub, sortSub, grpSub,
                MI.Item("Refresh",        "reload",  (s,e)=>LoadPath(CurrentPath)),
                MI.Sep(),
                MI.Item("Paste",          "paste",   (s,e)=>PasteFromClipboard()),
                MI.Item("Paste shortcut", "paste"),
                MI.Item("Undo Delete",    "undo"),
                MI.Sep(),
                giveSub,
                MI.Sep(),
                newSub,
                MI.Sep(),
                MI.Item("Properties",     "properties"),
            });

            // ── Folder ────────────────────────────────────────────────────────
            _folderMenu = new ContextMenuStrip { Renderer = rend };
            var fGive = GiveAccessSubMenu();
            var fSend = SendToSubMenu();
            _folderMenu.Items.AddRange(new ToolStripItem[]
            {
                MI.Item("Open",              "folder",       (s,e)=>OpenSelected()),
                MI.Item("Open in new window","folder"),
                MI.Item("Pin to Quick access","quick_access"),
                MI.Item("Take Ownership",    "properties"),
                MI.Sep(),
                fGive,
                MI.Item("Restore",           "undo"),
                MI.Sep(),
                fSend,
                MI.Sep(),
                MI.Item("Cut",               "cut",          (s,e)=>CutSelected()),
                MI.Item("Copy",              "copy",         (s,e)=>CopySelected()),
                MI.Sep(),
                MI.Item("Create shortcut",   "shortcut"),
                MI.Item("Delete",            "delete",       (s,e)=>DeleteSelected()),
                MI.Item("Rename",            "rename",       (s,e)=>RenameSelected()),
                MI.Sep(),
                MI.Item("Properties",        "properties"),
            });

            // ── File ──────────────────────────────────────────────────────────
            _fileMenu = new ContextMenuStrip { Renderer = rend };
            var fiGive = GiveAccessSubMenu();
            var fiWith = OpenWithSubMenu();
            var fiSend = SendToSubMenu();
            _fileMenu.Items.AddRange(new ToolStripItem[]
            {
                MI.Item("Open",                  "file",        (s,e)=>OpenSelected()),
                MI.Item("Pin",                   "quick_access"),
                MI.Item("Edit",                  "rename"),
                MI.Item("Take Ownership",        "properties"),
                fiWith,
                MI.Sep(),
                fiGive,
                MI.Item("Restore previous version","undo"),
                MI.Sep(),
                fiSend,
                MI.Item("Cut",                   "cut",         (s,e)=>CutSelected()),
                MI.Item("Copy",                  "copy",        (s,e)=>CopySelected()),
                MI.Sep(),
                MI.Item("Create shortcut",       "shortcut"),
                MI.Item("Delete",                "delete",      (s,e)=>DeleteSelected()),
                MI.Item("Rename",                "rename",      (s,e)=>RenameSelected()),
                MI.Sep(),
                MI.Item("Properties",            "properties"),
            });
        }

        void OpenSelected()
        {
            if (_sel.Count == 0) return;
            var item = _items[_sel.First()];
            if (item.IsDirectory) ItemActivated?.Invoke(item);
            else try { Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true }); } catch { }
        }

        static ToolStripMenuItem ViewSubMenu()
        {
            var sub = MI.Sub("View", "change_view");
            foreach (var lbl in new[] { "Extra Large Icons","Large Icons","Medium Icons",
                                        "Small Icons","List","Details","Tiles","Content" })
                sub.DropDownItems.Add(MI.Item(lbl, "change_view"));
            return sub;
        }
        static ToolStripMenuItem SortSubMenu()
        {
            var sub = MI.Sub("Sort by", "ascending");
            sub.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI.Item("Name","organize"), MI.Item("Date modified","organize"),
                MI.Item("Type","organize"), MI.Item("Size","organize"),
                MI.Sep(),
                MI.Item("Ascending","ascending"), MI.Item("Descending","descending"),
                MI.Sep(), MI.Item("More...","options"),
            });
            return sub;
        }
        static ToolStripMenuItem GroupSubMenu()
        {
            var sub = MI.Sub("Group by", "organize");
            sub.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI.Item("Name","organize"), MI.Item("Date modified","organize"),
                MI.Item("Type","organize"), MI.Item("Size","organize"),
                MI.Sep(),
                MI.Item("Ascending","ascending"), MI.Item("Descending","descending"),
                MI.Sep(), MI.Item("More...","options"),
            });
            return sub;
        }
        static ToolStripMenuItem GiveAccessSubMenu()
        {
            var sub = MI.Sub("Give access to", "network");
            sub.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI.Item("Remove access",             "delete"),
                MI.Item("Homegroup (view)",           "network"),
                MI.Item("Homegroup (view and edit)",  "network"),
                MI.Sep(),
                MI.Item("Specific people...",         "network"),
            });
            return sub;
        }
        static ToolStripMenuItem NewSubMenu()
        {
            var sub = MI.Sub("New", "new_folder");
            sub.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI.Item("Folder",          "folder"),
                MI.Item("Shortcut",        "shortcut"),
                MI.Sep(),
                MI.Item("Bitmap image",    "file"),
                MI.Item("Contact",         "file"),
                MI.Item("Rich Text Format","file"),
                MI.Item("Text Document",   "file"),
            });
            return sub;
        }
        static ToolStripMenuItem SendToSubMenu()
        {
            var sub = MI.Sub("Send to", "sendto");
            sub.DropDownItems.AddRange(new ToolStripItem[]
            {
                MI.Item("Compressed (zipped) folder","folder"),
                MI.Item("Desktop (create shortcut)","desktop"),
                MI.Item("Mail recipient",           "file"),
                MI.Item("Documents",                "documents"),
            });
            return sub;
        }
        static ToolStripMenuItem OpenWithSubMenu()
        {
            var sub = MI.Sub("Open with", "openwith");
            sub.DropDownItems.Add(MI.Item("Choose another app...", "openwith"));
            return sub;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  SPLITTER BAR
    // ──────────────────────────────────────────────────────────────────────────
    class SplitterBar : Control
    {
        bool _drag; int _startX, _startW; Control _left;
        public SplitterBar(Control left)
        {
            _left = left; Width = 4; Cursor = Cursors.VSplit;
            BackColor = Th.PaneSep; Padding = Padding.Empty;
            MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _startX = Cursor.Position.X; _startW = _left.Width; Capture = true; } };
            MouseMove += (s, e) => { if (_drag) { _left.Width = Math.Max(100, _startW + Cursor.Position.X - _startX); Parent?.PerformLayout(); } };
            MouseUp   += (s, e) => { _drag = false; Capture = false; };
        }
        protected override void OnPaint(PaintEventArgs e) => e.Graphics.Clear(Th.PaneSep);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  STATUS BAR
    // ──────────────────────────────────────────────────────────────────────────
    class StatusBar : Panel
    {
        Label _lbl;
        public StatusBar()
        {
            Height = 22; Dock = DockStyle.Bottom; BackColor = Th.Bg; Padding = Padding.Empty;
            _lbl = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = Th.UiFont, Padding = new Padding(6, 0, 0, 0) };
            Controls.Add(_lbl);
        }
        public string Text { get => _lbl.Text; set => _lbl.Text = value; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Th.PaneSep)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MAIN FORM
    // ══════════════════════════════════════════════════════════════════════════
    class ExplorerForm : Form
    {
        TopNavBar  _nav;
        CommandBar _cmd;
        TreePane   _tree;
        ContentPane _content;
        SplitterBar _splitter;
        StatusBar   _status;
        Panel       _main;

        List<string> _hist = new List<string>();
        int _hi = -1;

        public ExplorerForm()
        {
            Text           = "File Explorer";
            MinimumSize    = new Size(700, 450);
            Size           = new Size(1100, 680);
            StartPosition  = FormStartPosition.CenterScreen;
            BackColor      = Th.Bg;
            Font           = Th.UiFont;
            Padding        = Padding.Empty;
            AutoScaleMode  = AutoScaleMode.None;

            Build();
            Wire();
            Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        void Build()
        {
            SuspendLayout();
            _nav      = new TopNavBar();
            _cmd      = new CommandBar();
            _status   = new StatusBar();

            _main = new Panel { Dock = DockStyle.Fill, Padding = Padding.Empty };

            _tree     = new TreePane    { Dock = DockStyle.Left, Width = 220 };
            _content  = new ContentPane { Dock = DockStyle.Fill };
            _splitter = new SplitterBar(_tree) { Dock = DockStyle.Left };

            _main.Controls.Add(_content);
            _main.Controls.Add(_splitter);
            _main.Controls.Add(_tree);

            Controls.Add(_main);
            Controls.Add(_status);
            Controls.Add(_cmd);
            Controls.Add(_nav);
            ResumeLayout(false);
        }

        void Wire()
        {
            // Nav bar
            _nav.BackClick    += (s, e) => GoBack();
            _nav.ForwardClick += (s, e) => GoFwd();
            _nav.UpClick      += (s, e) => GoUp();
            _nav.Navigate     += p => Navigate(p);

            // Command bar
            _cmd.NewFolderClick += (s, e) => NewFolder();
            _cmd.HelpClick      += (s, e) => OpenHelp();
            _cmd.PreviewClick   += (s, e) => _status.Text = "Preview pane (placeholder)";
            _cmd.ViewChanged    += vm => _status.Text = $"View: {vm}";
            _cmd.OrgCut         += (s, e) => _content.CutSelected();
            _cmd.OrgCopy        += (s, e) => _content.CopySelected();
            _cmd.OrgPaste       += (s, e) => _content.PasteFromClipboard();
            _cmd.OrgSelectAll   += (s, e) => _content.SelectAll();
            _cmd.OrgDelete      += (s, e) => _content.DeleteSelected();
            _cmd.OrgRename      += (s, e) => _content.RenameSelected();
            _cmd.OrgProperties  += (s, e) => ShowProperties();
            _cmd.OrgClose       += (s, e) => Close();

            // Tree
            _tree.NodeSelected += n =>
            {
                if (n.Path != null && Directory.Exists(n.Path)) Navigate(n.Path);
                else SetStatus(n.Label);
            };

            // Content
            _content.ItemActivated += item =>
            {
                if (item.IsDirectory) Navigate(item.FullPath);
                else try { Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true }); } catch { }
            };
        }

        // ── Navigation ────────────────────────────────────────────────────────
        void Navigate(string path)
        {
            if (_hi < _hist.Count - 1) _hist.RemoveRange(_hi + 1, _hist.Count - _hi - 1);
            _hist.Add(path); _hi = _hist.Count - 1;
            Apply(path);
        }

        void Apply(string path)
        {
            _nav.CurrentPath    = path;
            _nav.BackEnabled    = _hi > 0;
            _nav.ForwardEnabled = _hi < _hist.Count - 1;

            if (Directory.Exists(path))
            {
                _content.LoadPath(path);
                _tree.SelectPath(path);
                Text = $"{Path.GetFileName(path) ?? path} – File Explorer";
            }
            else
            {
                Text = $"{path} – File Explorer";
            }
            SetStatus(null);
        }

        void GoBack()  { if (_hi > 0) { _hi--; Apply(_hist[_hi]); } }
        void GoFwd()   { if (_hi < _hist.Count - 1) { _hi++; Apply(_hist[_hi]); } }
        void GoUp()
        {
            string cur = _nav.CurrentPath;
            try
            {
                string up = Directory.GetParent(cur)?.FullName;
                if (up != null) Navigate(up);
            }
            catch { }
        }

        void NewFolder()
        {
            string cur = _content.CurrentPath;
            if (!Directory.Exists(cur)) return;
            string p = Path.Combine(cur, "New folder");
            int i = 2; while (Directory.Exists(p)) p = Path.Combine(cur, $"New folder ({i++})");
            try { Directory.CreateDirectory(p); _content.LoadPath(cur); SetStatus($"Created: {Path.GetFileName(p)}"); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        static void OpenHelp()
            => Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/?LinkID=2004439") { UseShellExecute = true });

        void ShowProperties()
        {
            string p = _content.CurrentPath;
            if (Directory.Exists(p)) MessageBox.Show($"Path: {p}", "Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void SetStatus(string msg)
        {
            if (msg != null) { _status.Text = msg; return; }
            string path = _content.CurrentPath;
            if (!Directory.Exists(path)) { _status.Text = path; return; }
            try
            {
                int d = Directory.GetDirectories(path).Length, f = Directory.GetFiles(path).Length;
                _status.Text = $"{d + f} items";
            }
            catch { _status.Text = path; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if      (e.Alt && e.KeyCode == Keys.Left)  GoBack();
            else if (e.Alt && e.KeyCode == Keys.Right) GoFwd();
            else if (e.Alt && e.KeyCode == Keys.Up)    GoUp();
            else if (e.KeyCode == Keys.F5)             _content.LoadPath(_content.CurrentPath);
            else if (e.Control && e.KeyCode == Keys.A) _content.SelectAll();
            else if (e.Control && e.KeyCode == Keys.C) _content.CopySelected();
            else if (e.Control && e.KeyCode == Keys.X) _content.CutSelected();
            else if (e.Control && e.KeyCode == Keys.V) _content.PasteFromClipboard();
            else if (e.KeyCode == Keys.Delete)         _content.DeleteSelected();
            else if (e.KeyCode == Keys.F2)             _content.RenameSelected();
        }
    }
}
