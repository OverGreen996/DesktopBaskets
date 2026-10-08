namespace DesktopBaskets;

internal static partial class Verification
{
    internal static object DragPreviewTest(string root)
    {
        Grid.Configure(new Size(76,99));int assertions=0;
        void Check(bool condition,string message){Require(condition,message);assertions++;}
        var lower=new DisplayScreen("lower",new Rectangle(0,0,3440,1440),new Rectangle(0,0,3440,1392),true);
        var upper=new DisplayScreen("upper",new Rectangle(0,-1080,1920,1080),new Rectangle(0,-1080,1920,1040));
        var left=new DisplayScreen("left",new Rectangle(-1920,0,1920,1080),new Rectangle(-1920,0,1920,1040));
        var small=new DisplayScreen("small",new Rectangle(3440,0,1280,720),new Rectangle(3440,0,1280,680));
        var tiny=new DisplayScreen("tiny",new Rectangle(5000,0,320,180),new Rectangle(5000,0,320,140));
        var basket=new Basket{Name="預覽驗證",X=120,Y=150,Width=780,Height=448,Locked=false};
        var before=JsonCodec.Serialize(basket);
        Check(DisplayLayout.TryDrop(basket,new Rectangle(20,-900,780,448),false,upper,out var placement)&&placement.Bounds(false)==new Rectangle(20,-900,780,448),"Upper monitor preview uses negative Y without offset or scaling.");
        Check(DisplayLayout.TryDrop(basket,new Rectangle(-1800,200,780,448),false,left,out placement)&&placement.Bounds(false)==new Rectangle(-1800,200,780,448),"Left monitor preview uses negative X without offset or scaling.");
        Check(DisplayLayout.TryDrop(basket,new Rectangle(1800,-1150,780,448),false,upper,out placement)&&placement.Bounds(false)==new Rectangle(1140,-1080,780,448),"Preview shows the clamped drop rectangle, including both screen edges.");
        Check(DisplayLayout.TryDrop(basket,new Rectangle(3000,1200,780,448),false,lower,out placement)&&placement.Bounds(false)==new Rectangle(2660,944,780,448),"Preview respects the taskbar working area.");
        Check(!DisplayLayout.TryDrop(basket,new Rectangle(5000,0,780,448),false,tiny,out _),"Undersized monitor does not throw or promise an invalid drop.");
        foreach(var screen in new[]{lower,upper,left,small})
        foreach(var proposed in new[]{new Rectangle(screen.Bounds.Left-50,screen.Bounds.Top-40,780,448),new Rectangle(screen.Bounds.Right-10,screen.Bounds.Bottom-10,780,448),new Rectangle(screen.Bounds.Left+40,screen.Bounds.Top+40,1500,900)})
        foreach(bool resize in new[]{false,true})
        {
            Check(DisplayLayout.TryDrop(basket,proposed,resize,screen,out placement)&&screen.WorkingArea.Contains(placement.Bounds(false)),"Drop preview remains fully within its destination work area.");
        }
        Check(JsonCodec.Serialize(basket)==before,"Preview planning never moves the basket or changes its data.");
        var collapsed=new Basket{X=40,Y=40,Width=780,Height=1500,Collapsed=true};
        Check(DisplayLayout.TryDrop(collapsed,new Rectangle(50,-1000,780,Grid.HeaderFor(780)),false,upper,out placement)&&placement.Height==1500&&placement.Bounds(true).Height==Grid.HeaderFor(780),"Collapsed preview and drop preserve expansion height.");
        var physical=Native.DisplayScreens();
        using(var overlay=new DragPreviewWindow())
        {
            foreach(var screen in physical)
            {
                var wanted=new Rectangle(screen.WorkingArea.Left+30,screen.WorkingArea.Top+30,Math.Min(780,screen.WorkingArea.Width-60),Math.Min(448,screen.WorkingArea.Height-60));
                if(wanted.Width<=0||wanted.Height<=0)continue;
                var focus=Native.GetForegroundWindow();overlay.ShowAt(wanted);Application.DoEvents();
                Check(Native.PhysicalWindowBounds(overlay.Handle)==wanted,"Retained preview native bounds differ from physical destination pixels.");
                Check(Native.IsWindowVisible(overlay.Handle),"Preview is not visible after monitor transition.");
                Check(Native.GetForegroundWindow()==focus,"Preview stole foreground activation.");
                long style=Native.GetWindowLongPtr(overlay.Handle,-20).ToInt64();
                Check((style&0x080800A0)==0x080800A0,"Preview lost click-through, layered, no-activate or tool-window styles.");
                overlay.Invalidate();Application.DoEvents();
                Check(Native.IsWindowVisible(overlay.Handle)&&Native.PhysicalWindowBounds(overlay.Handle)==wanted,"Repaint erased or relocated the preview.");
                var borderPoint=new Native.POINT(new Point(wanted.Left+1,wanted.Top+1));
                Check(Native.WindowFromPoint(borderPoint)!=overlay.Handle,"Preview intercepts mouse input at its border.");
            }
            var hwnd=overlay.Handle;overlay.Dispose();Check(!Native.IsWindow(hwnd),"Preview window remained allocated after disposal.");
        }
        var work=Path.Combine(Path.GetFullPath(root),"drag-preview-"+Guid.NewGuid().ToString("N"));
        var store=new Store(work);store.State.Baskets.Add(basket);
        using(var app=new App(store,true))
        {
            app.Manager.Hide();var screen=physical.First(s=>s.WorkingArea.Width>=780&&s.WorkingArea.Height>=448);
            Check(DisplayLayout.TryDrop(basket,new Rectangle(screen.WorkingArea.Right-15,screen.WorkingArea.Top-20,780,448),false,screen,out placement),"Physical destination planning failed.");
            var visiblePreview=placement.Bounds(false);app.PlacePlanned(basket,placement);
            Check(basket.ScreenBounds==visiblePreview&&basket.Monitor==screen.DeviceName,"Committed placement differs from the visible drop preview.");
            basket.Locked=true;var locked=basket.ScreenBounds;placement.X+=10;app.PlacePlanned(basket,placement);
            Check(basket.ScreenBounds==locked,"LOCK no longer prevents a preview placement commit.");
            basket.Locked=false;placement.Monitor="removed-monitor";app.PlacePlanned(basket,placement);
            Check(basket.ScreenBounds==locked,"Disconnected target changed the basket after preview.");
            app.Quit();
        }
        return new{Passed=true,Assertions=assertions,PhysicalMonitorCount=physical.Count,NegativeCoordinates=true,RetainedPreview=true,NoActivation=true,ClickThrough=true,PreviewEqualsDrop=true,DesktopStateIsolated=true};
    }
    internal static void DragPreviewDemo(string root)
    {
        var work=Path.Combine(Path.GetFullPath(root),"drag-demo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        using var dpi=new Native.PhysicalDpiScope();
        var screens=Native.DisplayScreens();
        var source=screens.FirstOrDefault(s=>s.Primary)??screens[0];
        var hosts=screens.Select((screen,index)=>new Form{Text=$"Desktop Baskets · 預覽測試 · 螢幕 {index+1}",FormBorderStyle=FormBorderStyle.None,BackColor=Theme.Background,ShowInTaskbar=true,StartPosition=FormStartPosition.Manual,AutoScaleMode=AutoScaleMode.None,Bounds=screen.WorkingArea}).ToArray();
        var store=new Store(work);var basket=new Basket{Name="拖曳標題跨螢幕 / PREVIEW",X=source.WorkingArea.Left+160,Y=source.WorkingArea.Top+160,Width=780,Height=448,Locked=false};
        store.State.Baskets.Add(basket);using var app=new App(store,true);app.Manager.Hide();using var frame=new BasketWindow(app,basket);
        var trace=new List<object>();Rectangle? last=null;bool matched=true;int completed=0;
        void Save()=>File.WriteAllText(Path.Combine(work,"mouse-verification.json"),JsonCodec.Serialize(new{CompletedDrags=completed,AllPreviewNativeBoundsMatched=matched,Basket=basket.ScreenBounds,PreviewDisposed=!frame.PreviewVisible,Traces=trace}));
        frame.PreviewChanged+=(bounds,valid)=>
        {
            var native=frame.PreviewNativeBounds;matched&=native==bounds&&frame.PreviewVisible;
            last=valid?bounds:null;trace.Add(new{Bounds=bounds,Native=native,Visible=frame.PreviewVisible,Valid=valid,Cursor=Native.PhysicalCursor});
        };
        frame.DragFinished+=()=>
        {
            completed++;trace.Add(new{Committed=basket.ScreenBounds,MatchesPreview=last==basket.ScreenBounds});
            int index=screens.ToList().FindIndex(s=>s.DeviceName==basket.Monitor);if(index<0)index=screens.ToList().IndexOf(source);
            frame.AttachToHost(hosts[index].Handle);frame.RefreshItems();Save();
        };
        bool closing=false;
        for(int index=0;index<hosts.Length;index++)
        {
            var host=hosts[index];var screen=screens[index];
            host.Paint+=(_,e)=>
            {
                using var pen=new Pen(Theme.Accent);e.Graphics.DrawRectangle(pen,2,2,host.ClientSize.Width-5,host.ClientSize.Height-5);
                e.Graphics.DrawString(screen.DeviceName+" · 拖曳標題到這個螢幕",SystemFonts.MessageBoxFont,Brushes.White,24,24);
            };
            host.FormClosed+=(_,_)=>
            {
                if(closing)return;closing=true;Save();frame.Dispose();foreach(var other in hosts)if(!other.IsDisposed)other.Close();app.Quit();
            };
            host.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)host.Close();};
            host.Show();var area=screen.WorkingArea;Native.SetWindowPos(host.Handle,IntPtr.Zero,area.X,area.Y,area.Width,area.Height,0x10);
        }
        frame.AttachToHost(hosts[screens.ToList().IndexOf(source)].Handle);frame.Show();
        File.WriteAllText(Path.Combine(work,"screens.json"),JsonCodec.Serialize(new{Screens=screens,Start=basket.ScreenBounds}));
        Application.Run(app);
        foreach(var host in hosts)host.Dispose();
    }
}
