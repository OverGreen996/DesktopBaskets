using System.Runtime.InteropServices;
using System.Text;

namespace DesktopBaskets;

// Menus and verbs come from the actual file's Windows Shell handlers.
internal sealed class ShellContextMenu : IDisposable
{
    object? folder;
    IFileContextMenu? menu;
    IntPtr pidl, popup;
    readonly MenuHost host;
    public ShellContextMenu(string path,IntPtr owner,bool extended=false)
    {
        host=new MenuHost();
        try
        {
            Check(SHParseDisplayName(Path.GetFullPath(path),IntPtr.Zero,out pidl,0,out _),"ParseDisplayName");
            var folderId=typeof(IFileShellFolder).GUID;
            Check(SHBindToParent(pidl,ref folderId,out folder,out var child),"BindToParent");
            var menuId=typeof(IFileContextMenu).GUID;
            Check(((IFileShellFolder)folder).GetUIObjectOf(owner,1,new[]{child},ref menuId,IntPtr.Zero,out var value),"GetUIObjectOf");
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
    public void Show(Point point,IntPtr commandOwner,Keys modifiers)
    {
        using var dpi=new Native.PhysicalDpiScope();
        host.Create(point);SetForegroundWindow(host.Handle);
        try
        {
            uint command=TrackPopupMenuEx(popup,0x100|0x2,point.X,point.Y,host.Handle,IntPtr.Zero);
            PostMessage(host.Handle,0,IntPtr.Zero,IntPtr.Zero);
            if(command==0)return;
            Invoke(command,commandOwner,point,modifiers);
        }
        finally{host.Close();}
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
        if(pidl!=IntPtr.Zero){Marshal.FreeCoTaskMem(pidl);pidl=IntPtr.Zero;}
    }
    sealed class MenuHost : NativeWindow
    {
        public IFileContextMenu? Menu;
        public void Create(Point p)=>CreateHandle(new CreateParams{Caption="Desktop Baskets Shell menu",X=p.X,Y=p.Y,Width=1,Height=1,Style=unchecked((int)0x80000000),ExStyle=0x80});
        public void Close(){if(Handle!=IntPtr.Zero)DestroyHandle();}
        protected override void WndProc(ref Message m)
        {
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
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr menu,int index);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr owner,IntPtr parameters);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
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
