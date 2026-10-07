namespace DesktopBaskets;

internal sealed class BasketWindow : Form
{
    readonly App app;
    readonly BasketGlassWindow glass;
    IntPtr attachedHandle,attachedHost;
    Rectangle attachedBounds;
    public int NativeVisibilityChanges {get;private set;}
    public int GlassVisibilityChanges=>glass.NativeVisibilityChanges;
    public IntPtr GlassHandle=>glass.Handle;
    public int GlassPaintCount=>glass.PaintCount;
    public int ChromePaintCount {get;private set;}
    public static uint TransparentKey=>(uint)(Theme.Panel.R|(Theme.Panel.G<<8)|(Theme.Panel.B<<16));
    public Basket Basket { get; }
    readonly Panel header=new ArtPanel(){Dock=DockStyle.Top,Height=Grid.Header};
    readonly BasketViewport items;
    readonly SharePanel? shared;
    public BasketViewport Viewport => items;
    readonly Panel footer=new ArtPanel(){Dock=DockStyle.Bottom,Height=Grid.Footer};
    public int FrameOrdinal=>Math.Max(1,app.Store.State.Baskets.IndexOf(Basket)+1);
    public int ObjectCount=>Basket.Shared?app.Sharing.Snapshot?.files.Length??0:Basket.Entries.Count;
    public string FrameStatus=>Basket.Locked?"LOCKED":app.IsStandby?"STANDBY":"READY";
    readonly Button lockButton,menuButton;
    readonly Control[] captureTargets;
    readonly ToolTip help=new();
    bool suppressMenuClick;
    Point dragStart;
    Rectangle before,preview;
    bool moving,resizing;
    [Flags] enum Edge {None=0,Left=1,Right=2,Top=4,Bottom=8}
    Edge resizeEdge;
    Control? dragControl;
    public int ResizeEdgesAvailable=>Basket.Locked||Basket.Collapsed?0:8;
    bool outline;
    public BasketWindow(App app,Basket basket)
    {
        this.app=app; Basket=basket;
        items=new BasketViewport(app,basket);
        if(basket.Shared){shared=new SharePanel(app,basket);items.Controls.Add(shared);shared.BringToFront();app.Sharing.Changed+=RefreshStatus;}
        items.EdgeCursor=()=>CursorForEdge(ResizeAt(PointToClient(Cursor.Position)));
        Theme.Form(this); FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;TopLevel=false;
        AutoScaleMode=AutoScaleMode.None; StartPosition=FormStartPosition.Manual;
        BackColor=Theme.Panel; Padding=Padding.Empty; MinimumSize=new Size(Grid.MinWidth,Grid.Header);
        AllowDrop=true; items.BackColor=Theme.Panel;items.TabStop=true;
        items.MouseDown+=(_,_)=>items.Focus();
        header.BackColor=Theme.Panel;header.Paint+=(_,e)=>PaintHeader(e.Graphics);
        lockButton=new LockButton(()=>basket.Locked,()=>FrameStatus,()=>Grid.Compact(Width)){AccessibleName="鎖定位置與大小",Size=new Size(18,18),Margin=new Padding(0),FlatStyle=FlatStyle.Flat,BackColor=Theme.Panel,ForeColor=Theme.Text,Cursor=Cursors.Hand};
        lockButton.FlatAppearance.BorderSize=0;lockButton.Click+=(_,_)=>app.ToggleLock(basket);
        menuButton=new FrameMenuButton{AccessibleName="分類選單",FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};
        menuButton.FlatAppearance.BorderSize=0;menuButton.Click+=(_,_)=>{if(suppressMenuClick){suppressMenuClick=false;return;}app.BasketMenu(basket).Show(Cursor.Position);};
        captureTargets=new Control[]{lockButton,menuButton,items,header,footer,this};
        help.SetToolTip(menuButton,"分類選單：改名、鎖定、加入檔案");help.SetToolTip(lockButton,"點擊鎖定或解鎖位置與大小");help.SetToolTip(header,"拖曳標頭移動；雙擊改名；拖曳邊緣按格數縮放");
        help.SetToolTip(footer,"拖曳右下十字按格數縮放；鎖定時停用");
        header.Controls.Add(lockButton);header.Controls.Add(menuButton);header.Resize+=(_,_)=>LayoutChrome();
        header.MouseDoubleClick+=(_,e)=>{if(e.Button==MouseButtons.Left&&!Basket.Locked)app.EditBasket(Basket);};
        footer.Paint+=(_,e)=>{ChromePaintCount++;FrameArt.ReferenceFooter(e.Graphics,footer.Width,footer.Height,FrameOrdinal,app.Store.State.Revision,app.Store.State.ModifiedUtc,Basket.Columns,Basket.Rows,Basket.Locked);};
        header.MouseDown+=(_,e)=>{if(ResizeAt(PointToClient(Cursor.Position))==Edge.None)StartDrag(e,false);};
        KeyPreview=true; KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)CancelDrag();};
        Controls.Add(items); Controls.Add(footer); Controls.Add(header);
        foreach(var surface in new Control[]{this,header,footer,items,menuButton})
        {
            surface.MouseDown+=(_,e)=>{var edge=ResizeAt(PointToClient(Cursor.Position));if(e.Button==MouseButtons.Left&&edge!=Edge.None){resizeEdge=edge;StartDrag(e,true,surface);}};
            surface.MouseMove+=(_,e)=>{if(moving||resizing)MoveDrag(e);else UpdateResizeCursor(surface,surface.PointToScreen(e.Location));};
            surface.MouseUp+=(_,_)=>EndDrag();surface.MouseCaptureChanged+=(_,_)=>CancelLostCapture();
        }
        SizeChanged+=(_,_)=>LayoutChrome();LayoutChrome();
        items.Resize+=(_,_)=>LayoutChrome();
        DragEnter+=OnDragEnter; DragDrop+=OnDrop;
        // Child controls also participate in OLE file drop.
        foreach(var control in new Control[]{items,header,footer}) { control.AllowDrop=true; control.DragEnter+=OnDragEnter; control.DragDrop+=OnDrop; }
        glass=new BasketGlassWindow(app,this);
        RefreshItems();
    }
    static Button HeaderButton(string text,string label,Action action)
    {
        var b=new Button{Text=text,AccessibleName=label,Size=new Size(28,24),Margin=new Padding(0,0,2,0),FlatStyle=FlatStyle.Flat,BackColor=Theme.Panel,ForeColor=Theme.Text,Cursor=Cursors.Hand};
        b.FlatAppearance.BorderSize=0; b.Click+=(_,_)=>action();
        var tip=new ToolTip();tip.SetToolTip(b,label);b.Disposed+=(_,_)=>tip.Dispose();return b;
    }
    internal void OnDragEnter(object? sender,DragEventArgs e)
    {
        app.Wake();
        if(e.Data?.GetDataPresent("DesktopBaskets.Entries")==true||e.Data?.GetDataPresent("DesktopBaskets.Entry")==true)e.Effect=(e.AllowedEffect&DragDropEffects.Move)!=0?DragDropEffects.Move:DragDropEffects.Link;
        else if(e.Data?.GetDataPresent(DataFormats.FileDrop)==true)e.Effect=(e.AllowedEffect&DragDropEffects.Link)!=0?DragDropEffects.Link:DragDropEffects.Copy;
    }
    internal void OnDrop(object? sender,DragEventArgs e)
    {
        if(e.Data?.GetData("DesktopBaskets.Entries") is string[] ids)app.TransferEntries(Basket,ids);
        else if(e.Data?.GetData("DesktopBaskets.Entry") is string id)app.TransferEntry(Basket,id);
        else if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)app.AddPaths(Basket,paths);
    }
    public void RefreshItems()
    {
        if(Basket.Locked)CancelDrag();
        items.RefreshItems();LayoutChrome();header.Cursor=Basket.Locked?Cursors.Default:Cursors.SizeAll;
        Cursor=footer.Cursor=items.Cursor=Cursors.Default;menuButton.Cursor=Basket.Locked?Cursors.Default:Cursors.Hand;
        if(glass!=null)glass.Cursor=Cursors.Default;
        header.Invalidate(true);footer.Invalidate();
        AccessibleName=$"分類 {FrameOrdinal:00}：{Basket.Name}";AccessibleDescription=$"{ObjectCount} 個圖示；{FrameStatus}；{Basket.Columns} × {Basket.Rows} 格；原始路徑不變";
        help.SetToolTip(header,Basket.Name+(Basket.Locked?"\n位置與大小已鎖定；點擊 LOCK 解鎖":"\n拖曳標頭移動；雙擊改名；拖曳邊緣按格數縮放"));
    }
    public void RefreshStatus(){header.Invalidate(true);footer.Invalidate();}
    void PaintHeader(Graphics g)
    {
        ChromePaintCount++;
        FrameArt.Prepare(g);
        FrameArt.ReferenceHeader(g,header.Width,header.Height,FrameOrdinal,Basket.Name,ObjectCount,FrameStatus);
    }
    void LayoutChrome()
    {
        header.Height=Grid.HeaderFor(Width);footer.Height=Grid.FooterFor(Width);
        if(shared!=null)shared.Bounds=new Rectangle(Grid.ContentLeft,Grid.ContentTopFor(Width),Math.Max(1,items.ClientSize.Width-Grid.Side),Math.Max(1,items.ClientSize.Height-Grid.ContentTopFor(Width)-Grid.ContentBottomFor(Width)));
        if(menuButton==null||lockButton==null)return;
        float s=header.Width/1537f,t=header.Height/102f;
        int flagWidth=Grid.Compact(Width)?(int)Math.Round(header.Height*63.0/102):Math.Max(13,header.Width-(int)(1474*s));
        menuButton.Bounds=new Rectangle(header.Width-flagWidth,0,flagWidth,header.Height);
        lockButton.Bounds=Grid.Compact(Width)?new Rectangle(header.Width-176,52,176-flagWidth,20):new Rectangle(header.Width-215,(int)(60*t),215-flagWidth,22);
    }
    public void ScrollBy(int rows)
    {
        items.ScrollBy(rows);
    }
    public void Attach(DesktopShell shell)=>AttachToHost(shell.Host);
    internal void AttachToHost(IntPtr host)
    {
        var hwnd=Handle;
        bool reattach=attachedHandle!=hwnd||attachedHost!=host||Native.GetParent(hwnd)!=host;
        if(reattach)
        {
            long style=Native.GetWindowLongPtr(hwnd,-16).ToInt64();
            Native.SetWindowLongPtr(hwnd,-16,new IntPtr((style&~0x80000000L)|0x40000000L));
            Native.SetParent(hwnd,host);
            if(Native.GetParent(hwnd)!=host)throw new InvalidOperationException("無法把整理籃附加到桌面，這個分類尚未顯示。");
            Native.SetWindowLongPtr(hwnd,-20,new IntPtr(Native.GetWindowLongPtr(hwnd,-20).ToInt64()|0x80000));
            if(!Native.SetLayeredWindowAttributes(hwnd,TransparentKey,255,1))throw new InvalidOperationException("Windows 無法設定整理框的清晰前景。");
        }
        var p=new Native.POINT(new Point(Basket.X,Basket.Y));Native.ScreenToClient(host,ref p);
        var bounds=new Rectangle(p.X,p.Y,Basket.Width,Basket.Collapsed?Grid.HeaderFor(Basket.Width):Basket.Height);
        if(reattach||attachedBounds!=bounds)
            if(!Native.SetWindowPos(hwnd,IntPtr.Zero,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x10|(reattach?0x20u:0)))throw new InvalidOperationException("Windows 無法設定整理籃位置。");
        attachedHandle=hwnd;attachedHost=host;attachedBounds=bounds;
        glass.AttachToHost(host);
        items.Visible=!Basket.Collapsed;footer.Visible=!Basket.Collapsed;
        items.RefreshItems();
    }
    public Control PointerTarget(Point screenPoint)
    {
        // A press owns its move/release sequence even across transparent
        // sibling surfaces or outside the original target's rectangle.
        foreach(var control in captureTargets)
            if(control.Capture)return control;
        if(shared!=null)
        {
            Control? Captured(Control parent)
            {
                if(parent.Capture)return parent;
                foreach(Control child in parent.Controls){var found=Captured(child);if(found!=null)return found;}
                return null;
            }
            var captured=Captured(shared);if(captured!=null)return captured;
        }
        var p=PointToClient(screenPoint);
        if(header.Bounds.Contains(p))
        {
            p.Offset(-header.Left,-header.Top);
            if(menuButton.Bounds.Contains(p))return menuButton;
            if(lockButton.Bounds.Contains(p))return lockButton;
            return header;
        }
        if(footer.Visible&&footer.Bounds.Contains(p))return footer;
        if(shared!=null&&shared.Visible&&shared.ClientRectangle.Contains(shared.PointToClient(screenPoint)))
        {
            Control target=shared;
            while(target.GetChildAtPoint(target.PointToClient(screenPoint),GetChildAtPointSkip.Invisible) is Control child)target=child;
            return target;
        }
        return items;
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if(glass==null||glass.IsDisposed)return;
        if(Visible&&!TopLevel)glass.ShowBehind();else glass.Hide();
    }
    protected override void WndProc(ref Message m)
    {
        // Stop context requests from header/footer controls at the basket,
        // whose native parent belongs to Explorer rather than this process.
        if(m.Msg==0x7B){m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x18)NativeVisibilityChanges++;
        base.WndProc(ref m);
    }
    Edge ResizeAt(Point p)
    {
        if(ResizeEdgesAvailable==0)return Edge.None;
        int margin=p.Y>=Height-32&&(p.X<32||p.X>=Width-32)?32:(p.X<12||p.X>=Width-12)&&(p.Y<12||p.Y>=Height-12)?12:6;
        Edge edge=Edge.None;if(p.X<margin)edge|=Edge.Left;else if(p.X>=Width-margin)edge|=Edge.Right;
        if(p.Y<margin)edge|=Edge.Top;else if(p.Y>=Height-margin)edge|=Edge.Bottom;return edge;
    }
    void UpdateResizeCursor(Control surface,Point screenPoint)
    {
        var edge=ResizeAt(PointToClient(screenPoint));
        if(edge!=Edge.None)surface.Cursor=CursorForEdge(edge)!;
        else if(surface==header)surface.Cursor=Basket.Locked?Cursors.Default:Cursors.SizeAll;
        else if(surface==footer||surface==this)surface.Cursor=Cursors.Default;
        else if(surface==menuButton)surface.Cursor=Basket.Locked?Cursors.Default:Cursors.Hand;
    }
    static Cursor? CursorForEdge(Edge edge)=>edge==Edge.None?null:edge is Edge.Left or Edge.Right?Cursors.SizeWE:edge is Edge.Top or Edge.Bottom?Cursors.SizeNS:
        edge==(Edge.Left|Edge.Top)||edge==(Edge.Right|Edge.Bottom)?Cursors.SizeNWSE:Cursors.SizeNESW;
    void StartDrag(MouseEventArgs e,bool resize,Control? source=null)
    {
        if(e.Button!=MouseButtons.Left||Basket.Locked||resize&&Basket.Collapsed)return;
        before=Basket.ScreenBounds;preview=before;dragStart=Native.PhysicalCursor;
        moving=!resize;resizing=resize;
        if(resize&&source==null)resizeEdge=Edge.Right|Edge.Bottom;
        dragControl=source??(resize?footer:header);if(resize&&dragControl==menuButton)suppressMenuClick=true;dragControl.Capture=true;
    }
    void MoveDrag(MouseEventArgs e)
    {
        using var dpi=new Native.PhysicalDpiScope();
        if(!moving&&!resizing)return;
        if(outline)ControlPaint.DrawReversibleFrame(preview,Color.White,FrameStyle.Dashed);outline=false;
        var cursor=Native.PhysicalCursor;var delta=new Size(cursor.X-dragStart.X,cursor.Y-dragStart.Y);
        if(moving)preview=app.Magnetize(Basket,new Rectangle(before.Location+delta,before.Size));
        else
        {
            int w=before.Width+((resizeEdge&Edge.Right)!=0?delta.Width:(resizeEdge&Edge.Left)!=0?-delta.Width:0);
            int h=before.Height+((resizeEdge&Edge.Bottom)!=0?delta.Height:(resizeEdge&Edge.Top)!=0?-delta.Height:0);
            Size size;
            try
            {
                var area=DisplayLayout.At(cursor,Native.DisplayScreens()).WorkingArea;
                if(area.Width<Grid.MinWidth||area.Height<Grid.MinHeight)return;
                size=Grid.FitProportional(before.Size,new Size(w,h),area.Size);
            }
            catch(Exception ex)when(ex is InvalidOperationException||ex is System.ComponentModel.Win32Exception)
            {CancelDrag();return;}
            preview=new Rectangle((resizeEdge&Edge.Left)!=0?before.Right-size.Width:before.Left,
                (resizeEdge&Edge.Top)!=0?before.Bottom-size.Height:before.Top,size.Width,size.Height);
        }
        ControlPaint.DrawReversibleFrame(preview,Color.White,FrameStyle.Dashed);outline=true;
    }
    void EndDrag()
    {
        if(!moving&&!resizing)return;
        var target=preview;var wasResize=resizing;CancelDrag();
        app.Place(Basket,target,wasResize,Native.PhysicalCursor);
    }
    void CancelLostCapture() { if((moving||resizing)&&dragControl?.Capture!=true)CancelDrag(); }
    void CancelDrag()
    {
        using var dpi=new Native.PhysicalDpiScope();
        if(outline)ControlPaint.DrawReversibleFrame(preview,Color.White,FrameStyle.Dashed);
        outline=false;moving=false;resizing=false;if(dragControl!=null)dragControl.Capture=false;dragControl=null;
    }
    protected override void Dispose(bool disposing) { if(disposing){if(shared!=null)app.Sharing.Changed-=RefreshStatus;CancelDrag();help.Dispose();glass?.Dispose();}base.Dispose(disposing); }
    sealed class LockButton : Button
    {
        readonly Func<bool> locked,compact;readonly Func<string> status;bool pressed;
        public LockButton(Func<bool> locked,Func<string> status,Func<bool> compact){this.locked=locked;this.status=status;this.compact=compact;}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg is 0x201 or 0x203 or 0x202)
            {
                long packed=m.LParam.ToInt64();var point=new Point(unchecked((short)(packed&0xffff)),unchecked((short)((packed>>16)&0xffff)));
                if(m.Msg!=0x202){pressed=Enabled&&ClientRectangle.Contains(point);if(pressed){Focus();Capture=true;}}
                else
                {
                    bool click=pressed&&Enabled&&ClientRectangle.Contains(point);pressed=false;Capture=false;
                    // Color-key foreground and glass share this target. Use
                    // the delivered release position instead of checking the
                    // native window under the latest physical cursor.
                    if(click)PerformClick();
                }
                m.Result=IntPtr.Zero;return;
            }
            base.WndProc(ref m);
        }
        protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)pressed=false;base.OnMouseCaptureChanged(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Panel);FrameArt.Prepare(e.Graphics);bool state=locked();int y=compact()?2:1;
            using var signal=new SolidBrush(Theme.Accent);e.Graphics.FillEllipse(signal,0,y,14,14);
            FrameArt.Tracked(e.Graphics,"STATUS : "+status(),22,y+1,12,compact()?.2f:1.2f,Theme.Text);
            if(state)
            {
                using var pen=new Pen(Theme.Background,1);e.Graphics.TranslateTransform(0,y);e.Graphics.ScaleTransform(14/18f,14/18f);
                e.Graphics.DrawRectangle(pen,5,8,8,6);e.Graphics.DrawArc(pen,6,2,6,10,180,180);e.Graphics.DrawLine(pen,9,10,9,12);
            }
            AccessibleDescription=state?"已鎖定位置與大小；點擊解鎖":"未鎖定；點擊固定位置與大小";
        }
    }
    sealed class FrameMenuButton:Button
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Panel);FrameArt.Prepare(e.Graphics);FrameArt.Flag(e.Graphics,Width,Height);
            if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(1,1,Width-2,Height-2));
        }
    }
}
