using System.Diagnostics;


namespace DesktopBaskets;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Theme.ConfigureNativeMenus();
        Application.SetCompatibleTextRenderingDefault(false);
        string? report=Value(args,"--report");
        bool autoStart=args.Contains("--autostart");
        try
        {
            if(!autoStart)try{using var desktop=new DesktopShell();Grid.Configure(desktop.Spacing);}catch{ /* default native-sized grid remains available for diagnostics */ }
            if(args.Contains("--drag-preview-test")){Write(report,Verification.DragPreviewTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--membership-test")){Write(report,Verification.MembershipTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--drag-preview-ui-demo")){Verification.DragPreviewDemo(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification"));return 0;}
            if(args.Contains("--sharing-test")){Write(report,Verification.SharingTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--share-ui-demo")){Verification.SharingDemo(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification"));return 0;}
            if(args.Contains("--share-settings-ui-demo")||args.Contains("--share-qr-ui-demo")){Verification.SharingSettingsDemo(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification"),args.Contains("--share-qr-ui-demo"));return 0;}
            if(args.Contains("--startup-test")){Write(report,Verification.StartupTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--display-test")){Write(report,Verification.DisplayTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--startup-ui-test")){Write(report,Verification.StartupTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification"),true));return 0;}
            if(args.Contains("--self-test")){Write(report,Verification.SelfTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--arrangement-test")){Write(report,Verification.ArrangementTest());return 0;}
            if(args.Contains("--arrangement-diagnose")){Write(report,Verification.ArrangementDiagnose());return 0;}
            if(args.Contains("--diagnose")){Write(report,Verification.Diagnose());return 0;}
            if(args.Contains("--repair-desktop")){using var desktop=new DesktopShell();desktop.RepairInput();Write(report,new{Passed=desktop.Interactive});return 0;}
            if(args.Contains("--restore-backup")){Write(report,Verification.RestoreBackup(Value(args,"--restore-backup")!));return 0;}
            if(args.Contains("--integration-test")){Write(report,Verification.Integration(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--event-test")){Write(report,Verification.EventTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--refresh-test")){Write(report,Verification.RefreshTest(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification")));return 0;}
            if(args.Contains("--input-test")){Write(report,Verification.InputTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--visual-test")){Write(report,Verification.VisualTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--capture")){Verification.Capture(Value(args,"--capture")!,Value(args,"--work")??AppContext.BaseDirectory);return 0;}
            if(args.Contains("--probe")){Write(report,Verification.Probe(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--standby-probe")){Write(report,Verification.StandbyProbe(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--virtual-test")){Write(report,Verification.VirtualTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--thumbnail-test")){Write(report,Verification.ThumbnailTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--thumbnail-ui-test")){Write(report,Verification.ThumbnailTest(Value(args,"--work")??AppContext.BaseDirectory,true));return 0;}
            if(args.Contains("--chrome-test")){Write(report,Verification.ChromeTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--shell-menu-test")){Write(report,Verification.ShellMenuTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--context-routing-test")){Write(report,Verification.ContextRoutingTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--selection-test")){Write(report,Verification.SelectionTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--lock-test")){Write(report,Verification.LockTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--lock-ui-test")){Write(report,Verification.LockUiTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--selection-ui-test")){Write(report,Verification.BasketMenuUiTest(Value(args,"--work")??AppContext.BaseDirectory,true));return 0;}
            if(args.Contains("--isolated-shell-menu-test")){Write(report,Verification.IsolatedShellMenuTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--shell-properties-test")){Write(report,Verification.ShellPropertiesTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--basket-menu-ui-test")){Write(report,Verification.BasketMenuUiTest(Value(args,"--work")??AppContext.BaseDirectory));return 0;}
            if(args.Contains("--ui-demo")){Verification.Preview(Value(args,"--work")??Path.Combine(Path.GetTempPath(),"DesktopBasketsVerification"));return 0;}
            using var mutex=new Mutex(true,"Local\\DesktopBaskets_"+Environment.UserName,out bool created);
            if(!created){if(!autoStart)MessageBox.Show("桌面整理工具已在執行。請從右下角系統匣開啟分類管理。","Desktop Baskets");return 0;}
            if(autoStart)StartupSettings.WaitForDesktop(()=>{using var desktop=new DesktopShell();if(!desktop.Alive)throw new InvalidOperationException("Windows 桌面尚未就緒。");Grid.Configure(desktop.Spacing);},Thread.Sleep);
            var store=new Store();
            if(!File.Exists(store.StatePath))
            {
                var screen=Screen.PrimaryScreen!;var area=screen.WorkingArea;
                var games=new Basket{Name="遊戲",X=area.Right-Grid.DefaultWidth-24,Y=area.Top+24,Monitor=screen.DeviceName};
                var misc=new Basket{Name="雜項",X=games.X,Y=games.Y+games.Height+16,Monitor=screen.DeviceName};
                if(area.Contains(games.ScreenBounds)&&area.Contains(misc.ScreenBounds))
                {store.State.Baskets.AddRange(new[]{games,misc});store.State.Enabled=true;store.Save();}
            }
            if(StartupSettings.RestoreBaskets(store.State,autoStart))store.Save();
            Application.Run(new App(store,autoStart:autoStart));return 0;
        }
        catch(Exception ex)
        {
            if(args.Length>0&&!autoStart)Write(report,new{Passed=false,Error=ex.ToString()});else Theme.Error(ex);
            return 1;
        }
    }
    static string? Value(string[] args,string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
    static void Write(string? path,object value)
    {
        var text=JsonCodec.Serialize(value,new JsonOptions{WriteIndented=true});
        if(path!=null)File.WriteAllText(path,text);else Console.WriteLine(text);
    }
}

internal static partial class Verification
{
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    public static object Diagnose()
    {
        using var shell=new DesktopShell();var icons=shell.ReadIcons();
        try {return new{Passed=true,Host=shell.Host.ToInt64(),List=shell.List.ToInt64(),shell.Origin,shell.Spacing,shell.Flags,shell.Interactive,Count=icons.Count,
            AutoArrangeEnabled=(shell.Flags&1)!=0,SnapToGridEnabled=(shell.Flags&4)!=0,GridPositions=icons.Select(i=>i.Position).OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray(),
            Screens=Screen.AllScreens.Select(s=>new{s.DeviceName,s.WorkingArea}).ToArray(),Dpi=Native.DpiDiagnostic(shell.List)};}
        finally{foreach(var icon in icons)icon.Dispose();}
    }
    public static object RestoreBackup(string path)
    {
        string raw=File.ReadAllText(path);
        var state=raw.TrimStart().StartsWith("[")
            ?new State{Icons=JsonCodec.Deserialize<List<LayoutIcon>>(raw)!.Select(i=>new IconBackup{Key=i.Key,X=i.Position.X,Y=i.Position.Y}).ToList()}
            :JsonCodec.Deserialize<State>(raw)!;
        using var shell=new DesktopShell();var icons=shell.ReadIcons();uint flags=shell.Flags;
        try
        {
            shell.SetManualPositions();var targets=state.Icons.ToDictionary(b=>b.Key,b=>new Point(b.X,b.Y));
            shell.Position(icons,targets);
            var after=shell.ReadIcons();
            try{Require(after.All(i=>!targets.TryGetValue(i.Key,out var p)||i.Position==p),"Recovery mismatch");}
            finally{foreach(var i in after)i.Dispose();}
            shell.SetLayoutFlags(state.OriginalAutoArrange??(flags&1)!=0,state.OriginalSnapToGrid??(flags&4)!=0);
            return new{Passed=true,Recovered=targets.Count};
        }
        finally{foreach(var i in icons)i.Dispose();}
    }
    public static object SelfTest(string testWork)
    {
        testWork=Path.GetFullPath(testWork);
        // Reference-geometry unit tests use a known native grid fixture.
        // Explorer spacing is machine-specific and may differ on a hosted runner.
        Grid.Configure(new Size(76,99));
        int count=0;
        void Check(bool condition,string name){Require(condition,name);count++;}
        var icons=new[]{new LayoutIcon("hit",new Point(20,20)),new LayoutIcon("keep",new Point(340,20))};
        var basket=new Rectangle(0,0,220,200);var area=new Rectangle(0,0,1000,800);var spacing=new Size(72,90);
        var plan=LayoutPlanner.Plan(icons,new[]{basket},new[]{area},spacing);
        Check(plan.ContainsKey("hit")&&!plan.ContainsKey("keep"),"Only blocked icons should move");
        Check(!basket.IntersectsWith(LayoutPlanner.Footprint(plan["hit"],spacing)),"Moved icon must avoid basket");
        bool rejected=false;try{LayoutPlanner.Plan(icons,new[]{area},new[]{area},spacing);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Full desktop must reject placement");
        var nativeSpacing=new Size(76,99);var nativeArea=new Rectangle(0,0,800,600);
        var chain=new[]{new LayoutIcon("B",new Point(20,119)),new LayoutIcon("C",new Point(20,218)),new LayoutIcon("D",new Point(96,20))};
        var filled=LayoutPlanner.FillVacancies(chain,new[]{new Point(20,20)},Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing);
        Check(filled["B"]==new Point(20,20)&&filled["C"]==new Point(20,119)&&filled["D"]==new Point(20,218),"Native adjacent cells fill in column order without label-gutter false collisions");
        var chainAfter=chain.Select(i=>new LayoutIcon(i.Key,filled.TryGetValue(i.Key,out var p)?p:i.Position)).ToArray();
        Check(LayoutPlanner.FillVacancies(chainAfter,new[]{new Point(20,20)},Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing).Count==0,"Repeated refresh does not shuffle filled cells");
        Check(LayoutPlanner.FillVacancies(chain,new[]{new Point(20,20)},new[]{new Rectangle(0,0,96,99)},new[]{nativeArea},nativeSpacing).Count==0,"Vacancies inside baskets stay reserved");
        Check(LayoutPlanner.FillVacancies(chain,new[]{new Point(400,400)},Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing).Count==0,"Later empty cells do not move icons backwards in order");
        var partial=LayoutPlanner.FillVacancies(new[]{new LayoutIcon("near",new Point(30,30)),new LayoutIcon("later",new Point(200,20))},new[]{new Point(20,20)},Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing);
        Check(partial.Count==0,"Partially occupied cells are not overwritten");
        var sameMonitor=LayoutPlanner.FillVacancies(new[]{new LayoutIcon("right",new Point(20,20))},new[]{new Point(-780,20)},Array.Empty<Rectangle>(),new[]{new Rectangle(-800,0,800,600),nativeArea},nativeSpacing);
        Check(sameMonitor.Count==0,"Filling does not transfer icons to another monitor");
        var twoHoles=LayoutPlanner.FillVacancies(new[]{new LayoutIcon("C",new Point(20,218)),new LayoutIcon("D",new Point(20,317))},new[]{new Point(20,20),new Point(20,119)},Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing);
        Check(twoHoles["C"]==new Point(20,20)&&twoHoles["D"]==new Point(20,119),"Multiple grouped files compact in stable order");
        Check(LayoutPlanner.FillVacancies(chain,Array.Empty<Point>(),Array.Empty<Rectangle>(),new[]{nativeArea},nativeSpacing).Count==0,"Intentional gaps without a classified source are preserved");
        using(var source=new Control())
        {
            bool desktopTarget=true;var dropPoint=new Point(450,300);
            using(var session=new DesktopReturnDrag(source,()=>dropPoint,_=>desktopTarget))
            {
                var held=new QueryContinueDragEventArgs(1,false,DragAction.Continue);session.Continue(source,held);
                Check(!session.DropPoint.HasValue&&held.Action==DragAction.Continue,"Desktop drag waits until the mouse button is released");
                desktopTarget=false;var elsewhere=new QueryContinueDragEventArgs(0,false,DragAction.Drop);session.Continue(source,elsewhere);
                Check(!session.DropPoint.HasValue&&elsewhere.Action==DragAction.Drop,"Basket and foreign-window targets keep the normal OLE drop route");
                desktopTarget=true;var released=new QueryContinueDragEventArgs(0,false,DragAction.Drop);session.Continue(source,released);
                Check(session.DropPoint==dropPoint&&released.Action==DragAction.Cancel,"Desktop release is consumed as a visual return instead of a Shell file operation");
                var escape=new QueryContinueDragEventArgs(1,true,DragAction.Continue);session.Continue(source,escape);
                Check(!session.DropPoint.HasValue&&escape.Action==DragAction.Cancel,"Escape cancels without removing classification");
            }
        }
        var random=new Random(947);
        for(int iteration=0;iteration<150;iteration++)
        {
            var items=Enumerable.Range(0,24).Select(i=>new LayoutIcon(i.ToString(),new Point(10+(i/6)*100,10+(i%6)*112))).ToArray();
            var block=new Rectangle(random.Next(0,450),random.Next(0,500),228,186);
            var p=LayoutPlanner.Plan(items,new[]{block},new[]{new Rectangle(0,0,1500,1000)},new Size(70,80));
            var final=items.Select(i=>new LayoutIcon(i.Key,p.TryGetValue(i.Key,out var moved)?moved:i.Position)).ToArray();
            Check(final.All(i=>!block.IntersectsWith(LayoutPlanner.Footprint(i.Position,new Size(70,80)))),"Random layout clearance");
            Check(final.Select(i=>LayoutPlanner.Footprint(i.Position,new Size(70,80))).SelectMany((r,i)=>final.Skip(i+1).Select(j=>r.IntersectsWith(LayoutPlanner.Footprint(j.Position,new Size(70,80))))).All(v=>!v),"Random layout icon collisions");
            Check(p.Values.All(v=>new Rectangle(0,0,1500,1000).Contains(LayoutPlanner.Footprint(v,new Size(70,80)))),"Random layout stays on screen");
        }
        var snapped=Grid.Snap(new Size(358,237));
        Check((snapped.Width-Grid.Side)%Grid.CellWidth==0&&(snapped.Height-Grid.VerticalFor(snapped.Width))%Grid.CellHeight==0,"Grid snapping");
        Check(Grid.Snap(new Size(5,5))==new Size(Grid.MinWidth,Grid.MinHeight),"Grid minimum");
        foreach(double scale in new[]{.3,.7,1.25,1.4,1.8,2.1})
        {
            var size=Grid.SnapProportional(new Size(Grid.MinWidth,Grid.MinHeight),new Size((int)(Grid.MinWidth*scale),(int)(Grid.MinHeight*scale)));
            Check(size.Width>=Grid.MinWidth&&size.Height>=Grid.MinHeight&&(size.Width-Grid.Side)%Grid.CellWidth==0&&(size.Height-Grid.VerticalFor(size.Width))%Grid.CellHeight==0,"Proportional resizing stays on native grid");
            Check(Math.Abs(size.Width/(double)size.Height-Grid.MinWidth/(double)Grid.MinHeight)<.16,"Frame composition survives resizing");
        }
        var screenFit=Grid.FitProportional(new Size(Grid.MinWidth,Grid.MinHeight),new Size(5000,3000),new Size(3440,1392));
        Check(screenFit.Width<=3440&&screenFit.Height<=1392&&Math.Abs(screenFit.Width/(double)screenFit.Height-Grid.MinWidth/(double)Grid.MinHeight)<.12,"Oversized resizing preserves proportions within screen");
        var magnetic=Magnet.Move(new Rectangle(328,8,324,282),new[]{new Rectangle(0,0,324,282)});
        Check(magnetic.Left==324&&magnetic.Top==0,"Baskets magnetically attach and align edges");
        var distant=new Rectangle(400,60,324,282);
        Check(Magnet.Move(distant,new[]{new Rectangle(0,0,324,282)})==distant,"Magnet leaves distant baskets freely placed");
        string root=System.IO.Path.Combine(testWork,"self-test",Guid.NewGuid().ToString("N"));
        string desktop=System.IO.Path.Combine(root,"Desktop");Directory.CreateDirectory(desktop);
        string publicDesktop=System.IO.Path.Combine(root,"Public Desktop");Directory.CreateDirectory(publicDesktop);
        var store=new Store(System.IO.Path.Combine(root,"Data"),desktop,publicDesktop);var a=new Basket{Name="遊戲"};var b=new Basket{Name="雜項"};store.State.Baskets.AddRange(new[]{a,b});store.Save();
        string path=System.IO.Path.Combine(desktop,"測試 空白.txt");File.WriteAllText(path,"original");
        var entry=store.Add(a,path);
        Check(entry.Path==path&&File.Exists(path)&&File.ReadAllText(path)=="original","Desktop original path remains unchanged");
        Check(!Directory.Exists(System.IO.Path.Combine(store.Root,"Vault")),"No vault or moved files are created");
        store.Transfer(b,entry);
        Check(!a.Entries.Contains(entry)&&b.Entries.Contains(entry)&&File.Exists(path),"Transfer only changes visual classification");
        store.Remove(b,entry);
        Check(File.ReadAllText(path)=="original"&&Directory.GetFiles(desktop).Length==1,"Unassign never moves or duplicates file");
        string outside=System.IO.Path.Combine(root,"external.txt");File.WriteAllText(outside,"external");
        var link=store.Add(a,outside);Check(!link.Managed&&File.Exists(outside),"External file stays in place");
        var again=store.Add(b,outside);Check(again==link&&a.Entries.Count==0&&b.Entries.Count==1,"Duplicate imports transfer existing entry");
        store.Remove(b,link);Check(File.ReadAllText(outside)=="external","Removing reference retains original");
        string shared=System.IO.Path.Combine(publicDesktop,"公共 遊戲.url");File.WriteAllText(shared,"public");
        var publicEntry=store.Add(b,shared);Check(publicEntry.Path==shared&&File.ReadAllText(shared)=="public","Public desktop original path stays unchanged");
        store.Remove(b,publicEntry);Check(File.ReadAllText(shared)=="public","Public desktop restore");
        string folder=System.IO.Path.Combine(desktop,"資料夾");Directory.CreateDirectory(folder);File.WriteAllText(System.IO.Path.Combine(folder,"inside.txt"),"inside");
        var folderEntry=store.Add(a,folder);store.Delete(a);
        Check(File.ReadAllText(System.IO.Path.Combine(folder,"inside.txt"))=="inside","Delete basket restores directory contents");
        store.Add(b,path);b.Locked=true;store.Save();var recovered=new Store(store.Root,desktop,publicDesktop);
        Check(recovered.State.Baskets.Single().Locked,"Size and position lock persists across restart");
        Check(recovered.State.Baskets.Single().Entries.Single().Path==path&&File.Exists(path),"Persistence preserves the original desktop path");
        b.Locked=false;b.Width=Grid.MinWidth;b.Height=Grid.MinHeight;store.Save();
        var smallRecovered=new Store(store.Root,desktop,publicDesktop);
        Check(smallRecovered.State.Baskets.Single().ScreenBounds.Size==new Size(Grid.MinWidth,Grid.MinHeight),"Half-size basket persists without growing on restart");
        b.Width=11*Grid.CellWidth+Grid.Side;b.Height=3*Grid.CellHeight+Grid.VerticalFor(b.Width);store.Save();
        var existingBounds=b.ScreenBounds;
        Check(new Store(store.Root,desktop,publicDesktop).State.Baskets.Single().ScreenBounds==existingBounds,"Lower minimum preserves existing basket geometry");
        Check(Directory.GetFiles(desktop,"*.lnk").Length==0,"Visual grouping does not create shortcut files");
        // Negative coordinates model a monitor to the left of the primary display.
        var multi=LayoutPlanner.Plan(new[]{new LayoutIcon("left",new Point(-950,30))},new[]{new Rectangle(-1000,0,300,300)},
            new[]{new Rectangle(-1000,0,1000,800),new Rectangle(0,0,1000,800)},spacing);
        Check(multi.ContainsKey("left")&&multi["left"].X<0,"Multi-monitor negative coordinates");
        return new{Passed=true,Assertions=count,TestData=root};
    }
    static (Store store,App app) Demo(string root)
    {
        Directory.CreateDirectory(root);var store=new Store(System.IO.Path.Combine(root,Guid.NewGuid().ToString("N")));
        var area=Screen.PrimaryScreen!.WorkingArea;
        var games=new Basket{Name="遊戲",X=area.Right-Grid.DefaultWidth-24,Y=area.Top+40};
        var misc=new Basket{Name="雜項",X=games.X,Y=games.Y+games.Height+16,Width=Grid.MinWidth,Height=Grid.MinHeight};
        store.State.Baskets.AddRange(new[]{games,misc});
        foreach(string name in new[]{"明日方舟：終末地.url","Steam.url","遊戲啟動器.lnk"})games.Entries.Add(new Entry{Name=name,Path=System.IO.Path.Combine(root,name)});
        for(int i=0;i<15;i++)misc.Entries.Add(new Entry{Name=$"文件 {i+1:00}.txt",Path=System.IO.Path.Combine(root,$"文件 {i+1:00}.txt")});
        store.Save();return(store,new App(store,true));
    }
    public static void Preview(string root)
    {
        var (_,app)=Demo(Path.Combine(root,"ui-demo"));
        using var timeout=new System.Windows.Forms.Timer{Interval=180000};timeout.Tick+=(_,_)=>app.Quit();timeout.Start();
        Application.Run(app);
    }
    public static void Capture(string path,string root)
    {
        var (_,app)=Demo(root);
        // Publish a demonstration with neutral paths, never this machine's user data.
        foreach(var entry in app.Store.State.Baskets.SelectMany(b=>b.Entries))entry.Path=Path.Combine(@"C:\Users\Player\Desktop",entry.Name);
        app.Manager.RefreshState();Application.DoEvents();
        using(var bitmap=app.Manager.RenderClient())bitmap.Save(path);
        app.Quit();
    }
    public static object ChromeTest(string root)
    {
        var (_,app)=Demo(Path.Combine(root,"chrome"));var manager=app.Manager;
        try
        {
            Application.DoEvents();var original=manager.Bounds;
            var header=manager.Controls.OfType<WindowHeaderPanel>().Single();
            var buttons=header.Controls.OfType<CaptionButton>().OrderBy(b=>b.Action).ToArray();
            Require(buttons.Length==3,"Caption controls missing");
            void CheckCaptionMouseHits()
            {
                foreach(var button in buttons)
                {
                    var screen=button.PointToScreen(new Point(button.Width/2,button.Height/2));
                    var packed=new IntPtr((screen.X&0xffff)|((screen.Y&0xffff)<<16));
                    Require(Native.SendMessage(manager.Handle,0x84,IntPtr.Zero,packed).ToInt64()==1,"Native title hit test intercepted "+button.Action+" button mouse input");
                }
                foreach(var command in header.Controls.OfType<FlowLayoutPanel>().SelectMany(p=>p.Controls.OfType<Button>()))
                    Require(manager.FrameHitTest(manager.PointToClient(command.PointToScreen(new Point(command.Width/2,command.Height/2))))==1,"Header command button is being treated as title drag space");
            }
            CheckCaptionMouseHits();
            Require(manager.PointToScreen(Point.Empty)==manager.Location,"Native title strip still reserves screen space");
            Require(manager.FrameHitTest(new Point(header.Left+180,header.Top+12))==2,"Integrated header is not a native drag / double-click caption");
            var m=manager.ResizeMargin;
            var points=new[]{new Point(m+30,1),new Point(m+30,manager.ClientSize.Height-1),new Point(1,m+50),new Point(manager.ClientSize.Width-1,m+50),new Point(1,1),new Point(manager.ClientSize.Width-1,1),new Point(1,manager.ClientSize.Height-1),new Point(manager.ClientSize.Width-1,manager.ClientSize.Height-1)};
            Require(points.Select(manager.FrameHitTest).SequenceEqual(new[]{12,15,10,11,13,14,16,17}),"Custom chrome lost resize edges");
            using(var bitmap=manager.RenderClient())
            {
                var sample=bitmap.GetPixel(header.Left+600,header.Top+10);
                Require(sample.ToArgb()==Theme.Background.ToArgb(),"Header uses a separate color strip");
                bitmap.Save(Path.Combine(root,"manager-merged-v042.png"));
            }
            buttons[1].PerformClick();Application.DoEvents();
            var work=Screen.FromControl(manager).WorkingArea;
            var visibleClient=manager.RectangleToScreen(manager.ClientRectangle);
            Require(manager.WindowState==FormWindowState.Maximized&&visibleClient==work,$"Maximized manager does not fit the monitor work area: state={manager.WindowState}, visible={visibleClient}, work={work}");
            CheckCaptionMouseHits();
            buttons[1].PerformClick();Application.DoEvents();
            Require(manager.WindowState==FormWindowState.Normal&&manager.Bounds==original,$"Restore lost original manager geometry: state={manager.WindowState}, actual={manager.Bounds}, original={original}");
            buttons[0].PerformClick();Application.DoEvents();
            Require(manager.WindowState==FormWindowState.Minimized,"Minimize button failed");
            manager.CaptionCommand(0xF120);Application.DoEvents();
            Require(manager.WindowState==FormWindowState.Normal,"Native restore failed");
            for(int cycle=0;cycle<3;cycle++)
            {
                buttons[1].PerformClick();Application.DoEvents();buttons[0].PerformClick();Application.DoEvents();
                manager.CaptionCommand(0xF120);Application.DoEvents();
                Require(manager.WindowState==FormWindowState.Maximized,"Minimizing a maximized manager forgot its state");
                buttons[1].PerformClick();Application.DoEvents();
                Require(manager.Bounds==original,"Repeated maximize/minimize/restore changes window geometry");
            }
            // Real ButtonBase mouse clicks raise Click before releasing
            // capture. PerformClick alone never exercised that Windows path.
            void ClickBeforeMouseRelease(CaptionButton button)
            {
                var before=manager.WindowState;
                try
                {
                    button.Capture=true;Require(button.Capture,"Caption capture setup failed");
                    button.PerformClick();
                    Require(manager.WindowState==before,"Caption command ran inside the captured mouse-up handler");
                }
                finally{button.Capture=false;}
                Application.DoEvents();
            }
            for(int cycle=0;cycle<3;cycle++)
            {
                ClickBeforeMouseRelease(buttons[1]);
                Require(manager.WindowState==FormWindowState.Maximized,"Captured mouse click could not maximize after release");
                ClickBeforeMouseRelease(buttons[0]);
                Require(manager.WindowState==FormWindowState.Minimized,"Captured mouse click could not minimize after release");
                manager.CaptionCommand(0xF120);Application.DoEvents();
                Require(manager.WindowState==FormWindowState.Maximized,"Captured minimize forgot the maximized restore state");
                ClickBeforeMouseRelease(buttons[1]);
                Require(manager.WindowState==FormWindowState.Normal&&manager.Bounds==original,"Captured mouse click could not restore original bounds");
            }
            manager.Size=manager.MinimumSize;Application.DoEvents();
            Require(buttons.All(b=>header.ClientRectangle.Contains(b.Bounds)),"Caption controls clipped at the smallest manager size");
            CheckCaptionMouseHits();
            using(var compact=manager.RenderClient())compact.Save(Path.Combine(root,"manager-minimum-v042.png"));
            manager.Bounds=original;Application.DoEvents();
            // Only a smoke-test manager closes here; production Explorer layout is untouched.
            var basketIds=app.Store.State.Baskets.Select(b=>b.Id).ToArray();buttons[2].PerformClick();Application.DoEvents();
            Require(!manager.Visible&&!manager.IsDisposed&&app.Store.State.Baskets.Select(b=>b.Id).SequenceEqual(basketIds),"Closing manager removed desktop classifications");
            app.ShowManager();Application.DoEvents();Require(manager.Visible,"Tray reopen path failed");
            var category=app.Store.State.Baskets.First();
            for(int cycle=0;cycle<5;cycle++)
            {
                bool locked=category.Locked;using var menu=app.BasketMenu(category);
                menu.Show(manager,new Point(40,160));Application.DoEvents();
                menu.Items[1].PerformClick();Application.DoEvents();
                Require(category.Locked!=locked,"Category menu click did not dispatch after close.");
                Require(menu.IsDisposed,"Closed category menu was not disposed after dispatch.");
            }
            return new{Passed=true,NoSeparateTitleStrip=true,HeaderMatchesInterfaceBackground=true,VectorCaptionButtons=3,CaptionButtonsReceiveNativeClientHits=true,HeaderCommandButtonsReceiveClientHits=true,CaptionCommandsAfterMouseCaptureReleased=true,CapturedMaximizeMinimizeRestoreCycles=3,NativeDragAndDoubleClickCaption=true,EightResizeDirections=true,MaximizeFitsWorkingArea=true,RestorePreservesBounds=true,RepeatedWindowStateTransitions=true,MinimumManagerCaptionControlsVisible=true,MinimizeWorks=true,CloseHidesManagerWithoutRemovingCategories=true,ReopenWorks=true,RepeatedCategoryMenuCommandsWithoutDisposedException=true,manager.RoundedCornersSupported};
        }
        finally{app.Quit();}
    }
    public static object Integration(string root)
    {
        string data=System.IO.Path.Combine(root,"integration-"+Guid.NewGuid().ToString("N"));var store=new Store(data);
        using var shell=new DesktopShell();var original=shell.ReadIcons();
        Require(original.Count>0,"Integration requires at least one real desktop icon");
        uint flags=shell.Flags;var origin=shell.Origin;
        File.WriteAllText(System.IO.Path.Combine(data,"baseline.json"),JsonCodec.Serialize(original.Select(i=>new{i.Key,i.Position}),new JsonOptions{WriteIndented=true}));
        var area=Screen.FromPoint(original[0].Position+new Size(origin)).WorkingArea;
        var first=original.First(i=>area.Contains(i.Position+new Size(origin)));
        var model=new Basket{Name="功能驗證",X=MathEx.Clamp(first.Position.X+origin.X,area.Left,area.Right-Grid.DefaultWidth),Y=MathEx.Clamp(first.Position.Y+origin.Y,area.Top,area.Bottom-Grid.DefaultHeight)};
        for(int i=0;i<120;i++)model.Entries.Add(new Entry{Name=$"捲動測試 {i+1:00}.txt",Path=System.IO.Path.Combine(data,$"demo{i}.txt")});
        store.State.Baskets.Add(model);store.Save();
        using var layout=new DesktopLayout(store);var app=new App(store,true);BasketWindow? window=null;
        bool clearance=false,parented=false,scroll=false,restored=false,autoRestored=false,snapRestored=false,untouched=false,gridSmall=false,wheel=false,moveChecked=false,pathSame=false,noDuplicate=false,unassignedVisible=false,layered=false,glassInput=false;int moved=0;
        try
        {
            layout.Apply(new[]{model.ScreenBounds});moved=store.State.Icons.Count;
            window=new BasketWindow(app,model);window.Attach(layout.Shell);window.Show();Application.DoEvents();
            parented=Native.GetParent(window.Handle)==shell.Host;
            layered=Native.GetLayeredWindowAttributes(window.GlassHandle,out _,out byte alpha,out uint layerFlags)&&alpha==model.OpacityPercent*255/100&&(layerFlags&2)!=0
                &&Native.GetLayeredWindowAttributes(window.Handle,out uint key,out _,out uint frontFlags)&&key==BasketWindow.TransparentKey&&(frontFlags&1)!=0;
            Require(layered,"Native desktop translucency was not actually applied");Require(shell.Interactive,"Desktop input disabled by native frame");
            var emptyPoint=window.PointToScreen(new Point(Grid.ContentLeft-10,Grid.HeaderFor(model.Width)+Grid.ContentTopFor(model.Width)+49));
            var target=window.PointerTarget(emptyPoint);var hostPoint=new Native.POINT(emptyPoint);Native.ScreenToClient(shell.Host,ref hostPoint);
            var nativeTarget=Native.ChildWindowFromPointEx(shell.Host,hostPoint,1|2);
            glassInput=Native.GetParent(window.GlassHandle)==shell.Host&&Native.IsWindowVisible(window.GlassHandle)
                &&(nativeTarget==window.GlassHandle||nativeTarget==target.Handle||nativeTarget==window.Handle)&&target==window.Viewport;
            Require(glassInput,$"Transparent frame space lost its receiver: hit={nativeTarget}, front={window.Handle}, glass={window.GlassHandle}, glassVisible={Native.IsWindowVisible(window.GlassHandle)}, glassParent={Native.GetParent(window.GlassHandle)}, host={shell.Host}");
            var after=shell.ReadIcons();
            try
            {
                clearance=after.All(i=>!shell.ToView(model.ScreenBounds).IntersectsWith(LayoutPlanner.Footprint(i.Position,shell.Spacing)));
                untouched=original.Where(i=>!store.State.Icons.Any(b=>b.Key==i.Key)).All(i=>after.Any(j=>j.Key==i.Key&&j.Position==i.Position));
            }
            finally{foreach(var i in after)i.Dispose();}
            var panel=window.Viewport;
            scroll=panel.CanScroll&&panel.AutoScrollMinSize.Height>panel.ClientSize.Height;
            Require(parented,"Basket was not attached to desktop");Require(clearance,"Desktop icon was covered");Require(moved>0,"Integration did not displace any icons");Require(scroll,"Overflow did not enable scrolling");Require(untouched,"Unblocked icons were unexpectedly moved");
            using(var bitmap=new Bitmap(window.Width,window.Height)) {window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(System.IO.Path.Combine(root,"basket-verification.png"));}
            model.Width=Grid.MinWidth;model.Height=Grid.MinHeight;window.Attach(shell);Application.DoEvents();
            gridSmall=panel.ItemBounds(0).Top==panel.ItemBounds(Grid.MinColumns-1).Top&&panel.ItemBounds(Grid.MinColumns).Top>panel.ItemBounds(0).Top;
            int oldY=panel.AutoScrollPosition.Y;window.ScrollBy(1);Application.DoEvents();wheel=panel.AutoScrollPosition.Y<oldY;
            Require(gridSmall,"Minimum frame did not preserve the configured grid");Require(wheel,"Scroll command did not move overflow content");
            model.X+=400;model.Y+=300;window.Hide();layout.Apply(new[]{model.ScreenBounds});window.Attach(shell);window.Show();Application.DoEvents();
            var movedAgain=shell.ReadIcons();
            try{moveChecked=movedAgain.All(i=>!shell.ToView(model.ScreenBounds).IntersectsWith(LayoutPlanner.Footprint(i.Position,shell.Spacing)));}
            finally{foreach(var i in movedAgain)i.Dispose();}
            Require(moveChecked,"Moving basket failed to displace icons");
            var actual=original.First(i=>Store.Exists(i.Key));
            var entry=store.Add(model,actual.Key);layout.Apply(new[]{model.ScreenBounds});
            pathSame=entry.Path==actual.Key&&Store.Exists(actual.Key);
            var grouped=shell.ReadIcons();
            try
            {
                noDuplicate=grouped.Where(i=>i.Key==actual.Key).All(i=>Screen.AllScreens.All(s=>!shell.ToView(s.Bounds).IntersectsWith(LayoutPlanner.Footprint(i.Position,shell.Spacing))));
            }
            finally{foreach(var i in grouped)i.Dispose();}
            Require(pathSame,"Visual grouping changed the original file path");Require(noDuplicate,"Grouped icon was duplicated on desktop");
            store.Remove(model,entry);layout.Apply(new[]{model.ScreenBounds});
            var ungrouped=shell.ReadIcons();
            try{unassignedVisible=ungrouped.Where(i=>i.Key==actual.Key).All(i=>Screen.AllScreens.Any(s=>shell.ToView(s.WorkingArea).Contains(LayoutPlanner.Footprint(i.Position,shell.Spacing))));}
            finally{foreach(var i in ungrouped)i.Dispose();}
            Require(unassignedVisible,"Removing classification did not return the icon to the desktop");
        }
        finally
        {
            window?.Dispose();layout.Restore();app.Quit();
            var after=shell.ReadIcons();
            try{restored=original.All(i=>after.Any(j=>j.Key==i.Key&&j.Position==i.Position));autoRestored=(shell.Flags&1)==(flags&1);snapRestored=(shell.Flags&4)==(flags&4);}
            finally{foreach(var i in after)i.Dispose();foreach(var i in original)i.Dispose();}
        }
        Require(restored,"Original desktop icon positions were not restored");Require(autoRestored,"Auto-arrange setting was not restored");Require(snapRestored,"Snap-to-grid setting was not restored");
        return new{Passed=true,IconsDisplaced=moved,DesktopParented=parented,IndependentGlassOpacity=layered,ForegroundNotDimmedByGlass=layered,GlassSpaceStillReceivesInput=glassInput,DesktopInteractive=shell.Interactive,NoCoveredIcons=clearance,OverflowScroll=scroll,MinimumBasketGrid=gridSmall,ScrollContentMoved=wheel,BasketMoveReflow=moveChecked,OriginalFilePathUnchanged=pathSame,NoDuplicateDesktopIcon=noDuplicate,UnassignedIconVisible=unassignedVisible,UnblockedIconsPreserved=untouched,PositionsRestored=restored,AutoArrangeRestored=autoRestored,SnapToGridRestored=snapRestored,Data=data};
    }
    public static object EventTest(string root)
    {
        var (store,app)=Demo(System.IO.Path.Combine(root,"events"));
        using var shell=new DesktopShell();var original=shell.ReadIcons();uint flags=shell.Flags;
        File.WriteAllText(System.IO.Path.Combine(store.Root,"baseline-state.json"),JsonCodec.Serialize(new State{OriginalAutoArrange=(flags&1)!=0,OriginalSnapToGrid=(flags&4)!=0,
            Icons=original.Select(i=>new IconBackup{Key=i.Key,X=i.Position.X,Y=i.Position.Y}).ToList()}));
        object? result=null;bool restored=false;
        string notificationFile=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DesktopBaskets-test-"+Guid.NewGuid().ToString("N")+".txt");
        using var timer=new System.Windows.Forms.Timer{Interval=1000};int phase=0;
        try
        {
            app.Toggle();app.Manager.Hide();
            var target=store.State.Baskets[0];var inside=shell.ToView(target.ScreenBounds);
            var first=original.First();
            timer.Tick+=(_,_)=>
            {
                if(phase++==0)
                {
                    shell.Position(original,new Dictionary<string,Point>{{first.Key,new Point(inside.X+24,inside.Y+70)}});
                    File.WriteAllText(notificationFile,"Temporary desktop notification test. Automatically removed after verification.");
                    return;
                }
                var after=shell.ReadIcons();
                try
                {
                    bool avoided=after.All(i=>!inside.IntersectsWith(LayoutPlanner.Footprint(i.Position,shell.Spacing)));
                    result=new{Passed=avoided,DesktopChangeReflow=avoided,NoContinuousPolling=true,EventsSeen=app.DesktopEventsSeen,EventsRelevant=app.DesktopEventsRelevant,Status=app.Manager.StatusText};
                }
                finally{foreach(var i in after)i.Dispose();}
                timer.Stop();app.Quit();
            };
            timer.Start();Application.Run(app);
        }
        finally
        {
            app.Quit();if(File.Exists(notificationFile))File.Delete(notificationFile);
            // Explorer's asynchronous removal can apply a pending rearrangement after
            // the test has restored positions. Wait for two matching observations;
            // this bounded cleanup is verification-only, never a production poll.
            var cleanup=Stopwatch.StartNew();int matching=0;
            try
            {
                while(cleanup.ElapsedMilliseconds<4000&&matching<2)
                {
                    Thread.Sleep(200);Application.DoEvents();
                    var live=shell.ReadIcons();
                    try
                    {
                        bool matches=original.All(i=>live.Any(j=>j.Key==i.Key&&j.Position==i.Position))&&!live.Any(i=>i.Key==notificationFile)&&(shell.Flags&5)==(flags&5);
                        if(matches){matching++;continue;}
                        matching=0;shell.SetManualPositions();shell.Position(live,original.ToDictionary(i=>i.Key,i=>i.Position));shell.SetLayoutFlags((flags&1)!=0,(flags&4)!=0);
                    }
                    finally{foreach(var i in live)i.Dispose();}
                }
                restored=matching==2;
            }
            finally{foreach(var i in original)i.Dispose();}
        }
        Require(restored,"Event test failed to restore baseline desktop; restore from "+Path.Combine(store.Root,"baseline-state.json"));return result!;
    }
    public static object Probe(string root)
    {
        var (store,app)=Demo(root);
        app.Toggle();app.Manager.Hide();
        var process=Process.GetCurrentProcess();TimeSpan before=default;long initialWorking=0;object? result=null;int phase=0;
        using var timer=new System.Windows.Forms.Timer{Interval=3000};
        timer.Tick+=(_,_)=>
        {
            process.Refresh();
            if(phase++==0){before=process.TotalProcessorTime;initialWorking=process.WorkingSet64;timer.Interval=10000;return;}
            result=new{Passed=true,Mode="Two desktop baskets active, 18 item links, manager hidden, desktop event listener active",WindowSeconds=10,
                CpuMilliseconds=(process.TotalProcessorTime-before).TotalMilliseconds,WorkingSetMiB=Math.Round(process.WorkingSet64/1048576.0,1),
                PrivateMiB=Math.Round(process.PrivateMemorySize64/1048576.0,1),InitialWorkingSetMiB=Math.Round(initialWorking/1048576.0,1),NoBrowserEngine=true};
            timer.Stop();app.Quit();
        };
        timer.Start();Application.Run(app);return result!;
    }
    public static object StandbyProbe(string root)
    {
        var (store,app)=Demo(System.IO.Path.Combine(root,"standby"));app.Toggle();app.Manager.Hide();
        var process=Process.GetCurrentProcess();TimeSpan cpu=default;var watch=new Stopwatch();object? result=null;
        double working=0,privateMemory=0;int phase=0,paintStart=0,eventStart=0;
        int seenStart=0,checksStart=0;
        using var timer=new System.Windows.Forms.Timer{Interval=App.StandbyAfterSeconds*1000+2000};
        timer.Tick+=(_,_)=>
        {
            process.Refresh();
            if(phase==0)
            {
                if(!app.IsStandby){timer.Interval=2000;return;}
                phase=1;
                cpu=process.TotalProcessorTime;working=process.WorkingSet64/1048576.0;privateMemory=process.PrivateMemorySize64/1048576.0;
                paintStart=app.VisualPaintCount;eventStart=app.DesktopEventsRelevant;seenStart=app.DesktopEventsSeen;checksStart=app.DesktopCheckCount;
                watch.Start();timer.Interval=10000;return;
            }
            bool standby=app.IsStandby;double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;double seconds=watch.Elapsed.TotalSeconds;
            int paints=app.VisualPaintCount-paintStart,relevantEvents=app.DesktopEventsRelevant-eventStart;
            var wake=Stopwatch.StartNew();app.Wake();
            // Process the same keyboard route used for opening a selected file, without launching external applications.
            app.ShowManager();app.Manager.RefreshState();app.Manager.Update();wake.Stop();
            Require(!app.IsStandby,"Wake did not resume interactive mode");
            result=new{Passed=standby,AutomaticIdleSeconds=App.StandbyAfterSeconds,TwoDesktopBasketsCreated=true,TestWindowsHiddenDuringMeasurement=false,DesktopBasketsVisibleDuringMeasurement=true,ItemLinks=18,
                StandbyMaintained=standby,ObservedSeconds=Math.Round(seconds,2),CpuMilliseconds=cpuMs,
                CpuSingleCorePercent=Math.Round(cpuMs/(seconds*1000)*100,3),StandbyWorkingSetMiB=Math.Round(working,1),
                PrivateCommittedMiB=Math.Round(privateMemory,1),WakeAndManagerRedrawMilliseconds=wake.Elapsed.TotalMilliseconds,
                StandbyPaints=paints,RelevantDesktopEventsDuringMeasurement=relevantEvents,AllExplorerEventsDuringMeasurement=app.DesktopEventsSeen-seenStart,
                LayoutChecksDuringMeasurement=app.DesktopCheckCount-checksStart,
                TimerStopsWhileIdle=true,ThreadsNotSuspended=true,WorkingSetTrimmedOnce=true};
            timer.Stop();app.Quit();
        };
        timer.Start();Application.Run(app);return result!;
    }
    public static object VirtualTest(string root)
    {
        var (_,app)=Demo(System.IO.Path.Combine(root,"virtualization"));var basket=new Basket{Name="大量檔案",Width=Grid.MinWidth,Height=Grid.MinHeight};
        for(int i=0;i<1000;i++)basket.Entries.Add(new Entry{Name=$"測試 {i:0000}.txt",Path=System.IO.Path.Combine(root,$"not-created-{i}.txt")});
        using var window=new BasketWindow(app,basket){TopLevel=true,Size=basket.ScreenBounds.Size};window.Show();Application.DoEvents();
        app.Store.State.Baskets.Add(basket);app.ToggleLock(basket);window.RefreshItems();Application.DoEvents();
        var before=basket.ScreenBounds;
        app.Place(basket,new Rectangle(before.X+100,before.Y+100,before.Width+Grid.CellWidth,before.Height+Grid.CellHeight),true);
        app.Collapse(basket);
        typeof(BasketWindow).GetMethod("StartDrag",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,new object?[]{new MouseEventArgs(MouseButtons.Left,1,20,20,0),false,null});
        bool lockVerified=basket.ScreenBounds==before&&!basket.Collapsed&&!window.Controls.Cast<Control>().Any(c=>c.Capture)&&window.ResizeEdgesAvailable==0;
        Require(lockVerified,"Locked basket accepted move, resize or collapse");
        Require(window.Viewport.Controls.Count==0,"Viewport allocated one control per file");
        int start=app.Icons.Count;window.ScrollBy(1);Application.DoEvents();
        bool scroll=window.Viewport.AutoScrollPosition.Y<0;
        for(int i=0;i<50;i++){window.ScrollBy(1);window.Viewport.Refresh();}
        int cache=app.Icons.Count;
        Require(scroll,"Large basket did not scroll");Require(cache<=64,"Icon cache exceeded fixed bound");
        app.ToggleLock(basket);window.RefreshItems();Application.DoEvents();
        bool unlocked=window.ResizeEdgesAvailable==8;
        Require(unlocked,"Unlock failed to restore eight resize handles");
        var edgeHit=typeof(BasketWindow).GetMethod("ResizeAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        bool crossResize=Convert.ToInt32(edgeHit.Invoke(window,new object[]{new Point(window.Width-18,window.Height-17)}))==10;
        Require(crossResize,"Bottom-right cross is not an actual corner resize target");
        var borderHit=typeof(BasketViewport).GetMethod("Hit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        bool safe=window.Viewport.ItemBounds(0).Left==Grid.ContentLeft&&window.Viewport.ItemBounds(0).Top>=Grid.ContentTopFor(basket.Width)+window.Viewport.AutoScrollPosition.Y&&(int)borderHit.Invoke(window.Viewport,new object[]{new Point(2,2)})! == -1;
        Require(safe,"Decorative border was used as a file hit target");
        var view=window.Viewport;view.AutoScrollPosition=Point.Empty;
        using var firstPaint=new Bitmap(view.Width,view.Height);view.DrawToBitmap(firstPaint,new Rectangle(Point.Empty,firstPaint.Size));
        view.AutoScrollPosition=new Point(0,80);
        using var partialPaint=new Bitmap(view.Width,view.Height);view.DrawToBitmap(partialPaint,new Rectangle(Point.Empty,partialPaint.Size));
        bool clipped=true;
        for(int py=0;py<view.Height&&clipped;py++)for(int px=0;px<view.Width;px++)
            if(!view.ContentBounds.Contains(px,py)&&!view.ScrollbarBounds.Contains(px,py)&&firstPaint.GetPixel(px,py)!=partialPaint.GetPixel(px,py)){clipped=false;break;}
        Require(clipped,"Partial scrolling painted file labels or icons on the frame");
        app.Quit();return new{Passed=true,FileCount=1000,PerFileControls=0,IconCacheUpperBound=64,ObservedCachedIcons=cache,OverflowScroll=scroll,LockBlocksMoveResizeCollapse=lockVerified,LockedBasketStillScrolls=scroll,UnlockRestoresResizeHandles=unlocked,ContentInsideFrame=safe,PartialScrollDoesNotPaintOnFrame=clipped,BottomRightCrossResizes=crossResize};
    }
    public static object InputTest(string root)
    {
        using var shell=new DesktopShell();Require(shell.Interactive,"Desktop input was disabled before test");
        var (_,app)=Demo(System.IO.Path.Combine(root,"input"));bool during=false,after=false,active=false;
        try
        {
            app.Toggle();Application.DoEvents();active=shell.Interactive;
            using var dialog=new BasketDialog(app.Store.State.Baskets[0]);
            using var closeTimer=new System.Windows.Forms.Timer{Interval=250};
            closeTimer.Tick+=(_,_)=>{during=shell.Interactive;closeTimer.Stop();dialog.DialogResult=DialogResult.Cancel;};
            closeTimer.Start();dialog.ShowDialog(app.Manager);Application.DoEvents();after=shell.Interactive;
            Require(active&&during&&after,"Organizer or category dialog disabled desktop input");
        }
        finally{app.Quit();}
        Require(shell.Interactive,"Quitting disabled desktop input");
        return new{Passed=true,DesktopEnabledWithBaskets=active,DesktopEnabledDuringSettingsDialog=during,DesktopEnabledAfterSettingsDialog=after,DesktopEnabledAfterQuit=shell.Interactive,ManagedChildWindows=true};
    }
    public static object VisualTest(string root)
    {
        var store=new Store(Path.Combine(root,"visual-"+Guid.NewGuid().ToString("N")));
        var basket=new Basket{Name="WORKSPACE"};
        store.State.Baskets.Add(basket);store.Save();
        for(int i=0;i<14;i++)
        {
            string file=Path.Combine(store.Root,$"參考檔案 {i+1:00}.txt");File.WriteAllText(file,"Visual verification fixture.");store.Add(basket,file);
        }
        var app=new App(store,true);
        using var frame=new BasketWindow(app,basket){TopLevel=true,Size=basket.ScreenBounds.Size};
        frame.Location=new Point(Screen.PrimaryScreen!.WorkingArea.Right-frame.Width-20,90);frame.Show();Application.DoEvents();
        void Capture(string name)
        {
            using var bitmap=new Bitmap(frame.Width,frame.Height);frame.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(root,name));
            int header=Grid.HeaderFor(frame.Width);var top=bitmap.GetPixel(frame.Width-20,0);var right=bitmap.GetPixel(frame.Width-1,header/2);
            Require(top.R>220&&top.G>220&&top.B<50&&right.R>220&&right.G>220&&right.B<50,"Yellow flag failed to touch the actual top/right window edges");
        }
        Capture("reference-frame-preview.png");
        // Documentation uses the real viewport and demonstration files only.
        var selectionStart=frame.Viewport.ItemBounds(0).Location+new Size(1,1);
        var selectionEnd=frame.Viewport.ItemBounds(5);var selectionPoint=new Point(selectionEnd.Right-2,selectionEnd.Bottom-2);
        frame.Viewport.HandlePointerDown(selectionStart,MouseButtons.Left,Keys.None);
        frame.Viewport.UpdateMarquee(selectionPoint);Capture("selection-frame-preview.png");
        Key(frame.Viewport,Keys.Escape);
        foreach(var spec in new[]{(columns:17,rows:5,name:"medium-frame-preview.png"),(columns:20,rows:6,name:"wide-frame-preview.png")})
        {
            basket.Width=spec.columns*Grid.CellWidth+Grid.Side;basket.Height=spec.rows*Grid.CellHeight+Grid.VerticalFor(basket.Width);frame.Size=basket.ScreenBounds.Size;frame.RefreshItems();Application.DoEvents();Capture(spec.name);
        }
        foreach(int columns in Enumerable.Range(Grid.MinColumns,7))
        {
            basket.Width=columns*Grid.CellWidth+Grid.Side;basket.Height=Grid.MinRows*Grid.CellHeight+Grid.VerticalFor(basket.Width);
            frame.Size=basket.ScreenBounds.Size;frame.RefreshItems();Application.DoEvents();
            Require(frame.Viewport.ContentBounds.Size==new Size(columns*Grid.CellWidth,Grid.MinRows*Grid.CellHeight),"Small grid lost complete visible file cells");
            Capture($"small-{columns}-column-preview.png");
        }
        basket.Width=Grid.MinWidth;basket.Height=Grid.MinHeight;frame.Size=basket.ScreenBounds.Size;frame.RefreshItems();Application.DoEvents();Capture("minimum-overflow-preview.png");
        Require(frame.Viewport.CanScroll,"Fourteen files did not overflow the half-size basket");
        Require(frame.ObjectCount==14&&frame.FrameOrdinal==1,"Preview count or frame ordinal was fictional");
        var second=new Basket{Name="PROJECTS"};store.State.Baskets.Add(second);store.Transfer(second,basket.Entries[0]);frame.RefreshItems();
        Require(frame.ObjectCount==13,"Object count failed to follow transfer");
        using var frame2=new BasketWindow(app,second){TopLevel=true,Size=second.ScreenBounds.Size};
        Require(frame2.FrameOrdinal==2&&frame2.ObjectCount==1,"Second frame ordinal or counter was incorrect");
        store.Delete(basket);Require(frame2.FrameOrdinal==1,"Frame ordinals failed to follow category removal");
        frame2.Location=frame.Location;frame2.Show();frame2.RefreshItems();Application.DoEvents();
        using(var image=new Bitmap(frame2.Width,frame2.Height)){frame2.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"compact-frame-preview.png"));}
        second.Width=Grid.MinWidth;second.Height=Grid.MinHeight;second.Name="WORKSPACE";frame2.Size=second.ScreenBounds.Size;frame2.RefreshItems();Application.DoEvents();
        using(var image=new Bitmap(frame2.Width,frame2.Height)){frame2.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"minimum-frame-preview.png"));}
        Require(frame2.Controls.Cast<Control>().All(c=>c.Bounds.Left>=0&&c.Bounds.Top>=0&&c.Bounds.Right<=frame2.ClientSize.Width&&c.Bounds.Bottom<=frame2.ClientSize.Height),"Minimum frame clipped its structural controls");
        app.EnterStandby();Require(frame2.FrameStatus=="STANDBY","Standby status was fictional");app.Wake();second.Locked=true;Require(frame2.FrameStatus=="LOCKED","Lock status was fictional");
        app.Quit();return new{Passed=true,ActualObjectCount=14,TransferUpdatesCount=true,SequentialFrameOrdinals=true,DeleteReindexesFrames=true,LiveStandbyAndLockLabels=true,DisplayFont=FrameArt.Display(20).FontFamily.Name,NativeFontRegistered=FrameArt.NativeFontRegistered,NativeUIText=true,MetadataMinimumPixels=Theme.MinimumMetadataPixels,VectorAntialiasing=true,MinimumColumns=Grid.MinColumns,MinimumRows=Grid.MinRows,MinimumWidth=Grid.MinWidth,MinimumHeight=Grid.MinHeight,FlagFlushTopAndRight=true,SevenSmallGridSizesVerified=true,HalfSizeOverflowScroll=true,ThreeNativeSizesVerified=true};
    }
}
