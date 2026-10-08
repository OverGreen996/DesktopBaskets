using System.Drawing.Drawing2D;

namespace DesktopBaskets;

// Exists only during a frame drag. A retained surface survives desktop repaint,
// unlike XOR drawing, and never becomes the mouse or keyboard input target.
internal sealed class DragPreviewWindow : Form
{
    static readonly Color Key=Color.Magenta;
    Rectangle physicalBounds;
    bool valid=true;
    internal Rectangle PhysicalBounds=>physicalBounds;
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters=base.CreateParams;
            parameters.ExStyle|=0x08000000|0x00000020|0x00000080|0x00080000;
            return parameters;
        }
    }
    internal DragPreviewWindow()
    {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
        AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;
        BackColor=Key;TransparencyKey=Key;DoubleBuffered=true;TabStop=false;
        AccessibleName="籃框落點預覽";
    }
    internal void ShowAt(Rectangle bounds,bool allowed=true)
    {
        if(bounds.Width<=0||bounds.Height<=0){Hide();return;}
        if(Visible&&physicalBounds==bounds&&valid==allowed)return;
        using var dpi=new Native.PhysicalDpiScope();
        physicalBounds=bounds;valid=allowed;
        if(!IsHandleCreated)CreateHandle();
        // Bypass WinForms' cached/virtualized screen bounds and keep pixel sizes
        // fixed when WM_DPICHANGED arrives while crossing monitor boundaries.
        if(!Native.SetWindowPos(Handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x10|0x40))
            throw new System.ComponentModel.Win32Exception("無法顯示籃框落點預覽。");
        if(!Visible)Show();Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Key);e.Graphics.SmoothingMode=SmoothingMode.None;
        var border=new Rectangle(1,1,ClientSize.Width-3,ClientSize.Height-3);
        if(border.Width<1||border.Height<1)return;
        using var shadow=new Pen(Color.Black,3);
        using var line=new Pen(valid?Theme.Accent:Color.OrangeRed,1);
        e.Graphics.DrawRectangle(shadow,border);e.Graphics.DrawRectangle(line,border);
        base.OnPaint(e);
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==0x84){message.Result=new IntPtr(-1);return;} // HTTRANSPARENT
        if(message.Msg==0x21){message.Result=new IntPtr(3);return;} // MA_NOACTIVATE
        if(message.Msg==0x02E0){message.Result=IntPtr.Zero;return;} // retain physical geometry
        base.WndProc(ref message);
    }
}
