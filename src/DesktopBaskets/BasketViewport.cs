namespace DesktopBaskets;

// One window per basket viewport, regardless of file count. Only visible cells paint icons.
internal sealed class BasketViewport : Control
{
    readonly App app;
    readonly Basket basket;
    readonly ToolTip tip=new();
    int hover=-1,selected=-1;
    readonly HashSet<string> selection=new();
    HashSet<string> marqueeStart=new();
    bool marquee,collapseOnRelease;
    Point marqueeAnchor,marqueePointer;
    Rectangle marqueeBounds;
    Keys marqueeModifiers;
    int anchor=-1;
    string? focusId,anchorId;
    public Entry[] SelectedEntries=>basket.Entries.Where(e=>selection.Contains(e.Id)).ToArray();
    public bool IsSelecting=>marquee;
    public event EventHandler? SelectionChanged;
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
        selection.IntersectWith(basket.Entries.Select(e=>e.Id));
        selected=focusId==null?-1:basket.Entries.FindIndex(e=>e.Id==focusId);
        anchor=anchorId==null?-1:basket.Entries.FindIndex(e=>e.Id==anchorId);
        if(marquee)EndMarquee(false);
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
        return index<basket.Entries.Count&&TileBounds(index).Contains(point)?index:-1;
    }
    Rectangle TileBounds(int index){var rect=ItemBounds(index);return new Rectangle(rect.X+4,rect.Y+4,rect.Width-8,rect.Height-8);}
    void SetFocus(int index,bool setAnchor=false)
    {
        selected=index;focusId=index>=0?basket.Entries[index].Id:null;
        if(setAnchor){anchor=index;anchorId=focusId;}
    }
    void Changed(){Invalidate();SelectionChanged?.Invoke(this,EventArgs.Empty);}
    void SelectItem(int index,Keys modifiers,bool keepSelected=false)
    {
        if((modifiers&Keys.Shift)!=0)
        {
            int start=anchor>=0?anchor:Math.Max(0,selected);
            if((modifiers&Keys.Control)==0)selection.Clear();
            for(int i=Math.Min(start,index);i<=Math.Max(start,index);i++)selection.Add(basket.Entries[i].Id);
            if(anchor<0){anchor=start;anchorId=basket.Entries[start].Id;}
            SetFocus(index);
        }
        else
        {
            string id=basket.Entries[index].Id;
            if((modifiers&Keys.Control)!=0){if(!selection.Remove(id))selection.Add(id);}
            else if(!keepSelected||!selection.Contains(id)){selection.Clear();selection.Add(id);}
            SetFocus(index,true);
        }
        Changed();
    }
    internal void HandlePointerDown(Point point,MouseButtons button,Keys modifiers)
    {
        down=point;collapseOnRelease=false;int index=Hit(point);
        if(button==MouseButtons.Right)
        {
            if(index>=0)SelectItem(index,Keys.None,true);
            else if((modifiers&(Keys.Control|Keys.Shift))==0){selection.Clear();SetFocus(-1,true);Changed();}
            return;
        }
        if(button!=MouseButtons.Left||!ContentRectangle.Contains(point))return;
        if(index>=0)
        {
            bool keep=(modifiers&(Keys.Control|Keys.Shift))==0&&selection.Contains(basket.Entries[index].Id);
            SelectItem(index,modifiers,keep);collapseOnRelease=keep&&selection.Count>1;
            dragging=selection.Contains(basket.Entries[index].Id);return;
        }
        marqueeStart=new HashSet<string>(selection);marqueeModifiers=modifiers;
        if((modifiers&(Keys.Control|Keys.Shift))==0)selection.Clear();
        SetFocus(-1);marquee=true;dragging=false;marqueePointer=point;
        marqueeAnchor=new Point(point.X,point.Y+scrollOffset);marqueeBounds=Rectangle.Empty;
        tip.SetToolTip(this,"");Capture=true;Changed();
    }
    internal void UpdateMarquee(Point point)
    {
        if(!marquee)return;
        marqueePointer=point;
        point=new Point(MathEx.Clamp(point.X,ContentRectangle.Left,ContentRectangle.Right),MathEx.Clamp(point.Y,ContentRectangle.Top,ContentRectangle.Bottom)+scrollOffset);
        marqueeBounds=Rectangle.FromLTRB(Math.Min(marqueeAnchor.X,point.X),Math.Min(marqueeAnchor.Y,point.Y),Math.Max(marqueeAnchor.X,point.X),Math.Max(marqueeAnchor.Y,point.Y));
        var next=(marqueeModifiers&(Keys.Control|Keys.Shift))!=0?new HashSet<string>(marqueeStart):new HashSet<string>();
        if(marqueeBounds.Width>0&&marqueeBounds.Height>0)
        {
            int first=Math.Max(0,(marqueeBounds.Top-ContentTop)/Grid.CellHeight)*Columns;
            int end=Math.Min(basket.Entries.Count,((marqueeBounds.Bottom-ContentTop)/Grid.CellHeight+1)*Columns);
            for(int i=first;i<end;i++)
            {
                var tile=TileBounds(i);tile.Offset(0,scrollOffset);
                if(!tile.IntersectsWith(marqueeBounds))continue;
                string id=basket.Entries[i].Id;
                if((marqueeModifiers&Keys.Control)!=0){if(!next.Remove(id))next.Add(id);}else next.Add(id);
            }
        }
        bool changed=!selection.SetEquals(next);selection.Clear();selection.UnionWith(next);Invalidate();
        if(changed)SelectionChanged?.Invoke(this,EventArgs.Empty);
    }
    void EndMarquee(bool cancel)
    {
        if(!marquee)return;
        marquee=false;marqueeBounds=Rectangle.Empty;
        if(cancel){selection.Clear();selection.UnionWith(marqueeStart);selection.IntersectWith(basket.Entries.Select(e=>e.Id));}
        int focus=basket.Entries.FindIndex(e=>selection.Contains(e.Id));SetFocus(focus,true);
        Capture=false;Changed();
    }
    public void ScrollBy(int rows)=>AutoScrollPosition=new Point(0,Math.Max(0,-AutoScrollPosition.Y+rows*Grid.CellHeight));
    protected override void WndProc(ref Message m)
    {
        // MouseUp and Apps/Shift+F10 already request the file's Shell menu.
        // DefWindowProc otherwise forwards WM_CONTEXTMENU through this child
        // to Explorer, opening a second desktop menu and dismissing the first.
        if(m.Msg==0x7B){m.Result=IntPtr.Zero;return;}
        base.WndProc(ref m);
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollBy(-(e.Delta/120));Invalidate();
        if(marquee)UpdateMarquee(marqueePointer);
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
            bool chosen=selection.Contains(entry.Id);
            if(index==hover||chosen){using var fill=new SolidBrush(chosen?Color.FromArgb(58,Theme.Accent):Theme.Raised);e.Graphics.FillRectangle(fill,tile);}
            if(chosen){using var border=new Pen(Color.FromArgb(145,Theme.Accent));e.Graphics.DrawRectangle(border,tile.X,tile.Y,tile.Width-1,tile.Height-1);}
            var image=app.Icons.Get(entry.Path,app.Manager);
            var imageBox=app.Icons.HasThumbnail(entry.Path)
                ?new Rectangle(tile.X+(tile.Width-48)/2,tile.Y+4,48,36)
                :new Rectangle(tile.X+(tile.Width-32)/2,tile.Y+8,32,32);
            var interpolation=e.Graphics.InterpolationMode;
            e.Graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image,IconCache.Fit(image.Size,imageBox));
            e.Graphics.InterpolationMode=interpolation;
            TextRenderer.DrawText(e.Graphics,entry.DisplayName,Font,new Rectangle(tile.X+3,tile.Y+44,tile.Width-6,Math.Max(20,tile.Height-44)),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.PreserveGraphicsClipping);
            if(Focused&&index==selected){using var focus=new Pen(Theme.Accent){DashStyle=System.Drawing.Drawing2D.DashStyle.Dot};e.Graphics.DrawRectangle(focus,tile);}
        }
        if(marquee&&marqueeBounds.Width>0&&marqueeBounds.Height>0)
        {
            var rect=marqueeBounds;rect.Offset(0,-scrollOffset);
            using var fill=new SolidBrush(Color.FromArgb(32,Theme.Accent));using var border=new Pen(Theme.Accent);
            e.Graphics.FillRectangle(fill,rect);e.Graphics.DrawRectangle(border,rect);
        }
        e.Graphics.Restore(drawing);
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);AutoScrollPosition=new Point(0,scrollOffset);}
    public void RefreshImage(string path)
    {
        for(int index=0;index<basket.Entries.Count;index++)
            if(string.Equals(basket.Entries[index].Path,path,StringComparison.OrdinalIgnoreCase))
            {
                var rect=Rectangle.Intersect(ItemBounds(index),ContentRectangle);
                if(!rect.IsEmpty)Invalidate(rect);
            }
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);Focus();
        if(e.Button==MouseButtons.Left&&EdgeCursor?.Invoke()!=null)return;
        if(e.Button==MouseButtons.Left&&CanScroll&&new Rectangle(ScrollTrack.X-6,ScrollTrack.Y,16,ScrollTrack.Height).Contains(e.Location))
        {
            scrolling=true;thumbStart=e.Y;offsetStart=scrollOffset;Capture=true;
            if(!ScrollThumb.Contains(new Point(ScrollThumb.X,e.Y)))AutoScrollPosition=new Point(0,(e.Y-ScrollTrack.Top)*Math.Max(0,contentHeight-ContentRectangle.Height)/Math.Max(1,ScrollTrack.Height));
            offsetStart=scrollOffset;return;
        }
        HandlePointerDown(e.Location,e.Button,ModifierKeys);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(app.IsStandby&&e.Button==MouseButtons.None)return;
        base.OnMouseMove(e);int next=Hit(e.Location);
        if(marquee){UpdateMarquee(e.Location);Cursor=Cursors.Cross;return;}
        if(scrolling){AutoScrollPosition=new Point(0,offsetStart+(e.Y-thumbStart)*Math.Max(0,contentHeight-ContentRectangle.Height)/Math.Max(1,ScrollTrack.Height-ScrollThumb.Height));return;}
        if(hover!=next)
        {
            hover=next;Cursor=next>=0?Cursors.Hand:Cursors.Default;
            tip.SetToolTip(this,next>=0?basket.Entries[next].DisplayName+"\n原始路徑："+basket.Entries[next].Path:"");Invalidate();
        }
        if(dragging&&e.Button==MouseButtons.Left&&Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)>=8&&selected>=0)
        {
            dragging=false;collapseOnRelease=false;var entries=SelectedEntries;var data=new DataObject();data.SetData("DesktopBaskets.Entries",entries.Select(x=>x.Id).ToArray());
            using var desktopDrag=new DesktopReturnDrag(this,()=>Native.PhysicalCursor,p=>entries.All(app.CanReturnToDesktop)&&app.IsDesktopDrop(p));
            DoDragDrop(data,DragDropEffects.Move);
            if(desktopDrag.DropPoint.HasValue)app.ReturnEntriesToDesktop(entries.Select(x=>x.Id),desktopDrag.DropPoint.Value);
        }
        Cursor=EdgeCursor?.Invoke()??(next>=0?Cursors.Hand:Cursors.Default);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if(marquee&&e.Button==MouseButtons.Left){UpdateMarquee(e.Location);EndMarquee(false);}
        else if(collapseOnRelease&&e.Button==MouseButtons.Left&&selected>=0)SelectItem(selected,Keys.None);
        collapseOnRelease=false;dragging=false;if(scrolling){scrolling=false;Capture=false;}base.OnMouseUp(e);
        int index=Hit(e.Location);
        if(e.Button==MouseButtons.Right&&index>=0)app.ShowEntriesMenu(SelectedEntries,Native.PhysicalCursor);
    }
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture){scrolling=false;dragging=false;if(marquee)EndMarquee(false);}base.OnMouseCaptureChanged(e);}
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
    protected override bool IsInputKey(Keys keyData)=>(keyData&Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.KeyCode==Keys.Escape){if(marquee)EndMarquee(true);else{selection.Clear();Changed();}e.Handled=true;e.SuppressKeyPress=true;return;}
        if(basket.Entries.Count==0)return;
        if(e.Control&&e.KeyCode==Keys.A){selection.UnionWith(basket.Entries.Select(x=>x.Id));SetFocus(Math.Max(0,selected),true);Changed();e.Handled=true;e.SuppressKeyPress=true;return;}
        if(e.KeyCode==Keys.Apps||(e.KeyCode==Keys.F10&&e.Shift))
        {
            if(selected>=0&&selection.Count>0){using var dpi=new Native.PhysicalDpiScope();app.ShowEntriesMenu(SelectedEntries,PointToScreen(ItemBounds(selected).Location));}
            e.Handled=true;e.SuppressKeyPress=true;return;
        }
        int next=Math.Max(0,selected);
        switch(e.KeyCode)
        {
            case Keys.Left:if(selected>=0)next--;break;case Keys.Right:if(selected>=0)next++;break;case Keys.Up:if(selected>=0)next-=Columns;break;case Keys.Down:if(selected>=0)next+=Columns;break;
            case Keys.Home:next=0;break;case Keys.End:next=basket.Entries.Count-1;break;
            case Keys.PageUp:next-=Columns*Math.Max(1,ClientSize.Height/Grid.CellHeight);break;
            case Keys.PageDown:next+=Columns*Math.Max(1,ClientSize.Height/Grid.CellHeight);break;
            case Keys.Enter:if(selected>=0)Theme.Try(()=>Theme.Open(basket.Entries[selected].Path));return;
            case Keys.Space:if(e.Control&&selected>=0){SelectItem(selected,Keys.Control);e.Handled=true;e.SuppressKeyPress=true;}return;
            case Keys.Delete:app.RemoveEntries(basket,SelectedEntries);return;
            default:return;
        }
        next=MathEx.Clamp(next,0,basket.Entries.Count-1);
        if(e.Control&&!e.Shift){SetFocus(next);Changed();}else SelectItem(next,e.Modifiers);
        var rect=ItemBounds(selected);
        if(rect.Top<ContentRectangle.Top)AutoScrollPosition=new Point(0,Math.Max(0,scrollOffset+rect.Top-ContentRectangle.Top));
        else if(rect.Bottom>ContentRectangle.Bottom)AutoScrollPosition=new Point(0,scrollOffset+rect.Bottom-ContentRectangle.Bottom);
        Invalidate();e.Handled=true;
    }
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override AccessibleObject CreateAccessibilityInstance()=>new ViewAccessible(this);
    sealed class ViewAccessible : ControlAccessibleObject
    {
        readonly BasketViewport owner;
        public ViewAccessible(BasketViewport owner):base(owner){this.owner=owner;}
        public override AccessibleStates State=>base.State|AccessibleStates.MultiSelectable|AccessibleStates.ExtSelectable;
        public override AccessibleObject? GetFocused()=>owner.Focused&&owner.selected>=0?GetChild(owner.selected):null;
        public override int GetChildCount()=>owner.basket.Entries.Count;
        public override AccessibleObject? GetChild(int index)=>index>=0&&index<GetChildCount()?new EntryAccessible(owner,index,this):null;
    }
    sealed class EntryAccessible : AccessibleObject
    {
        readonly BasketViewport owner;readonly int index;readonly AccessibleObject parent;
        public EntryAccessible(BasketViewport owner,int index,AccessibleObject parent){this.owner=owner;this.index=index;this.parent=parent;}
        public override string? Name{get=>owner.basket.Entries[index].DisplayName;set{}}
        public override AccessibleObject Parent=>parent;
        public override AccessibleRole Role=>AccessibleRole.ListItem;
        public override string DefaultAction=>"開啟";
        public override Rectangle Bounds=>owner.RectangleToScreen(owner.ItemBounds(index));
        public override AccessibleStates State=>AccessibleStates.Selectable|(owner.selection.Contains(owner.basket.Entries[index].Id)?AccessibleStates.Selected:0)|(owner.Focused&&owner.selected==index?AccessibleStates.Focused:0)|(owner.ItemBounds(index).IntersectsWith(owner.ContentRectangle)?0:AccessibleStates.Offscreen);
        public override void DoDefaultAction()=>Theme.Try(()=>Theme.Open(owner.basket.Entries[index].Path));
        public override void Select(AccessibleSelection flags)
        {
            owner.Focus();string id=owner.basket.Entries[index].Id;
            if((flags&AccessibleSelection.ExtendSelection)!=0)owner.SelectItem(index,Keys.Shift);
            else if((flags&AccessibleSelection.TakeSelection)!=0)owner.SelectItem(index,Keys.None);
            else{if((flags&AccessibleSelection.AddSelection)!=0)owner.selection.Add(id);if((flags&AccessibleSelection.RemoveSelection)!=0)owner.selection.Remove(id);owner.SetFocus(index);owner.Changed();}
        }
    }
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
