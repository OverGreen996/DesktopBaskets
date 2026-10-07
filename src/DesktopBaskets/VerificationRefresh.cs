using System.Diagnostics;

namespace DesktopBaskets;

internal static partial class Verification
{
    public static object RefreshTest(string root)
    {
        // Real Explorer verification must not race an enabled production organizer.
        var userStore=new Store();
        if(Mutex.TryOpenExisting("Local\\DesktopBaskets_"+Environment.UserName,out var production))
        {
            using(production)Require(!userStore.State.Enabled,"Pause the existing organizer before native refresh verification.");
        }
        string data=Path.Combine(root,"refresh-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        using var shell=new DesktopShell();var original=shell.ReadIcons();uint flags=shell.Flags;
        File.WriteAllText(Path.Combine(data,"baseline-state.json"),JsonCodec.Serialize(new State{OriginalAutoArrange=(flags&1)!=0,OriginalSnapToGrid=(flags&4)!=0,
            Icons=original.Select(i=>new IconBackup{Key=i.Key,X=i.Position.X,Y=i.Position.Y}).ToList()}));
        var spacing=shell.Spacing;var area=shell.ToView(Screen.PrimaryScreen!.WorkingArea);
        var basket=new Basket{Name="Refresh verification",X=Screen.PrimaryScreen.WorkingArea.Right-Grid.MinWidth,Y=Screen.PrimaryScreen.WorkingArea.Top,Width=Grid.MinWidth,Height=Grid.MinHeight};
        var blocked=shell.ToView(basket.ScreenBounds);
        Point? first=null;
        for(int x=area.Left+20;x+spacing.Width+20<area.Right&&!first.HasValue;x+=spacing.Width)
        for(int y=area.Top+20;y+spacing.Height*3+20<area.Bottom&&!first.HasValue;y+=spacing.Height)
        {
            var strip=new Rectangle(x-10,y-10,spacing.Width+20,spacing.Height*3+20);
            if(!strip.IntersectsWith(blocked)&&original.All(i=>!strip.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing))))first=new Point(x,y);
        }
        Require(first.HasValue,"No free three-cell column for native refresh verification.");
        var cells=Enumerable.Range(0,3).Select(i=>new Point(first!.Value.X,first.Value.Y+i*spacing.Height)).ToArray();
        string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var paths=Enumerable.Range(0,3).Select(i=>Path.Combine(desktop,"DesktopBaskets-refresh-"+Guid.NewGuid().ToString("N")+".txt")).ToArray();
        App? app=null;bool selectedCleared=false,filled=false,stable=false,restored=false,returned=false,frameStable=false;int relevant=0;string diagnostic="";
        void Pump(int milliseconds){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();System.Threading.Thread.Sleep(15);}}
        try
        {
            foreach(var path in paths)File.WriteAllText(path,"Temporary Desktop Baskets refresh verification.");
            shell.RefreshView();Pump(400);
            var created=shell.ReadIcons();
            try
            {
                Require(paths.All(p=>created.Any(i=>i.Key==p)),"Explorer did not enumerate verification files.");
                shell.SetManualPositions();shell.Position(created,paths.Select((p,i)=>new{p,Cell=cells[i]}).ToDictionary(i=>i.p,i=>i.Cell));
                shell.SelectForVerification(created.FindIndex(i=>i.Key==paths[0]));
            }
            finally{foreach(var i in created)i.Dispose();}
            var store=new Store(Path.Combine(data,"config"));store.State.Baskets.Add(basket);
            app=new App(store,true);app.Toggle();Pump(500);
            var packed=shell.ReadIcons();
            try{for(int i=0;i<paths.Length;i++)cells[i]=packed.Single(icon=>icon.Key==paths[i]).Position;}
            finally{foreach(var icon in packed)icon.Dispose();}
            var frame=app.DesktopWindows.Single();var frameHandle=frame.Handle;var glassHandle=frame.GlassHandle;
            int frameVisibility=frame.NativeVisibilityChanges,glassVisibility=frame.GlassVisibilityChanges;
            app.AddPaths(basket,new[]{paths[0]});Pump(500);
            frameStable=frame.Handle==frameHandle&&frame.GlassHandle==glassHandle&&Native.IsWindowVisible(frameHandle)&&Native.IsWindowVisible(glassHandle)
                &&frame.NativeVisibilityChanges==frameVisibility&&frame.GlassVisibilityChanges==glassVisibility&&frame.ObjectCount==1;
            Require(frameStable,"Adding a desktop file hid/recreated the frame or glass surface, or failed to update its count.");
            app.HideVerificationWindows();Pump(100);
            bool CheckNative()
            {
                var icons=shell.ReadIcons();
                try
                {
                    int index=icons.FindIndex(i=>i.Key==paths[0]);
                    selectedCleared=index>=0&&!shell.IsSelected(index);
                    using var currentShell=new DesktopShell();
                    diagnostic=$"selectedCleared={selectedCleared}; positions={string.Join(" / ",paths.Select(p=>icons.Single(i=>i.Key==p).Position))}; expected={cells[0]} / {cells[1]}; events={app.DesktopEventsRelevant}/{app.DesktopEventsSeen}; list={shell.List}/{currentShell.List}; alive={shell.Alive}; status={app.Manager.StatusText}; flags={shell.Flags}";
                    diagnostic+=$"; checks={app.DesktopCheckCount}; physical={string.Join(" / ",Native.PhysicalScreens().Select(s=>s.Bounds))}; assignedBackup={string.Join(" / ",app.Store.State.Icons.Where(i=>i.Key==paths[0]).Select(i=>$"hidden={i.Hidden},last={i.LastX},{i.LastY}"))}; screens={string.Join(" / ",Screen.AllScreens.Select(s=>s.Bounds))}; assigned={app.Store.State.Baskets.Single().Entries.Single().Path==paths[0]}";
                    return selectedCleared&&icons.Single(i=>i.Key==paths[1]).Position==cells[0]&&icons.Single(i=>i.Key==paths[2]).Position==cells[1]
                        &&!area.IntersectsWith(LayoutPlanner.Footprint(icons[index].Position,spacing))&&paths.All(File.Exists);
                }
                finally{foreach(var i in icons)i.Dispose();}
            }
            filled=CheckNative();Require(filled,"Classifying the selected icon did not fill its native cell or clear selection: "+diagnostic);
            for(int n=0;n<3;n++)
            {
                shell.RefreshView();Pump(900);bool okay=CheckNative();var settle=Stopwatch.StartNew();
                while(!okay&&settle.ElapsedMilliseconds<4000){Pump(200);okay=CheckNative();}
                Require(okay,"Explorer refresh left a duplicate, selected hidden item, or unfilled cell: "+diagnostic);
            }
            relevant=app.DesktopEventsRelevant;Require(relevant>0,"Native Explorer refresh did not exercise the event hook.");
            Pump(500);int settled=app.DesktopEventsRelevant;Pump(500);stable=settled==app.DesktopEventsRelevant;
            Require(stable,"Desktop event repair did not settle; possible self-triggered loop.");
            var entry=store.State.Baskets.Single().Entries.Single();
            var dropPoint=new Point(area.Left+area.Width*3/4+shell.Origin.X,area.Top+area.Height*3/4+shell.Origin.Y);
            app.ReturnEntryToDesktop(entry.Id,dropPoint);Pump(400);
            var returnedIcons=shell.ReadIcons();
            try
            {
                var icon=returnedIcons.Single(i=>i.Key==paths[0]);
                var screen=icon.Position+new Size(shell.Origin);
                returned=store.State.Baskets.All(b=>b.Entries.All(e=>e.Id!=entry.Id))&&area.Contains(LayoutPlanner.Cell(icon.Position,spacing))
                    &&!blocked.IntersectsWith(LayoutPlanner.Footprint(icon.Position,spacing))&&Math.Abs(screen.X-dropPoint.X)+Math.Abs(screen.Y-dropPoint.Y)<2*(spacing.Width+spacing.Height)
                    &&paths.All(p=>File.ReadAllText(p)=="Temporary Desktop Baskets refresh verification.");
                Require(returned,"Dragging back to desktop did not unassign, display near the drop, or preserve the original file.");
            }
            finally{foreach(var i in returnedIcons)i.Dispose();}
        }
        finally
        {
            app?.Quit();
            // These exact files were created by this test; original desktop files are untouched.
            foreach(var path in paths)if(File.Exists(path))File.Delete(path);
            shell.RefreshView();Pump(400);
            var after=shell.ReadIcons();
            try
            {
                shell.SetManualPositions();shell.Position(after,original.ToDictionary(i=>i.Key,i=>i.Position));shell.SetLayoutFlags((flags&1)!=0,(flags&4)!=0);
            }
            finally{foreach(var i in after)i.Dispose();}
            var verify=shell.ReadIcons();
            try{restored=original.All(i=>verify.Any(j=>j.Key==i.Key&&j.Position==i.Position))&&(shell.Flags&5)==(flags&5);}
            finally{foreach(var i in verify)i.Dispose();foreach(var i in original)i.Dispose();}
        }
        Require(restored,"Native refresh verification did not restore the original desktop layout.");
        return new{Passed=true,VacancyFilledInNativeOrder=filled,NativeSelectionCleared=selectedCleared,ThreeExplorerRefreshesPassed=true,
            EventDrivenRepair=true,DesktopEventsRelevant=relevant,NoSelfTriggeredEventLoop=stable,OriginalFilePathsUnchanged=true,
            AddingFileKeepsFrameAndGlassVisible=frameStable,AddingFilePreservesBothWindowHandles=frameStable,AddingFileUpdatesObjectCount=frameStable,
            DragReturnDisplaysNativeIconNearDrop=returned,DragReturnRemovesVisualMembership=returned,DragReturnPreservesFileContents=returned,DesktopPositionsAndFlagsRestored=restored};
    }
}
