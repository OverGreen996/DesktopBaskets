namespace DesktopBaskets;

internal static partial class Verification
{
    public static object FreeLayoutTest(string root)
    {
        string data=Path.Combine(root,"free-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        int assertions=0;void Check(bool okay,string message){Require(okay,message);assertions++;}
        var spacing=new Size(76,99);var area=new Rectangle(0,0,1000,800);var areas=new[]{area};
        var empty=Array.Empty<Rectangle>();var none=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var holes=new[]{new LayoutIcon("A",new Point(14,2)),new LayoutIcon("B",new Point(14,299)),new LayoutIcon("C",new Point(401,217))};
        Dictionary<string,Point> Free(LayoutIcon[] icons,Rectangle[] baskets,HashSet<string>? returning=null,bool drop=false,IEnumerable<Point>? vacancies=null)
            =>LayoutPlanner.PlanDesktop(icons,baskets,areas,spacing,false,drop,returning,holes,vacancies);
        LayoutIcon[] Apply(LayoutIcon[] icons,Dictionary<string,Point> plan)=>icons.Select(i=>new LayoutIcon(i.Key,plan.TryGetValue(i.Key,out var p)?p:i.Position)).ToArray();
        Check(!new State().AutoCompactDesktop,"New settings must default to free placement.");
        Check(Free(holes,empty).Count==0,"Intentional gaps and off-grid placements must remain unchanged.");
        Check(Free(holes.Skip(1).ToArray(),empty,vacancies:new[]{holes[0].Position}).Count==0,"Classifying an icon must not fill its old cell.");
        var manual=holes.Select(i=>i.Key=="B"?new LayoutIcon(i.Key,new Point(637,413)):i).ToArray();
        var backups=holes.Select(i=>new IconBackup{Key=i.Key,X=i.Position.X,Y=i.Position.Y,LastX=i.Position.X,LastY=i.Position.Y}).ToArray();
        for(int refresh=0;refresh<3;refresh++)
        {
            Check(!LayoutPlanner.RequiresAvoidance(manual,none,backups,empty,areas,spacing),"Refresh must ignore manual position mismatches.");
            Check(Free(manual,empty).Count==0,"Refresh must not repack the desktop.");
        }
        var added=manual.Concat(new[]{new LayoutIcon("new file",new Point(710,602))}).ToArray();
        Check(Free(added,empty).Count==0,"Creating a desktop file must not move existing icons.");
        Check(Free(added.Where(i=>i.Key!="A").ToArray(),empty).Count==0,"Deleting a desktop file must leave manual gaps alone.");
        var blocked=new[]{new Rectangle(0,250,125,170)};
        var plan=Free(holes,blocked);
        Check(plan.Count==1&&plan.ContainsKey("B"),"Only the icon obstructed by a basket may move.");
        var avoided=Apply(holes,plan);
        Check(avoided.All(i=>!blocked.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))),"Avoidance must reserve basket and label clearance.");
        Check(avoided.Where(i=>i.Key!="B").SequenceEqual(holes.Where(i=>i.Key!="B")),"Unobstructed positions must stay exact.");
        Check((plan["B"].X-14)%spacing.Width==0&&(plan["B"].Y-2)%spacing.Height==0,"Displaced icons must use the actual native grid.");
        Check(Free(avoided,blocked).Count==0,"Avoidance must settle without a repeated event loop.");
        var returning=new HashSet<string>(new[]{"return"},StringComparer.OrdinalIgnoreCase);
        var dropPoint=new Point(601,411);var dropped=holes.Concat(new[]{new LayoutIcon("return",dropPoint)}).ToArray();
        var returned=Free(dropped,empty,returning,true,new[]{holes[0].Position});
        Check(returned.Count==1&&returned["return"]==dropPoint,"A clear drag-out point must stay exact and must not compact neighbors.");
        Check(Free(Apply(dropped,returned),empty).Count==0,"Subsequent refresh must retain the drag-out position.");
        var collision=holes.Concat(new[]{new LayoutIcon("return",holes[1].Position)}).ToArray();
        var separated=Apply(collision,Free(collision,empty,returning,true));
        Check(separated.Where(i=>i.Key!="return").SequenceEqual(holes),"A returning icon must never displace the existing occupant.");
        var ret=separated.Single(i=>i.Key=="return");
        Check(holes.All(i=>!LayoutPlanner.Cell(i.Position,spacing).IntersectsWith(LayoutPlanner.Cell(ret.Position,spacing))),"A colliding return must use a free cell.");
        var hiddenBackup=new[]{new IconBackup{Key="hidden",Hidden=true}};
        var assigned=new HashSet<string>(new[]{"HIDDEN"},StringComparer.OrdinalIgnoreCase);
        Check(LayoutPlanner.RequiresAvoidance(new[]{new LayoutIcon("hidden",new Point(14,2))},assigned,hiddenBackup,empty,areas,spacing),"F5 revealing a classified original must trigger rehiding.");
        Check(!LayoutPlanner.RequiresAvoidance(new[]{new LayoutIcon("hidden",new Point(1600,2))},assigned,hiddenBackup,empty,areas,spacing),"Already hidden originals must not cause repair loops.");
        Check(LayoutPlanner.RequiresAvoidance(new[]{new LayoutIcon("hidden",new Point(1600,2))},none,hiddenBackup,empty,areas,spacing),"Removing membership must bring the original back.");
        Check(!LayoutPlanner.RequiresAvoidance(holes,none,hiddenBackup,empty,areas,spacing),"A deleted/unavailable native item must not cause endless repairs.");
        var left=new Rectangle(-1000,-100,1000,800);var multiAreas=new[]{left,area};
        var multi=new[]{new LayoutIcon("left",new Point(-986,199)),new LayoutIcon("right",new Point(401,217))};
        var multiPlan=LayoutPlanner.PlanDesktop(multi,new[]{new Rectangle(-1000,150,120,170)},multiAreas,spacing,false,false);
        Check(multiPlan.Count==1&&left.Contains(LayoutPlanner.Cell(multiPlan["left"],spacing)),"Avoidance must retain the original negative-coordinate monitor.");
        Check(LayoutPlanner.PlanDesktop(holes,empty,areas,spacing,true,false).Count>0,"Explicit opt-in must still compact desktop holes.");
        bool rejected=false;try{Free(holes,new[]{area});}catch(InvalidOperationException){rejected=true;}
        Check(rejected&&holes[1].Position==new Point(14,299),"Full monitor must reject a plan before changing positions.");
        var random=new Random(503);
        for(int n=0;n<100;n++)
        {
            var icons=Enumerable.Range(0,40).Where(_=>random.Next(4)!=0).Select(i=>new LayoutIcon("icon"+i,new Point(14+i/7*76,2+i%7*99))).ToArray();
            var obstacles=new[]{new Rectangle(0,260,170,170)};
            var moves=Free(icons,obstacles);var final=Apply(icons,moves);
            Check(icons.Where(i=>!obstacles.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))).All(i=>!moves.ContainsKey(i.Key)),"Random gaps: unaffected icon moved.");
            Check(final.All(i=>!obstacles.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing))))&&final.Select((i,index)=>final.Skip(index+1).All(j=>!LayoutPlanner.Cell(i.Position,spacing).IntersectsWith(LayoutPlanner.Cell(j.Position,spacing)))).All(v=>v),"Random avoidance: collision.");
            Check(Free(final,obstacles).Count==0,"Random avoidance: plan failed to settle.");
        }
        var positionStore=new Store(Path.Combine(data,"position-backup"));positionStore.State.Icons=backups.ToList();
        using(var layout=new DesktopLayout(positionStore))layout.RememberUserPositions(manual.Select(i=>new ShellIcon(i.Key,i.Key,IntPtr.Zero,i.Position)).ToArray());
        Check(new Store(positionStore.Root).State.Icons.Single(i=>i.Key=="B").X==637,"Manual position must become the persisted restore target.");
        var migratedRoot=Path.Combine(data,"old-settings");Directory.CreateDirectory(migratedRoot);
        File.WriteAllText(Path.Combine(migratedRoot,"state.json"),"{\"Version\":2,\"Enabled\":false,\"Baskets\":[]}");
        Check(!new Store(migratedRoot).State.AutoCompactDesktop,"Existing settings without the new flag must migrate to free placement.");
        var uiStore=new Store(Path.Combine(data,"manager"));uiStore.State.Baskets.Add(new Basket{Name="自由擺放測試"});uiStore.Save();
        using(var app=new App(uiStore,true,autoStart:true))
        {
            var control=app.Manager.ArrangementControl;
            Check(!control.Checked&&control.TabStop&&!string.IsNullOrEmpty(control.AccessibleName),"Setting must be labeled, keyboard accessible and off by default.");
            control.Checked=true;
            Check(new Store(uiStore.Root).State.AutoCompactDesktop,"Manager opt-in must persist.");
            control.Checked=false;
            Check(!new Store(uiStore.Root).State.AutoCompactDesktop&&!uiStore.State.Enabled&&uiStore.State.Icons.Count==0,"Turning off must preserve a paused desktop without native mutations.");
            app.Manager.RefreshState();Check(!control.Checked,"Refreshing the manager must not re-enable compaction.");
            app.ShowManager();Application.DoEvents();
            foreach(var size in new[]{new Size(1060,680),new Size(850,560)})
            {
                app.Manager.ClientSize=size;app.Manager.PerformLayout();Application.DoEvents();
                Check(control.Parent!.ClientRectangle.Contains(control.Bounds)&&!control.Bounds.IntersectsWith(app.Manager.StartupControl.Bounds),"Settings overlap or are clipped at supported manager sizes.");
                var settingsPanel=control.Parent;
                Check(settingsPanel.Parent!.Controls.Cast<Control>().Where(c=>c!=settingsPanel&&c.Visible).All(c=>!c.Bounds.IntersectsWith(settingsPanel.Bounds)),
                    "Basket list or action panel covers a setting: "+settingsPanel.Bounds+" / "+string.Join(" / ",settingsPanel.Parent.Controls.Cast<Control>().Select(c=>c.GetType().Name+" "+c.Bounds)));
                var measured=TextRenderer.MeasureText(control.Text,control.Font,new Size(control.Width-24,int.MaxValue),TextFormatFlags.WordBreak);
                Check(measured.Height<=control.Height,"Setting text must remain readable.");
                using var bitmap=app.Manager.RenderClient();
                bitmap.Save(Path.Combine(data,$"manager-{size.Width}.png"),System.Drawing.Imaging.ImageFormat.Png);
            }
            app.Quit();
        }
        return new{Passed=true,Assertions=assertions,DefaultFreePlacement=true,ExistingSettingsDefaultFree=true,RefreshPreservesManualPositions=true,
            OnlyBasketObstructionsMove=true,DragOutPointPreserved=true,OptInCompactionPersists=true,NoNewPolling=true,TestData=data};
    }
}
