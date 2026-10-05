using System.Runtime.InteropServices;

namespace DesktopBaskets;

// Keep Windows' window styles and system commands, but paint the caption as
// part of our existing header. No extra title strip, image or render timer.
internal class ModernWindow : Form
{
    public Rectangle CaptionRegion {get;set;}
    public int ResizeMargin=>Math.Max(6,(int)Math.Round(6*DeviceDpi/96.0));
    public bool RoundedCornersSupported {get;private set;}
    Rectangle restoreWindowBounds;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo {public Native.POINT Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    public ModernWindow()
    {
        FormBorderStyle=FormBorderStyle.Sizable;
        SetStyle(ControlStyles.ResizeRedraw,true);
        Padding=new Padding(ResizeMargin);
    }
    public void CaptionCommand(int command)=>SendMessage(Handle,0x112,new IntPtr(command),IntPtr.Zero);
    public Bitmap RenderClient()
    {
        var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height);
        using var g=Graphics.FromImage(bitmap);var dc=g.GetHdc();
        try{SendMessage(Handle,0x317,dc,new IntPtr(4|8|16));} // WM_PRINT: client + erase + children, no legacy NC print.
        finally{g.ReleaseHdc(dc);}
        return bitmap;
    }
    public void ToggleMaximize()=>CaptionCommand(WindowState==FormWindowState.Maximized?0xF120:0xF030);
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark=1,corner=2,border=Theme.Background.R|(Theme.Background.G<<8)|(Theme.Background.B<<16);
        DwmSetWindowAttribute(Handle,20,ref dark,4);
        RoundedCornersSupported=DwmSetWindowAttribute(Handle,33,ref corner,4)==0;
        DwmSetWindowAttribute(Handle,34,ref border,4);
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        Padding=WindowState==FormWindowState.Maximized?Padding.Empty:new Padding(ResizeMargin);
        base.OnSizeChanged(e);
    }
    public int FrameHitTest(Point point)
    {
        if(WindowState!=FormWindowState.Maximized)
        {
            int edge=ResizeMargin;bool left=point.X<edge,right=point.X>=ClientSize.Width-edge;
            bool top=point.Y<edge,bottom=point.Y>=ClientSize.Height-edge;
            if(top)return left?13:right?14:12;
            if(bottom)return left?16:right?17:15;
            if(left)return 10;if(right)return 11;
        }
        if(!CaptionRegion.Contains(point))return 1;
        // Windows asks the top-level window before dispatching to child buttons.
        // Returning HTCAPTION here would turn an actual button click into a title
        // drag and prevent Click from firing. Keep buttons as native client hits.
        var child=GetChildAtPoint(point,GetChildAtPointSkip.Invisible|GetChildAtPointSkip.Disabled);
        while(child!=null)
        {
            if(child is ButtonBase)return 1;
            point=new Point(point.X-child.Left,point.Y-child.Top);
            child=child.GetChildAtPoint(point,GetChildAtPointSkip.Invisible|GetChildAtPointSkip.Disabled);
        }
        return 2;
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x112)
        {
            int command=(int)(m.WParam.ToInt64()&0xfff0);
            if((command==0xF020||command==0xF030)&&WindowState==FormWindowState.Normal)restoreWindowBounds=Bounds;
            base.WndProc(ref m);
            // WinForms' cached client size includes the legacy caption metrics
            // during restore. Reapply the saved native bounds to avoid growth.
            if(command==0xF120&&WindowState==FormWindowState.Normal&&!restoreWindowBounds.IsEmpty)Bounds=restoreWindowBounds;
            return;
        }
        if(m.Msg==0x83&&m.WParam!=IntPtr.Zero)
        {
            // Windows may retain its invisible sizing border outside the work
            // area when maximized. Keep the visible client inside the work area.
            if(IsZoomed(m.HWnd))
            {
                var work=Screen.FromHandle(m.HWnd).WorkingArea;
                var rect=Marshal.PtrToStructure<NativeRect>(m.LParam);
                rect.Left=Math.Max(rect.Left,work.Left);rect.Top=Math.Max(rect.Top,work.Top);
                rect.Right=Math.Min(rect.Right,work.Right);rect.Bottom=Math.Min(rect.Bottom,work.Bottom);
                Marshal.StructureToPtr(rect,m.LParam,false);
            }
            m.Result=IntPtr.Zero;return;
        } // WM_NCCALCSIZE
        if(m.Msg==0x84)
        {
            long packed=m.LParam.ToInt64();var screen=new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff));
            m.Result=new IntPtr(FrameHitTest(PointToClient(screen)));return;
        }
        if(m.Msg==0x24)
        {
            // Use monitor-relative work bounds, including monitors left/above
            // the primary display. The taskbar must stay outside maximization.
            base.WndProc(ref m);
            var monitor=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
            if(GetMonitorInfo(MonitorFromWindow(Handle,2),ref monitor))
            {
                var info=Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
                info.MaxPosition=new Native.POINT(new Point(monitor.Work.Left-monitor.Monitor.Left,monitor.Work.Top-monitor.Monitor.Top));
                info.MaxSize=new Native.POINT(new Point(monitor.Work.Right-monitor.Work.Left,monitor.Work.Bottom-monitor.Work.Top));
                Marshal.StructureToPtr(info,m.LParam,false);
            }
            return;
        }
        base.WndProc(ref m);
    }
}

