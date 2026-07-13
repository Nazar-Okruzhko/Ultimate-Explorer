// ---------------------------------------------------------------------------
//  WINDOWS EXPLORER  –  GDI+ Recreation  v3
//  .NET Framework 4.8  |  Single File
// ---------------------------------------------------------------------------
//  Icons  ?  16×16 PNG files in  <exe-dir>\icons\Win10\
//  (see Icons.MakePlaceholder for names; real PNGs override auto-generated ones)
// ---------------------------------------------------------------------------
 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Collections.Specialized;
using System.Reflection;
 
// ---------------------------------------------------------------------------
//  GLOBAL FEATURE FLAGS
//  system_icons = 0  ?  use embedded PNG icons (icons\Win10\*.png)
//  system_icons = 1  ?  use Windows shell / system icons
// ---------------------------------------------------------------------------
static class Config { public static int system_icons = 0; }
 
namespace WinExplorer
{
    static class Program
    {
        [STAThread] static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ExplorerForm());
        }
    }
 
    // -------------------------------------------------------------------------
    //  Theme
    // -------------------------------------------------------------------------
    static class Th
    {
        public static readonly Color SelFill        = Color.FromArgb(204, 232, 255);
        public static readonly Color SelBorder      = Color.FromArgb(153, 209, 255);
        public static readonly Color ItemHover      = Color.FromArgb(229, 243, 255);
        public static readonly Color InactiveDirFill= Color.FromArgb(217, 217, 217);
        public static readonly Color HdrBg          = Color.White;
        public static readonly Color HdrHover       = Color.FromArgb(217, 235, 249);
        public static readonly Color HdrBorder      = Color.FromArgb(213, 213, 213);
        public static readonly Color Bg             = Color.FromArgb(240, 240, 240);
        public static readonly Color BtnHoverFill   = Color.FromArgb(229, 241, 251);
        public static readonly Color BtnHoverBord   = Color.FromArgb(0, 120, 215);
        public static readonly Color BtnPressFill   = Color.FromArgb(204, 228, 247);
        public static readonly Color BtnPressBord   = Color.FromArgb(0, 84, 153);
        public static readonly Color SepColor       = Color.FromArgb(208, 208, 208);
        public static readonly Color PaneSep        = Color.FromArgb(180, 180, 180);
        public static readonly Color TxtColor       = Color.Black;
        public static readonly Color TxtDisabled    = Color.FromArgb(160, 160, 160);
        public static readonly Color TreeBg         = Color.White;
        public static readonly Color ContentBg      = Color.White;
        public static readonly Color PreviewBg      = Color.FromArgb(250, 250, 252);
        // CommandBar
        public static readonly Color CmdBarBg  = Color.FromArgb(245,246,247); // #F5F6F7
        public static readonly Color CmdSepBot = Color.FromArgb(232,233,234); // #E8E9EA
        // Arrow button overlays
        public static readonly Color ArrHovOverlay = Color.FromArgb(40,0,120,215);   // blue tint
        public static readonly Color ArrDisOverlay = Color.FromArgb(70,170,170,170); // grey tint
        // Menu background
        public static readonly Color MenuBg    = Color.FromArgb(242,242,242); // #F2F2F2
 
        public static readonly Font  UiFont  = new Font("Segoe UI", 9f);
        public static readonly Font  UiBold  = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font  UiSmall = new Font("Segoe UI", 8f);
        public static readonly Font  Mono    = new Font("Consolas", 8.5f);
 
        public static void FillSel(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(SelFill)) g.FillRectangle(b, r);
            using (var p = new Pen(SelBorder)) g.DrawRectangle(p, r.X, r.Y, r.Width-1, r.Height-1);
        }
        public static void FillHover(Graphics g, Rectangle r)
        { using (var b = new SolidBrush(ItemHover)) g.FillRectangle(b, r); }
        public static void FillBtnHov(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(BtnHoverFill)) g.FillRectangle(b, r);
            using (var p = new Pen(BtnHoverBord)) g.DrawRectangle(p, r.X, r.Y, r.Width-1, r.Height-1);
        }
        public static void FillBtnPrs(Graphics g, Rectangle r)
        {
            using (var b = new SolidBrush(BtnPressFill)) g.FillRectangle(b, r);
            using (var p = new Pen(BtnPressBord)) g.DrawRectangle(p, r.X, r.Y, r.Width-1, r.Height-1);
        }
        public static void DropArr(Graphics g, int cx, int cy, Color c)
        { Point[] t = {new Point(cx-3,cy-2),new Point(cx+3,cy-2),new Point(cx,cy+2)}; using(var b=new SolidBrush(c)) g.FillPolygon(b,t); }
    }
 
    // -------------------------------------------------------------------------
    //  Shell Helper  (Windows icons & thumbnails via P/Invoke)
    // -------------------------------------------------------------------------
    static class Shell
    {
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
        struct SHFILEINFO
        {
            public IntPtr hIcon; public int iIcon; public uint dwAttr;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]  public string szTypeName;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Auto)]
        static extern IntPtr SHGetFileInfo(string path,uint fileAttr,ref SHFILEINFO sfi,uint sz,uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("gdi32.dll")]  static extern bool DeleteObject(IntPtr h);
 
        [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=false)]
        static extern int SHCreateItemFromParsingName(string path,IntPtr pbc,ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);
 
        [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
         Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage([In,MarshalAs(UnmanagedType.Struct)] Size sz,
                [In] uint flags, out IntPtr phbm);
        }
 
        const uint SHGFI_ICON=0x100, SHGFI_SMALL=0x001, SHGFI_LARGE=0x000,
                   SHGFI_USEATTR=0x010, SHGFI_TYPENAME=0x400, FA_NORMAL=0x080, FA_DIR=0x010;
 
        static readonly Dictionary<string,Image> _s16 = new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string,Image> _s32 = new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
 
        static string Key(string path)
        {
            if (path == null) return "";
            // Special folders each have a unique icon — key by path, not generic "__dir__"
            if (Directory.Exists(path)) return "dir:" + path.ToLower();
            string ext = Path.GetExtension(path).ToLower();
            return ext.Length > 0 ? ext : path.ToLower();
        }
 
        public static Image SmallIcon(string path)
        {
            if(Config.system_icons==0) return null;   // use embedded icons instead
            var k = Key(path);
            if (_s16.TryGetValue(k,out var c)) return c;
            var img = GetIcon(path, true);
            return _s16[k] = img;
        }
        public static Image LargeIcon(string path)
        {
            if(Config.system_icons==0) return null;
            var k = Key(path);
            if (_s32.TryGetValue(k,out var c)) return c;
            var img = GetIcon(path, false);
            return _s32[k] = img;
        }
 
        static Image GetIcon(string path, bool small)
        {
            bool exists = File.Exists(path)||Directory.Exists(path);
            bool isDir  = Directory.Exists(path);
            var sfi = new SHFILEINFO();
            uint flags = SHGFI_ICON|(small?SHGFI_SMALL:SHGFI_LARGE);
            if (!exists) flags|=SHGFI_USEATTR;
            uint fa = !exists ? (isDir?FA_DIR:FA_NORMAL) : 0u;
            var r = SHGetFileInfo(path??string.Empty,fa,ref sfi,(uint)Marshal.SizeOf(sfi),flags);
            if (r==IntPtr.Zero||sfi.hIcon==IntPtr.Zero) return null;
            Image img=null;
            try
            {
                int sz = small?16:32;
                using (var ico=Icon.FromHandle(sfi.hIcon))
                {
                    var tmp=ico.ToBitmap();
                    var dst=new Bitmap(sz,sz);
                    using(var g=Graphics.FromImage(dst)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(tmp,0,0,sz,sz);}
                    tmp.Dispose(); img=dst;
                }
            }
            catch{img=null;}
            finally{DestroyIcon(sfi.hIcon);}
            return img;
        }
 
        public static Image Thumbnail(string path, int sz=128)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var riid=new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                int hr=SHCreateItemFromParsingName(path,IntPtr.Zero,ref riid,out var fac);
                if (hr!=0||fac==null) return null;
                hr=fac.GetImage(new Size(sz,sz),0,out IntPtr hbm);
                Marshal.ReleaseComObject(fac);
                if (hr!=0||hbm==IntPtr.Zero) return null;
                var bmp=Image.FromHbitmap(hbm);
                DeleteObject(hbm);
                return bmp;
            }
            catch{return null;}
        }
 
        public static string TypeName(string path)
        {
            bool exists=File.Exists(path)||Directory.Exists(path);
            var sfi=new SHFILEINFO();
            uint flags=SHGFI_TYPENAME;
            if (!exists) flags|=SHGFI_USEATTR;
            SHGetFileInfo(path,exists?0u:FA_NORMAL,ref sfi,(uint)Marshal.SizeOf(sfi),flags);
            return string.IsNullOrEmpty(sfi.szTypeName)?Path.GetExtension(path)+" File":sfi.szTypeName;
        }
    }
 
    // -------------------------------------------------------------------------
    //  PNG icon loader (fallback if Shell fails)
    // -------------------------------------------------------------------------
    static class Icons
    {
        const int SZ=16;
 
        // Map internal icon names ? actual PNG filenames in the icons\Win10 folder
        static readonly Dictionary<string,string> NameMap=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            {"backward_arrow","previous_arrow"},
            {"forward_arrow","forward_arrow"},   // generated if missing
            {"change_view","layout"},
            {"more_options","more_options"},
            {"selectall","select_all"},
            {"quick_access","pinned"},
            {"drives","drives"},
            {"3dobjects","folder"},
            {"shortcut","file"},
            {"sendto","file"},
            {"openwith","file"},
            {"options","properties"},
            {"ascending","up_arrow"},
            {"descending","up_arrow"},
            {"organize","properties"},
            {"new_folder","folder"},
        };
 
        // Load PNG from embedded resources (namespace WinExplorer.icons.Win10.<name>.png)
        static Image LoadEmbedded(string name)
        {
            string mapped=NameMap.TryGetValue(name,out string m)?m:name;
            var asm=Assembly.GetExecutingAssembly();
            // try exact mapped name, then original
            foreach(var n2 in new[]{mapped,name})
            {
                string rn=$"WinExplorer.icons.Win10.{n2}.png";
                try
                {
                    var st=asm.GetManifestResourceStream(rn);
                    if(st==null)continue;
                    var bytes=new byte[st.Length]; st.Read(bytes,0,bytes.Length); st.Dispose();
                    using(var ms=new MemoryStream(bytes)){var img=Image.FromStream(ms);var b=new Bitmap(SZ,SZ);using(var g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(img,0,0,SZ,SZ);}img.Dispose();return b;}
                }
                catch{}
            }
            return null;
        }
        static readonly string Dir=Path.Combine(
            Path.GetDirectoryName(Application.ExecutablePath)??"","icons","Win10");
        static readonly Dictionary<string,Image> Cache=new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
 
        public static Image Get(string name)
        {
            if (Cache.TryGetValue(name,out var h)) return h;
            Image img=null;
            if(Config.system_icons==0)
            {
                img=LoadEmbedded(name);           // embedded resource
            }
            if(img==null)                         // fallback: file on disk
            {
                string fp=Path.Combine(Dir,name+".png");
                if(File.Exists(fp))try{using(var r=Image.FromFile(fp)){var b=new Bitmap(SZ,SZ);using(var g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(r,0,0,SZ,SZ);}img=b;}}catch{}
            }
            if(img==null)img=MakePH(name);        // generated placeholder
            return Cache[name]=img;
        }
 
        static Image MakePH(string n)
        {
            var b=new Bitmap(SZ,SZ);
            using(var g=Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                var ink=new SolidBrush(Color.FromArgb(50,50,50));
                var pen=new Pen(Color.FromArgb(50,50,50),1.2f);
                switch(n)
                {
                    case "backward_arrow": g.DrawLine(pen,12,8,4,8);g.DrawLine(pen,4,8,8,4);g.DrawLine(pen,4,8,8,12);break;
                    case "forward_arrow":  g.DrawLine(pen,4,8,12,8);g.DrawLine(pen,12,8,8,4);g.DrawLine(pen,12,8,8,12);break;
                    case "up_arrow":       g.DrawLine(pen,8,12,8,4);g.DrawLine(pen,8,4,4,8);g.DrawLine(pen,8,4,12,8);break;
                    case "previous_small_arrow": Th.DropArr(g,8,9,Color.FromArgb(50,50,50));break;
                    case "search": g.DrawEllipse(pen,2,2,9,9);g.DrawLine(pen,10,10,13,13);break;
                    case "reload": g.DrawArc(pen,2,2,11,11,-30,270);g.DrawLine(pen,12,4,9,1);g.DrawLine(pen,12,4,14,7);break;
                    case "change_view": g.FillRectangle(ink,1,1,5,5);g.FillRectangle(ink,9,1,5,5);g.FillRectangle(ink,1,9,5,5);g.FillRectangle(ink,9,9,5,5);break;
                    case "preview_pane": g.DrawRectangle(pen,1,2,13,11);g.DrawLine(pen,8,2,8,13);break;
                    case "help": g.DrawEllipse(pen,1,1,13,13);using(var f2=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("?",new Font("Segoe UI",7.5f,FontStyle.Bold),ink,new RectangleF(0,0,16,16),f2);break;
                    case "organize": g.DrawLine(pen,2,4,14,4);g.DrawLine(pen,2,8,14,8);g.DrawLine(pen,2,12,14,12);break;
                    case "folder": case "desktop": case "downloads": case "documents": case "pictures": case "music": case "videos": case "3dobjects": case "new_folder":
                        g.FillRectangle(new SolidBrush(Color.FromArgb(255,196,42)),1,6,14,9);
                        g.FillRectangle(new SolidBrush(Color.FromArgb(255,196,42)),1,4,7,3);
                        g.DrawRectangle(pen,1,6,13,8);g.DrawRectangle(pen,1,4,6,2);break;
                    case "quick_access":
                        var s=StarPts(8,8,7,3,5);g.FillPolygon(new SolidBrush(Color.FromArgb(255,185,0)),s);break;
                    case "drives": case "this_pc":
                        g.FillRectangle(new SolidBrush(Color.FromArgb(220,220,220)),1,4,14,9);
                        g.DrawRectangle(pen,1,4,13,8);g.FillEllipse(new SolidBrush(Color.FromArgb(0,120,215)),9,9,3,3);
                        g.DrawLine(pen,2,7,7,7);break;
                    case "network": g.DrawEllipse(pen,5,1,5,4);g.DrawEllipse(pen,1,9,4,4);g.DrawEllipse(pen,11,9,4,4);g.DrawLine(pen,8,5,3,9);g.DrawLine(pen,8,5,13,9);break;
                    case "file": g.DrawPolygon(pen,new[]{new Point(3,1),new Point(10,1),new Point(13,4),new Point(13,15),new Point(3,15)});g.DrawLine(pen,10,1,10,4);g.DrawLine(pen,10,4,13,4);break;
                    case "cut": g.DrawLine(pen,5,1,11,13);g.DrawLine(pen,11,1,5,13);g.DrawEllipse(pen,2,11,4,4);g.DrawEllipse(pen,10,11,4,4);break;
                    case "copy": g.DrawRectangle(pen,4,4,9,10);g.DrawRectangle(pen,2,2,9,10);break;
                    case "paste": g.DrawRectangle(pen,3,5,10,10);g.FillRectangle(new SolidBrush(Color.FromArgb(210,210,210)),5,3,6,4);g.DrawRectangle(pen,5,3,5,3);break;
                    case "delete": g.FillRectangle(new SolidBrush(Color.FromArgb(210,210,210)),3,5,10,10);g.DrawRectangle(pen,3,5,9,9);g.DrawLine(pen,1,3,15,3);g.DrawLine(pen,6,1,10,1);break;
                    case "rename": g.DrawLine(pen,3,13,12,4);g.DrawLine(pen,14,2,12,4);g.DrawLine(pen,1,15,3,13);break;
                    case "undo": g.DrawArc(pen,2,3,10,9,180,180);g.DrawLine(pen,2,3,5,5);g.DrawLine(pen,2,3,4,6);break;
                    case "redo": g.DrawArc(pen,4,3,10,9,0,180);g.DrawLine(pen,14,3,11,5);g.DrawLine(pen,14,3,12,6);break;
                    case "selectall": g.DrawRectangle(pen,1,1,7,7);g.DrawRectangle(pen,1,8,7,7);g.DrawLine(pen,10,4,15,4);g.DrawLine(pen,10,11,15,11);break;
                    case "properties": g.DrawEllipse(pen,2,2,12,12);g.DrawLine(pen,8,5,8,9);g.FillEllipse(ink,7,10,2,2);break;
                    case "shortcut": g.DrawPolygon(pen,new[]{new Point(1,1),new Point(8,1),new Point(11,4),new Point(11,11),new Point(1,11)});g.FillPolygon(ink,new[]{new Point(8,9),new Point(12,13),new Point(13,10),new Point(15,15),new Point(10,13)});break;
                    case "sendto": g.DrawRectangle(pen,1,4,9,9);g.DrawLine(pen,10,8,15,8);g.DrawLine(pen,12,5,15,8);g.DrawLine(pen,12,11,15,8);break;
                    case "openwith": g.DrawRectangle(pen,1,3,11,9);g.DrawLine(pen,12,7,15,7);g.DrawLine(pen,13,5,15,7);g.DrawLine(pen,13,9,15,7);break;
                    case "options": g.DrawEllipse(pen,4,4,8,8);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,(float)(8+5*Math.Cos(a)),(float)(8+5*Math.Sin(a)),(float)(8+7*Math.Cos(a)),(float)(8+7*Math.Sin(a)));}break;
                    case "ascending": g.DrawLine(pen,2,13,2,3);g.DrawLine(pen,2,3,5,6);g.DrawLine(pen,6,5,14,5);g.DrawLine(pen,6,9,12,9);g.DrawLine(pen,6,13,9,13);break;
                    case "descending": g.DrawLine(pen,2,3,2,13);g.DrawLine(pen,2,13,5,10);g.DrawLine(pen,6,5,14,5);g.DrawLine(pen,6,9,12,9);g.DrawLine(pen,6,13,9,13);break;
                    default:
                        g.DrawRectangle(pen,2,2,11,11);
                        using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                            g.DrawString(n.Length>0?n[0].ToString().ToUpper():"?",new Font("Segoe UI",6f),ink,new RectangleF(0,0,16,16),fmt);
                        break;
                }
                ink.Dispose();pen.Dispose();
            }
            return b;
        }
        static Point[] StarPts(int cx,int cy,int outerR,int innerR,int n)
        {
            var p=new Point[n*2]; double step=Math.PI/n;
            for(int i=0;i<n*2;i++){double a=i*step-Math.PI/2;int r=i%2==0?outerR:innerR;p[i]=new Point(cx+(int)(r*Math.Cos(a)),cy+(int)(r*Math.Sin(a)));}
            return p;
        }
    }
 
    // -------------------------------------------------------------------------
    //  Models & Enums
    // -------------------------------------------------------------------------
    enum SortCol{Name,Date,Type,Size} enum SortDir{Asc,Desc} enum ViewMode{Details,LargeIcons,MediumIcons,SmallIcons,List,ExtraLargeIcons,Tiles,Content}
 
    class TreeNode2
    {
        public string Label,Path,IconName; public bool Expanded,IsVirtual,IsRoot,IsPinned; public int Level;
        public List<TreeNode2> Children=new List<TreeNode2>(); public TreeNode2 Parent;
        public bool HasChildren; public Rectangle Bounds;
    }
 
    class ContentItem
    {
        public string Name,FullPath,ItemType; public DateTime DateModified; public long Size; public bool IsDirectory;
        public Image Icon16; // shell icon, loaded on demand
        public string SizeStr=>IsDirectory?"":(Size<1024?$"{Size} B":Size<1048576?$"{Size/1024.0:F1} KB":$"{Size/1048576.0:F1} MB");
        public string DateStr=>DateModified==default(DateTime)?"":(DateModified.ToString("M/d/yyyy h:mm tt"));
    }
 
    // -------------------------------------------------------------------------
    //  Menu renderer
    // -------------------------------------------------------------------------
    class ExMenuRenderer:ToolStripProfessionalRenderer
    {
        public ExMenuRenderer():base(new ExColorTable()){}
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {e.Graphics.Clear(Th.MenuBg);if(e.Item.Selected&&e.Item.Enabled)Th.FillSel(e.Graphics,new Rectangle(2,0,e.Item.Width-4,e.Item.Height-1));}
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {using(var p=new Pen(Th.SepColor))e.Graphics.DrawLine(p,30,e.Item.Height/2,e.Item.Width-4,e.Item.Height/2);}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {e.TextColor=e.Item.Enabled?Color.Black:Th.TxtDisabled;e.Graphics.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;base.OnRenderItemText(e);}
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)=>e.Graphics.Clear(Th.MenuBg);
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){using(var p=new Pen(Th.PaneSep))e.Graphics.DrawRectangle(p,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){if(e.Image==null)return;var r=e.ImageRectangle;e.Graphics.DrawImage(e.Image,r.X+(r.Width-16)/2,r.Y+(r.Height-16)/2,16,16);}
    }
    class ExColorTable:ProfessionalColorTable
    {
        public override Color MenuBorder=>Th.PaneSep; public override Color MenuItemBorder=>Color.Transparent;
        public override Color MenuItemSelectedGradientBegin=>Th.SelFill; public override Color MenuItemSelectedGradientEnd=>Th.SelFill;
        public override Color ToolStripDropDownBackground=>Color.White; public override Color ImageMarginGradientBegin=>Color.White;
        public override Color ImageMarginGradientMiddle=>Color.White; public override Color ImageMarginGradientEnd=>Color.White;
    }
    static class MI
    {
        static readonly ExMenuRenderer Rend=new ExMenuRenderer();
        public static ToolStripMenuItem Item(string t,string ico=null,EventHandler clk=null){var m=new ToolStripMenuItem(t){Font=Th.UiFont};if(ico!=null)m.Image=Icons.Get(ico);if(clk!=null)m.Click+=clk;return m;}
        public static ToolStripMenuItem Sub(string t,string ico=null){var m=new ToolStripMenuItem(t){Font=Th.UiFont};if(ico!=null)m.Image=Icons.Get(ico);return m;}
        public static ToolStripSeparator Sep()=>new ToolStripSeparator();
        public static ContextMenuStrip MakeMenu(){return new ContextMenuStrip{Renderer=Rend,ShowImageMargin=true};}
    }
 
    // -------------------------------------------------------------------------
    //  TOP NAV BAR  (34 px)
    // -------------------------------------------------------------------------
    class TopNavBar:Panel
    {
        public const int H=34; const int BTN_H=22,BTN_Y=6,ICO=16,SM=14;
        enum Hit{None,Back,Fwd,RecLoc,Up,Reload,RecFold}
        Hit _hov,_prs; Rectangle _rBack,_rFwd,_rRL,_rUp,_rReload,_rRF;
        TextBox _path; Panel _srchWrap; TextBox _srch; bool _backOn,_fwdOn;
        public string CurrentPath{get=>_path.Text;set=>_path.Text=value;}
        public bool BackEnabled{set{_backOn=value;Invalidate();}}
        public bool ForwardEnabled{set{_fwdOn=value;Invalidate();}}
        public event EventHandler BackClick,ForwardClick,UpClick,ReloadClick;
        public event Action<string> Navigate,SearchChanged;
 
        public TopNavBar()
        {
            Height=H;Dock=DockStyle.Top;BackColor=Color.White;DoubleBuffered=true;Padding=Padding.Empty;
            _path=new TextBox{BorderStyle=BorderStyle.None,Font=Th.UiFont,BackColor=Color.White,ForeColor=Th.TxtColor};
            _path.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Return){Navigate?.Invoke(_path.Text);e.SuppressKeyPress=true;}};
            Controls.Add(_path);
            _srchWrap=new Panel{BackColor=Color.White};
            _srchWrap.Paint+=PaintSrch;
            _srch=new TextBox{BorderStyle=BorderStyle.None,Font=Th.UiFont,BackColor=Color.White,ForeColor=Th.TxtDisabled,Text="Search"};
            _srch.GotFocus+=(s,e)=>{if(_srch.Text=="Search"){_srch.Text="";_srch.ForeColor=Th.TxtColor;}};
            _srch.LostFocus+=(s,e)=>{if(_srch.Text==""){_srch.Text="Search";_srch.ForeColor=Th.TxtDisabled;}};
            _srch.TextChanged+=(s,e)=>SearchChanged?.Invoke(_srch.Text=="Search"?"":_srch.Text);
            _srchWrap.Controls.Add(_srch); Controls.Add(_srchWrap);
            MouseMove+=(s,e)=>{var h=HitAt(e.Location);if(h!=_hov){_hov=h;Invalidate();}};
            MouseLeave+=(s,e)=>{_hov=Hit.None;Invalidate();};
            MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){_prs=HitAt(e.Location);Invalidate();}};
            MouseUp+=OnMU; Resize+=(s,e)=>DoLayout();
        }
        void PaintSrch(object s,PaintEventArgs e)
        {e.Graphics.Clear(Color.White);using(var p=new Pen(Th.PaneSep))e.Graphics.DrawRectangle(p,0,0,_srchWrap.Width-1,_srchWrap.Height-1);e.Graphics.DrawImage(Icons.Get("search"),_srchWrap.Width-19,3,14,14);}
        void DoLayout()
        {
            int x=2;
            _rBack=new Rectangle(x,BTN_Y,28,BTN_H);x+=28;
            _rFwd=new Rectangle(x,BTN_Y,28,BTN_H);x+=28;
            _rRL=new Rectangle(x,BTN_Y,SM,BTN_H);x+=SM+2;
            _rUp=new Rectangle(x,BTN_Y,28,BTN_H);x+=28+4;
            const int sw=202,gap=12,rfw=SM,reloadW=28;
            // Path fills: Width - (what's already used) - recFold - reload - gap - search - end-gap
            int pw=Math.Max(60,Width-x-rfw-4-reloadW-gap-sw-gap-2);
            _path.SetBounds(x+3,BTN_Y+3,pw-6,BTN_H-6); x+=pw+2;
            _rRF=new Rectangle(x,BTN_Y,rfw,BTN_H); x+=rfw+4;
            _rReload=new Rectangle(x,BTN_Y,reloadW,BTN_H); x+=reloadW+gap;
            _srchWrap.SetBounds(x,BTN_Y,sw,BTN_H);
            _srch.SetBounds(3,3,sw-22,BTN_H-6);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit; g.Clear(Color.White);
            using(var p=new Pen(Color.FromArgb(220,220,220)))g.DrawLine(p,0,H-1,Width,H-1);
            DrawNB(g,_rBack,"backward_arrow",_backOn,Hit.Back); DrawNB(g,_rFwd,"forward_arrow",_fwdOn,Hit.Fwd);
            DrawAB(g,_rRL,Hit.RecLoc); DrawNB(g,_rUp,"up_arrow",true,Hit.Up);
            DrawNB(g,_rReload,"reload",true,Hit.Reload);
            int px=_path.Left-3,py=BTN_Y,pw=_path.Width+6;
            using(var p=new Pen(Th.PaneSep))g.DrawRectangle(p,px,py,pw-1,BTN_H-1);
            DrawAB(g,_rRF,Hit.RecFold);
        }
        void DrawNB(Graphics g,Rectangle r,string ico,bool en,Hit btn)
        {
            if(_prs==btn&&en)Th.FillBtnPrs(g,r);else if(_hov==btn&&en)Th.FillBtnHov(g,r);
            var img=Icons.Get(ico); int ix=r.X+(r.Width-ICO)/2,iy=r.Y+(r.Height-ICO)/2;
            // Always draw icon at full opacity
            g.DrawImage(img,ix,iy,ICO,ICO);
            // Blue overlay when hovered+enabled; grey overlay when disabled
            if(!en)
            {
                using(var ob=new SolidBrush(Th.ArrDisOverlay))
                    g.FillRectangle(ob,r.X,r.Y,r.Width,r.Height);
            }
            else if(_hov==btn)
            {
                using(var ob=new SolidBrush(Th.ArrHovOverlay))
                    g.FillRectangle(ob,r.X,r.Y,r.Width,r.Height);
            }
        }
        void DrawAB(Graphics g,Rectangle r,Hit btn)
        {if(_prs==btn)Th.FillBtnPrs(g,r);else if(_hov==btn)Th.FillBtnHov(g,r);Th.DropArr(g,r.X+r.Width/2,r.Y+r.Height/2,Th.TxtColor);}
        Hit HitAt(Point p){if(_rBack.Contains(p))return Hit.Back;if(_rFwd.Contains(p))return Hit.Fwd;if(_rRL.Contains(p))return Hit.RecLoc;if(_rUp.Contains(p))return Hit.Up;if(_rRF.Contains(p))return Hit.RecFold;return Hit.None;}
        void OnMU(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;var h=HitAt(e.Location);_prs=Hit.None;Invalidate();if(h==Hit.Back&&_backOn)BackClick?.Invoke(this,EventArgs.Empty);if(h==Hit.Fwd&&_fwdOn)ForwardClick?.Invoke(this,EventArgs.Empty);if(h==Hit.Up)UpClick?.Invoke(this,EventArgs.Empty);}
        protected override void OnCreateControl(){base.OnCreateControl();DoLayout();}
    }
 
    // -------------------------------------------------------------------------
    //  COMMAND BAR  (31 px)
    // -------------------------------------------------------------------------
    class CommandBar:Panel
    {
        public const int H=31; const int BTN_Y=3,BTN_H=26;
        enum Hit{None,Org,NF,CV,MO,Prev,Help} Hit _hov,_prs;
        Rectangle _rOrg,_rNF,_rCV,_rMO,_rPrev,_rHelp;
        ContextMenuStrip _orgMenu,_viewMenu;
        public event EventHandler NewFolderClick,PreviewClick,HelpClick;
        public event Action<ViewMode> ViewChanged;
        public event EventHandler OrgCut,OrgCopy,OrgPaste,OrgUndo,OrgRedo,OrgSelectAll,OrgDelete,OrgRename,OrgProps,OrgClose;
 
        public CommandBar()
        {
            Height=H;Dock=DockStyle.Top;BackColor=Th.CmdBarBg;DoubleBuffered=true;Padding=Padding.Empty;
            BuildMenus();
            MouseMove+=(s,e)=>{var h=HitAt(e.Location);if(h!=_hov){_hov=h;Invalidate();}};
            MouseLeave+=(s,e)=>{_hov=Hit.None;Invalidate();};
            MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){_prs=HitAt(e.Location);Invalidate();}};
            MouseUp+=OnMU; Resize+=(s,e)=>DoLayout();
        }
        void DoLayout()
        {
            int x=3;_rOrg=new Rectangle(x,BTN_Y,91,BTN_H);x+=93;_rNF=new Rectangle(x,BTN_Y,88,BTN_H);
            int rx=Width-9;rx-=28;_rHelp=new Rectangle(rx,BTN_Y,28,BTN_H);rx-=8;
            rx-=28;_rPrev=new Rectangle(rx,BTN_Y,28,BTN_H);rx-=10;rx-=19;_rMO=new Rectangle(rx,BTN_Y,19,BTN_H);rx-=27;_rCV=new Rectangle(rx,BTN_Y,27,BTN_H);
        }
        void BuildMenus()
        {
            _orgMenu=MI.MakeMenu();
            _orgMenu.Items.AddRange(new ToolStripItem[]{
                MI.Item("Cut","cut",(s,e)=>OrgCut?.Invoke(this,EventArgs.Empty)),
                MI.Item("Copy","copy",(s,e)=>OrgCopy?.Invoke(this,EventArgs.Empty)),
                MI.Item("Paste","paste",(s,e)=>OrgPaste?.Invoke(this,EventArgs.Empty)),
                MI.Item("Undo","undo",(s,e)=>OrgUndo?.Invoke(this,EventArgs.Empty)),
                MI.Item("Redo","redo",(s,e)=>OrgRedo?.Invoke(this,EventArgs.Empty)),
                MI.Sep(),MI.Item("Select All","selectall",(s,e)=>OrgSelectAll?.Invoke(this,EventArgs.Empty)),MI.Sep(),
                MI.Item("Layout","change_view"),MI.Item("Options","options"),MI.Sep(),
                MI.Item("Delete","delete",(s,e)=>OrgDelete?.Invoke(this,EventArgs.Empty)),
                MI.Item("Rename","rename",(s,e)=>OrgRename?.Invoke(this,EventArgs.Empty)),
                MI.Item("Remove Properties","properties"),MI.Item("Properties","properties",(s,e)=>OrgProps?.Invoke(this,EventArgs.Empty)),MI.Sep(),
                MI.Item("Close","file",(s,e)=>OrgClose?.Invoke(this,EventArgs.Empty))});
            _viewMenu=MI.MakeMenu();
            var modeLabels=new[]{"Extra Large Icons","Large Icons","Medium Icons","Small Icons","List","Details","Tiles","Content"};
            var modeVals=new[]{ViewMode.ExtraLargeIcons,ViewMode.LargeIcons,ViewMode.MediumIcons,ViewMode.SmallIcons,ViewMode.List,ViewMode.Details,ViewMode.Tiles,ViewMode.Content};
            for(int mi2=0;mi2<modeLabels.Length;mi2++){var vm2=modeVals[mi2];_viewMenu.Items.Add(MI.Item(modeLabels[mi2],"change_view",(s,e)=>ViewChanged?.Invoke(vm2)));}
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;g.Clear(Th.CmdBarBg);
            using(var p=new Pen(Th.CmdSepBot))g.DrawLine(p,0,H-1,Width,H-1);
            DrawTB(g,_rOrg,"Organize",Hit.Org,true); DrawTB(g,_rNF,"New folder",Hit.NF,false);
            DrawIB(g,_rCV,"change_view",Hit.CV); DrawDB(g,_rMO,Hit.MO);
            using(var sp=new Pen(Th.SepColor))g.DrawLine(sp,_rMO.Left,_rMO.Top+3,_rMO.Left,_rMO.Bottom-3);
            DrawIB(g,_rPrev,"preview_pane",Hit.Prev); DrawIB(g,_rHelp,"help",Hit.Help);
        }
        void DrawTB(Graphics g,Rectangle r,string t,Hit btn,bool arr)
        {
            if(_prs==btn)Th.FillBtnPrs(g,r);else if(_hov==btn)Th.FillBtnHov(g,r);
            // Text centred; reserve right margin only when there's a drop arrow
            int rightReserve=arr?14:0;
            using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter})
                g.DrawString(t,Th.UiFont,new SolidBrush(Color.FromArgb(0,102,204)),new RectangleF(r.X,r.Y,r.Width-rightReserve,r.Height),sf);
            if(arr)Th.DropArr(g,r.Right-8,r.Y+r.Height/2,Th.TxtColor);
        }
        void DrawIB(Graphics g,Rectangle r,string ico,Hit btn)
        {if(_prs==btn)Th.FillBtnPrs(g,r);else if(_hov==btn)Th.FillBtnHov(g,r);g.DrawImage(Icons.Get(ico),r.X+(r.Width-16)/2,r.Y+(r.Height-16)/2,16,16);}
        void DrawDB(Graphics g,Rectangle r,Hit btn)
        {if(_prs==btn)Th.FillBtnPrs(g,r);else if(_hov==btn)Th.FillBtnHov(g,r);Th.DropArr(g,r.X+r.Width/2,r.Y+r.Height/2+1,Th.TxtColor);}
        Hit HitAt(Point p){if(_rOrg.Contains(p))return Hit.Org;if(_rNF.Contains(p))return Hit.NF;if(_rCV.Contains(p))return Hit.CV;if(_rMO.Contains(p))return Hit.MO;if(_rPrev.Contains(p))return Hit.Prev;if(_rHelp.Contains(p))return Hit.Help;return Hit.None;}
        void OnMU(object s,MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Left)return;var h=HitAt(e.Location);_prs=Hit.None;Invalidate();
            switch(h){case Hit.Org:_orgMenu.Show(this,new Point(_rOrg.Left,H));break;case Hit.NF:NewFolderClick?.Invoke(this,EventArgs.Empty);break;case Hit.CV:case Hit.MO:_viewMenu.Show(this,new Point(_rCV.Left,H));break;case Hit.Prev:PreviewClick?.Invoke(this,EventArgs.Empty);break;case Hit.Help:HelpClick?.Invoke(this,EventArgs.Empty);break;}
        }
        protected override void OnCreateControl(){base.OnCreateControl();DoLayout();}
    }
 
    // -------------------------------------------------------------------------
    //  TREE PANE  (with hover + DnD drop target)
    // -------------------------------------------------------------------------
    class TreePane:Panel
    {
        const int ROW_H=22,ROOT_X=8,LVL=16,ICO=16,ARW=12;
        List<TreeNode2> _flat=new List<TreeNode2>(); TreeNode2 _root,_sel,_dropTarget; int _hov=-1;
        int _scrollY,_totalH; VScrollBar _vsb;
        public event Action<TreeNode2> NodeSelected;
        public event Action<string,string> DropFiles; // (srcPaths\n..., destPath)
 
        public TreePane()
        {
            BackColor=Th.TreeBg;DoubleBuffered=true;Padding=Padding.Empty;
            AllowDrop=true;
            _vsb=new VScrollBar{Dock=DockStyle.Right,Minimum=0,Visible=false};
            _vsb.ValueChanged+=(s,e)=>{_scrollY=_vsb.Value;Invalidate();};
            Controls.Add(_vsb);
            MouseMove+=OnMM; MouseDown+=OnMD; MouseLeave+=(s,e)=>{_hov=-1;Invalidate();};
            MouseWheel+=OnMW; Resize+=(s,e)=>UpdateScroll();
            DragEnter+=OnDE; DragOver+=OnDOv; DragLeave+=OnDL; DragDrop+=OnDD;
            Build();
        }
        // Fade-in/out for expand/collapse arrows
        float _arrowAlpha=0f; bool _mouseInTree=false;
        System.Windows.Forms.Timer _fadeTimer;
 
        void Build()
        {
            _fadeTimer=new System.Windows.Forms.Timer{Interval=30};
            _fadeTimer.Tick+=(s,e)=>
            {
                float target=_mouseInTree?1f:0f;
                if(Math.Abs(_arrowAlpha-target)<0.05f){_arrowAlpha=target;_fadeTimer.Stop();}
                else _arrowAlpha+=(_mouseInTree?0.12f:-0.12f);
                _arrowAlpha=Math.Max(0f,Math.Min(1f,_arrowAlpha));
                Invalidate();
            };
            MouseEnter+=(s,e)=>{_mouseInTree=true;_fadeTimer.Start();};
            MouseLeave+=(s,e)=>{_mouseInTree=false;_fadeTimer.Start();};
 
            _root=Node("__root__",null,null,true); _root.Expanded=true;
            var qa=Child(_root,"Quick access",null,"quick_access",true,true); qa.Expanded=true;
            var pDesktop =Child(qa,"Desktop",SF(Environment.SpecialFolder.DesktopDirectory),"desktop"); pDesktop.IsPinned=true;
            var pDownload=Child(qa,"Downloads",DL(),"downloads");                                        pDownload.IsPinned=true;
            var pDocuments=Child(qa,"Documents",SF(Environment.SpecialFolder.MyDocuments),"documents"); pDocuments.IsPinned=true;
            var pPictures=Child(qa,"Pictures",SF(Environment.SpecialFolder.MyPictures),"pictures");     pPictures.IsPinned=true;
            var tpc=Child(_root,"This PC",null,"this_pc",true,true);
            Child(tpc,"3D Objects",null,"3dobjects");
            Child(tpc,"Desktop",SF(Environment.SpecialFolder.DesktopDirectory),"desktop");
            Child(tpc,"Documents",SF(Environment.SpecialFolder.MyDocuments),"documents");
            Child(tpc,"Downloads",DL(),"downloads");
            Child(tpc,"Music",SF(Environment.SpecialFolder.MyMusic),"music");
            Child(tpc,"Pictures",SF(Environment.SpecialFolder.MyPictures),"pictures");
            Child(tpc,"Videos",SF(Environment.SpecialFolder.MyVideos),"videos");
            foreach(var d in DriveInfo.GetDrives())try{string lbl=d.IsReady&&d.VolumeLabel.Length>0?$"{d.VolumeLabel} ({d.Name.TrimEnd('\\')})":$"Local Disk ({d.Name.TrimEnd('\\')})";var n=Child(tpc,lbl,d.RootDirectory.FullName,"drives");n.HasChildren=true;}catch{}
            Child(_root,"Network",null,"network",true,true);
            Rebuild();
        }
        static string SF(Environment.SpecialFolder f)=>Environment.GetFolderPath(f);
        static string DL()=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        static TreeNode2 Node(string l,string p,string i,bool v=false)=>new TreeNode2{Label=l,Path=p,IconName=i,IsVirtual=v};
        TreeNode2 Child(TreeNode2 par,string l,string p,string i,bool virt=false,bool root=false)
        {var n=new TreeNode2{Label=l,Path=p,IconName=i,IsVirtual=virt,IsRoot=root,Parent=par,Level=par==_root?0:par.Level+1,HasChildren=!virt&&p!=null};par.Children.Add(n);return n;}
        // Precomputed Y positions including 8 px gap before every root node (except first)
        List<int> _yPos=new List<int>();
 
        void Rebuild()
        {
            _flat.Clear(); Flatten(_root); ComputeY(); UpdateScroll(); Invalidate();
        }
        void Flatten(TreeNode2 n){foreach(var c in n.Children){_flat.Add(c);if(c.Expanded)Flatten(c);}}
        void ComputeY()
        {
            _yPos.Clear();
            int y=16,firstRoot=1; // 16px top padding before first item
            foreach(var n in _flat)
            {
                if(n.IsRoot&&firstRoot++>1) y+=8; // 8 px gap between root categories
                _yPos.Add(y);
                y+=ROW_H;
            }
            _totalH=y;
        }
        int IdxAtDocY(int docY)
        {
            for(int i=0;i<_yPos.Count;i++)
                if(docY>=_yPos[i]&&docY<_yPos[i]+ROW_H) return i;
            return -1;
        }
        void UpdateScroll()
        {
            int vis=ClientSize.Height; _vsb.Visible=_totalH>vis;
            if(_vsb.Visible){_vsb.Maximum=Math.Max(0,_totalH-vis+100);_vsb.SmallChange=ROW_H;_vsb.LargeChange=100;_scrollY=Math.Min(_scrollY,Math.Max(0,_totalH-vis));_vsb.Value=_scrollY;}
            else _scrollY=0;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit; g.Clear(Th.TreeBg);
            int lw=Width-(_vsb.Visible?_vsb.Width:0)-2;
            var greyBrush=new SolidBrush(Color.FromArgb(100,100,100));
            var sf=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap};
            for(int i=0;i<_flat.Count;i++)
            {
                if(i>=_yPos.Count) break;
                var n=_flat[i]; int y=_yPos[i]-_scrollY;
                if(y+ROW_H<0)continue; if(y>Height)break;
                n.Bounds=new Rectangle(0,y,lw,ROW_H);
                if(n==_sel) Th.FillSel(g,new Rectangle(0,y,lw,ROW_H-1));
                else if(n==_dropTarget){using(var b=new SolidBrush(Th.SelFill))g.FillRectangle(b,new Rectangle(0,y,lw,ROW_H-1));}
                else if(i==_hov) Th.FillHover(g,new Rectangle(0,y,lw,ROW_H-1));
                int indent=ROOT_X+n.Level*LVL;
                bool hasK=(n.Children.Count>0||n.HasChildren)&&!n.IsPinned;
                if(hasK)
                {
                    // Use expand.png / collapse.png with fade based on cursor presence
                    var arrIco=n.Expanded?Icons.Get("collapse"):Icons.Get("expand");
                    int alpha=(int)(_arrowAlpha*255f); if(alpha<0)alpha=0; if(alpha>255)alpha=255;
                    if(alpha>0)
                    {
                        var ia=new ImageAttributes();
                        var cm=new ColorMatrix(); cm.Matrix33=alpha/255f;
                        ia.SetColorMatrix(cm);
                        g.DrawImage(arrIco,new Rectangle(indent-2,y+(ROW_H-ICO)/2,ICO,ICO),0,0,arrIco.Width,arrIco.Height,GraphicsUnit.Pixel,ia);
                        ia.Dispose();
                    }
                }
                // Item icon
                var ico=Shell.SmallIcon(n.Path??string.Empty)??Icons.Get(n.IconName??"folder");
                g.DrawImage(ico,indent+ARW,y+(ROW_H-ICO)/2,ICO,ICO);
                // Label – clip right if pinned icon present
                float labelW=n.IsPinned?lw-indent-ARW-ICO-22:lw-indent-ARW-ICO-5;
                var rf=new RectangleF(indent+ARW+ICO+3,y+1,Math.Max(1,labelW),ROW_H-2);
                g.DrawString(n.Label,Th.UiFont,Brushes.Black,rf,sf);
                // Pinned icon at right edge of row
                if(n.IsPinned)
                {
                    var pico=Icons.Get("pinned");
                    int px=lw-ICO-2; if(px>0) g.DrawImage(pico,px,y+(ROW_H-ICO)/2,ICO,ICO);
                }
            }
            greyBrush.Dispose(); sf.Dispose();
            using(var p=new Pen(Th.PaneSep))g.DrawLine(p,lw,0,lw,Height);
        }
        static void DrawTri(Graphics g,int cx,int cy,bool open)
        {g.SmoothingMode=SmoothingMode.AntiAlias;Point[]pts=open?new[]{new Point(cx-4,cy-2),new Point(cx+4,cy-2),new Point(cx,cy+3)}:new[]{new Point(cx-2,cy-4),new Point(cx-2,cy+4),new Point(cx+3,cy)};using(var b=new SolidBrush(Color.FromArgb(100,100,100)))g.FillPolygon(b,pts);g.SmoothingMode=SmoothingMode.Default;}
        void OnMM(object s,MouseEventArgs e)
        {
            int idx=IdxAtDocY(_scrollY+e.Y); if(idx<0&&e.Y>0)idx=-1;
            if(idx!=_hov){_hov=idx;Invalidate();}
        }
        void OnMD(object s,MouseEventArgs e)
        {
            int idx=IdxAtDocY(_scrollY+e.Y); if(idx<0||idx>=_flat.Count)return;
            var n=_flat[idx]; int indent=ROOT_X+n.Level*LVL;
            bool hasK=n.Children.Count>0||n.HasChildren;
            if(hasK&&e.X>=indent-4&&e.X<=indent+ARW+2){Toggle(n);return;}
            _sel=n;Invalidate();if(e.Button==MouseButtons.Left)NodeSelected?.Invoke(n);
        }
        void Toggle(TreeNode2 n){if(!n.Expanded){if(n.Children.Count==0&&n.Path!=null&&!n.IsVirtual)LoadFs(n);n.Expanded=true;}else n.Expanded=false;Rebuild();}
        void LoadFs(TreeNode2 n){try{foreach(var d in Directory.GetDirectories(n.Path))try{n.Children.Add(new TreeNode2{Label=Path.GetFileName(d),Path=d,IconName="folder",Parent=n,Level=n.Level+1,HasChildren=true});}catch{}n.HasChildren=false;}catch{}}
        void OnMW(object s,MouseEventArgs e){_scrollY=Math.Max(0,_scrollY-e.Delta/3);if(_vsb.Visible){_scrollY=Math.Min(_scrollY,_vsb.Maximum-_vsb.LargeChange+1);_vsb.Value=_scrollY;}Invalidate();}
        // -- DnD target --
        void OnDE(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop)||e.Data.GetDataPresent("FileNameW"))e.Effect=DragDropEffects.Copy;else e.Effect=DragDropEffects.None;}
        void OnDOv(object s,DragEventArgs e)
        {
            var pt=PointToClient(new Point(e.X,e.Y)); int idx=IdxAtDocY(_scrollY+pt.Y);
            if(idx<0||idx>=_flat.Count){_dropTarget=null;Invalidate();e.Effect=DragDropEffects.None;return;}
            var n=_flat[idx]; if(n.Path!=null&&Directory.Exists(n.Path)){_dropTarget=n;e.Effect=DragDropEffects.Copy;}
            else{_dropTarget=null;e.Effect=DragDropEffects.None;}
            Invalidate();
        }
        void OnDL(object s,EventArgs e){_dropTarget=null;Invalidate();}
        void OnDD(object s,DragEventArgs e)
        {
            var dest=_dropTarget?.Path; _dropTarget=null; Invalidate();
            if(dest==null||!Directory.Exists(dest))return;
            string[]files=null;
            if(e.Data.GetDataPresent(DataFormats.FileDrop))files=(string[])e.Data.GetData(DataFormats.FileDrop);
            if(files==null)return;
            DropFiles?.Invoke(string.Join("\n",files),dest);
        }
        public void SelectPath(string path){if(path==null){_sel=null;Invalidate();return;}foreach(var n in _flat)if(n.Path!=null&&n.Path.Equals(path,StringComparison.OrdinalIgnoreCase)){_sel=n;Invalidate();return;}}
    }
 
    // -------------------------------------------------------------------------
    //  INLINE RENAME BOX
    // -------------------------------------------------------------------------
    class InlineRenameBox:TextBox
    {
        public int ItemIdx=-1;
        public event Action<int,string> Committed;
        public event Action Cancelled;
        public InlineRenameBox(){BorderStyle=BorderStyle.FixedSingle;Font=Th.UiFont;BackColor=Color.White;}
        protected override void OnKeyDown(KeyEventArgs e)
        {if(e.KeyCode==Keys.Return){Committed?.Invoke(ItemIdx,Text);e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){Cancelled?.Invoke();e.SuppressKeyPress=true;}base.OnKeyDown(e);}
        protected override bool IsInputKey(Keys k)=>k==Keys.Return||k==Keys.Escape||base.IsInputKey(k);
    }
 
    // -------------------------------------------------------------------------
    //  HEX VIEW PANEL
    // -------------------------------------------------------------------------
    class HexPanel:Panel
    {
        byte[]_data; int _scroll; const int BPR=16,RH=14,HH=18;
        VScrollBar _vsb;
        public HexPanel(){BackColor=Color.FromArgb(30,30,30);DoubleBuffered=true;_vsb=new VScrollBar{Dock=DockStyle.Right,Minimum=0,Visible=false};_vsb.ValueChanged+=(s,e)=>{_scroll=_vsb.Value;Invalidate();};Controls.Add(_vsb);MouseWheel+=OnMW;}
        public void Load(string path)
        {
            _data=null;_scroll=0;
            if(!File.Exists(path)){Invalidate();return;}
            try{using(var fs=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){int len=(int)Math.Min(fs.Length,65536);_data=new byte[len];fs.Read(_data,0,len);}}catch{}
            if(_data!=null){int rows=(_data.Length+BPR-1)/BPR,vis=(Height-HH)/RH;_vsb.Visible=rows>vis;if(_vsb.Visible){_vsb.Maximum=Math.Max(0,rows-vis+20);_vsb.SmallChange=1;_vsb.LargeChange=vis;}}
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Color.FromArgb(30,30,30));
            if(_data==null){g.DrawString("Select a file to view hex",Th.UiFont,Brushes.Gray,4,4);return;}
            g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
            int lw=Width-(_vsb.Visible?_vsb.Width:0);
            g.FillRectangle(new SolidBrush(Color.FromArgb(50,50,50)),0,0,lw,HH);
            g.DrawString("Offset    00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII",Th.Mono,new SolidBrush(Color.FromArgb(140,190,255)),2,2);
            int vis=(Height-HH)/RH+1;
            for(int row=_scroll;row<_scroll+vis;row++)
            {
                int off=row*BPR; if(off>=_data.Length)break;
                int y=HH+(row-_scroll)*RH;
                if((row-_scroll)%2==0)g.FillRectangle(new SolidBrush(Color.FromArgb(36,36,36)),0,y,lw,RH);
                var sb=new StringBuilder();sb.AppendFormat("{0:X8}  ",off);
                for(int b=0;b<BPR;b++){if(off+b<_data.Length)sb.AppendFormat("{0:X2} ",_data[off+b]);else sb.Append("   ");if(b==7)sb.Append(' ');}
                sb.Append(' ');
                for(int b=0;b<BPR;b++){if(off+b<_data.Length){char c=(char)_data[off+b];sb.Append(c>=32&&c<127?c:'.');}else sb.Append(' ');}
                g.DrawString(sb.ToString(),Th.Mono,new SolidBrush(Color.FromArgb(200,200,200)),2,y);
            }
        }
        void OnMW(object s,MouseEventArgs e){_scroll=Math.Max(0,_scroll-e.Delta/40);if(_vsb.Visible){_scroll=Math.Min(_scroll,Math.Max(0,_vsb.Maximum-_vsb.LargeChange));_vsb.Value=_scroll;}Invalidate();}
    }
 
    // -------------------------------------------------------------------------
    //  OBJ WIREFRAME PANEL
    // -------------------------------------------------------------------------
    class ObjPanel:Panel
    {
        struct V3{public float X,Y,Z;}
        struct F3{public int A,B,C;}
 
        List<V3> _v=new List<V3>(); List<F3> _f=new List<F3>();
        float _rx=0.3f,_ry=0f,_zoom=1f;
        Point _last; bool _drag;
        System.Windows.Forms.Timer _t;
        string _fmtLabel="";
 
        // Light direction (normalised)
        static V3 LightDir=NormV(new V3{X=0.5f,Y=0.8f,Z=0.6f});
        const float Ambient=0.30f;
 
        public ObjPanel()
        {
            BackColor=Color.FromArgb(240,240,240); DoubleBuffered=true;
            _t=new System.Windows.Forms.Timer{Interval=40};
            _t.Tick+=(s,e)=>{_ry+=0.018f;Invalidate();};
            MouseDown+=(s,e)=>{_drag=true;_last=e.Location;_t.Stop();};
            MouseMove+=(s,e)=>{if(!_drag)return;_ry+=(e.X-_last.X)*0.012f;_rx+=(e.Y-_last.Y)*0.012f;_last=e.Location;Invalidate();};
            MouseUp+=(s,e)=>{_drag=false;_t.Start();};
            MouseWheel+=(s,e)=>{_zoom=Math.Max(0.05f,Math.Min(8f,_zoom*(e.Delta>0?1.12f:0.89f)));Invalidate();};
        }
 
        public void Load(string path)
        {
            _v.Clear();_f.Clear();_zoom=1f;_rx=0.3f;_ry=0f;
            _fmtLabel=""; _t.Stop();
            if(!File.Exists(path)){Invalidate();return;}
            string ext=Path.GetExtension(path).ToLower();
            _fmtLabel=ext.TrimStart('.').ToUpper();
            try
            {
                if(ext==".obj") LoadObj(path);
                else if(ext==".stl") LoadStl(path);
                else if(ext==".ply") LoadPly(path);
            }
            catch{}
            NormalizeModel(); BuildVertexNormals();
            _t.Start(); Invalidate();
        }
 
        // -- OBJ ----------------------------------------------------------
        void LoadObj(string path)
        {
            var ci=System.Globalization.CultureInfo.InvariantCulture;
            foreach(var line in File.ReadAllLines(path))
            {
                if(line.StartsWith("v "))
                {
                    var p=line.Substring(2).Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                    if(p.Length>=3&&float.TryParse(p[0],System.Globalization.NumberStyles.Float,ci,out float x)&&float.TryParse(p[1],System.Globalization.NumberStyles.Float,ci,out float y)&&float.TryParse(p[2],System.Globalization.NumberStyles.Float,ci,out float z))
                        _v.Add(new V3{X=x,Y=y,Z=z});
                }
                else if(line.StartsWith("f "))
                {
                    var p=line.Substring(2).Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                    if(p.Length>=3){int a=FI(p[0]),b=FI(p[1]),c=FI(p[2]);_f.Add(new F3{A=a,B=b,C=c});for(int k=3;k<p.Length;k++)_f.Add(new F3{A=a,B=_f[_f.Count-1].C,C=FI(p[k])});}
                }
            }
        }
        static int FI(string s){var sl=s.IndexOf('/');var t=sl>=0?s.Substring(0,sl):s;return int.TryParse(t,out int v)?v-1:0;}
 
        // -- STL (ASCII & binary) ------------------------------------------
        void LoadStl(string path)
        {
            var ci=System.Globalization.CultureInfo.InvariantCulture;
            // Detect ASCII vs binary: binary has no "solid" text header
            bool isAscii=false;
            using(var sr=new StreamReader(path,System.Text.Encoding.ASCII,false,512)){var h=sr.ReadLine();isAscii=h!=null&&h.TrimStart().StartsWith("solid",StringComparison.OrdinalIgnoreCase);}
            if(isAscii)
            {
                V3[]tri=new V3[3]; int vi=0;
                foreach(var raw in File.ReadAllLines(path))
                {
                    var ln=raw.Trim();
                    if(ln.StartsWith("vertex",StringComparison.OrdinalIgnoreCase))
                    {
                        var p=ln.Substring(6).Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                        if(p.Length>=3&&float.TryParse(p[0],System.Globalization.NumberStyles.Float,ci,out float x)&&float.TryParse(p[1],System.Globalization.NumberStyles.Float,ci,out float y)&&float.TryParse(p[2],System.Globalization.NumberStyles.Float,ci,out float z))
                        {tri[vi%3]=new V3{X=x,Y=y,Z=z};vi++;}
                        if(vi%3==0&&vi>0){int b=_v.Count;_v.Add(tri[0]);_v.Add(tri[1]);_v.Add(tri[2]);_f.Add(new F3{A=b,B=b+1,C=b+2});}
                    }
                }
            }
            else
            {
                using(var br=new BinaryReader(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read)))
                {
                    br.ReadBytes(80); // header
                    uint count=br.ReadUInt32();
                    for(uint t=0;t<count&&br.BaseStream.Position+50<=br.BaseStream.Length;t++)
                    {
                        br.ReadBytes(12); // normal (ignore, we recompute)
                        int b=_v.Count;
                        for(int k=0;k<3;k++){float x=br.ReadSingle(),y=br.ReadSingle(),z=br.ReadSingle();_v.Add(new V3{X=x,Y=y,Z=z});}
                        _f.Add(new F3{A=b,B=b+1,C=b+2});
                        br.ReadUInt16(); // attr
                    }
                }
            }
        }
 
        // -- PLY (ASCII) ---------------------------------------------------
        void LoadPly(string path)
        {
            var ci=System.Globalization.CultureInfo.InvariantCulture;
            var lines=File.ReadAllLines(path);
            int vCount=0,fCount=0,hEnd=0;
            for(int i=0;i<lines.Length;i++)
            {
                var l=lines[i].Trim();
                if(l.StartsWith("element vertex"))int.TryParse(l.Split(' ')[2],out vCount);
                else if(l.StartsWith("element face"))int.TryParse(l.Split(' ')[2],out fCount);
                else if(l=="end_header"){hEnd=i+1;break;}
            }
            for(int i=hEnd;i<hEnd+vCount&&i<lines.Length;i++)
            {
                var p=lines[i].Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if(p.Length>=3&&float.TryParse(p[0],System.Globalization.NumberStyles.Float,ci,out float x)&&float.TryParse(p[1],System.Globalization.NumberStyles.Float,ci,out float y)&&float.TryParse(p[2],System.Globalization.NumberStyles.Float,ci,out float z))
                    _v.Add(new V3{X=x,Y=y,Z=z});
            }
            for(int i=hEnd+vCount;i<hEnd+vCount+fCount&&i<lines.Length;i++)
            {
                var p=lines[i].Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                if(p.Length>=4&&int.TryParse(p[0],out int cnt)&&cnt>=3)
                {
                    int a=int.Parse(p[1]),b=int.Parse(p[2]),c=int.Parse(p[3]);
                    _f.Add(new F3{A=a,B=b,C=c});
                    for(int k=4;k<p.Length;k++)_f.Add(new F3{A=a,B=int.Parse(p[k-1]),C=int.Parse(p[k])});
                }
            }
        }
 
        V3[] _vNormals=null; // per-vertex normals for smooth shading
 
        void BuildVertexNormals()
        {
            _vNormals=new V3[_v.Count];
            foreach(var f in _f)
            {
                if(f.A>=_v.Count||f.B>=_v.Count||f.C>=_v.Count)continue;
                var fn=NormV(CrossV(SubV(_v[f.B],_v[f.A]),SubV(_v[f.C],_v[f.A])));
                _vNormals[f.A]=AddV(_vNormals[f.A],fn);
                _vNormals[f.B]=AddV(_vNormals[f.B],fn);
                _vNormals[f.C]=AddV(_vNormals[f.C],fn);
            }
            for(int i=0;i<_vNormals.Length;i++)_vNormals[i]=NormV(_vNormals[i]);
        }
        static V3 AddV(V3 a,V3 b)=>new V3{X=a.X+b.X,Y=a.Y+b.Y,Z=a.Z+b.Z};
 
        void NormalizeModel()
        {
            if(_v.Count==0)return;
            float mnX=_v[0].X,mxX=mnX,mnY=_v[0].Y,mxY=mnY,mnZ=_v[0].Z,mxZ=mnZ;
            foreach(var v in _v){mnX=Math.Min(mnX,v.X);mxX=Math.Max(mxX,v.X);mnY=Math.Min(mnY,v.Y);mxY=Math.Max(mxY,v.Y);mnZ=Math.Min(mnZ,v.Z);mxZ=Math.Max(mxZ,v.Z);}
            float cx=(mnX+mxX)/2,cy=(mnY+mxY)/2,cz=(mnZ+mxZ)/2;
            float maxExt=Math.Max(1e-6f,Math.Max(Math.Max(mxX-mnX,mxY-mnY),mxZ-mnZ));
            float sc=1f/maxExt; // normalise largest axis to [-0.5, 0.5]
            for(int i=0;i<_v.Count;i++){var v=_v[i];_v[i]=new V3{X=(v.X-cx)*sc,Y=(v.Y-cy)*sc,Z=(v.Z-cz)*sc};}
        }
 
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;
            g.Clear(Color.FromArgb(240,240,240));
            if(_v.Count==0){g.DrawString("No model loaded – drag an .obj/.stl/.ply file here",Th.UiSmall,new SolidBrush(Color.FromArgb(140,140,140)),4,4);return;}
            g.SmoothingMode=SmoothingMode.AntiAlias;
 
            float cx=Width/2f,cy=Height/2f;
            // Camera scale: derived from panel size + user zoom
            float sc=Math.Min(Width,Height)*0.44f*_zoom;
 
            float cosX=(float)Math.Cos(_rx),sinX=(float)Math.Sin(_rx);
            float cosY=(float)Math.Cos(_ry),sinY=(float)Math.Sin(_ry);
 
            // Project every vertex
            var r3=new V3[_v.Count];
            var p2=new PointF[_v.Count];
            for(int i=0;i<_v.Count;i++)
            {
                var v=_v[i];
                float x1=v.X*cosY+v.Z*sinY,z1=-v.X*sinY+v.Z*cosY;
                float y1=v.Y*cosX-z1*sinX,z2=v.Y*sinX+z1*cosX;
                r3[i]=new V3{X=x1,Y=y1,Z=z2};
                float pf=1f/(1.6f+z2*0.25f);
                p2[i]=new PointF(cx+x1*sc*pf,cy-y1*sc*pf);
            }
 
            // Collect valid faces with depth
            var validFaces=new List<int>(_f.Count);
            for(int fi=0;fi<_f.Count;fi++)
            {
                var f=_f[fi];
                if(f.A<0||f.A>=r3.Length||f.B<0||f.B>=r3.Length||f.C<0||f.C>=r3.Length)continue;
                validFaces.Add(fi);
            }
            // Painter's sort: back-to-front by average rotated Z
            validFaces.Sort((a,b)=>
            {
                var fa=_f[a]; var fb=_f[b];
                float za=(r3[fa.A].Z+r3[fa.B].Z+r3[fa.C].Z);
                float zb=(r3[fb.A].Z+r3[fb.B].Z+r3[fb.C].Z);
                return za.CompareTo(zb);
            });
 
            foreach(int fi in validFaces)
            {
                var f=_f[fi];
                V3 a=r3[f.A],b=r3[f.B],c2=r3[f.C];
 
                // Smooth shading: rotate pre-computed vertex normals, average them
                V3 rna,rnb,rnc;
                if(_vNormals!=null&&f.A<_vNormals.Length&&f.B<_vNormals.Length&&f.C<_vNormals.Length)
                {
                    rna=RotN(_vNormals[f.A],cosX,sinX,cosY,sinY);
                    rnb=RotN(_vNormals[f.B],cosX,sinX,cosY,sinY);
                    rnc=RotN(_vNormals[f.C],cosX,sinX,cosY,sinY);
                }
                else
                {
                    var e1s=SubV(b,a); var e2s=SubV(c2,a);
                    rna=rnb=rnc=NormV(CrossV(e1s,e2s));
                }
                var norm=NormV(AddV(AddV(rna,rnb),rnc));
 
                // Back-face cull: if average normal points away from camera, skip
                if(norm.Z<0f)continue;
 
                float diff=Math.Max(0f,DotV(norm,LightDir));
                float bri=Ambient+(1f-Ambient)*diff;
                // Slight cool tint on model to stand out from grey bg
                int rc=(int)(bri*175); int gc=(int)(bri*180); int bc2=(int)(bri*190);
                var pts=new PointF[]{p2[f.A],p2[f.B],p2[f.C]};
                using(var fb2=new SolidBrush(Color.FromArgb(rc,gc,bc2)))g.FillPolygon(fb2,pts);
                // Subtle edge
                using(var ep=new Pen(Color.FromArgb(40,80,80,90),0.5f))g.DrawPolygon(ep,pts);
            }
 
            // Format label bottom-left
            if(_fmtLabel.Length>0)
                g.DrawString(_fmtLabel,Th.UiSmall,new SolidBrush(Color.FromArgb(130,130,130)),3,Height-16);
 
            // Hint text
            g.DrawString("Drag to rotate  •  Scroll to zoom",Th.UiSmall,new SolidBrush(Color.FromArgb(150,150,150)),3,3);
        }
 
        // -- Vector helpers -------------------------------------------------
        static V3 SubV(V3 a,V3 b)=>new V3{X=a.X-b.X,Y=a.Y-b.Y,Z=a.Z-b.Z};
        static V3 CrossV(V3 a,V3 b)=>new V3{X=a.Y*b.Z-a.Z*b.Y,Y=a.Z*b.X-a.X*b.Z,Z=a.X*b.Y-a.Y*b.X};
        static float DotV(V3 a,V3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        static V3 NormV(V3 v){float l=(float)Math.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z);return l<1e-8f?v:new V3{X=v.X/l,Y=v.Y/l,Z=v.Z/l};}
        // Rotate normal the same way as vertices (Y-then-X rotation)
        static V3 RotN(V3 n,float cosX,float sinX,float cosY,float sinY)
        {
            float x1=n.X*cosY+n.Z*sinY,z1=-n.X*sinY+n.Z*cosY;
            float y1=n.Y*cosX-z1*sinX,z2=n.Y*sinX+z1*cosX;
            return new V3{X=x1,Y=y1,Z=z2};
        }
    }
 
    // -------------------------------------------------------------------------
    //  AUDIO PREVIEW PANEL
    // -------------------------------------------------------------------------
    class AudioPanel:Panel
    {
        [DllImport("winmm.dll",CharSet=CharSet.Unicode)]
        static extern int mciSendString(string cmd,System.Text.StringBuilder ret,int retLen,IntPtr cb);
 
        Image _ico; string _path; bool _playing; Rectangle _pbr;
        SoundPlayer _sp;
        const string Alias="wex_aud";
        static readonly HashSet<string> WavFormats=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".wav"};
        static readonly HashSet<string> MciFormats=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".mp3",".wma",".m4a",".aac"};
 
        public AudioPanel(){BackColor=Color.FromArgb(245,248,255);DoubleBuffered=true;MouseClick+=OnClick;}
        public void Load(string path,Image ico)
        {
            StopAll();
            _path=path;_ico=ico;_playing=false;
            Invalidate();
        }
        void StopAll()
        {
            _sp?.Stop();_sp=null;
            try{mciSendString($"stop {Alias}",null,0,IntPtr.Zero);}catch{}
            try{mciSendString($"close {Alias}",null,0,IntPtr.Zero);}catch{}
            _playing=false;
        }
        void OnClick(object s,MouseEventArgs e)
        {
            if(!_pbr.Contains(e.Location)||_path==null)return;
            if(_playing){StopAll();Invalidate();return;}
            string ext=Path.GetExtension(_path).ToLower();
            if(WavFormats.Contains(ext))
            {
                try{_sp=new SoundPlayer(_path);_sp.PlayLooping();_playing=true;}catch{}
            }
            else if(MciFormats.Contains(ext))
            {
                int r=mciSendString("open \""+_path+"\" type mpegvideo alias "+Alias,null,0,IntPtr.Zero);
                if(r!=0)r=mciSendString("open \""+_path+"\" alias "+Alias,null,0,IntPtr.Zero);
                if(r==0){mciSendString("play "+Alias,null,0,IntPtr.Zero);_playing=true;}
                else try{Process.Start(new ProcessStartInfo(_path){UseShellExecute=true});}catch{}
            }
            else // ogg/flac/opus: open with default player
            {
                try{Process.Start(new ProcessStartInfo(_path){UseShellExecute=true});}catch{}
                return;
            }
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            int cx=Width/2,cy=Height/2;
            if(_ico!=null)g.DrawImage(_ico,cx-32,cy-55,64,64);
            // waveform
            using(var wp=new Pen(Color.FromArgb(0,120,215),1.5f))
                for(int i=0;i<28;i++){float x=8+i*(Width-16f)/28;float h2=4+(float)(Math.Sin(i*0.8)*8+Math.Sin(i*1.5)*4+2);g.DrawLine(wp,x,cy+20-h2,x,cy+20+h2);}
            // play/stop button
            int bx=cx-14,by=cy+36;_pbr=new Rectangle(bx,by,28,22);
            var col=_playing?Color.FromArgb(200,0,0):Color.FromArgb(0,120,215);
            g.FillEllipse(new SolidBrush(col),bx,by,28,22);
            if(_playing){g.FillRectangle(Brushes.White,bx+7,by+5,4,12);g.FillRectangle(Brushes.White,bx+17,by+5,4,12);}
            else{g.FillPolygon(Brushes.White,new[]{new Point(bx+8,by+4),new Point(bx+8,by+18),new Point(bx+21,by+11)});}
            string msg=_sp!=null?(_playing?"Click to stop":"Click to play"):"Click to open";
            using(var fmt=new StringFormat{Alignment=StringAlignment.Center})
                g.DrawString(msg,Th.UiSmall,Brushes.Gray,new RectangleF(0,by+26,Width,20),fmt);
        }
    }
 
    // -------------------------------------------------------------------------
    //  PREVIEW PANE  (360 px wide)
    //  Top 256 px: file content  |  Bottom: file info
    // -------------------------------------------------------------------------
    class VideoPanel:Panel
    {
        [DllImport("winmm.dll",CharSet=CharSet.Unicode)]
        static extern int mciSendString(string cmd,System.Text.StringBuilder ret,int retLen,IntPtr hwnd);
 
        string _path; bool _playing; Image _thumb; Rectangle _playBtn;
        const string VA="wex_vid";
 
        public VideoPanel(){BackColor=Color.Black;DoubleBuffered=true;MouseClick+=OnClick;Resize+=(s,e)=>{if(_playing)ResizeVideo();else Invalidate();};}
 
        public void Load(string path)
        {
            StopVid();_path=path;_thumb=null;_playing=false;
            if(path!=null){try{_thumb=Shell.Thumbnail(path,256);}catch{}}
            if(_thumb==null&&path!=null)_thumb=Shell.LargeIcon(path)??Icons.Get("videos");
            Invalidate();
        }
 
        void StopVid()
        {
            if(!_playing)return;
            try{mciSendString("stop "+VA,null,0,IntPtr.Zero);}catch{}
            try{mciSendString("close "+VA,null,0,IntPtr.Zero);}catch{}
            _playing=false;
        }
 
        void ResizeVideo()
        {
            try{mciSendString("put "+VA+" window at 0 0 "+Width+" "+Height,null,0,IntPtr.Zero);}catch{}
        }
 
        void OnClick(object s,MouseEventArgs e)
        {
            if(_path==null)return;
            if(_playing){StopVid();Invalidate();return;}
            if(!_playBtn.Contains(e.Location))return;
            // Try MCI video in this panel
            try
            {
                string open="open \""+_path+"\" type mpegvideo alias "+VA;
                int r=mciSendString(open,null,0,IntPtr.Zero);
                if(r!=0){r=mciSendString("open \""+_path+"\" alias "+VA,null,0,IntPtr.Zero);}
                if(r==0)
                {
                    mciSendString("window "+VA+" handle "+Handle.ToString(),null,0,IntPtr.Zero);
                    mciSendString("put "+VA+" window at 0 0 "+Width+" "+Height,null,0,IntPtr.Zero);
                    mciSendString("play "+VA,null,0,IntPtr.Zero);
                    _playing=true; Invalidate(); return;
                }
            }catch{}
            // Fallback: OS default player
            try{Process.Start(new ProcessStartInfo(_path){UseShellExecute=true});}catch{}
        }
 
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.Clear(Color.FromArgb(20,20,20));
            if(!_playing&&_thumb!=null)
            {
                float sc=Math.Min((float)Width/_thumb.Width,(float)Height/_thumb.Height);
                int w=(int)(_thumb.Width*sc),h=(int)(_thumb.Height*sc);
                g.DrawImage(_thumb,(Width-w)/2,(Height-h)/2,w,h);
            }
            int bx=Width/2-18,by=Height-46;
            _playBtn=new Rectangle(bx-4,by-4,44,38);
            using(var bb=new SolidBrush(Color.FromArgb(190,0,0,0)))
                g.FillEllipse(bb,bx,by,36,30);
            if(!_playing)
                g.FillPolygon(Brushes.White,new[]{new Point(bx+10,by+6),new Point(bx+10,by+24),new Point(bx+27,by+15)});
            else
            {g.FillRectangle(Brushes.White,bx+8,by+7,5,16);g.FillRectangle(Brushes.White,bx+19,by+7,5,16);}
            if(!_playing){using(var fmt=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("Click to play",Th.UiSmall,new SolidBrush(Color.FromArgb(180,255,255,255)),new RectangleF(0,by+36,Width,18),fmt);}
        }
    }
 
    class PreviewPane:Panel
    {
        public const int PW=360; const int TOP_H=256,TAB_H=20;
 
        Panel _top; Panel _content; Panel _info;
        Button _btnPrev,_btnHex,_btnTxt;
        PictureBox _picBox; RichTextBox _txtBox; RichTextBox _rawTxtBox; HexPanel _hexPanel; ObjPanel _objPanel; AudioPanel _audioPanel; VideoPanel _vidPanel; Label _iconLbl;
        bool _showHex; Control _activePreview;
 
        Label _lbName,_lbType,_lbPath,_lbModified,_lbSize;
 
        public PreviewPane()
        {
            Width=PW;Dock=DockStyle.Right;BackColor=Th.PreviewBg;Padding=Padding.Empty;
            Build();
            _btnTxt.Click+=(s,e)=>ShowRawText();  // wired after Build so ShowRawText is in scope
        }
        void Build()
        {
            // -- top holder (256px) ------------------------------------------
            _top=new Panel{Dock=DockStyle.Top,Height=TOP_H,BackColor=Color.White};
            // tab bar
            var tabs=new Panel{Dock=DockStyle.Top,Height=TAB_H,BackColor=Th.Bg};
            _btnPrev=new Button{Text="Preview",FlatStyle=FlatStyle.Flat,Width=72,Height=TAB_H,Dock=DockStyle.Left,Font=Th.UiSmall,BackColor=Th.SelFill};
            _btnPrev.FlatAppearance.BorderSize=0;
            _btnHex =new Button{Text="Hex",FlatStyle=FlatStyle.Flat,Width=40,Height=TAB_H,Dock=DockStyle.Left,Font=Th.UiSmall,BackColor=Color.Transparent};
            _btnHex.FlatAppearance.BorderSize=0;
            _btnTxt =new Button{Text="Text",FlatStyle=FlatStyle.Flat,Width=44,Height=TAB_H,Dock=DockStyle.Left,Font=Th.UiSmall,BackColor=Color.Transparent};
            _btnTxt.FlatAppearance.BorderSize=0;
            _btnPrev.Click+=(s,e)=>SetHex(false); _btnHex.Click+=(s,e)=>SetHex(true);
            // _btnTxt wired after Build() – see constructor below
            tabs.Controls.Add(_btnHex); tabs.Controls.Add(_btnTxt); tabs.Controls.Add(_btnPrev);
            _top.Controls.Add(tabs);
            // content area
            _content=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};
            _picBox  =new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,Visible=false};
            _txtBox  =new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,Font=Th.Mono,BackColor=Color.FromArgb(252,252,255),BorderStyle=BorderStyle.None,Visible=false,ScrollBars=RichTextBoxScrollBars.Vertical};
            _rawTxtBox=new RichTextBox{Dock=DockStyle.Fill,ReadOnly=true,Font=Th.Mono,BackColor=Color.White,BorderStyle=BorderStyle.None,Visible=false,ScrollBars=RichTextBoxScrollBars.Vertical};
            _hexPanel=new HexPanel{Dock=DockStyle.Fill,Visible=false};
            _objPanel=new ObjPanel{Dock=DockStyle.Fill,Visible=false};
            _audioPanel=new AudioPanel{Dock=DockStyle.Fill,Visible=false};
            _vidPanel=new VideoPanel{Dock=DockStyle.Fill,Visible=false};
            _iconLbl =new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=Th.UiFont,ForeColor=Th.TxtDisabled,Text="No selection",Visible=true};
            _content.Controls.AddRange(new Control[]{_iconLbl,_vidPanel,_audioPanel,_objPanel,_hexPanel,_rawTxtBox,_txtBox,_picBox});
            _top.Controls.Add(_content);
 
            // -- info panel -------------------------------------------------
            _info=new Panel{Dock=DockStyle.Fill,BackColor=Th.PreviewBg,Padding=new Padding(8,6,8,6)};
            _info.Paint+=PaintInfoSep;
            _lbName    =MkLbl(true);
            _lbType    =MkPropLbl("Type:");
            _lbPath    =MkPropLbl("Location:");
            _lbModified=MkPropLbl("Date modified:");
            _lbSize    =MkPropLbl("Size:");
            // reverse add for DockStyle.Top ordering
            _info.Controls.Add(_lbSize); _info.Controls.Add(_lbModified);
            _info.Controls.Add(_lbPath); _info.Controls.Add(_lbType); _info.Controls.Add(_lbName);
 
            Controls.Add(_info); Controls.Add(_top);
 
            Paint+=(s2,e2)=>{using(var p=new Pen(Th.PaneSep))e2.Graphics.DrawLine(p,0,0,0,Height);};
        }
        static void PaintInfoSep(object s,PaintEventArgs e){using(var p=new Pen(Th.HdrBorder))e.Graphics.DrawLine(p,0,0,((Control)s).Width,0);}
        static Label MkLbl(bool bold){return new Label{Dock=DockStyle.Top,Height=24,Font=bold?Th.UiBold:Th.UiFont,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft};}
        static Label MkPropLbl(string prefix)
        {
            var l=new Label{Dock=DockStyle.Top,Height=20,Font=Th.UiFont,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Th.TxtDisabled};
            l.Tag=prefix; return l;
        }
        void SetHex(bool hex)
        {
            _showHex=hex;
            _btnPrev.BackColor=hex?Color.Transparent:Th.SelFill;
            _btnHex.BackColor=hex?Th.SelFill:Color.Transparent;
            ShowCtrl(hex?_hexPanel:_activePreview);
        }
        void ShowCtrl(Control c)
        {
            _picBox.Visible=c==_picBox; _txtBox.Visible=c==_txtBox; _rawTxtBox.Visible=c==_rawTxtBox; _hexPanel.Visible=c==_hexPanel;
            _objPanel.Visible=c==_objPanel; _audioPanel.Visible=c==_audioPanel; _vidPanel.Visible=c==_vidPanel;
            _iconLbl.Visible=c==_iconLbl||c==null;
        }
        void ShowRawText()
        {
            _btnPrev.BackColor=Color.Transparent;
            _btnTxt .BackColor=Th.SelFill;
            _btnHex .BackColor=Color.Transparent;
            ShowCtrl(_rawTxtBox);
        }
 
        public void ShowItem(ContentItem item)
        {
            if(item==null){Clear();return;}
            _lbName.Text=item.Name;
            _lbType.Text    ="Type: "+(item.IsDirectory?"File folder":item.ItemType);
            _lbPath.Text    ="Location: "+(item.FullPath!=null?Path.GetDirectoryName(item.FullPath):"");
            _lbModified.Text="Date modified: "+item.DateStr;
            _lbSize.Text    ="Size: "+(item.IsDirectory?"":item.SizeStr);
 
            _hexPanel.Load(item.FullPath);
            // Plain UTF-8 text tab – lazy 128 KB read
            System.Threading.Tasks.Task.Run(()=>
            {
                string result="";
                try
                {
                    using(var sr=new StreamReader(item.FullPath,System.Text.Encoding.UTF8,true,8192))
                    {
                        var buf=new char[128*1024]; int read=sr.Read(buf,0,buf.Length);
                        result=new string(buf,0,read);
                        if(!sr.EndOfStream)result+="\n\n... [first 128 KB shown — file is larger] ...";
                    }
                }
                catch(Exception ex){result="(error reading file: "+ex.Message+")";}
                try{BeginInvoke((Action)(()=>{if(!IsDisposed)_rawTxtBox.Text=result;}));}catch{}
            });
 
            string ext=item.FullPath!=null?Path.GetExtension(item.FullPath).ToLower():"";
            Control chosen;
 
            if(!item.IsDirectory&&IsImg(ext))
            {
                // Load via MemoryStream so the file handle is released immediately
                try
                {
                    _picBox.Image?.Dispose();
                    if(IsGdiImg(ext))
                    {
                        var bytes=File.ReadAllBytes(item.FullPath);
                        using(var ms=new MemoryStream(bytes)) _picBox.Image=new Bitmap(Image.FromStream(ms));
                    }
                    else
                    {
                        // DDS / TGA / SVG / others: ask Windows shell for a thumbnail
                        var th=Shell.Thumbnail(item.FullPath,224)??Shell.LargeIcon(item.FullPath);
                        _picBox.Image=th;
                    }
                }
                catch{_picBox.Image=null;}
                chosen=_picBox;
            }
            else if(!item.IsDirectory&&IsTxt(ext))
            {
                try
                {
                    string raw=File.ReadAllText(item.FullPath);
                    _txtBox.Text=raw.Length>60000?raw.Substring(0,60000)+"\n... [truncated]":raw;
                    ColorCode(ext);
                }
                catch{_txtBox.Text="(could not read file)";}
                chosen=_txtBox;
            }
            else if(!item.IsDirectory&&Is3D(ext))
            {
                _objPanel.Load(item.FullPath); chosen=_objPanel;
            }
            else if(!item.IsDirectory&&IsVideo(ext))
            {
                _vidPanel.Load(item.FullPath); chosen=_vidPanel;
            }
            else if(!item.IsDirectory&&IsAudio(ext))
            {
                var ico=Shell.LargeIcon(item.FullPath)??Icons.Get("file");
                _audioPanel.Load(item.FullPath,ico); chosen=_audioPanel;
            }
            else
            {
                // Thumbnail from shell (images, video, etc.) or large icon
                // Default: stretch to 256×256 in PictureBox (fits panel)
                Image thumb=Shell.Thumbnail(item.FullPath??string.Empty,256)??Shell.LargeIcon(item.FullPath??string.Empty);
                if(thumb==null){string icoN=item.IsDirectory?"folder":FileIcon.Get(ext);thumb=Icons.Get(icoN);}
                _iconLbl.Image=thumb; _iconLbl.ImageAlign=ContentAlignment.MiddleCenter; _iconLbl.Text="";
                chosen=_iconLbl;
            }
            _activePreview=chosen;
            if(!_showHex)ShowCtrl(chosen);
        }
 
        public void Clear()
        {
            _lbName.Text=""; _lbType.Text=""; _lbPath.Text=""; _lbModified.Text=""; _lbSize.Text="";
            _iconLbl.Image=null; _iconLbl.Text="No selection"; _picBox.Image=null; _txtBox.Text=""; _rawTxtBox.Text="";
            _activePreview=_iconLbl; ShowCtrl(_iconLbl);
        }
 
        // GDI+ natively supported (load via Image.FromStream)
        static bool IsGdiImg(string e)=>".png.jpg.jpeg.bmp.gif.ico.tiff.tif.webp.emf.wmf".Contains(e);
        // All image types we handle (non-GDI via shell thumbnail)
        static bool Is3D(string e)=>".obj.stl.ply".Contains(e);
        static bool IsImg(string e)=>IsGdiImg(e)||".dds.tga.svg.svgz.exr.hdr.psd.pcx.ppm.pgm.pbm.xbm.xpm.raw.cr2.nef.arw.orf.heic.heif.avif.jxl.tga".Contains(e);
        static bool IsTxt(string e)=>".txt.cs.py.cpp.c.h.hpp.cxx.cc.js.ts.jsx.tsx.html.htm.css.scss.less.xml.json.md.ini.cfg.log.bat.cmd.sh.bash.zsh.yaml.yml.toml.lua.rb.java.php.go.rs.swift.kt.vb.fs.sql.ps1.r.m.asm.s.awk.sed.dockerfile.makefile.cmake.gradle.properties.env".Contains(e.TrimStart('.').ToLower().Contains('.')?e:"."+e.TrimStart('.'));
        static bool IsAudio(string e)=>".mp3.wav.ogg.flac.aac.m4a.wma.opus.ape.aiff.aif.au.mid.midi.xm.mod.it.s3m.wv.tta.tak.mka.ra.rm.mpc.snd".Contains(e);
        static bool IsVideo(string e)=>".mp4.avi.mkv.mov.wmv.flv.webm.mpg.mpeg.m4v.3gp.ts.vob.rm.rmvb.divx.f4v.asf.m2v.ogv.mts.m2ts.tp.trp.mxf.dv.amv.m2p.xvid".Contains(e);
 
        void ColorCode(string ext)
        {
            _txtBox.SuspendLayout();
            _txtBox.SelectAll(); _txtBox.SelectionColor=Color.Black;
            if(".cs.cpp.c.h.java".Contains(ext))
            {
                var kw=new[]{"using","namespace","class","void","int","string","bool","return","if","else","for","foreach","while","new","static","public","private","protected","override","virtual","abstract","sealed","readonly","const","null","true","false","var","this","base","try","catch","finally","throw","event","delegate","async","await","ref","out","in"};
                foreach(var k in kw)ColWord(_txtBox,k,Color.FromArgb(0,0,180));
                ColComments(_txtBox,"//",'\n',Color.FromArgb(0,128,0));
                ColStrings(_txtBox,Color.FromArgb(163,21,21));
            }
            else if(ext==".py")
            {
                var kw=new[]{"def","class","import","from","return","if","else","elif","for","while","in","not","and","or","None","True","False","self","with","as","pass","break","continue","try","except","finally","raise","lambda","yield","print"};
                foreach(var k in kw)ColWord(_txtBox,k,Color.FromArgb(0,0,180));
                ColComments(_txtBox,"#",'\n',Color.FromArgb(0,128,0));
                ColStrings(_txtBox,Color.FromArgb(163,21,21));
            }
            else if(".html.htm.xml".Contains(ext))
            {
                ColTag(_txtBox,Color.FromArgb(0,0,200),Color.FromArgb(128,0,0));
            }
            _txtBox.Select(0,0); _txtBox.ResumeLayout();
        }
        static void ColWord(RichTextBox r,string kw,Color c)
        {
            string t=r.Text; int s=0;
            while(true){int i=t.IndexOf(kw,s,StringComparison.Ordinal);if(i<0)break;bool before=i==0||(!char.IsLetterOrDigit(t[i-1])&&t[i-1]!='_');bool after=i+kw.Length>=t.Length||(!char.IsLetterOrDigit(t[i+kw.Length])&&t[i+kw.Length]!='_');if(before&&after){r.Select(i,kw.Length);r.SelectionColor=c;}s=i+kw.Length;}
        }
        static void ColComments(RichTextBox r,string marker,char end,Color c)
        {
            string t=r.Text; int s=0;
            while(true){int i=t.IndexOf(marker,s,StringComparison.Ordinal);if(i<0)break;int e2=t.IndexOf(end,i);if(e2<0)e2=t.Length;r.Select(i,e2-i);r.SelectionColor=c;s=e2;}
        }
        static void ColStrings(RichTextBox r,Color c)
        {
            string t=r.Text; int s=0;
            while(true){int i=t.IndexOf('"',s);if(i<0)break;int j=t.IndexOf('"',i+1);if(j<0)break;r.Select(i,j-i+1);r.SelectionColor=c;s=j+1;}
        }
        static void ColTag(RichTextBox r,Color tagC,Color attrC)
        {
            string t=r.Text; int s=0;
            while(true){int i=t.IndexOf('<',s);if(i<0)break;int j=t.IndexOf('>',i);if(j<0)break;r.Select(i,j-i+1);r.SelectionColor=tagC;s=j+1;}
        }
    }
 
    // -------------------------------------------------------------------------
    //  CONTENT PANE  (with shell icons, inline rename, DnD, extended marquee)
    // -------------------------------------------------------------------------
    // Maps a file extension to the embedded icon name used in the UI
    static class FileIcon
    {
        static readonly Dictionary<string,string> Map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            // Text / code
            {".txt","txt"},{".log","txt"},{".md","txt"},{".readme","txt"},
            {".cs","txt"},{".cpp","txt"},{".c","txt"},{".h","txt"},
            {".py","txt"},{".js","txt"},{".ts","txt"},{".java","txt"},
            {".html","txt"},{".htm","txt"},{".xml","txt"},{".json","txt"},
            {".yaml","txt"},{".yml","txt"},{".toml","txt"},{".sql","txt"},
            // Config
            {".ini","ini"},{".cfg","ini"},{".conf","ini"},{".env","ini"},
            // Executable / library
            {".exe","exe"},{".dll","dll"},{".so","dll"},{".dylib","dll"},
            // Script
            {".bat","bat"},{".cmd","bat"},{".sh","bat"},{".ps1","bat"},
            // Archive
            {".zip","zip"},{".7z","zip"},{".rar","zip"},{".tar","zip"},
            {".gz","zip"},{".bz2","zip"},{".xz","zip"},{".zst","zip"},
            // Audio
            {".mp3","music"},{".wav","music"},{".flac","music"},{".ogg","music"},
            {".aac","music"},{".m4a","music"},{".wma","music"},{".opus","music"},
            {".ape","music"},{".aiff","music"},{".aif","music"},
            // Video
            {".mp4","videos"},{".avi","videos"},{".mkv","videos"},{".mov","videos"},
            {".wmv","videos"},{".flv","videos"},{".webm","videos"},{".mpg","videos"},
            {".mpeg","videos"},{".m4v","videos"},{".3gp","videos"},
            // Image
            {".png","pictures"},{".jpg","pictures"},{".jpeg","pictures"},
            {".bmp","pictures"},{".gif","pictures"},{".tiff","pictures"},
            {".tif","pictures"},{".webp","pictures"},{".ico","pictures"},
            {".svg","pictures"},{".dds","pictures"},{".tga","pictures"},
            // Office / document
            {".pdf","file"},{".doc","documents"},{".docx","documents"},
            {".xls","file"},{".xlsx","file"},{".ppt","file"},{".pptx","file"},
        };
 
        public static string Get(string ext)
        {
            if(string.IsNullOrEmpty(ext))return"file";
            return Map.TryGetValue(ext,out var n)?n:"file";
        }
    }
 
    class ContentPane:Panel
    {
        const int HDR_H=25,ROW_H=22,ICO=16,DIV=4;
 
        int _wN=272,_wD=120,_wT=120,_wS=80;
        List<ContentItem> _items=new List<ContentItem>();
        HashSet<int> _sel=new HashSet<int>(); int _lastSel=-1; bool _focused;
        SortCol _sortCol=SortCol.Name; SortDir _sortDir=SortDir.Asc;
        int _scrollY; int _hdrHovCol=-1,_hdrHovDiv=-1; int _hovRow=-1;
        int _resizeCol=-1,_resizeStartX,_resizeStartW;
        bool _marquee; Point _mqA,_mqB;
 
        // Drag source
        bool _dragPending; Point _dragStartPt;
 
        InlineRenameBox _rnBox;
        VScrollBar _vsb;
        ContextMenuStrip _bgMenu,_folderMenu,_fileMenu;
 
        public string CurrentPath{get;private set;}="";
        public event Action<ContentItem> ItemActivated;
        // -- Search ----------------------------------------------------------
        string _searchQuery=""; bool _searchMode=false;
        List<ContentItem> _allItems=new List<ContentItem>();
        public event Action<ContentItem> SelectionChanged;
        HashSet<string> _cutPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 
        public ContentPane()
        {
            BackColor=Th.ContentBg;DoubleBuffered=true;Padding=Padding.Empty;
            SetStyle(ControlStyles.Selectable,true);TabStop=true;AllowDrop=true;
            _vsb=new VScrollBar{Dock=DockStyle.Right,Minimum=0,Visible=false};
            _vsb.ValueChanged+=(s,e)=>{_scrollY=_vsb.Value;Invalidate();};
            Controls.Add(_vsb);
            GotFocus+=(s,e)=>{_focused=true;Invalidate();};
            LostFocus+=(s,e)=>{_focused=false;Invalidate();};
            _rnBox=new InlineRenameBox{Visible=false};
            _rnBox.Committed+=(idx,name)=>CommitRename(idx,name);
            _rnBox.Cancelled+=()=>CancelRename();
            Controls.Add(_rnBox); _rnBox.BringToFront();
            MouseDown+=OnMD; MouseMove+=OnMM; MouseUp+=OnMU;
            MouseWheel+=OnMW; MouseLeave+=(s,e)=>{_hovRow=-1;Invalidate();};
            DoubleClick+=OnDbl;Resize+=(s,e)=>UpdateScroll();
            DragEnter+=OnDE; DragOver+=OnDOv; DragDrop+=OnDD;
            DragLeave+=(s,e)=>{_dndHovRow=-1;Invalidate();};
            BuildMenus();
        }
 
        // -- Public API -----------------------------------------------------
        volatile int _loadTok=0;
 
        public void LoadPath(string path,bool keepScroll=false)
        {
            CancelRename();
            CurrentPath=path; _searchMode=false; _searchQuery="";
            _sel.Clear(); _lastSel=-1;
            int saved=keepScroll?_scrollY:0;
            _items=new List<ContentItem>(); _allItems=new List<ContentItem>();
            if(!keepScroll){_scrollY=0;if(_vsb.Visible){try{_vsb.Value=0;}catch{}}}
            Invalidate();
            if(string.IsNullOrEmpty(path)||!Directory.Exists(path))return;
 
            int tok=++_loadTok;
            System.Threading.Tasks.Task.Run(()=>
            {
                var list=new List<ContentItem>();
                try
                {
                    foreach(var d in Directory.GetDirectories(path))
                    {if(_loadTok!=tok)return;try{var di=new DirectoryInfo(d);list.Add(new ContentItem{Name=di.Name,FullPath=di.FullName,DateModified=di.LastWriteTime,ItemType="File folder",IsDirectory=true});}catch{}}
                    foreach(var f in Directory.GetFiles(path))
                    {if(_loadTok!=tok)return;try{var fi=new FileInfo(f);list.Add(new ContentItem{Name=fi.Name,FullPath=fi.FullName,DateModified=fi.LastWriteTime,ItemType=Shell.TypeName(fi.FullName),Size=fi.Length,IsDirectory=false});}catch{}}
                }
                catch{}
                if(_loadTok!=tok||IsDisposed)return;
                try
                {
                    BeginInvoke((Action)(()=>
                    {
                        if(_loadTok!=tok||IsDisposed)return;
                        _items=list; _allItems=new List<ContentItem>(list);
                        SortItems(); UpdateScroll();
                        _scrollY=Math.Max(0,keepScroll?Math.Min(saved,Math.Max(0,_items.Count*ROW_H-ClientSize.Height+HDR_H)):0);
                        if(_vsb.Visible)try{_vsb.Value=_scrollY;}catch{}
                        Invalidate();
                    }));
                }
                catch{}
            });
        }
        public void SelectAll(){for(int i=0;i<_items.Count;i++)_sel.Add(i);Invalidate();}
        public void SetSearch(string query)
        {
            _searchQuery=(query??string.Empty).Trim();
            _searchMode=_searchQuery.Length>0;
            _sel.Clear(); _lastSel=-1; _scrollY=0;
            if(!_searchMode){_items=new List<ContentItem>(_allItems);}
            else
            {
                _items=new List<ContentItem>();
                SearchDir(CurrentPath,_searchQuery,_items,0);
            }
            SortItems(); UpdateScroll(); Invalidate();
        }
        void SearchDir(string dir,string q,List<ContentItem> out2,int depth)
        {
            if(depth>4||!Directory.Exists(dir))return;
            try
            {
                foreach(var d in Directory.GetDirectories(dir))
                    try{var di=new DirectoryInfo(d);if(di.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)out2.Add(new ContentItem{Name=di.Name,FullPath=di.FullName,DateModified=di.LastWriteTime,ItemType="File folder",IsDirectory=true});SearchDir(d,q,out2,depth+1);}catch{}
                foreach(var f in Directory.GetFiles(dir))
                    try{if(Path.GetFileName(f).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0){var fi=new FileInfo(f);out2.Add(new ContentItem{Name=fi.Name,FullPath=fi.FullName,DateModified=fi.LastWriteTime,ItemType=Shell.TypeName(fi.FullName),Size=fi.Length,IsDirectory=false});}}catch{}
            }catch{}
        }
        public void CutSelected(){if(_sel.Count==0)return;_cutPaths.Clear();foreach(int i in _sel)_cutPaths.Add(_items[i].FullPath);CopyClip(true);}
        public void CopySelected(){if(_sel.Count>0)CopyClip(false);}
        public void PasteFromClipboard()
        {
            if(!Clipboard.ContainsFileDropList()||!Directory.Exists(CurrentPath))return;
            var files=Clipboard.GetFileDropList();
            foreach(string src in files)try{string name=Path.GetFileName(src);string dst=Path.Combine(CurrentPath,name);if(File.Exists(src))File.Copy(src,dst,false);else if(Directory.Exists(src))CopyDir(src,dst);}catch(Exception ex){MessageBox.Show(ex.Message,"Paste Error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            LoadPath(CurrentPath);
        }
        public void DeleteSelected()
        {
            if(_sel.Count==0)return;
            string msg=_sel.Count==1?$"Delete '{_items[_sel.First()].Name}'?":$"Delete {_sel.Count} items?";
            if(MessageBox.Show(msg,"Delete",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            foreach(int i in _sel.OrderByDescending(x=>x))try{var it=_items[i];if(it.IsDirectory)Directory.Delete(it.FullPath,true);else File.Delete(it.FullPath);}catch(Exception ex){MessageBox.Show(ex.Message,"Delete Error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            LoadPath(CurrentPath);
        }
        public void StartRename(){if(_sel.Count==1)BeginRename(_sel.First());}
        public IEnumerable<string> SelectedPaths=>_sel.Select(i=>_items[i].FullPath);
        public ContentItem FirstSelected=>_sel.Count>0?_items[_sel.First()]:null;
 
        // -- Layout ---------------------------------------------------------
        int ListW()=>Width-(_vsb.Visible?_vsb.Width:0);
        int TotalW()=>_wN+_wD+_wT+_wS;
        int X1()=>_wN; int X2()=>_wN+_wD; int X3()=>_wN+_wD+_wT;
        int Div0()=>_wN; int Div1()=>_wN+_wD; int Div2()=>_wN+_wD+_wT;
        bool NearDiv(int x,int d)=>Math.Abs(x-d)<=DIV;
        int ColAt(int x){if(x<_wN)return 0;if(x<_wN+_wD)return 1;if(x<_wN+_wD+_wT)return 2;if(x<TotalW())return 3;return -1;}
        int DivAt(int x){if(NearDiv(x,Div0()))return 0;if(NearDiv(x,Div1()))return 1;if(NearDiv(x,Div2()))return 2;return -1;}
 
        // -- Paint ----------------------------------------------------------
        static readonly SolidBrush MetaBrush = new SolidBrush(Color.FromArgb(110,110,110));
 
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit; g.Clear(Th.ContentBg);
            // Rows drawn first; header painted on top to prevent any overlap
            DrawRows(g); DrawHdr(g);
            if(_marquee) DrawMarquee(g);
        }
        void DrawHdr(Graphics g)
        {
            int lw=ListW();
            g.FillRectangle(Brushes.White,0,0,lw,HDR_H);
            using(var hb=new Pen(Th.HdrBorder))g.DrawLine(hb,0,HDR_H-1,lw,HDR_H-1);
            // Column definitions: (startX, width, label, sortCol)
            int[] xs={0,X1(),X2(),X3()}, ws={_wN,_wD,_wT,_wS};
            string[] lbls={"Name","Date modified","Type","Size"};
            SortCol[] scs={SortCol.Name,SortCol.Date,SortCol.Type,SortCol.Size};
            var fmt=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
            for(int i=0;i<4;i++)
            {
                int x=xs[i],w=ws[i]; if(x>=lw)break;
                int cw=Math.Min(w,lw-x);
                if(_hdrHovCol==i&&_hdrHovDiv<0)
                    g.FillRectangle(new SolidBrush(Th.HdrHover),x,0,cw,HDR_H-1);
                // Grey label, same thin font as filenames
                g.DrawString(lbls[i],Th.UiFont,MetaBrush,new RectangleF(x+4,0,cw-14,HDR_H),fmt);
                if(scs[i]==_sortCol)
                {int ax=x+cw-10,ay=HDR_H/2;if(_sortDir==SortDir.Asc)g.FillPolygon(MetaBrush,new[]{new Point(ax-3,ay+2),new Point(ax+3,ay+2),new Point(ax,ay-2)});else g.FillPolygon(MetaBrush,new[]{new Point(ax-3,ay-2),new Point(ax+3,ay-2),new Point(ax,ay+2)});}
                if(i<3&&x+w<lw)using(var sp=new Pen(Th.HdrBorder))g.DrawLine(sp,x+w-1,3,x+w-1,HDR_H-3);
            }
            fmt.Dispose();
        }
        void DrawRows(Graphics g)
        {
            int lw=ListW(), tw=Math.Min(TotalW(),lw);
            if(_searchMode&&_items.Count==0)
            {
                using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Near})
                    g.DrawString("No items match your search.",Th.UiFont,MetaBrush,
                        new RectangleF(0,HDR_H+10,lw,30),fmt);
                return;
            }
            var elp=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap};
            var rgt=new StringFormat(elp){Alignment=StringAlignment.Far};
            // Clip so rows never paint over the column header
            var oldClip=g.Clip;
            g.SetClip(new Rectangle(0,HDR_H,lw,Math.Max(0,Height-HDR_H)));
            for(int i=0;i<_items.Count;i++)
            {
                var it=_items[i]; int y=HDR_H+i*ROW_H-_scrollY;
                if(y+ROW_H<=HDR_H)continue; if(y>Height)break;
                bool sel=_sel.Contains(i),hov=i==_hovRow,dndHov=i==_dndHovRow;
                var row=new Rectangle(0,y,tw,ROW_H-1);
                if(sel){if(_focused)Th.FillSel(g,row);else if(it.IsDirectory){using(var b=new SolidBrush(Th.InactiveDirFill))g.FillRectangle(b,row);}else{using(var p2=new Pen(Th.SelBorder))g.DrawRectangle(p2,row.X,row.Y,row.Width-1,row.Height-1);}}
                else if(hov||dndHov){Th.FillHover(g,row);if(dndHov)using(var p2=new Pen(Th.SelBorder))g.DrawRectangle(p2,row.X,row.Y,row.Width-1,row.Height-1);}
                // Resolve per-file-type icon; shell overrides when system_icons==1
                string icoName=it.IsDirectory?"folder":FileIcon.Get(Path.GetExtension(it.Name));
                var ico=it.Icon16??(Shell.SmallIcon(it.FullPath??string.Empty)??Icons.Get(icoName));
                it.Icon16=ico;
                g.DrawImage(ico,2,y+(ROW_H-ICO)/2,ICO,ICO);
                bool cut=_cutPaths.Contains(it.FullPath??"");
                var nameBrush=cut?new SolidBrush(Color.FromArgb(160,0,0,0)):Brushes.Black;
                g.DrawString(it.Name,Th.UiFont,nameBrush,new RectangleF(ICO+6,y,_wN-ICO-10,ROW_H),elp);
                // Date, Type, Size in grey
                if(X1()<lw)g.DrawString(it.DateStr,Th.UiFont,MetaBrush,new RectangleF(X1()+4,y,_wD-8,ROW_H),elp);
                if(X2()<lw)g.DrawString(it.ItemType,Th.UiFont,MetaBrush,new RectangleF(X2()+4,y,_wT-8,ROW_H),elp);
                if(X3()<lw)g.DrawString(it.SizeStr,Th.UiFont,MetaBrush,new RectangleF(X3()+2,y,_wS-4,ROW_H),rgt);
                if(cut&&nameBrush!=Brushes.Black)(nameBrush as SolidBrush)?.Dispose();
            }
            g.Clip=oldClip;
            elp.Dispose(); rgt.Dispose();
        }
        void DrawMarquee(Graphics g)
        {
            int x=Math.Min(_mqA.X,_mqB.X),y=Math.Min(_mqA.Y,_mqB.Y),w=Math.Abs(_mqB.X-_mqA.X),h=Math.Abs(_mqB.Y-_mqA.Y);
            using(var b=new SolidBrush(Color.FromArgb(60,Th.SelFill)))g.FillRectangle(b,x,y,w,h);
            using(var p=new Pen(Th.SelBorder))g.DrawRectangle(p,x,y,w-1,h-1);
        }
 
        // -- Mouse ----------------------------------------------------------
        void OnMD(object s,MouseEventArgs e)
        {
            Focus(); CancelRename();
            if(e.Y<HDR_H){HandleHdrClick(e);return;}
            int idx=RowAt(e.Y);
            if(e.Button==MouseButtons.Left)
            {
                bool ctrl=(ModifierKeys&Keys.Control)!=0,shift=(ModifierKeys&Keys.Shift)!=0;
                // Only treat as item-click when X is within the columns area
                if(idx>=0&&idx<_items.Count&&e.X<=TotalW())
                {
                    if(ctrl){if(_sel.Contains(idx))_sel.Remove(idx);else _sel.Add(idx);_lastSel=idx;}
                    else if(shift&&_lastSel>=0){_sel.Clear();for(int i=Math.Min(_lastSel,idx);i<=Math.Max(_lastSel,idx);i++)_sel.Add(i);}
                    else{_sel.Clear();_sel.Add(idx);_lastSel=idx;}
                    _dragPending=true; _dragStartPt=e.Location;
                }
                else{if(!ctrl&&!shift)_sel.Clear();_lastSel=-1;_marquee=true;_mqA=_mqB=e.Location;Capture=true;_dragPending=false;}
            }
            else if(e.Button==MouseButtons.Right)
            {
                if(idx>=0&&idx<_items.Count){if(!_sel.Contains(idx)){_sel.Clear();_sel.Add(idx);_lastSel=idx;}Invalidate();(_items[idx].IsDirectory?_folderMenu:_fileMenu).Show(this,e.Location);}
                else{Invalidate();_bgMenu.Show(this,e.Location);}
            }
            Invalidate(); SelectionChanged?.Invoke(FirstSelected);
        }
        void OnMM(object s,MouseEventArgs e)
        {
            if(_resizeCol>=0){int dx=e.X-_resizeStartX,nw=Math.Max(40,_resizeStartW+dx);switch(_resizeCol){case 0:_wN=nw;break;case 1:_wD=nw;break;case 2:_wT=nw;break;}Invalidate();return;}
            if(_dragPending&&e.Button==MouseButtons.Left&&(Math.Abs(e.X-_dragStartPt.X)>4||Math.Abs(e.Y-_dragStartPt.Y)>4))
            {
                _dragPending=false;
                var paths=SelectedPaths.Where(p=>p!=null).ToArray();
                if(paths.Length>0){var data=new DataObject();var sc=new StringCollection();sc.AddRange(paths);data.SetFileDropList(sc);DoDragDrop(data,DragDropEffects.Copy|DragDropEffects.Move);}
                return;
            }
            if(_marquee){_mqB=e.Location;UpdateMqSel();Invalidate();return;}
            if(e.Y<HDR_H){int div=DivAt(e.X),col=div>=0?-1:ColAt(e.X);if(div!=_hdrHovDiv||col!=_hdrHovCol){_hdrHovDiv=div;_hdrHovCol=col;Invalidate();}Cursor=div>=0?Cursors.VSplit:Cursors.Default;_hovRow=-1;}
            else{if(_hdrHovDiv>=0||_hdrHovCol>=0){_hdrHovDiv=-1;_hdrHovCol=-1;Invalidate();}Cursor=Cursors.Default;int idx=RowAt(e.Y);if(idx!=_hovRow){_hovRow=idx;Invalidate();}}
        }
        void OnMU(object s,MouseEventArgs e)
        {
            _dragPending=false;
            if(_resizeCol>=0){_resizeCol=-1;Capture=false;Cursor=Cursors.Default;Invalidate();return;}
            if(_marquee){_marquee=false;Capture=false;Invalidate();}
        }
        void OnMW(object s,MouseEventArgs e){_scrollY=Math.Max(0,_scrollY-e.Delta/3);if(_vsb.Visible){_scrollY=Math.Min(_scrollY,Math.Max(0,_vsb.Maximum-_vsb.LargeChange));_vsb.Value=_scrollY;}Invalidate();}
        void OnDbl(object s,EventArgs e){var mp=PointToClient(Cursor.Position);int idx=RowAt(mp.Y);if(idx>=0&&idx<_items.Count)ItemActivated?.Invoke(_items[idx]);}
        void HandleHdrClick(MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Left)return;
            int div=DivAt(e.X);
            if(div>=0){_resizeCol=div;_resizeStartX=e.X;_resizeStartW=div==0?_wN:div==1?_wD:_wT;Capture=true;return;}
            int col=ColAt(e.X); if(col<0)return;
            var sc=col==0?SortCol.Name:col==1?SortCol.Date:col==2?SortCol.Type:SortCol.Size;
            if(_sortCol==sc)_sortDir=_sortDir==SortDir.Asc?SortDir.Desc:SortDir.Asc;else{_sortCol=sc;_sortDir=SortDir.Asc;}
            SortItems();_sel.Clear();Invalidate();
        }
        void UpdateMqSel()
        {
            // Use full Y range; X range need not intersect columns (marquee works anywhere in panel)
            int y1=Math.Min(_mqA.Y,_mqB.Y),y2=Math.Max(_mqA.Y,_mqB.Y);
            _sel.Clear();
            for(int i=0;i<_items.Count;i++){int ry=HDR_H+i*ROW_H-_scrollY;if(ry+ROW_H>y1&&ry<y2)_sel.Add(i);}
        }
        // -- DnD drops INTO content pane -------------------------------------
        void OnDE(object s,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;}
        int _dndHovRow=-1;  // row being hovered during DnD
        void OnDOv(object s,DragEventArgs e)
        {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effect=DragDropEffects.None;return;}
            var pt=PointToClient(new Point(e.X,e.Y));
            int idx=RowAt(pt.Y);
            bool onFolder=idx>=0&&idx<_items.Count&&_items[idx].IsDirectory;
            _dndHovRow=onFolder?idx:-1;
            e.Effect=DragDropEffects.Copy; Invalidate();
        }
        void OnDD(object s,DragEventArgs e)
        {
            if(!e.Data.GetDataPresent(DataFormats.FileDrop)||!Directory.Exists(CurrentPath))return;
            var files=(string[])e.Data.GetData(DataFormats.FileDrop);
            foreach(var src in files)
                try{string name=Path.GetFileName(src);string dst=Path.Combine(CurrentPath,name);if(File.Exists(src)){if(!dst.Equals(src,StringComparison.OrdinalIgnoreCase))File.Copy(src,dst,false);}else if(Directory.Exists(src)){if(!dst.Equals(src,StringComparison.OrdinalIgnoreCase))CopyDir(src,dst);}}
                catch(Exception ex){MessageBox.Show(ex.Message,"Drop Error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            LoadPath(CurrentPath);
        }
 
        int RowAt(int y)=>y<HDR_H?-1:(_scrollY+y-HDR_H)/ROW_H;
        void UpdateScroll(){int tot=_items.Count*ROW_H,vis=Math.Max(1,ClientSize.Height-HDR_H);_vsb.Visible=tot>vis;if(_vsb.Visible){_vsb.Maximum=Math.Max(0,tot-vis+100);_vsb.SmallChange=ROW_H;_vsb.LargeChange=100;}else _scrollY=0;}
        void SortItems()
        {
            IEnumerable<ContentItem> s;
            if(_sortCol==SortCol.Date) s=_items.OrderBy(i=>i.DateModified);
            else if(_sortCol==SortCol.Type) s=_items.OrderBy(i=>i.ItemType,StringComparer.OrdinalIgnoreCase);
            else if(_sortCol==SortCol.Size) s=_items.OrderBy(i=>i.Size);
            else s=_items.OrderBy(i=>!i.IsDirectory).ThenBy(i=>i.Name,StringComparer.OrdinalIgnoreCase);
            if(_sortDir==SortDir.Desc)s=s.Reverse();_items=s.ToList();
        }
        void CopyClip(bool cut)
        {
            var sc=new StringCollection(); foreach(int i in _sel)sc.Add(_items[i].FullPath);
            var data=new DataObject(); data.SetFileDropList(sc);
            if(cut){var de=new MemoryStream(4);de.Write(BitConverter.GetBytes((int)DragDropEffects.Move),0,4);data.SetData("Preferred DropEffect",de);}
            Clipboard.SetDataObject(data,true);
        }
        static void CopyDir(string s,string d){Directory.CreateDirectory(d);foreach(var f in Directory.GetFiles(s))File.Copy(f,Path.Combine(d,Path.GetFileName(f)));foreach(var sub in Directory.GetDirectories(s))CopyDir(sub,Path.Combine(d,Path.GetFileName(sub)));}
 
        // -- Inline Rename -------------------------------------------------
        void BeginRename(int idx)
        {
            if(idx<0||idx>=_items.Count)return;
            var it=_items[idx];
            int y=HDR_H+idx*ROW_H-_scrollY;
            _rnBox.ItemIdx=idx;
            _rnBox.Text=it.IsDirectory?it.Name:Path.GetFileNameWithoutExtension(it.Name);
            _rnBox.SetBounds(ICO+6,y,_wN-ICO-10,ROW_H);
            _rnBox.Visible=true; _rnBox.SelectAll(); _rnBox.Focus();
        }
        void CommitRename(int idx,string newBase)
        {
            if(idx<0||idx>=_items.Count){CancelRename();return;}
            var it=_items[idx];
            string newName=it.IsDirectory?newBase:(newBase+Path.GetExtension(it.Name));
            if(string.IsNullOrWhiteSpace(newName)||newName==it.Name){CancelRename();return;}
            _rnBox.Visible=false;
            // Release any cached shell icon so the handle is freed before the move
            it.Icon16=null;
            try
            {
                string dir=Path.GetDirectoryName(it.FullPath)??"";
                string newPath=Path.Combine(dir,newName);
                if(it.IsDirectory) Directory.Move(it.FullPath,newPath);
                else               File.Move(it.FullPath,newPath);
                LoadPath(CurrentPath);
            }
            catch(Exception ex){MessageBox.Show(ex.Message,"Rename Error",MessageBoxButtons.OK,MessageBoxIcon.Error);CancelRename();}
        }
        void CancelRename(){_rnBox.Visible=false;Focus();}
 
        // -- Context Menus -------------------------------------------------
        void BuildMenus()
        {
            _bgMenu=MI.MakeMenu();
            var vs=ViewSub();var ss=SortSub();var gs=GroupSub();var ga=GiveSub();var ns=NewSub();
            _bgMenu.Items.AddRange(new ToolStripItem[]{vs,ss,gs,MI.Item("Refresh","reload",(s,e)=>LoadPath(CurrentPath)),MI.Sep(),MI.Item("Paste","paste",(s,e)=>PasteFromClipboard()),MI.Item("Paste shortcut","paste"),MI.Item("Undo Delete","undo"),MI.Sep(),ga,MI.Sep(),ns,MI.Sep(),MI.Item("Properties","properties")});
            _folderMenu=MI.MakeMenu();var fg=GiveSub();
            _folderMenu.Items.AddRange(new ToolStripItem[]{MI.Item("Open","folder",(s,e)=>OpenSel()),MI.Item("Open in new window","folder"),MI.Item("Pin to Quick access","quick_access"),MI.Item("Take Ownership","properties"),MI.Sep(),fg,MI.Item("Restore","undo"),MI.Sep(),SendSub(),MI.Sep(),MI.Item("Cut","cut",(s,e)=>CutSelected()),MI.Item("Copy","copy",(s,e)=>CopySelected()),MI.Sep(),MI.Item("Create shortcut","shortcut"),MI.Item("Delete","delete",(s,e)=>DeleteSelected()),MI.Item("Rename","rename",(s,e)=>StartRename()),MI.Sep(),MI.Item("Properties","properties")});
            _fileMenu=MI.MakeMenu();var fig=GiveSub();var fiow=OpenWithSub();
            _fileMenu.Items.AddRange(new ToolStripItem[]{MI.Item("Open","file",(s,e)=>OpenSel()),MI.Item("Pin","quick_access"),MI.Item("Edit","rename"),MI.Item("Take Ownership","properties"),fiow,MI.Sep(),fig,MI.Item("Restore previous version","undo"),MI.Sep(),SendSub(),MI.Item("Cut","cut",(s,e)=>CutSelected()),MI.Item("Copy","copy",(s,e)=>CopySelected()),MI.Sep(),MI.Item("Create shortcut","shortcut"),MI.Item("Delete","delete",(s,e)=>DeleteSelected()),MI.Item("Rename","rename",(s,e)=>StartRename()),MI.Sep(),MI.Item("Properties","properties")});
        }
        void OpenSel(){if(_sel.Count==0)return;var it=_items[_sel.First()];if(it.IsDirectory)ItemActivated?.Invoke(it);else try{Process.Start(new ProcessStartInfo(it.FullPath){UseShellExecute=true});}catch{}}
        static ToolStripMenuItem ViewSub(){var s=MI.Sub("View","change_view");foreach(var l in new[]{"Extra Large Icons","Large Icons","Medium Icons","Small Icons","List","Details","Tiles","Content"})s.DropDownItems.Add(MI.Item(l,"change_view"));return s;}
        static ToolStripMenuItem SortSub(){var s=MI.Sub("Sort by","ascending");s.DropDownItems.AddRange(new ToolStripItem[]{MI.Item("Name","organize"),MI.Item("Date modified","organize"),MI.Item("Type","organize"),MI.Item("Size","organize"),MI.Sep(),MI.Item("Ascending","ascending"),MI.Item("Descending","descending"),MI.Sep(),MI.Item("More...","options")});return s;}
        static ToolStripMenuItem GroupSub(){var s=MI.Sub("Group by","organize");s.DropDownItems.AddRange(new ToolStripItem[]{MI.Item("Name","organize"),MI.Item("Date modified","organize"),MI.Item("Type","organize"),MI.Item("Size","organize"),MI.Sep(),MI.Item("Ascending","ascending"),MI.Item("Descending","descending"),MI.Sep(),MI.Item("More...","options")});return s;}
        static ToolStripMenuItem GiveSub(){var s=MI.Sub("Give access to","network");s.DropDownItems.AddRange(new ToolStripItem[]{MI.Item("Remove access","delete"),MI.Item("Homegroup (view)","network"),MI.Item("Homegroup (view and edit)","network"),MI.Sep(),MI.Item("Specific people...","network")});return s;}
        static ToolStripMenuItem NewSub(){var s=MI.Sub("New","new_folder");s.DropDownItems.AddRange(new ToolStripItem[]{MI.Item("Folder","folder"),MI.Item("Shortcut","shortcut"),MI.Sep(),MI.Item("Bitmap image","file"),MI.Item("Contact","file"),MI.Item("Rich Text Format","file"),MI.Item("Text Document","file")});return s;}
        static ToolStripMenuItem SendSub(){var s=MI.Sub("Send to","sendto");s.DropDownItems.AddRange(new ToolStripItem[]{MI.Item("Compressed folder","folder"),MI.Item("Desktop (create shortcut)","desktop"),MI.Item("Mail recipient","file"),MI.Item("Documents","documents")});return s;}
        static ToolStripMenuItem OpenWithSub(){var s=MI.Sub("Open with","openwith");s.DropDownItems.Add(MI.Item("Choose another app...","openwith"));return s;}
    }
 
    // -------------------------------------------------------------------------
    //  Splitter bar
    // -------------------------------------------------------------------------
    class SplitterBar:Control
    {
        bool _d;int _sx,_sw;Control _left;
        public SplitterBar(Control l){_left=l;Width=4;Cursor=Cursors.VSplit;BackColor=Th.PaneSep;Padding=Padding.Empty;MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left){_d=true;_sx=Cursor.Position.X;_sw=_left.Width;Capture=true;}};MouseMove+=(s,e)=>{if(_d){_left.Width=Math.Max(100,_sw+Cursor.Position.X-_sx);Parent?.PerformLayout();}};MouseUp+=(s,e)=>{_d=false;Capture=false;};}
        protected override void OnPaint(PaintEventArgs e)=>e.Graphics.Clear(Th.PaneSep);
    }
 
    // -------------------------------------------------------------------------
    //  Status bar
    // -------------------------------------------------------------------------
    class StatusBar:Panel
    {
        Label _l;
        public StatusBar(){Height=22;Dock=DockStyle.Bottom;BackColor=Th.Bg;Padding=Padding.Empty;_l=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=Th.UiFont,Padding=new Padding(6,0,0,0)};Controls.Add(_l);}
        public new string Text{get=>_l.Text;set=>_l.Text=value;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var p=new Pen(Th.PaneSep))e.Graphics.DrawLine(p,0,0,Width,0);}
    }
 
    // -------------------------------------------------------------------------
    //  MAIN FORM
    // -------------------------------------------------------------------------
    class ExplorerForm:Form
    {
        TopNavBar _nav; CommandBar _cmd; TreePane _tree; ContentPane _content;
        SplitterBar _split; PreviewPane _preview; StatusBar _status; Panel _main;
        List<string> _hist=new List<string>(); int _hi=-1;
 
        public ExplorerForm()
        {
            Text="File Explorer";MinimumSize=new Size(700,450);Size=new Size(1160,700);
            StartPosition=FormStartPosition.CenterScreen;BackColor=Th.Bg;Font=Th.UiFont;
            Padding=Padding.Empty;AutoScaleMode=AutoScaleMode.None;AllowDrop=true;
            // Load Icon1.ico from the same directory as the executable
            string icoPath=Path.Combine(Path.GetDirectoryName(Application.ExecutablePath)??"","Icon1.ico");
            if(File.Exists(icoPath))try{Icon=new Icon(icoPath);}catch{}
            Build();Wire();
            Navigate(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        void Build()
        {
            SuspendLayout();
            _nav=new TopNavBar();_cmd=new CommandBar();_status=new StatusBar();
            _main=new Panel{Dock=DockStyle.Fill,Padding=Padding.Empty};
            _tree=new TreePane{Dock=DockStyle.Left,Width=220};
            _split=new SplitterBar(_tree){Dock=DockStyle.Left};
            _content=new ContentPane{Dock=DockStyle.Fill};
            _preview=new PreviewPane{Visible=false};
            _main.Controls.Add(_content); _main.Controls.Add(_preview);
            _main.Controls.Add(_split); _main.Controls.Add(_tree);
            Controls.Add(_main); Controls.Add(_status); Controls.Add(_cmd); Controls.Add(_nav);
            ResumeLayout(false);
        }
        void Wire()
        {
            _nav.BackClick+=(s,e)=>GoBack(); _nav.ForwardClick+=(s,e)=>GoFwd();
            _nav.UpClick+=(s,e)=>GoUp(); _nav.ReloadClick+=(s,e)=>_content.LoadPath(_content.CurrentPath);
            _nav.Navigate+=p=>Navigate(p);
            _nav.SearchChanged+=q=>_content.SetSearch(q);
            _cmd.NewFolderClick+=(s,e)=>NewFolder();
            _cmd.HelpClick+=(s,e)=>Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/?LinkID=2004439"){UseShellExecute=true});
            _cmd.PreviewClick+=(s,e)=>{_preview.Visible=!_preview.Visible;if(_preview.Visible)_preview.ShowItem(_content.FirstSelected);};
            _cmd.ViewChanged+=vm=>SetStatus($"View: {vm}");
            _cmd.OrgCut+=(s,e)=>_content.CutSelected(); _cmd.OrgCopy+=(s,e)=>_content.CopySelected();
            _cmd.OrgPaste+=(s,e)=>_content.PasteFromClipboard(); _cmd.OrgSelectAll+=(s,e)=>_content.SelectAll();
            _cmd.OrgDelete+=(s,e)=>_content.DeleteSelected(); _cmd.OrgRename+=(s,e)=>_content.StartRename();
            _cmd.OrgProps+=(s,e)=>ShowProps(); _cmd.OrgClose+=(s,e)=>Close();
            _tree.NodeSelected+=n=>{if(n.Path!=null&&Directory.Exists(n.Path))Navigate(n.Path);else SetStatus(n.Label);};
            _tree.DropFiles+=(srcs,dest)=>PerformDrop(srcs.Split('\n'),dest);
            _content.ItemActivated+=it=>{if(it.IsDirectory)Navigate(it.FullPath);else try{Process.Start(new ProcessStartInfo(it.FullPath){UseShellExecute=true});}catch{}};
            _content.SelectionChanged+=it=>{if(_preview.Visible)_preview.ShowItem(it);};
            // Form-level DnD (drag from desktop etc.)
            DragEnter+=(s,e)=>{e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;};
            DragDrop+=(s,e)=>{if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;var files=(string[])e.Data.GetData(DataFormats.FileDrop);var folders=files.Where(Directory.Exists).ToArray();if(folders.Length>0)Navigate(folders[0]);else if(files.Length>0)Navigate(Path.GetDirectoryName(files[0])??"");};
        }
 
        void Navigate(string path)
        {
            if(_hi<_hist.Count-1)_hist.RemoveRange(_hi+1,_hist.Count-_hi-1);
            _hist.Add(path);_hi=_hist.Count-1;Apply(path);
        }
        void Apply(string path)
        {
            _nav.CurrentPath=path;_nav.BackEnabled=_hi>0;_nav.ForwardEnabled=_hi<_hist.Count-1;
            if(Directory.Exists(path)){_content.LoadPath(path);_tree.SelectPath(path);Text=$"{Path.GetFileName(path)??path} – File Explorer";}
            else Text=$"{path} – File Explorer";
            if(_preview.Visible)_preview.ShowItem(_content.FirstSelected);
            SetStatus(null);
        }
        void GoBack(){if(_hi>0){_hi--;Apply(_hist[_hi]);}}
        void GoFwd(){if(_hi<_hist.Count-1){_hi++;Apply(_hist[_hi]);}}
        void GoUp(){try{string up=Directory.GetParent(_nav.CurrentPath)?.FullName;if(up!=null)Navigate(up);}catch{}}
        void NewFolder()
        {
            string cur=_content.CurrentPath; if(!Directory.Exists(cur))return;
            string p=Path.Combine(cur,"New folder"); int i=2; while(Directory.Exists(p))p=Path.Combine(cur,$"New folder ({i++})");
            try{Directory.CreateDirectory(p);_content.LoadPath(cur);SetStatus($"Created: {Path.GetFileName(p)}");}catch(Exception ex){MessageBox.Show(ex.Message,"Error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
        void ShowProps(){string p=_content.CurrentPath;if(Directory.Exists(p))MessageBox.Show($"Path: {p}","Properties",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        void PerformDrop(string[] srcs,string dest)
        {
            foreach(var src in srcs)
                try{string name=Path.GetFileName(src);string d=Path.Combine(dest,name);if(File.Exists(src)){if(!d.Equals(src,StringComparison.OrdinalIgnoreCase))File.Move(src,d);}else if(Directory.Exists(src)){if(!d.Equals(src,StringComparison.OrdinalIgnoreCase)){Directory.Move(src,d);}}}
                catch(Exception ex){MessageBox.Show(ex.Message,"Move Error",MessageBoxButtons.OK,MessageBoxIcon.Error);}
            _content.LoadPath(_content.CurrentPath);
        }
        void SetStatus(string msg)
        {
            if(msg!=null){_status.Text=msg;return;}
            string path=_content.CurrentPath; if(!Directory.Exists(path)){_status.Text=path;return;}
            try{int d=Directory.GetDirectories(path).Length,f=Directory.GetFiles(path).Length;_status.Text=$"{d+f} items";}catch{_status.Text=path;}
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if(e.Alt&&e.KeyCode==Keys.Left)GoBack();
            else if(e.Alt&&e.KeyCode==Keys.Right)GoFwd();
            else if(e.Alt&&e.KeyCode==Keys.Up)GoUp();
            else if(e.KeyCode==Keys.F5)_content.LoadPath(_content.CurrentPath);
            else if(e.Control&&e.KeyCode==Keys.A)_content.SelectAll();
            else if(e.Control&&e.KeyCode==Keys.C)_content.CopySelected();
            else if(e.Control&&e.KeyCode==Keys.X)_content.CutSelected();
            else if(e.Control&&e.KeyCode==Keys.V)_content.PasteFromClipboard();
            else if(e.KeyCode==Keys.Delete)_content.DeleteSelected();
            else if(e.KeyCode==Keys.F2)_content.StartRename();
        }
    }
}
 
