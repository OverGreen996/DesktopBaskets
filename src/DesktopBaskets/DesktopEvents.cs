using System.Runtime.InteropServices;

namespace DesktopBaskets;

// No polling: Explorer emits accessibility events when desktop items change.
internal sealed class DesktopEvents : IDisposable
{
    delegate void WinEvent(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
    [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent,IntPtr child);
    readonly WinEvent callback;
    readonly IntPtr hook;
    readonly IntPtr interactionHook;
    readonly ShellChangeWindow shellChanges;
    public int Seen { get; private set; }
    public int Relevant { get; private set; }
    public DesktopEvents(DesktopShell shell,Action<bool> changed)
    {
        GetWindowThreadProcessId(shell.List,out uint process);
        callback=(_,evt,hwnd,obj,child,_,_)=>
        {
            Seen++;
            if(hwnd==shell.List||hwnd==shell.Host||IsChild(shell.List,hwnd)){Relevant++;changed(evt==0x8004);}
        };
        hook=SetWinEventHook(0x8000,0x800C,IntPtr.Zero,callback,process,0,2); // WINEVENT_SKIPOWNPROCESS
        interactionHook=SetWinEventHook(8,15,IntPtr.Zero,callback,process,0,2); // capture/drag end, no mouse-move hook
        if(hook==IntPtr.Zero)throw new InvalidOperationException("無法監聽桌面變動。請暫停桌面整理後重試。");
        try{shellChanges=new ShellChangeWindow(()=>{Seen++;Relevant++;changed(true);});}
        catch{UnhookWinEvent(hook);if(interactionHook!=IntPtr.Zero)UnhookWinEvent(interactionHook);throw;}
    }
    public void Dispose(){shellChanges.Dispose();if(hook!=IntPtr.Zero)UnhookWinEvent(hook);if(interactionHook!=IntPtr.Zero)UnhookWinEvent(interactionHook);GC.KeepAlive(callback);}
}

// A message-only window sleeps until the Shell delivers a directory update.
internal sealed class ShellChangeWindow : NativeWindow,IDisposable
{
    const int NotifyMessage=0x8000+193;
    [StructLayout(LayoutKind.Sequential)] struct NotifyEntry{public IntPtr Pidl;[MarshalAs(UnmanagedType.Bool)]public bool Recursive;}
    [DllImport("shell32.dll")] static extern int SHGetSpecialFolderLocation(IntPtr owner,int folder,out IntPtr pidl);
    [DllImport("shell32.dll")] static extern uint SHChangeNotifyRegister(IntPtr hwnd,int sources,int events,uint message,int count,ref NotifyEntry entry);
    [DllImport("shell32.dll")] static extern bool SHChangeNotifyDeregister(uint registration);
    [DllImport("shell32.dll")] static extern IntPtr SHChangeNotification_Lock(IntPtr change,uint process,out IntPtr pidls,out int events);
    [DllImport("shell32.dll")] static extern bool SHChangeNotification_Unlock(IntPtr locked);
    readonly Action changed;uint registration;IntPtr pidl;
    public ShellChangeWindow(Action changed)
    {
        this.changed=changed;
        try
        {
            CreateHandle(new CreateParams{Parent=new IntPtr(-3)});
            Native.Check(SHGetSpecialFolderLocation(IntPtr.Zero,0,out pidl));
            var entry=new NotifyEntry{Pidl=pidl,Recursive=false};
            registration=SHChangeNotifyRegister(Handle,0x8002,0x1000,NotifyMessage,1,ref entry);
            if(registration==0)throw new InvalidOperationException("無法監聽桌面重新整理通知。");
        }
        catch{Dispose();throw;}
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==NotifyMessage)
        {
            var locked=SHChangeNotification_Lock(message.WParam,unchecked((uint)message.LParam.ToInt64()),out _,out int events);
            if(locked!=IntPtr.Zero){SHChangeNotification_Unlock(locked);if((events&0x1000)!=0)changed();}
            return;
        }
        base.WndProc(ref message);
    }
    public void Dispose(){if(registration!=0){SHChangeNotifyDeregister(registration);registration=0;}if(pidl!=IntPtr.Zero){Marshal.FreeCoTaskMem(pidl);pidl=IntPtr.Zero;}if(Handle!=IntPtr.Zero)DestroyHandle();}
}
