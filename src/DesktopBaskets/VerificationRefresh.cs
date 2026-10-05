using System.Diagnostics;

namespace DesktopBaskets;

internal static partial class Verification
{
    public static object RefreshTest(string root)
    {
        // Real Explorer verification must not race an enabled production organizer.
        var userStore=new Store();
        Require(!userStore.State.Enabled,"Pause the existing organizer before native refresh verification.");
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
        App? app=null;bool selectedCleared=false,filled=false,stable=false,restored=false;int relevant=0;string diagnostic="";
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
            var store=new Store(Path.Combine(data,"config"));store.State.Baskets.Add(basket);store.Add(basket,paths[0]);
            app=new App(store,true);app.Toggle();app.HideVerificationWindows();Pump(500);
            bool CheckNative()
            {
                var icons=shell.ReadIcons();
                try
                {
                    int index=icons.FindIndex(i=>i.Key==paths[0]);
                    selectedCleared=index>=0&&!shell.IsSelected(index);
                    using var currentShell=new DesktopShell();
                    diagnostic=$"selectedCleared={selectedCleared}; positions={string.Join(" / ",paths.Select(p=>icons.Single(i=>i.Key==p).Position))}; expected={cells[0]} / {cells[1]}; events={app.DesktopEventsRelevant}/{app.DesktopEventsSeen}; list={shell.List}/{currentShell.List}; alive={shell.Alive}; status={app.Manager.StatusText}; flags={shell.Flags}";
                    return selectedCleared&&icons.Single(i=>i.Key==paths[1]).Position==cells[0]&&icons.Single(i=>i.Key==paths[2]).Position==cells[1]
                        &&!area.IntersectsWith(LayoutPlanner.Footprint(icons[index].Position,spacing))&&paths.All(File.Exists);
                }
                finally{foreach(var i in icons)i.Dispose();}
            }
            filled=CheckNative();Require(filled,"Classifying the selected icon did not fill its native cell or clear selection.");
            for(int n=0;n<3;n++){shell.RefreshView();Pump(900);bool okay=CheckNative();Require(okay,"Explorer refresh left a duplicate, selected hidden item, or unfilled cell: "+diagnostic);}
            relevant=app.DesktopEventsRelevant;Require(relevant>0,"Native Explorer refresh did not exercise the event hook.");
            Pump(500);int settled=app.DesktopEventsRelevant;Pump(500);stable=settled==app.DesktopEventsRelevant;
            Require(stable,"Desktop event repair did not settle; possible self-triggered loop.");
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
            EventDrivenRepair=true,DesktopEventsRelevant=relevant,NoSelfTriggeredEventLoop=stable,OriginalFilePathsUnchanged=true,DesktopPositionsAndFlagsRestored=restored};
    }
}
