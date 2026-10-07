namespace DesktopBaskets;

internal static partial class Verification
{
    public static object DisplayTest(string root)
    {
        int assertions=0;
        void Check(bool ok,string message){assertions++;Require(ok,message);}
        var bottom=new DisplayScreen("bottom",new Rectangle(0,0,3440,1440),new Rectangle(0,0,3440,1392),true);
        var top=new DisplayScreen("top",new Rectangle(0,-1440,3440,1440),new Rectangle(0,-1440,3440,1392));
        var left=new DisplayScreen("left",new Rectangle(-1920,0,1920,1080),new Rectangle(-1920,0,1920,1040));
        var tiny=new DisplayScreen("top",new Rectangle(0,-1440,320,180),new Rectangle(0,-1440,320,140));
        var upper=new Basket{Name="upper",X=2200,Y=-1380,Width=780,Height=480,Monitor=top.DeviceName,Locked=true};
        upper.Entries.Add(new Entry{Name="same.url",Path=Path.Combine(root,"same.url")});
        var lower=new Basket{Name="lower",X=2200,Y=40,Width=780,Height=480,Monitor=bottom.DeviceName,Locked=true};
        upper.SavedChromeHeight=Grid.VerticalFor(upper.Width);lower.SavedChromeHeight=Grid.VerticalFor(lower.Width);
        var originals=JsonCodec.Serialize(new[]{upper,lower});
        Check(DisplayLayout.TryRecover(new[]{upper,lower},new[]{bottom,top},out var stable),"Vertical dual monitor layout is accepted.");
        Check(stable.All(m=>m.Home==null),"Stable screens preserve geometry without creating recovery backups.");
        Check(DisplayLayout.At(new Point(10,-800),new[]{bottom,top})==top,"Negative Y pointer selects upper monitor.");
        Check(DisplayLayout.At(new Point(-1800,100),new[]{bottom,left})==left,"Negative X pointer selects left monitor.");
        Check(DisplayLayout.TryPlace(upper,new Rectangle(3200,-1600,780,480),top,out var drop)&&top.WorkingArea.Contains(drop.Bounds(false)),"Cross-screen drop clamps inside the upper working area.");
        Check(DisplayLayout.TryPlace(upper,new Rectangle(-2000,50,780,480),left,out drop)&&left.WorkingArea.Contains(drop.Bounds(false)),"Cross-screen drop clamps inside the left working area.");
        Check(!DisplayLayout.TryPlace(upper,new Rectangle(0,-1440,780,480),tiny,out _),"An undersized target is rejected without throwing.");
        Check(!DisplayLayout.TryRecover(new[]{upper,lower},Array.Empty<DisplayScreen>(),out var none)&&none.Count==0,"Empty transient topology rejects atomically.");
        Check(!DisplayLayout.TryRecover(new[]{upper,lower},new[]{tiny},out none)&&none.Count==0,"Undersized transient topology rejects atomically.");
        Check(JsonCodec.Serialize(new[]{upper,lower})==originals,"Failed planning changes no names, paths, lock or geometry.");
        Check(DisplayLayout.TryRecover(new[]{upper,lower},new[]{bottom,tiny},out var fallback),"A tiny secondary monitor falls back to a viable monitor.");
        Check(fallback.All(m=>bottom.WorkingArea.Contains(m.Placement.Bounds(false))),"Fallback frames remain fully visible.");
        Check(!fallback[0].Placement.Bounds(false).IntersectsWith(fallback[1].Placement.Bounds(false)),"Fallback frames do not overlap.");
        Check(fallback.Single(m=>m.Basket==lower).Home==null,"Connected monitor's original frame stays in place.");
        Check(fallback.Single(m=>m.Basket==upper).Home?.Monitor==top.DeviceName,"Disconnected monitor's preferred placement is retained.");
        foreach(var move in fallback){move.Placement.Apply(move.Basket);move.Basket.DisplayHome=move.Home;}
        var store=new Store(Path.Combine(root,"display",Guid.NewGuid().ToString("N")));store.State.Baskets.AddRange(new[]{upper,lower});store.Save();
        var loaded=new Store(store.Root);
        Check(loaded.State.Baskets.Single(b=>b.Name=="upper").DisplayHome?.Y==-1380,"Preferred negative-coordinate placement survives restart.");
        Check(DisplayLayout.TryRecover(loaded.State.Baskets,new[]{bottom,top},out var returned),"Reconnected monitor restores preferred positions.");
        foreach(var move in returned){move.Placement.Apply(move.Basket);move.Basket.DisplayHome=move.Home;}
        Check(JsonCodec.Serialize(loaded.State.Baskets)==originals,"Reconnect restores exact geometry, monitor, lock and paths.");
        var smaller=new DisplayScreen("top",new Rectangle(0,-900,1024,900),new Rectangle(0,-900,1024,860));
        Check(DisplayLayout.TryRecover(new[]{upper},new[]{smaller},out var resized)&&resized[0].Home!=null&&smaller.WorkingArea.Contains(resized[0].Placement.Bounds(false)),"Resolution shrink fits while retaining original size.");
        var giant=new Basket{X=50,Y=20,Width=2100,Height=1100,Monitor="bottom"};
        var small=new DisplayScreen("small",new Rectangle(3440,0,1280,720),new Rectangle(3440,0,1280,680));
        Check(DisplayLayout.TryPlace(giant,new Rectangle(3440,20,2100,1100),small,out drop)&&small.WorkingArea.Contains(drop.Bounds(false))&&drop.Width>=Grid.MinWidth,"Cross-screen transfer to smaller screen fits the grid.");
        var collapsed=new Basket{X=10,Y=10,Width=Grid.MinWidth,Height=1500,Collapsed=true,Monitor="small"};
        Check(DisplayLayout.TryPlace(collapsed,collapsed.ScreenBounds,small,out drop)&&drop.Height==1500&&small.WorkingArea.Contains(drop.Bounds(true)),"Collapsed frame keeps its full expansion height.");
        foreach(int w in new[]{0,1,200,Grid.MinWidth-1})foreach(int h in new[]{0,1,100,Grid.MinHeight-1})
            Check(!DisplayLayout.TryFit(giant,BasketPlacement.From(giant),new Rectangle(0,0,w,h),out _),"Invalid working areas never throw or shrink below minimum.");
        using var app=new App(store,true);app.Manager.Hide();
        var saved=JsonCodec.Serialize(store.State.Baskets);
        app.UpdateDisplays(new[]{tiny},false);
        Check(JsonCodec.Serialize(store.State.Baskets)==saved&&app.Manager.StatusText.Contains("保留"),"App handles the reported small-screen failure without an uncaught exception.");
        app.UpdateDisplays(new[]{bottom,top},false);
        Check(store.State.Baskets.Single(b=>b.Name=="upper").Y==-1380,"App recovers when the normal topology returns.");
        upper=store.State.Baskets.Single(b=>b.Name=="upper");var locked=upper.ScreenBounds;
        app.MoveToScreen(upper,bottom);
        Check(upper.ScreenBounds==locked&&upper.Locked,"LOCK still prevents manual cross-screen movement.");
        app.Quit();
        var physical=Native.DisplayScreens();
        Check(physical.Count>0&&physical.All(s=>s.DeviceName.Length>0&&s.Bounds.Width>0),"Native monitor identities and physical bounds are available.");
        return new{Passed=true,Assertions=assertions,TransientSmallWorkAreaHandled=true,NegativeCoordinateScreens=true,CrossScreenPlacement=true,DisconnectedMonitorFallback=true,ReconnectRestoresHome=true,RecoveryPersists=true,LockPreserved=true,OriginalPathsPreserved=true,PhysicalMonitorCount=physical.Count};
    }
}
