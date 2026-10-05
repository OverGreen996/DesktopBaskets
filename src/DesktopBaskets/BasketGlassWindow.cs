using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DesktopBaskets;

// Independent native surfaces keep text/icons opaque while the tint is translucent.
internal sealed class BasketGlassWindow : Form
{
    readonly App app;
    readonly BasketWindow foreground;
    public int PaintCount {get;private set;}
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    public BasketGlassWindow(App app,BasketWindow foreground)
    {
        this.app=app;this.foreground=foreground;
        TopLevel=false;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;
        AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;
        BackColor=Theme.Panel;DoubleBuffered=true;TabStop=false;AllowDrop=true;
        AccessibleName=foreground.Basket.Name+"的玻璃底板";
        DragEnter+=foreground.OnDragEnter;DragDrop+=foreground.OnDrop;
    }
    public void Attach(DesktopShell shell)
    {
        var hwnd=Handle;Native.SetParent(hwnd,shell.Host);
        if(Native.GetParent(hwnd)!=shell.Host)throw new InvalidOperationException("無法附加桌面玻璃底板。");
        Native.SetWindowLongPtr(hwnd,-20,new IntPtr(Native.GetWindowLongPtr(hwnd,-20).ToInt64()|0x80000|0x08000000));
        var bounds=foreground.Basket.ScreenBounds;var p=new Native.POINT(bounds.Location);Native.ScreenToClient(shell.Host,ref p);
        if(!Native.SetWindowPos(hwnd,foreground.Handle,p.X,p.Y,bounds.Width,bounds.Height,0x10|0x20))throw new InvalidOperationException("無法設定玻璃底板的位置。");
        if(!Native.SetLayeredWindowAttributes(hwnd,0,(byte)(foreground.Basket.OpacityPercent*255/100),2))throw new InvalidOperationException("無法設定玻璃底板的透明度。");
        Invalidate();
    }
    public void ShowBehind()
    {
        if(IsDisposed||!IsHandleCreated)return;
        Show();Native.SetWindowPos(Handle,foreground.Handle,0,0,0,0,0x1|0x2|0x10);
    }
    public static void PaintGlass(Graphics g,Rectangle bounds)
    {
        if(bounds.Width<=0||bounds.Height<=0)return;
        using var tint=new LinearGradientBrush(bounds,Theme.GlassTop,Theme.GlassBottom,LinearGradientMode.Vertical);
        g.FillRectangle(tint,bounds);
        int sheenHeight=Math.Max(1,bounds.Height/3);
        using var sheen=new LinearGradientBrush(new Rectangle(bounds.X,bounds.Y,bounds.Width,sheenHeight),Color.FromArgb(16,255,255,255),Color.FromArgb(0,255,255,255),LinearGradientMode.Vertical);
        g.FillRectangle(sheen,bounds.X,bounds.Y,bounds.Width,sheenHeight);
        using var edge=new Pen(Color.FromArgb(42,255,255,255));g.DrawLine(edge,bounds.Left+48,bounds.Top+.5f,bounds.Right-52,bounds.Top+.5f);
    }
    protected override void OnPaint(PaintEventArgs e){PaintCount++;PaintGlass(e.Graphics,ClientRectangle);base.OnPaint(e);}
    protected override void WndProc(ref Message m)
    {
        if(foreground!=null&&!foreground.IsDisposed&&m.Msg>=0x200&&m.Msg<=0x20E)
        {
            if(m.Msg==0x200&&m.WParam==IntPtr.Zero&&app.IsStandby){m.Result=IntPtr.Zero;return;}
            if(m.Msg!=0x200||m.WParam.ToInt64()!=0)app.Wake();
            var target=foreground.PointerTarget(Cursor.Position);
            IntPtr point=m.LParam;
            if(m.Msg!=0x20A&&m.Msg!=0x20E)
            {
                var p=target.PointToClient(Cursor.Position);point=new IntPtr((p.X&0xffff)|((p.Y&0xffff)<<16));
            }
            m.Result=SendMessage(target.Handle,m.Msg,m.WParam,point);Cursor=target.Cursor;return;
        }
        base.WndProc(ref m);
    }
}
