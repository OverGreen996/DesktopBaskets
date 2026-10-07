namespace DesktopBaskets;

internal static partial class Verification
{
    public static object ArrangementTest()
    {
        var spacing=new Size(76,99);var area=new Rectangle(0,0,800,600);var areas=new[]{area};int assertions=0;
        void Check(bool condition,string message){Require(condition,message);assertions++;}
        LayoutIcon[] Apply(LayoutIcon[] icons,Dictionary<string,Point> plan)=>icons.Select(i=>new LayoutIcon(i.Key,plan.TryGetValue(i.Key,out var p)?p:i.Position)).ToArray();
        var holes=new[]{new LayoutIcon("A",new Point(14,2)),new LayoutIcon("B",new Point(14,200)),new LayoutIcon("C",new Point(90,101)),new LayoutIcon("D",new Point(90,299))};
        var plan=LayoutPlanner.Compact(holes,Array.Empty<Rectangle>(),areas,spacing);
        Check(plan["B"]==new Point(14,101)&&plan["C"]==new Point(14,200)&&plan["D"]==new Point(14,299),"Existing holes without classified source positions must be filled");
        var packed=Apply(holes,plan);
        Check(LayoutPlanner.Compact(packed,Array.Empty<Rectangle>(),areas,spacing).Count==0,"Repeated compaction must be idempotent");
        Check(LayoutPlanner.GridAnchor(holes,area,spacing)==new Point(14,2),"Native grid padding must be preserved");
        var blocked=new[]{new Rectangle(0,130,110,110)};
        var avoided=Apply(holes,LayoutPlanner.Compact(holes,blocked,areas,spacing));
        Check(avoided.All(i=>blocked.All(b=>!b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))),"Compaction must reserve basket and label clearance");
        Check(avoided.Select(i=>i.Position).SequenceEqual(new[]{new Point(14,2),new Point(14,299),new Point(14,398),new Point(14,497)}),"Only basket-blocked cells may be skipped");
        var offGrid=new[]{new LayoutIcon("A",new Point(14,2)),new LayoutIcon("B",new Point(90,299)),new LayoutIcon("C",new Point(14,200)),new LayoutIcon("D",new Point(160,183))};
        var snapped=Apply(offGrid,LayoutPlanner.Compact(offGrid,Array.Empty<Rectangle>(),areas,spacing));
        Check(snapped.All(i=>(i.Position.X-14)%76==0&&(i.Position.Y-2)%99==0),"Off-grid icons must rejoin the dominant native grid");
        var leftArea=new Rectangle(-800,-50,800,600);var multiAreas=new[]{leftArea,area};
        var multi=new[]{new LayoutIcon("leftA",new Point(-786,-48)),new LayoutIcon("leftB",new Point(-710,250)),new LayoutIcon("rightA",new Point(14,2)),new LayoutIcon("rightB",new Point(90,200))};
        var multiPacked=Apply(multi,LayoutPlanner.Compact(multi,Array.Empty<Rectangle>(),multiAreas,spacing));
        Check(multiPacked.Where(i=>i.Key.StartsWith("left")).All(i=>leftArea.Contains(LayoutPlanner.Cell(i.Position,spacing)))&&multiPacked.Where(i=>i.Key.StartsWith("right")).All(i=>area.Contains(LayoutPlanner.Cell(i.Position,spacing))),"Compaction must not transfer icons between monitors");
        Check(LayoutPlanner.Compact(multiPacked,Array.Empty<Rectangle>(),multiAreas,spacing).Count==0,"Negative-coordinate monitor packing must settle");
        bool rejected=false;try{LayoutPlanner.Compact(holes,new[]{area},areas,spacing);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Insufficient desktop space must reject the plan before any position mutation");
        // Reproduce this user's 76 x 99 native grid: empty cells across columns,
        // plus one off-grid icon. No hidden-file vacancy hints are supplied.
        var actual=new[]{new LayoutIcon("A",new Point(14,2)),new LayoutIcon("B",new Point(14,1289)),new LayoutIcon("C",new Point(90,2)),new LayoutIcon("D",new Point(90,1289)),new LayoutIcon("E",new Point(160,1183)),new LayoutIcon("F",new Point(242,497)),new LayoutIcon("G",new Point(318,1289))};
        var nativeArea=new Rectangle(0,0,3440,1392);var basket=new Rectangle(2659,0,780,1344);
        var repaired=Apply(actual,LayoutPlanner.Compact(actual,new[]{basket},new[]{nativeArea},spacing));
        Check(repaired.Select(i=>i.Position).SequenceEqual(Enumerable.Range(0,7).Select(i=>new Point(14,2+i*99))),"Reported desktop holes must compact in native column order");
        var random=new Random(417);
        for(int iteration=0;iteration<100;iteration++)
        {
            var refs=Enumerable.Range(0,40).Select(i=>new LayoutIcon("icon"+i,new Point(14+(i/6)*76,2+(i%6)*99))).ToArray();
            var remaining=refs.Where(_=>random.Next(4)!=0).ToArray();
            var repair=Apply(remaining,LayoutPlanner.Compact(remaining,blocked,areas,spacing,refs));
            Check(repair.All(i=>area.Contains(LayoutPlanner.Cell(i.Position,spacing))&&!blocked.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))),"Random gaps: icon crossed monitor/basket bounds");
            Check(repair.Select(i=>i.Position).Distinct().Count()==repair.Length,"Random gaps: icon cells overlap");
            Check(LayoutPlanner.Compact(repair,blocked,areas,spacing,refs).Count==0,"Random gaps: repeated repair changed stable layout");
        }
        return new{Passed=true,Assertions=assertions,ExistingHolesFillWithoutVacancyHistory=true,NativeGridSpacingAndPaddingPreserved=true,BasketClearancePreserved=true,ColumnOrderPreserved=true,OffGridIconsSnap=true,NoMonitorTransfer=true,FullDesktopRejectedBeforeMutation=true,RepeatedRepairSettles=true};
    }
    public static object ArrangementDiagnose()
    {
        using var shell=new DesktopShell();var icons=shell.ReadIcons();var store=new Store();
        try
        {
            var assigned=new HashSet<string>(store.State.Baskets.SelectMany(b=>b.Entries).Select(e=>e.Path),StringComparer.OrdinalIgnoreCase);
            var visible=icons.Where(i=>!assigned.Contains(i.Key)).Select(i=>new LayoutIcon(i.Key,i.Position)).ToArray();
            var areas=Native.PhysicalScreens().Select(s=>shell.ToView(s.WorkingArea)).ToArray();
            var baskets=store.State.Baskets.Select(b=>shell.ToView(b.ScreenBounds)).ToArray();
            var reference=icons.Select(i=>
            {
                var backup=store.State.Icons.FirstOrDefault(b=>b.Hidden&&string.Equals(b.Key,i.Key,StringComparison.OrdinalIgnoreCase));
                return new LayoutIcon(i.Key,backup==null?i.Position:new Point(backup.X,backup.Y));
            }).ToArray();
            var plan=LayoutPlanner.Compact(visible,baskets,areas,shell.Spacing,reference);
            var packed=visible.Select(i=>new LayoutIcon(i.Key,plan.TryGetValue(i.Key,out var p)?p:i.Position)).ToArray();
            return new{Passed=true,ReadOnly=true,VisibleIcons=visible.Length,ClassifiedIcons=icons.Count-visible.Length,PlannedMoves=plan.Count,
                NativeSpacing=shell.Spacing,GridAnchors=areas.Select(a=>LayoutPlanner.GridAnchor(reference,a,shell.Spacing)).ToArray(),
                NoBasketOverlap=packed.All(i=>!baskets.Any(b=>b.IntersectsWith(LayoutPlanner.Footprint(i.Position,shell.Spacing)))),
                StableAfterRepair=LayoutPlanner.Compact(packed,baskets,areas,shell.Spacing,reference).Count==0};
        }
        finally{foreach(var icon in icons)icon.Dispose();}
    }
}
