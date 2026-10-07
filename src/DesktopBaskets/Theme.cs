using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopBaskets;

internal static class Theme
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int PreferredAppMode(int mode);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void FlushMenuThemes();
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr module,IntPtr ordinal);
    public static bool NativeDarkMenus {get;private set;}
    public static void ConfigureNativeMenus()
    {
        // Windows 10 1903+ only: the ordinal had a different signature on 1809.
        // PowerToys ZoomIt uses these same private UXTheme exports. Resolve them
        // defensively and keep the native menu functional when unavailable.
        var version=Environment.OSVersion.Version;
        if(version.Major<10||version.Build<18362||SystemInformation.HighContrast)return;
        var module=GetModuleHandle("uxtheme.dll");if(module==IntPtr.Zero)return;
        var mode=GetProcAddress(module,new IntPtr(135));var flush=GetProcAddress(module,new IntPtr(136));
        if(mode==IntPtr.Zero||flush==IntPtr.Zero)return;
        Marshal.GetDelegateForFunctionPointer<PreferredAppMode>(mode)(2);
        Marshal.GetDelegateForFunctionPointer<FlushMenuThemes>(flush)();NativeDarkMenus=true;
    }
    public static readonly Color Background=Color.FromArgb(25,25,25);
    public static readonly Color Panel=Color.FromArgb(24,26,27);
    public static readonly Color Raised=Color.FromArgb(53,55,60);
    public static readonly Color Text=Color.FromArgb(235,239,242);
    public static readonly Color Muted=Color.FromArgb(169,180,187);
    public static readonly Color Accent=Color.FromArgb(255,250,0);
    public static readonly Color Line=Color.FromArgb(100,105,107);
    public static readonly Color FrameLine=Color.FromArgb(137,143,142);
    public static readonly Color InnerFrameLine=Color.FromArgb(106,112,114);
    public static readonly Color GridDot=Color.FromArgb(39,42,43);
    public static readonly Color GlassTop=Color.FromArgb(32,38,44);
    public static readonly Color GlassBottom=Color.FromArgb(14,18,23);
    public const int MinimumMetadataPixels=12;
    public static readonly Icon AppIcon=Icon.ExtractAssociatedIcon(Path.Combine(AppContext.BaseDirectory,"DesktopBaskets.exe"))??SystemIcons.Application;
    static readonly Dictionary<(float,FontStyle),Font> Fonts=new();
    public static Font Body(float size=10, FontStyle style=FontStyle.Regular)
    {
        if(!Fonts.TryGetValue((size,style),out var font)) Fonts[(size,style)]=font=new Font("Microsoft JhengHei UI",size,style);
        return font;
    }
    public static Button Button(string text, EventHandler action, bool primary=false)
    {
        var b=new Button { Text=text, AutoSize=true, Height=36, MinimumSize=new Size(100,36),
            Padding=new Padding(12,3,12,3), Margin=new Padding(0,0,8,8), FlatStyle=FlatStyle.Flat,
            BackColor=primary?Accent:Raised, ForeColor=primary?Background:Text, Cursor=Cursors.Hand };
        b.FlatAppearance.BorderColor=Line;
        b.Click+=action;
        return b;
    }
    public static void Form(Form form)
    {
        form.BackColor=Background; form.ForeColor=Text; form.Font=Body();
        form.StartPosition=FormStartPosition.CenterScreen;
    }
    public static Form? ErrorOwner {get;set;}
    public static void Error(Exception error)
    {
        // Always use our own top-level owner. A desktop child must never make
        // Explorer's root window the implicit owner of a modal error message.
        if(ErrorOwner is Form manager&&!manager.IsDisposed)
            MessageBox.Show(manager,error.Message,"Desktop Baskets",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        else
        {
            using var owner=new Form{ShowInTaskbar=false};_ = owner.Handle;
            MessageBox.Show(owner,error.Message,"Desktop Baskets",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    public static void Open(string path)
    {
        if(!Store.Exists(path)) throw new FileNotFoundException("檔案已被其他程式移動或刪除。請重新加入正確位置，或移除此分類項目。",path);
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
    public static void Reveal(string path)
    {
        var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};
        info.Arguments="/select,\""+path+"\""; Process.Start(info);
    }
    public static void Try(Action action) { try { action(); } catch(Exception ex) { Error(ex); } }
}

internal sealed class BasketDialog : Form
{
    readonly TextBox name=new();
    readonly NumericUpDown x=new(), y=new(), width=new(), height=new(), opacity=new();
    readonly CheckBox locked=new(){Text="鎖定位置與大小",AutoSize=true};
    public Basket Result { get; }
    public BasketDialog(Basket basket)
    {
        Theme.Form(this); Text="分類籃設定"; ClientSize=new Size(440,390); FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false; MinimizeBox=false;
        Result=basket;
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=2,RowCount=8};
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
        name.Text=basket.Name; name.MaxLength=40; name.Dock=DockStyle.Fill;
        Add("分類名稱",name,0);
        Number(x,-32768,32767,basket.X); Number(y,-32768,32767,basket.Y);
        Number(width,Grid.MinColumns,30,basket.Columns); Number(height,Grid.MinRows,25,basket.Rows);
        Add("水平位置（px）",x,1); Add("垂直位置（px）",y,2); Add("橫向格數",width,3); Add("直向格數",height,4);
        locked.Checked=basket.Locked; table.Controls.Add(locked,1,5);
        Number(opacity,40,100,basket.OpacityPercent);Add("玻璃底色濃度（%）",opacity,6);
        void UpdateLock(){x.Enabled=y.Enabled=width.Enabled=height.Enabled=!locked.Checked;}
        locked.CheckedChanged+=(_,_)=>UpdateLock();UpdateLock();
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
        buttons.Controls.Add(Theme.Button("儲存",(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(name.Text)) { name.Focus(); return; }
            basket.Name=name.Text.Trim();
            if(!basket.Locked||!locked.Checked){basket.X=(int)x.Value;basket.Y=(int)y.Value;basket.Width=(int)width.Value*Grid.CellWidth+Grid.Side;basket.Height=(int)height.Value*Grid.CellHeight+Grid.VerticalFor(basket.Width);}
            basket.Locked=locked.Checked;
            basket.OpacityPercent=(int)opacity.Value;
            DialogResult=DialogResult.OK;
        },true));
        var cancel=Theme.Button("取消",(_,_)=>DialogResult=DialogResult.Cancel);
        buttons.Controls.Add(cancel); CancelButton=cancel; table.Controls.Add(buttons,0,7); table.SetColumnSpan(buttons,2);
        Controls.Add(table);
        void Add(string label,Control control,int row)
        {
            table.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);
            table.Controls.Add(control,1,row);
        }
        static void Number(NumericUpDown n,int min,int max,int value) { n.Minimum=min; n.Maximum=max; n.Value=MathEx.Clamp(value,min,max); n.Dock=DockStyle.Fill; }
    }
}

