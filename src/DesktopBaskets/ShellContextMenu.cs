using System.Runtime.InteropServices;
using System.Text;

namespace DesktopBaskets;

// Menus and verbs come from the actual file's Windows Shell handlers.
internal sealed class ShellContextMenu : IDisposable
{
    object? folder;
    IFileContextMenu? menu;
    readonly List<IntPtr> pidls=new();
    IntPtr popup;
    public int SelectionCount {get;private set;}
    readonly MenuHost host;
    public bool OwnerWasForeground {get;private set;}
    public long OpenMilliseconds {get;private set;}
    public string LastCommandVerb {get;private set;}="";
    public uint MenuThreadId {get;private set;}
    public uint CommandOwnerThreadId {get;private set;}
    public string[] Lifecycle=>host.Lifecycle.ToArray();
    internal Action? BeforeTrackForVerification;
    internal static void ShowOnUiThread(string path,Point point,IntPtr owner,Keys modifiers,Action<object,Exception?> completed)
        =>ShowOnUiThread(new[]{path},point,owner,modifiers,completed);
    internal static void ShowOnUiThread(IEnumerable<string> paths,Point point,IntPtr owner,Keys modifiers,Action<object,Exception?> completed)
    {
        // The application's persistent STA continues pumping Shell property
        // pages after InvokeCommand returns. A disposable worker cannot do so.
        object observation;Exception? error=null;
        try
        {
            using var menu=new ShellContextMenu(paths,owner,(modifiers&Keys.Shift)!=0);
            menu.Show(point,owner,modifiers,true);
            observation=new{menu.OwnerWasForeground,menu.OpenMilliseconds,menu.LastCommandVerb,MenuItems=menu.Count,
                menu.MenuThreadId,menu.CommandOwnerThreadId,menu.Lifecycle,menu.SelectionCount,PersistentUiThread=true,Theme.NativeDarkMenus};
        }
        catch(Exception ex){error=ex;observation=new{Error=ex.ToString()};}
        completed(observation,error);
    }
    internal static Thread StartIsolated(string path,Point point,IntPtr owner,Keys modifiers,Action<object,Exception?> completed,Action<ShellContextMenu>? prepare=null)
    {
        // SetParent attaches the basket UI's input queue to Explorer. A new STA
        // owns the Shell COM objects and popup HWND without any cross-thread
        // parent/owner relationship, so Explorer focus changes cannot cancel it.
        var thread=new Thread(()=>
        {
            object observation;Exception? error=null;
            try
            {
                using var menu=new ShellContextMenu(path,owner,(modifiers&Keys.Shift)!=0);
                prepare?.Invoke(menu);menu.Show(point,owner,modifiers,true);
                observation=new{menu.OwnerWasForeground,menu.OpenMilliseconds,menu.LastCommandVerb,MenuItems=menu.Count,
                    menu.MenuThreadId,menu.CommandOwnerThreadId,menu.Lifecycle};
            }
            catch(Exception ex){error=ex;observation=new{Error=ex.ToString()};}
            completed(observation,error);
        }){IsBackground=true,Name="Desktop Baskets native file menu"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return thread;
    }
    public ShellContextMenu(string path,IntPtr owner,bool extended=false)
        :this(new[]{path},owner,extended){}
    public ShellContextMenu(IEnumerable<string> paths,IntPtr owner,bool extended=false)
    {
        host=new MenuHost();
        try
        {
            var files=paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(files.Length==0)throw new ArgumentException("No selected Shell items.",nameof(paths));
            foreach(var path in files){Check(SHParseDisplayName(path,IntPtr.Zero,out var pidl,0,out _),"ParseDisplayName");pidls.Add(pidl);}
            SelectionCount=files.Length;
            var folderId=typeof(IFileShellFolder).GUID;
            IntPtr[] children;
            if(files.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1)
            {
                Check(SHBindToParent(pidls[0],ref folderId,out folder,out _),"BindToParent");
                children=pidls.Select(ILFindLastID).ToArray();
            }
            else
            {
                // The Desktop namespace can resolve absolute PIDLs, including a
                // visual selection spanning the user's and public Desktop folders.
                Check(SHGetDesktopFolder(out folder),"GetDesktopFolder");children=pidls.ToArray();
            }
            var menuId=typeof(IFileContextMenu).GUID;
            Check(((IFileShellFolder)folder).GetUIObjectOf(owner,(uint)children.Length,children,ref menuId,IntPtr.Zero,out var value),"GetUIObjectOf");
            menu=(IFileContextMenu)value;host.Menu=menu;
            popup=CreatePopupMenu();if(popup==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
            Check(menu.QueryContextMenu(popup,0,1,0x7fff,extended?0x100u:0),"QueryContextMenu");
        }
        catch{Dispose();throw;}
    }
    public int Count=>GetMenuItemCount(popup);
    static void Check(int hr,string operation){if(hr<0)throw new COMException("Windows Shell "+operation+" failed: 0x"+hr.ToString("X8"),hr);}
    public string[] Verbs()
    {
        var verbs=new List<string>();
        for(uint i=0;i<Count;i++)
        {
            uint id=GetMenuItemID(popup,(int)i);if(id==uint.MaxValue||id==0)continue;
            var text=new StringBuilder(260);
            if(menu!.GetCommandString(new UIntPtr(id-1),4,IntPtr.Zero,text,260)>=0&&text.Length>0)verbs.Add(text.ToString());
        }
        return verbs.ToArray();
    }
    public void Show(Point point,IntPtr commandOwner,Keys modifiers,bool independentOwner=false)
    {
        using var dpi=new Native.PhysicalDpiScope();
        var previous=GetForegroundWindow();
        var root=Native.GetAncestor(Native.WindowFromPoint(new Native.POINT(point)),2);
        // Reuse an existing top-level WinForms owner when the click originates
        // there. Explorer-hosted baskets use our independent transparent owner;
        // never subclass or make Explorer the owner of a modal command dialog.
        var form=independentOwner?null:Control.FromHandle(root) as Form;
        host.Create(point,form!=null&&form.TopLevel&&form.Visible?root:IntPtr.Zero);
        MenuThreadId=GetWindowThreadProcessId(host.Handle,out _);
        CommandOwnerThreadId=GetWindowThreadProcessId(commandOwner,out _);
        SetForegroundWindow(host.Handle);OwnerWasForeground=GetForegroundWindow()==host.Handle;
        try
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            BeforeTrackForVerification?.Invoke();
            uint command=TrackPopupMenuEx(popup,0x100|0x2,point.X,point.Y,host.Handle,IntPtr.Zero);
            OpenMilliseconds=watch.ElapsedMilliseconds;
            PostMessage(host.Handle,0,IntPtr.Zero,IntPtr.Zero);
            if(command==0)return;
            var verb=new StringBuilder(260);menu!.GetCommandString(new UIntPtr(command-1),4,IntPtr.Zero,verb,260);LastCommandVerb=verb.ToString();
            Invoke(command,form!=null&&form.TopLevel&&form.Visible?root:commandOwner,point,modifiers);
        }
        finally
        {
            bool restore=GetForegroundWindow()==host.Handle;host.Close();
            if(restore&&previous!=IntPtr.Zero&&Native.IsWindowVisible(previous))SetForegroundWindow(previous);
        }
    }
    internal void ExecuteForVerification(string verb,IntPtr owner,bool noUi=false)
    {
        for(int i=0;i<Count;i++)
        {
            uint id=GetMenuItemID(popup,i);if(id==uint.MaxValue||id==0)continue;
            var name=new StringBuilder(260);
            if(menu!.GetCommandString(new UIntPtr(id-1),4,IntPtr.Zero,name,260)>=0&&name.ToString().Equals(verb,StringComparison.OrdinalIgnoreCase))
            {Invoke(id,owner,new Point(40,40),Keys.None,noUi);return;}
        }
        throw new InvalidOperationException("Native Shell verb missing: "+verb);
    }
    void Invoke(uint command,IntPtr owner,Point point,Keys modifiers,bool noUi=false)
    {
        var info=new InvokeInfo{Size=(uint)Marshal.SizeOf<InvokeInfo>(),Mask=0x4000|0x20000000,
            Owner=owner,Verb=new IntPtr(command-1),VerbW=new IntPtr(command-1),Show=1,Point=new Native.POINT(point)};
        if((modifiers&Keys.Shift)!=0)info.Mask|=0x10000000;
        if((modifiers&Keys.Control)!=0)info.Mask|=0x40000000;
        if(noUi)info.Mask|=0x400;
        Native.Check(menu!.InvokeCommand(ref info));
    }
    public void Dispose()
    {
        host.Close();host.Menu=null;
        if(popup!=IntPtr.Zero){DestroyMenu(popup);popup=IntPtr.Zero;}
        if(menu!=null){Marshal.ReleaseComObject(menu);menu=null;}
        if(folder!=null){Marshal.ReleaseComObject(folder);folder=null;}
        foreach(var pidl in pidls)Marshal.FreeCoTaskMem(pidl);pidls.Clear();
    }
    sealed class MenuHost : NativeWindow
    {
        public IFileContextMenu? Menu;
        public readonly List<string> Lifecycle=new();
        bool borrowed;
        public void Create(Point p,IntPtr existing)
        {
            if(existing!=IntPtr.Zero){AssignHandle(existing);borrowed=true;return;}
            CreateHandle(new CreateParams{Caption="Desktop Baskets Shell menu",X=p.X,Y=p.Y,Width=1,Height=1,Style=unchecked((int)0x80000000),ExStyle=0x80080});
            // A hidden HWND cannot reliably own a foreground popup. Keep a real
            // visible/activatable tool window, with a fully transparent surface.
            if(!Native.SetLayeredWindowAttributes(Handle,0,0,2))throw new System.ComponentModel.Win32Exception();
            ShowWindow(Handle,4);
        }
        public void Close(){if(Handle!=IntPtr.Zero){if(borrowed)ReleaseHandle();else DestroyHandle();}borrowed=false;}
        protected override void WndProc(ref Message m)
        {
            if(Lifecycle.Count<48&&m.Msg is 0x6 or 0x1c or 0x8 or 0x1f or 0x211 or 0x212)
                Lifecycle.Add($"{m.Msg:X4}:{m.WParam.ToInt64():X}:foreground={GetForegroundWindow().ToInt64():X}");
            if(m.Msg is 0x117 or 0x2b or 0x2c or 0x120)
            {
                // Only forward menu owner-draw messages; controls keep their own messages.
                if((m.Msg is 0x2b or 0x2c)&&m.WParam!=IntPtr.Zero){base.WndProc(ref m);return;}
                if(Menu is IFileContextMenu3 three&&three.HandleMenuMsg2((uint)m.Msg,m.WParam,m.LParam,out var result)==0){m.Result=result;return;}
                if(Menu is IFileContextMenu2 two&&two.HandleMenuMsg((uint)m.Msg,m.WParam,m.LParam)==0){m.Result=IntPtr.Zero;return;}
            }
            base.WndProc(ref m);
        }
    }
    [StructLayout(LayoutKind.Sequential)] internal struct InvokeInfo
    {
        public uint Size,Mask;public IntPtr Owner,Verb,Parameters,Directory;public int Show;public uint HotKey;
        public IntPtr Icon,Title,VerbW,ParametersW,DirectoryW,TitleW;public Native.POINT Point;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHParseDisplayName(string name,IntPtr context,out IntPtr pidl,uint attrs,out uint actual);
    [DllImport("shell32.dll")] static extern int SHBindToParent(IntPtr pidl,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out object folder,out IntPtr child);
    [DllImport("shell32.dll")] static extern IntPtr ILFindLastID(IntPtr pidl);
    [DllImport("shell32.dll")] static extern int SHGetDesktopFolder([MarshalAs(UnmanagedType.Interface)] out object folder);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr menu,int index);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr owner,IntPtr parameters);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
}

