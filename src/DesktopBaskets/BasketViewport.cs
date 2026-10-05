namespace DesktopBaskets;

// One window per basket viewport, regardless of file count. Only visible cells paint icons.
internal sealed class BasketViewport : Control
{
    readonly App app;
    readonly Basket basket;
    readonly ToolTip tip=new();
    int hover=-1,selected=-1;
    Point down;
    bool dragging;
    int scrollOffset,contentHeight;
    bool scrolling;int thumbStart,offsetStart;
    public bool CanScroll=>contentHeight>ContentRectangle.Height;
    int ContentTop=>Grid.ContentTopFor(basket.Width);
    int ContentBottom=>Grid.ContentBottomFor(basket.Width);
    public Size AutoScrollMinSize=>new(0,contentHeight+ContentTop+ContentBottom);
    public Point AutoScrollPosition{get=>new(0,-scrollOffset);set{scrollOffset=MathEx.Clamp(value.Y,0,Math.Max(0,contentHeight-ContentRectangle.Height));Invalidate();}}
    Rectangle ContentRectangle=>new(Grid.ContentLeft,ContentTop,Math.Max(0,ClientSize.Width-Grid.Side),Math.Max(0,ClientSize.Height-ContentTop-ContentBottom));
    public Rectangle ContentBounds=>ContentRectangle;
    public Rectangle ScrollbarBounds=>ScrollTrack;
    public Func<Cursor?>? EdgeCursor {get;set;}
    public int PaintCount {get;private set;}
    Rectangle ScrollTrack=>new(ClientSize.Width-36,ContentTop,4,ContentRectangle.Height);
    Rectangle ScrollThumb
    {
        get{var track=ScrollTrack;int size=Math.Max(24,track.Height*ContentRectangle.Height/Math.Max(1,contentHeight));return new Rectangle(track.X,track.Y+scrollOffset*Math.Max(0,track.Height-size)/Math.Max(1,contentHeight-ContentRectangle.Height),track.Width,size);}
    }
    public int Columns => basket.Columns;
    public BasketViewport(App app,Basket basket)
    {
        this.app=app;this.basket=basket;BackColor=Theme.Panel;Dock=DockStyle.Fill;
        TabStop=true;AccessibleName=basket.Name+"的檔案";AccessibleRole=AccessibleRole.List;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true);
        Font=Theme.Body(9);RefreshItems();
    }
    public void RefreshItems()
    {
        int rows=(basket.Entries.Count+Columns-1)/Columns;
        contentHeight=rows*Grid.CellHeight;AutoScrollPosition=new Point(0,scrollOffset);
        if(selected>=basket.Entries.Count)selected=basket.Entries.Count-1;
        hover=-1;tip.SetToolTip(this,"");Invalidate();
    }
    public Rectangle ItemBounds(int index) => new(Grid.ContentLeft+(index%Columns)*Grid.CellWidth,
        ContentTop+(index/Columns)*Grid.CellHeight+AutoScrollPosition.Y,Grid.CellWidth,Grid.CellHeight);
    int Hit(Point point)
    {
        if(!ContentRectangle.Contains(point))return -1;
        int x=point.X-Grid.ContentLeft,y=point.Y-ContentTop-AutoScrollPosition.Y;
        if(x<0||y<0||x>=Columns*Grid.CellWidth)return -1;
        int index=(y/Grid.CellHeight)*Columns+x/Grid.CellWidth;
        return index<basket.Entries.Count?index:-1;
    }
    public void ScrollBy(int rows)=>AutoScrollPosition=new Point(0,Math.Max(0,-AutoScrollPosition.Y+rows*Grid.CellHeight));
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollBy(-(e.Delta/120));Invalidate();
        if(e is HandledMouseEventArgs handled)handled.Handled=true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        PaintCount++;
        base.OnPaint(e);
        FrameArt.Prepare(e.Graphics);
        using(var outline=new Pen(Theme.InnerFrameLine))
        {
            int w=ClientSize.Width,h=ClientSize.Height;
            float s=w/1537f;int inset=Math.Max(8,(int)(32*s)),cut=Math.Max(8,(int)(30*s));
            using var outer=new Pen(Theme.Muted);
            e.Graphics.DrawLine(outer,.5f,8,.5f,h*.28f);e.Graphics.DrawLine(outer,.5f,h*.45f,.5f,h*.96f);
            e.Graphics.DrawLine(outer,w-1.5f,8,w-1.5f,h*.34f);e.Graphics.DrawLine(outer,w-1.5f,h*.54f,w-1.5f,h*.92f);
            using var yellow=new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(yellow,0,20*s,Math.Max(2,4*s),72*s);e.Graphics.FillRectangle(yellow,w-Math.Max(2,4*s),h*.63f,Math.Max(2,4*s),h*.14f);
            e.Graphics.DrawLine(outline,inset,cut,inset+cut,1);e.Graphics.DrawLine(outline,inset+cut,1,w*.65f,1);e.Graphics.DrawLine(outline,w*.66f,1,w-inset,1);
            e.Graphics.DrawLine(outline,inset,cut,inset,h-22);e.Graphics.DrawLine(outline,inset,h-22,inset+cut,h-1);
            e.Graphics.DrawLine(outline,inset+cut,h-1,w*.68f,h-1);e.Graphics.DrawLine(outline,w*.69f,h-1,w-inset,h-1);
            e.Graphics.DrawLine(outline,w-inset,1,w-inset,h*.25f);e.Graphics.DrawLine(outline,w-inset,h*.37f,w-inset,h*.79f);e.Graphics.DrawLine(outline,w-inset,h*.94f,w-inset,h-1);
            int crossX=Math.Max(18,(int)(102*s)),crossY=Math.Min(ContentTop-(Grid.Compact(w)?7:14),Math.Max(24,(int)(63*s)));
            FrameArt.Cross(e.Graphics,crossX,crossY,Theme.Muted);FrameArt.Cross(e.Graphics,w-crossX,crossY,Theme.Muted);
            int crossBottom=h-(Grid.Compact(w)?7:20);
            FrameArt.Cross(e.Graphics,crossX,crossBottom,Theme.Muted);FrameArt.Cross(e.Graphics,w-crossX,crossBottom,Theme.Muted);
            var saved=e.Graphics.Save();e.Graphics.TranslateTransform(18,Grid.Compact(w)?26:h*.36f);e.Graphics.RotateTransform(90);FrameArt.TrackedVector(e.Graphics,"SECTOR "+Math.Max(1,app.Store.State.Baskets.IndexOf(basket)+1).ToString("00"),0,0,10,2*s,Theme.Muted);e.Graphics.Restore(saved);
            saved=e.Graphics.Save();e.Graphics.TranslateTransform(w-2,Grid.Compact(w)?8:h*.4f);e.Graphics.RotateTransform(90);FrameArt.TrackedVector(e.Graphics,Screen.FromRectangle(basket.ScreenBounds).DeviceName.Replace("\\\\.\\","LOCAL / "),0,0,10,Grid.Compact(w)?0:2*s,Theme.Muted);e.Graphics.Restore(saved);
        }
        using(var gridDot=new SolidBrush(Theme.GridDot))
            for(int y=12;y<ClientSize.Height-12;y+=16)
            for(int x=20;x<ClientSize.Width-16;x+=16)
                if(e.ClipRectangle.Contains(x,y))e.Graphics.FillRectangle(gridDot,x,y,1,1);
        using(var cluster=new SolidBrush(Color.FromArgb(71,76,75)))
            foreach(var point in new[]{new Point(ClientSize.Width-52,44),new Point(ClientSize.Width-42,44),new Point(ClientSize.Width-32,44),new Point(ClientSize.Width-52,54),new Point(27,ClientSize.Height-45),new Point(37,ClientSize.Height-45),new Point(47,ClientSize.Height-45)})e.Graphics.FillRectangle(cluster,point.X,point.Y,2,2);
        if(basket.Entries.Count==0)return;
        if(CanScroll)
        {
            using var track=new SolidBrush(Theme.Line);e.Graphics.FillRectangle(track,ScrollTrack);
            using var thumb=new SolidBrush(Theme.Accent);e.Graphics.FillRectangle(thumb,ScrollThumb);
        }
        var drawing=e.Graphics.Save();e.Graphics.SetClip(ContentRectangle,System.Drawing.Drawing2D.CombineMode.Intersect);
        int firstRow=Math.Max(0,scrollOffset/Grid.CellHeight);
        int lastRow=Math.Min((basket.Entries.Count-1)/Columns,firstRow+ClientSize.Height/Grid.CellHeight+1);
        for(int index=firstRow*Columns;index<Math.Min(basket.Entries.Count,(lastRow+1)*Columns);index++)
        {
            var rect=ItemBounds(index);if(!rect.IntersectsWith(e.ClipRectangle))continue;
            var entry=basket.Entries[index];var tile=new Rectangle(rect.X+4,rect.Y+4,Grid.CellWidth-8,Grid.CellHeight-8);
            if(index==hover||index==selected){using var fill=new SolidBrush(Theme.Raised);e.Graphics.FillRectangle(fill,tile);}
            e.Graphics.DrawImage(app.Icons.Get(entry.Path),new Rectangle(tile.X+(tile.Width-32)/2,tile.Y+8,32,32));
            TextRenderer.DrawText(e.Graphics,entry.Name,Font,new Rectangle(tile.X+3,tile.Y+44,tile.Width-6,Math.Max(20,tile.Height-44)),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping);
            if(Focused&&index==selected){using var focus=new Pen(Theme.Accent){DashStyle=System.Drawing.Drawing2D.DashStyle.Dot};e.Graphics.DrawRectangle(focus,tile);}
        }
        e.Graphics.Restore(drawing);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);AutoScrollPosition=new Point(0,scrollOffset);}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);Focus();
        if(e.Button==MouseButtons.Left&&CanScroll&&new Rectangle(ScrollTrack.X-6,ScrollTrack.Y,16,ScrollTrack.Height).Contains(e.Location))
        {
            scrolling=true;thumbStart=e.Y;offsetStart=scrollOffset;Capture=true;
            if(!ScrollThumb.Contains(new Point(ScrollThumb.X,e.Y)))AutoScrollPosition=new Point(0,(e.Y-ScrollTrack.Top)*Math.Max(0,contentHeight-ContentRectangle.Height)/Math.Max(1,ScrollTrack.Height));
            offsetStart=scrollOffset;return;
        }
        selected=Hit(e.Location);down=e.Location;dragging=e.Button==MouseButtons.Left&&selected>=0;Invalidate();
        if(e.Button==MouseButtons.Right&&selected>=0)app.EntryMenu(basket,basket.Entries[selected]).Show(this,e.Location);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(app.IsStandby&&e.Button==MouseButtons.None)return;
        base.OnMouseMove(e);int next=Hit(e.Location);
        if(scrolling){AutoScrollPosition=new Point(0,offsetStart+(e.Y-thumbStart)*Math.Max(0,contentHeight-ContentRectangle.Height)/Math.Max(1,ScrollTrack.Height-ScrollThumb.Height));return;}
        if(hover!=next)
        {
            hover=next;Cursor=next>=0?Cursors.Hand:Cursors.Default;
            tip.SetToolTip(this,next>=0?basket.Entries[next].Name+"\n原始路徑："+basket.Entries[next].Path:"");Invalidate();
        }
        if(dragging&&e.Button==MouseButtons.Left&&Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>=8&&selected>=0)
        {
            dragging=false;var entry=basket.Entries[selected];var data=new DataObject();data.SetData("DesktopBaskets.Entry",entry.Id);
            DoDragDrop(data,DragDropEffects.Move);
        }
        Cursor=EdgeCursor?.Invoke()??(next>=0?Cursors.Hand:Cursors.Default);
    }
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;if(scrolling){scrolling=false;Capture=false;}base.OnMouseUp(e);}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture){scrolling=false;dragging=false;}base.OnMouseCaptureChanged(e);}
    protected override void OnMouseLeave(EventArgs e)
    {
        // A color-key hole can transfer the native hit from the foreground to
        // its glass sibling without the pointer leaving the viewport.
        // Clearing hover in that case would alternately make the tile opaque
        // and transparent, causing a repeated mouse-leave/repaint cycle.
        if(!ClientRectangle.Contains(PointToClient(Cursor.Position))&&hover!=-1){hover=-1;Invalidate();}
        base.OnMouseLeave(e);
    }
    protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);int index=Hit(e.Location);if(index>=0)Theme.Try(()=>Theme.Open(basket.Entries[index].Path));}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);if(basket.Entries.Count==0)return;
        int next=Math.Max(0,selected);
        switch(e.KeyCode)
        {
            case Keys.Left:next--;break;case Keys.Right:next++;break;case Keys.Up:next-=Columns;break;case Keys.Down:next+=Columns;break;
            case Keys.Home:next=0;break;case Keys.End:next=basket.Entries.Count-1;break;
            case Keys.PageUp:next-=Columns*Math.Max(1,ClientSize.Height/Grid.CellHeight);break;
            case Keys.PageDown:next+=Columns*Math.Max(1,ClientSize.Height/Grid.CellHeight);break;
            case Keys.Enter:if(selected>=0)Theme.Try(()=>Theme.Open(basket.Entries[selected].Path));return;
            case Keys.Delete:if(selected>=0)app.RemoveEntry(basket,basket.Entries[selected]);return;
            case Keys.Apps:if(selected>=0)app.EntryMenu(basket,basket.Entries[selected]).Show(this,ItemBounds(selected).Location);return;
            default:return;
        }
        selected=MathEx.Clamp(next,0,basket.Entries.Count-1);var rect=ItemBounds(selected);
        if(rect.Top<ContentRectangle.Top)AutoScrollPosition=new Point(0,Math.Max(0,scrollOffset+rect.Top-ContentRectangle.Top));
        else if(rect.Bottom>ContentRectangle.Bottom)AutoScrollPosition=new Point(0,scrollOffset+rect.Bottom-ContentRectangle.Bottom);
        Invalidate();e.Handled=true;
    }
    protected override void OnGotFocus(EventArgs e){if(selected<0&&basket.Entries.Count>0)selected=0;Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override AccessibleObject CreateAccessibilityInstance()=>new ViewAccessible(this);
    sealed class ViewAccessible : ControlAccessibleObject
    {
        readonly BasketViewport owner;
        public ViewAccessible(BasketViewport owner):base(owner){this.owner=owner;}
        public override int GetChildCount()=>owner.basket.Entries.Count;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<GetChildCount()?new EntryAccessible(owner,index,this):null;
    }
    sealed class EntryAccessible : AccessibleObject
    {
        readonly BasketViewport owner;readonly int index;readonly AccessibleObject parent;
        public EntryAccessible(BasketViewport owner,int index,AccessibleObject parent){this.owner=owner;this.index=index;this.parent=parent;}
        public override string? Name{get=>owner.basket.Entries[index].Name;set{}}
        public override AccessibleObject Parent=>parent;
        public override AccessibleRole Role=>AccessibleRole.ListItem;
        public override string DefaultAction=>"開啟";
        public override Rectangle Bounds=>owner.RectangleToScreen(owner.ItemBounds(index));
        public override AccessibleStates State=>AccessibleStates.Selectable|(owner.selected==index?AccessibleStates.Selected:0)|(owner.ItemBounds(index).IntersectsWith(owner.ContentRectangle)?0:AccessibleStates.Offscreen);
        public override void DoDefaultAction()=>Theme.Try(()=>Theme.Open(owner.basket.Entries[index].Path));
        public override void Select(AccessibleSelection flags){owner.Focus();owner.selected=index;owner.Invalidate();}
    }
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
