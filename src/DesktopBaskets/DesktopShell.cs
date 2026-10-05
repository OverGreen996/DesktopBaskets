using System.Runtime.InteropServices;

namespace DesktopBaskets;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(Point p) { X=p.X; Y=p.Y; } public Point Point => new(X,Y); }
    [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr hwnd,bool enable);
    [DllImport("user32.dll")] public static extern bool InvalidateRect(IntPtr hwnd,IntPtr rect,bool erase);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr hwnd,out uint key,out byte alpha,out uint flags);
    [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr parent,POINT point,uint flags);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("shell32.dll")] public static extern uint ILGetSize(IntPtr pidl);
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] public static extern int SHGetNameFromIDList(IntPtr pidl, uint type, out IntPtr text);
    public static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
}

[ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IServiceProviderNative
{
    [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
}
[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IShellWindowsNative
{
    void Count(); void Item(); void NewEnum(); void Register(); void RegisterPending(); void Revoke(); void OnNavigate(); void OnActivated();
    [PreserveSig] int FindWindowSW([In,MarshalAs(UnmanagedType.Struct)] ref object location,
        [In,MarshalAs(UnmanagedType.Struct)] ref object root,int windowClass,out int hwnd,int options,
        [MarshalAs(UnmanagedType.IDispatch)] out object desktop);
}
[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellBrowser
{
    [PreserveSig] int GetWindow(out IntPtr hwnd);
    void ContextSensitiveHelp(); void InsertMenusSB(); void SetMenuSB(); void RemoveMenusSB(); void SetStatusTextSB();
    void EnableModelessSB(); void TranslateAcceleratorSB(); void BrowseObject(); void GetViewStateStream();
    void GetControlWindow(); void SendControlMsg();
    [PreserveSig] int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
}
[ComImport, Guid("000214e3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellViewNative
{
    [PreserveSig] int GetWindow(out IntPtr hwnd);
    void ContextSensitiveHelp();
    void TranslateAccelerator(); void EnableModeless(); void UIActivate();
    [PreserveSig] int Refresh();
}
[ComImport, Guid("1af3a467-214f-4298-908e-06b03e0b39f9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFolderView2
{
    [PreserveSig] int GetCurrentViewMode(out uint mode);
    void SetCurrentViewMode(); void GetFolder();
    [PreserveSig] int Item(int index, out IntPtr pidl);
    [PreserveSig] int ItemCount(uint flags, out int count);
    void Items(); void GetSelectionMarkedItem(); void GetFocusedItem();
    [PreserveSig] int GetItemPosition(IntPtr pidl, out Native.POINT point);
    [PreserveSig] int GetSpacing(ref Native.POINT point);
    [PreserveSig] int GetDefaultSpacing(out Native.POINT point);
    [PreserveSig] int GetAutoArrange();
    [PreserveSig] int SelectItem(int index,uint flags);
    [PreserveSig] int SelectAndPositionItems(uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IntPtr[] pidls,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] Native.POINT[] points, uint flags);
    void SetGroupBy(); void GetGroupBy(); void SetViewProperty(); void GetViewProperty();
    void SetTileViewProperties(); void SetExtendedTileViewProperties(); void SetText();
    [PreserveSig] int SetCurrentFolderFlags(uint mask, uint flags);
    [PreserveSig] int GetCurrentFolderFlags(out uint flags);
    void GetSortColumnCount(); void SetSortColumns(); void GetSortColumns();
    [PreserveSig] int GetItem(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
}
[ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(); void GetParent();
    [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr text);
}

internal sealed class ShellIcon : IDisposable
{
    public string Key { get; }
    public string Name { get; }
    public IntPtr Pidl { get; }
    public Point Position { get; }
    public ShellIcon(string key, string name, IntPtr pidl, Point point) { Key=key; Name=name; Pidl=pidl; Position=point; }
    public void Dispose() { Marshal.FreeCoTaskMem(Pidl); }
}

internal sealed class DesktopShell : IDisposable
{
    readonly object windows;
    readonly object desktop;
    readonly object browser;
    readonly object shellView;
    readonly IFolderView2 view;
    public IntPtr Host { get; }
    public IntPtr List { get; }
    public Point Origin { get { var p=new Native.POINT(); Native.ClientToScreen(List, ref p); return p.Point; } }
    public Size Spacing { get { var p=new Native.POINT(); Native.Check(view.GetSpacing(ref p)); return new Size(Math.Max(48,p.X), Math.Max(64,p.Y)); } }
    public uint Flags { get { Native.Check(view.GetCurrentFolderFlags(out uint flags)); return flags; } }
    public bool Alive => Native.IsWindow(Host) && Native.IsWindow(List);
    public bool IconsVisible => Native.IsWindowVisible(List) && (Flags & 0x1000) == 0;
    public bool Interactive => Native.IsWindowEnabled(Native.GetAncestor(Host,2))&&Native.IsWindowEnabled(Host)&&Native.IsWindowEnabled(List);
    public void RepairInput(){Native.EnableWindow(Native.GetAncestor(Host,2),true);Native.EnableWindow(Host,true);Native.EnableWindow(List,true);}
    public DesktopShell()
    {
        windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), true)!)!;
        // SWC_DESKTOP / SWFO_NEEDDISPATCH. By-ref variants are required by IShellWindows.
        object location=0, root=0;
        Native.Check(((IShellWindowsNative)windows).FindWindowSW(ref location,ref root,8,out _,1,out desktop));
        var provider=(IServiceProviderNative)desktop;
        Guid service=new("4C96BE40-915C-11CF-99D3-00AA004AE837");
        Guid iid=typeof(IShellBrowser).GUID;
        Native.Check(provider.QueryService(ref service, ref iid, out browser));
        Native.Check(((IShellBrowser)browser).QueryActiveShellView(out shellView));
        view=(IFolderView2)shellView;
        Native.Check(((IShellViewNative)shellView).GetWindow(out IntPtr host));
        Host=host;
        List=Native.FindWindowEx(host, IntPtr.Zero, "SysListView32", null);
        if (List == IntPtr.Zero) throw new InvalidOperationException("此 Explorer 版本沒有可支援的桌面圖示控制項；整理框不會覆蓋桌面。");
    }
    public List<ShellIcon> ReadIcons()
    {
        Native.Check(view.ItemCount(2, out int count));
        var result=new List<ShellIcon>();
        try
        {
            Guid iid=typeof(IShellItem).GUID;
            for (int i=0;i<count;i++)
            {
                Native.Check(view.Item(i,out IntPtr pidl));
                try
                {
                    Native.Check(view.GetItemPosition(pidl,out var point));
                    Native.Check(view.GetItem(i,ref iid,out object item));
                    string key, name;
                    try
                    {
                        var shellItem=(IShellItem)item;
                        Native.Check(shellItem.GetDisplayName(0x80028000,out var text));
                        try { key=Marshal.PtrToStringUni(text)!; } finally { Marshal.FreeCoTaskMem(text); }
                        Native.Check(shellItem.GetDisplayName(0,out text));
                        try { name=Marshal.PtrToStringUni(text)!; } finally { Marshal.FreeCoTaskMem(text); }
                    }
                    finally { Marshal.ReleaseComObject(item); }
                    result.Add(new ShellIcon(key,name,pidl,point.Point)); pidl=IntPtr.Zero;
                }
                finally { if(pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
            }
            return result;
        }
        catch { foreach(var icon in result) icon.Dispose(); throw; }
    }
    public void SetAutoArrange(bool enabled) => Native.Check(view.SetCurrentFolderFlags(1, enabled ? 1u : 0u));
    public void SetManualPositions() => Native.Check(view.SetCurrentFolderFlags(5,0));
    public void SetLayoutFlags(bool auto,bool snap) => Native.Check(view.SetCurrentFolderFlags(5,(auto?1u:0u)|(snap?4u:0u)));
    public void RefreshView()=>Native.Check(((IShellViewNative)shellView).Refresh());
    public bool IsSelected(int index)=>(Native.SendMessage(List,0x102C,new IntPtr(index),new IntPtr(2)).ToInt64()&2)!=0; // LVM_GETITEMSTATE / LVIS_SELECTED
    internal void SelectForVerification(int index)=>Native.Check(view.SelectItem(index,0x40000001));
    public void CleanAssignedSelection(IReadOnlyList<ShellIcon> icons,HashSet<string> assigned)
    {
        bool hasHidden=false;
        for(int i=0;i<icons.Count;i++)if(assigned.Contains(icons[i].Key))
        {
            hasHidden=true;
            if(IsSelected(i))Native.Check(view.SelectItem(i,0x40000000)); // deselect, do not take desktop focus
        }
        // One repaint after an Explorer event removes stale drag/selection pixels.
        // This does not refresh the folder, run a timer, or change file attributes.
        if(hasHidden)Native.InvalidateRect(List,IntPtr.Zero,true);
    }
    public void Position(IEnumerable<ShellIcon> icons, IReadOnlyDictionary<string,Point> positions)
    {
        var selected=icons.Where(i=>positions.TryGetValue(i.Key,out var target)&&target!=i.Position).ToArray();
        if(selected.Length==0) return;
        Native.Check(view.SelectAndPositionItems((uint)selected.Length, selected.Select(i=>i.Pidl).ToArray(),
            selected.Select(i=>new Native.POINT(positions[i.Key])).ToArray(),0x80));
        Native.InvalidateRect(List,IntPtr.Zero,true);
    }
    public Rectangle ToView(Rectangle screen) => new(screen.X-Origin.X,screen.Y-Origin.Y,screen.Width,screen.Height);
    public void Dispose()
    {
        foreach (var obj in new[]{shellView,browser,desktop,windows}) if(Marshal.IsComObject(obj)) Marshal.ReleaseComObject(obj);
    }
}

internal sealed class DesktopLayout : IDisposable
{
    readonly Store store;
    DesktopShell? shell;
    public DesktopShell Shell => shell != null && shell.Alive ? shell : Reconnect();
    public DesktopLayout(Store store) { this.store=store; }
    DesktopShell Reconnect() { shell?.Dispose(); return shell=new DesktopShell(); }
    public void RememberUserPositions(IReadOnlyList<ShellIcon> icons)
    {
        bool changed=false;
        foreach(var saved in store.State.Icons.Where(b=>!b.Hidden))
        {
            var icon=icons.FirstOrDefault(i=>string.Equals(i.Key,saved.Key,StringComparison.OrdinalIgnoreCase));
            if(icon!=null&&icon.Position!=new Point(saved.LastX,saved.LastY))
            {saved.X=saved.LastX=icon.Position.X;saved.Y=saved.LastY=icon.Position.Y;changed=true;}
        }
        if(changed)store.Save();
    }
    public void Apply(IReadOnlyList<Rectangle> baskets,bool preserveManagedPositions=false)
    {
        var desktop=Shell;
        var icons=desktop.ReadIcons();
        var oldBackups=store.State.Icons.Select(b=>new IconBackup{Key=b.Key,X=b.X,Y=b.Y,LastX=b.LastX,LastY=b.LastY,Hidden=b.Hidden}).ToList();
        var oldAuto=store.State.OriginalAutoArrange;
        var oldSnap=store.State.OriginalSnapToGrid;
        bool priorAuto=(desktop.Flags&1)!=0;
        bool priorSnap=(desktop.Flags&4)!=0;
        try
        {
            var workAreas=Screen.AllScreens.Select(s=>desktop.ToView(s.WorkingArea)).ToArray();
            var blocked=baskets.Select(b=>desktop.ToView(b)).ToArray();
            var assigned=new HashSet<string>(store.State.Baskets.SelectMany(b=>b.Entries).Select(e=>e.Path),StringComparer.OrdinalIgnoreCase);
            var returning=new HashSet<string>(store.State.Icons.Where(b=>b.Hidden&&!assigned.Contains(b.Key)).Select(b=>b.Key),StringComparer.OrdinalIgnoreCase);
            var visible=icons.Where(i=>!assigned.Contains(i.Key)).Select(i=>
            {
                var saved=store.State.Icons.FirstOrDefault(b=>b.Key==i.Key);
                return new LayoutIcon(i.Key,saved==null?i.Position:saved.Hidden?new Point(saved.X,saved.Y):preserveManagedPositions?new Point(saved.LastX,saved.LastY):i.Position);
            }).ToArray();
            var spacing=desktop.Spacing;
            var plan=LayoutPlanner.Plan(visible,blocked,workAreas,spacing,returning);
            if(preserveManagedPositions)foreach(var icon in visible)
                if(!plan.ContainsKey(icon.Key)&&icons.Any(i=>i.Key==icon.Key&&i.Position!=icon.Position))plan[icon.Key]=icon.Position;
            var vacancies=icons.Where(i=>assigned.Contains(i.Key)).Select(i=>
            {
                var saved=store.State.Icons.FirstOrDefault(b=>string.Equals(b.Key,i.Key,StringComparison.OrdinalIgnoreCase)&&b.Hidden);
                return saved==null?i.Position:new Point(saved.X,saved.Y);
            }).ToArray();
            var current=visible.Select(i=>new LayoutIcon(i.Key,plan.TryGetValue(i.Key,out var p)?p:i.Position)).ToArray();
            foreach(var move in LayoutPlanner.FillVacancies(current,vacancies,blocked,workAreas,spacing))plan[move.Key]=move.Value;
            int hiddenX=Screen.AllScreens.Max(s=>desktop.ToView(s.Bounds).Right)+512;
            int hiddenIndex=0;
            foreach(var icon in icons.Where(i=>assigned.Contains(i.Key)))
                plan[icon.Key]=new Point(hiddenX+(hiddenIndex/128)*(spacing.Width+20),20+(hiddenIndex++%128)*(spacing.Height+20));
            if (store.State.OriginalAutoArrange == null) store.State.OriginalAutoArrange=(desktop.Flags & 1)!=0;
            if (store.State.OriginalSnapToGrid == null) store.State.OriginalSnapToGrid=(desktop.Flags & 4)!=0;
            foreach(var icon in icons.Where(i=>plan.ContainsKey(i.Key)))
            {
                var backup=store.State.Icons.FirstOrDefault(b=>b.Key==icon.Key);
                if(backup==null) { backup=new IconBackup{Key=icon.Key,X=icon.Position.X,Y=icon.Position.Y}; store.State.Icons.Add(backup); }
                // Record a user-adjusted position as the new restore target.
                else if(!backup.Hidden&&!preserveManagedPositions&&icon.Position != new Point(backup.LastX,backup.LastY)) { backup.X=icon.Position.X; backup.Y=icon.Position.Y; }
                backup.LastX=plan[icon.Key].X; backup.LastY=plan[icon.Key].Y;
                backup.Hidden=assigned.Contains(icon.Key);
            }
            store.Save();
            desktop.SetManualPositions();
            try
            {
                desktop.CleanAssignedSelection(icons,assigned);
                desktop.Position(icons,plan);
                var after=desktop.ReadIcons();
                try
                {
                    if(after.Where(i=>!assigned.Contains(i.Key)).Any(i=>blocked.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))))
                        throw new InvalidOperationException("Windows 未接受圖示避讓位置。整理框已暫停，避免遮住原有圖示。");
                    if(after.Where(i=>assigned.Contains(i.Key)).Any(i=>Screen.AllScreens.Any(s=>desktop.ToView(s.Bounds).IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))))
                        throw new InvalidOperationException("Windows 未接受純視覺分類位置，這次分類不會顯示重複圖示。");
                    foreach(var moved in after.Where(i=>plan.ContainsKey(i.Key)))
                    {
                        if(!assigned.Contains(moved.Key)&&after.Where(other=>!assigned.Contains(other.Key)).Any(other=>other.Key!=moved.Key&&LayoutPlanner.Cell(moved.Position,spacing).IntersectsWith(LayoutPlanner.Cell(other.Position,spacing))))
                            throw new InvalidOperationException("Windows 調整了圖示位置，造成圖示互相重疊。這次移動已取消。");
                        var saved=store.State.Icons.Single(b=>b.Key==moved.Key);saved.LastX=moved.Position.X;saved.LastY=moved.Position.Y;
                    }
                    store.Save();
                }
                finally { foreach(var icon in after) icon.Dispose(); }
            }
            catch
            {
                desktop.Position(icons,icons.ToDictionary(i=>i.Key,i=>i.Position));
                desktop.SetLayoutFlags(priorAuto,priorSnap);
                store.State.Icons=oldBackups;store.State.OriginalAutoArrange=oldAuto;store.State.OriginalSnapToGrid=oldSnap;store.Save();
                throw;
            }
        }
        finally { foreach(var icon in icons) icon.Dispose(); }
    }
    public void Restore()
    {
        if(store.State.OriginalAutoArrange==null && store.State.Icons.Count==0) return;
        var desktop=Shell;
        var icons=desktop.ReadIcons();
        try
        {
            desktop.SetManualPositions();
            var restore=new Dictionary<string,Point>();
            foreach(var icon in icons)
            {
                var saved=store.State.Icons.FirstOrDefault(b=>b.Key==icon.Key);
                if(saved!=null && icon.Position==new Point(saved.LastX,saved.LastY)) restore[icon.Key]=new Point(saved.X,saved.Y);
            }
            desktop.Position(icons,restore);
            var verify=desktop.ReadIcons();
            try
            {
                if(verify.Any(i=>restore.TryGetValue(i.Key,out var target)&&i.Position!=target))
                    throw new InvalidOperationException("部分桌面圖示未能還原，位置備份仍保留。請重試。");
            }
            finally{foreach(var i in verify)i.Dispose();}
            desktop.SetLayoutFlags(store.State.OriginalAutoArrange??false,store.State.OriginalSnapToGrid??true);
            store.State.Icons.Clear(); store.State.OriginalAutoArrange=null;store.State.OriginalSnapToGrid=null;store.Save();
        }
        finally { foreach(var icon in icons) icon.Dispose(); }
    }
    public void Dispose() { shell?.Dispose(); }
}