internal sealed class DesktopPicker : Form
{
    readonly CheckedListBox files=new(){Dock=DockStyle.Fill,CheckOnClick=true};
    readonly List<string> paths=new();
    public string[] SelectedPaths => files.CheckedIndices.Cast<int>().Select(i=>paths[i]).ToArray();
    public DesktopPicker()
    {
        Theme.Form(this); Text="選擇桌面檔案"; ClientSize=new Size(620,490);
        files.BackColor=Theme.Panel; files.ForeColor=Theme.Text; files.BorderStyle=BorderStyle.FixedSingle;
        var header=new Label{Text="純視覺分類：原始路徑不變，桌面圖示會顯示在籃子中。",Dock=DockStyle.Top,Height=56,Padding=new Padding(12)};
        foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Distinct())
        {
            if(!Directory.Exists(root)) continue;
            foreach(var path in Directory.EnumerateFileSystemEntries(root).OrderBy(System.IO.Path.GetFileName))
            {
                if((File.GetAttributes(path)&(FileAttributes.Hidden|FileAttributes.System))!=0) continue;
                paths.Add(path); files.Items.Add(System.IO.Path.GetFileName(path)+(root==Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)?"  [公共桌面]":""));
            }
        }
        var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,Padding=new Padding(12),FlowDirection=FlowDirection.TopDown};
        var actions=new FlowLayoutPanel{AutoSize=true};
        actions.Controls.Add(Theme.Button("加入分類",(_,_)=>DialogResult=DialogResult.OK,true));
        actions.Controls.Add(Theme.Button("全選",(_,_)=>{for(int i=0;i<files.Items.Count;i++)files.SetItemChecked(i,true);}));
        var cancel=Theme.Button("取消",(_,_)=>DialogResult=DialogResult.Cancel); actions.Controls.Add(cancel); CancelButton=cancel;
        footer.Controls.Add(actions); Controls.Add(files); Controls.Add(footer); Controls.Add(header);
    }
}
