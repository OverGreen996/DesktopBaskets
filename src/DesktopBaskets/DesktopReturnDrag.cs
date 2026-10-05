namespace DesktopBaskets;

// Private OLE data only transfers visual membership. Explorer never receives a
// FileDrop payload that could move, duplicate, or create a shortcut to the file.
internal sealed class DesktopReturnDrag : IDisposable
{
    readonly Control source;
    readonly Func<Point> pointer;
    readonly Func<Point,bool> isDesktop;
    public Point? DropPoint {get;private set;}
    public DesktopReturnDrag(Control source,Func<Point> pointer,Func<Point,bool> isDesktop)
    {
        this.source=source;this.pointer=pointer;this.isDesktop=isDesktop;
        source.QueryContinueDrag+=Continue;source.GiveFeedback+=Feedback;
    }
    internal void Continue(object? sender,QueryContinueDragEventArgs drag)
    {
        if(drag.EscapePressed){DropPoint=null;drag.Action=DragAction.Cancel;return;}
        var point=pointer();
        if((drag.KeyState&0x13)==0&&isDesktop(point))
        {DropPoint=point;drag.Action=DragAction.Cancel;}
    }
    void Feedback(object? sender,GiveFeedbackEventArgs drag)
    {if(isDesktop(pointer())){drag.UseDefaultCursors=false;Cursor.Current=Cursors.Arrow;}}
    public void Dispose(){source.QueryContinueDrag-=Continue;source.GiveFeedback-=Feedback;}
}
