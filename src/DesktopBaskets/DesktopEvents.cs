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
    public int Seen { get; private set; }
    public int Relevant { get; private set; }
    public DesktopEvents(DesktopShell shell,Action changed)
    {
        GetWindowThreadProcessId(shell.List,out uint process);
        callback=(_,evt,hwnd,obj,child,_,_)=>
        {
            Seen++;
            if(hwnd==shell.List||hwnd==shell.Host||IsChild(shell.Host,hwnd)){Relevant++;changed();}
        };
        hook=SetWinEventHook(0x8000,0x800C,IntPtr.Zero,callback,process,0,2); // WINEVENT_SKIPOWNPROCESS
        interactionHook=SetWinEventHook(8,15,IntPtr.Zero,callback,process,0,2); // capture/drag end, no mouse-move hook
        if(hook==IntPtr.Zero)throw new InvalidOperationException("無法監聽桌面變動。請暫停桌面整理後重試。");
    }
    public void Dispose(){if(hook!=IntPtr.Zero)UnhookWinEvent(hook);if(interactionHook!=IntPtr.Zero)UnhookWinEvent(interactionHook);GC.KeepAlive(callback);}
}
