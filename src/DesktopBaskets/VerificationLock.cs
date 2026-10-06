namespace DesktopBaskets;
internal static partial class Verification
{
    public static object LockUiTest(string root)
    {
        Directory.CreateDirectory(root);string data=Path.Combine(root,"lock-ui",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        string path=Path.Combine(data,"滑鼠驗證.txt");File.WriteAllText(path,"unchanged");
        var store=new Store(Path.Combine(data,"config"));var basket=new Basket{Name="LOCK VERIFY",Width=Grid.MinWidth+Grid.CellWidth*4,Height=Grid.MinHeight+Grid.CellHeight*2};
        store.State.Baskets.Add(basket);store.Add(basket,path);var app=new App(store,true);app.Manager.Hide();
        using var owner=new Form{Text="Desktop Baskets · LOCK 滑鼠驗證",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.CenterScreen,Size=basket.ScreenBounds.Size,BackColor=Theme.Background};
        using var frame=new BasketWindow(app,basket);var observations=new List<object>();
        void Observe(string kind,Control? target=null,MouseEventArgs? pointer=null)
        {
            if(observations.Count<128)observations.Add(new{Kind=kind,basket.Locked,frame.FrameStatus,Bounds=basket.ScreenBounds.ToString(),Target=target?.AccessibleName,Cursor=target?.Cursor.ToString(),X=pointer?.X,Y=pointer?.Y,Selected=frame.Viewport.SelectedEntries.Length});
            File.WriteAllText(Path.Combine(root,"lock-ui-observations.json"),JsonCodec.Serialize(new{basket.Locked,frame.FrameStatus,Bounds=basket.ScreenBounds.ToString(),Selected=frame.Viewport.SelectedEntries.Length,SourceUnchanged=File.Exists(path)&&File.ReadAllText(path)=="unchanged",Observations=observations},new JsonOptions{WriteIndented=true}));
        }
        var header=frame.Controls.Cast<Control>().Single(c=>c.Dock==DockStyle.Top);
        var button=header.Controls.Cast<Control>().Single(c=>c.AccessibleName=="鎖定位置與大小");
        button.Click+=(_,_)=>{frame.RefreshItems();Observe("lock click",button);};
        foreach(var control in frame.Controls.Cast<Control>().Concat(header.Controls.Cast<Control>()))
        {
            control.MouseMove+=(_,e)=>{if(e.Button!=MouseButtons.None)Observe("drag",control,e);};
            control.MouseUp+=(_,e)=>Observe("release",control,e);
        }
        frame.Viewport.SelectionChanged+=(_,_)=>Observe("selection",frame.Viewport);
        owner.Shown+=(_,_)=>{using var dpi=new Native.PhysicalDpiScope();var point=owner.PointToScreen(Point.Empty);basket.X=point.X;basket.Y=point.Y;frame.AttachToHost(owner.Handle);frame.Show();frame.RefreshItems();Observe("shown");};
        owner.FormClosed+=(_,_)=>app.Quit();
        using var timeout=new System.Windows.Forms.Timer{Interval=600000};timeout.Tick+=(_,_)=>owner.Close();timeout.Start();
        try{owner.Show();app.Manager.BeginInvoke(new Action(()=>app.Manager.Hide()));Application.Run(app);return new{Completed=true,RealBasketAndGlass=true,SourceUnchanged=File.Exists(path)&&File.ReadAllText(path)=="unchanged"};}
        finally{app.Quit();}
    }
    public static object LockTest(string root)
    {
        var store=new Store(Path.Combine(root,"lock-test",Guid.NewGuid().ToString("N")));
        var basket=new Basket{Name="LOCK VERIFY",Width=Grid.MinWidth,Height=Grid.MinHeight};store.State.Baskets.Add(basket);
        var app=new App(store,true);app.Manager.Hide();int clicks=0;
        using var host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(80,80),ClientSize=new Size(1300,700)};
        using var frame=new BasketWindow(app,basket);
        try
        {
            host.Show();var origin=host.PointToScreen(Point.Empty);basket.X=origin.X;basket.Y=origin.Y;
            frame.AttachToHost(host.Handle);frame.Show();Application.DoEvents();
            var header=frame.Controls.Cast<Control>().Single(c=>c.Dock==DockStyle.Top);
            var footer=frame.Controls.Cast<Control>().Single(c=>c.Dock==DockStyle.Bottom);
            var button=header.Controls.Cast<Control>().Single(c=>c.AccessibleName=="鎖定位置與大小");
            Point FramePoint(Control c,Point p)=>frame.PointToClient(c.PointToScreen(p));
            void GlassMessage(int code,Point p,int buttons=0)=>SendMessage(frame.GlassHandle,code,new IntPtr(buttons),new IntPtr((p.X&0xffff)|((p.Y&0xffff)<<16)));
            void Click(Point p){GlassMessage(0x201,p,1);GlassMessage(0x202,p);Application.DoEvents();frame.RefreshItems();clicks++;}
            Require(button.Width>=100,"Lock text is outside the clickable lock target.");
            foreach(int width in new[]{Grid.MinWidth,Grid.MinWidth+Grid.CellWidth*8})
            {
                basket.Width=width;frame.AttachToHost(host.Handle);frame.RefreshItems();
                var circle=FramePoint(button,new Point(7,7));Click(circle);
                Require(basket.Locked,"Glass-forwarded circle click did not lock exactly once.");
                var before=basket.ScreenBounds;
                foreach(var p in new[]{new Point(1,1),new Point(width-2,1),new Point(1,frame.Height-2),new Point(width-2,frame.Height-2),new Point(width/2,1),new Point(width/2,frame.Height-2),new Point(1,frame.Height/2),new Point(width-2,frame.Height/2),new Point(width/3,header.Height/2)})
                {
                    GlassMessage(0x200,p);var target=frame.PointerTarget(frame.PointToScreen(p));
                    Require(target.Cursor==Cursors.Default&&Control.FromHandle(frame.GlassHandle)!.Cursor==Cursors.Default,"Locked frame advertised a resize or movement cursor.");
                    GlassMessage(0x201,p,1);GlassMessage(0x200,p+new Size(25,25),1);GlassMessage(0x202,p+new Size(25,25));
                    Require(basket.ScreenBounds==before&&!frame.Controls.Cast<Control>().Any(c=>c.Capture),"Locked frame accepted a geometry drag.");
                }
                var flag=header.Controls.Cast<Control>().Single(c=>c.AccessibleName=="分類選單");
                GlassMessage(0x200,FramePoint(flag,new Point(flag.Width/2,flag.Height/2)));
                Require(flag.Cursor==Cursors.Default,"Locked flag still displayed a hand cursor.");
                var text=FramePoint(button,new Point(45,7));GlassMessage(0x200,text);
                Require(button.Cursor==Cursors.Hand,"Lock control lost its clickable cursor.");
                app.EnterStandby();Require(frame.FrameStatus=="LOCKED","Standby hid the locked state.");
                Click(text);Require(!basket.Locked&&!app.IsStandby&&frame.ResizeEdgesAvailable==8,"Lock text could not unlock and wake the basket.");
                var corner=new Point(width-2,frame.Height-2);GlassMessage(0x200,corner);
                Require(footer.Cursor==Cursors.SizeNWSE,"Unlock did not restore the corner resize cursor.");
                Click(text);Require(basket.Locked,"Text could not lock again.");
                Require(footer.Cursor==Cursors.Default,"Lock retained a stale resize cursor before mouse movement.");
                var saved=new Store(store.Root);Require(saved.State.Baskets.Single().Locked,"Lock was not persisted.");
                var outside=FramePoint(button,new Point(-20,7));GlassMessage(0x201,text,1);GlassMessage(0x202,outside);
                Require(basket.Locked&&!button.Capture,"Releasing outside the lock toggled its state or leaked capture.");
                Click(text);Require(!basket.Locked,"Repeated lock/unlock failed.");
            }
            return new{Passed=true,CompactAndWide=true,LockCircleAndTextClicks=clicks,GlassInputTogglesExactlyOnce=true,LockedFrameHasDefaultCursor=true,OnlyLockControlHasHandCursor=true,MoveAndResizeBlocked=true,UnlockRestoresResizeCursor=true,StandbyPreservesLockAndClickWakes=true,StatePersists=true,OutsideReleaseCancels=true};
        }
        finally{host.Close();app.Quit();}
    }
}