internal sealed class WindowHeaderPanel : Panel
{
    public WindowHeaderPanel(){DoubleBuffered=true;BackColor=Theme.Background;}
    protected override void WndProc(ref Message m)
    {
        // Blank / painted header regions are native caption hit targets.
        // Actual child buttons retain their normal mouse and keyboard input.
        if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}
        base.WndProc(ref m);
    }
}

internal enum CaptionAction {Minimize,Maximize,Close}
internal sealed class CaptionButton : Button
{
    readonly ModernWindow window;
    readonly CaptionAction action;
    bool hover,pressed;
    public CaptionAction Action=>action;
    public CaptionButton(ModernWindow window,CaptionAction action)
    {
        this.window=window;this.action=action;
        BackColor=Theme.Background;ForeColor=Theme.Text;
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        AccessibleName=action==CaptionAction.Minimize?"最小化":action==CaptionAction.Maximize?"最大化／還原":"關閉管理視窗（保留桌面分類）";
        AccessibleRole=AccessibleRole.PushButton;TabStop=true;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        Click+=(_,_)=>PerformCaptionAction();
    }
    public void PerformCaptionAction()
    {
        if(action==CaptionAction.Minimize)window.CaptionCommand(0xF020);
        else if(action==CaptionAction.Maximize)window.ToggleMaximize();
        else window.Close();
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=e.Button==MouseButtons.Left;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;int shade=pressed?40:48;g.Clear(hover||pressed?(action==CaptionAction.Close?Color.FromArgb(pressed?160:196,43,28):Color.FromArgb(shade,shade,shade)):Theme.Background);
        FrameArt.Prepare(g);float s=window.DeviceDpi/96f,cx=Width/2f,cy=Height/2f;
        using var pen=new Pen(action==CaptionAction.Close&&hover?Color.White:Theme.Text,1.15f*s);
        float half=5*s;
        if(action==CaptionAction.Minimize)g.DrawLine(pen,cx-half,cy,cx+half,cy);
        else if(action==CaptionAction.Close){g.DrawLine(pen,cx-half,cy-half,cx+half,cy+half);g.DrawLine(pen,cx+half,cy-half,cx-half,cy+half);}
        else if(window.WindowState==FormWindowState.Maximized)
        {
            g.DrawLines(pen,new[]{new PointF(cx-half+3*s,cy-half),new PointF(cx+half,cy-half),new PointF(cx+half,cy+half-3*s)});
            g.DrawRectangle(pen,cx-half,cy-half+3*s,7*s,7*s);
        }
        else g.DrawRectangle(pen,cx-half,cy-half,half*2,half*2);
        if(Focused){using var focus=new Pen(Theme.Accent){DashStyle=System.Drawing.Drawing2D.DashStyle.Dot};g.DrawRectangle(focus,5,5,Width-11,Height-11);}
    }
}