[ComImport,Guid("000214e6-0000-0000-c000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileShellFolder
{
    void ParseDisplayName();void EnumObjects();void BindToObject();void BindToStorage();void CompareIDs();void CreateViewObject();void GetAttributesOf();
    [PreserveSig] int GetUIObjectOf(IntPtr owner,uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)] IntPtr[] children,ref Guid iid,IntPtr reserved,[MarshalAs(UnmanagedType.Interface)] out object result);
}
[ComImport,Guid("000214e4-0000-0000-c000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileContextMenu
{
    [PreserveSig] int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
    [PreserveSig] int InvokeCommand(ref ShellContextMenu.InvokeInfo info);
    [PreserveSig] int GetCommandString(UIntPtr command,uint flags,IntPtr reserved,[MarshalAs(UnmanagedType.LPWStr)] StringBuilder name,uint size);
}
[ComImport,Guid("000214f4-0000-0000-c000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileContextMenu2
{
    void QueryContextMenu();void InvokeCommand();void GetCommandString();
    [PreserveSig] int HandleMenuMsg(uint message,IntPtr wParam,IntPtr lParam);
}
[ComImport,Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileContextMenu3
{
    void QueryContextMenu();void InvokeCommand();void GetCommandString();void HandleMenuMsg();
    [PreserveSig] int HandleMenuMsg2(uint message,IntPtr wParam,IntPtr lParam,out IntPtr result);
}
