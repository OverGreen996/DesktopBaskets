namespace DesktopBaskets;

public sealed class BasketPlacement
{
    public int X {get;set;}
    public int Y {get;set;}
    public int Width {get;set;}
    public int Height {get;set;}
    public string Monitor {get;set;}="";
    internal Rectangle Bounds(bool collapsed)=>new(X,Y,Width,collapsed?Grid.HeaderFor(Width):Height);
    internal static BasketPlacement From(Basket basket)=>new(){X=basket.X,Y=basket.Y,Width=basket.Width,Height=basket.Height,Monitor=basket.Monitor};
    internal void Apply(Basket basket){basket.X=X;basket.Y=Y;basket.Width=Width;basket.Height=Height;basket.Monitor=Monitor;}
}
internal sealed record DisplayScreen(string DeviceName,Rectangle Bounds,Rectangle WorkingArea,bool Primary=false);
internal sealed record DisplayMove(Basket Basket,BasketPlacement Placement,BasketPlacement? Home);
internal static class DisplayLayout
{
    internal static bool TryFit(Basket basket,BasketPlacement placement,Rectangle area,out Size size)
    {
        size=new Size(placement.Width,placement.Height);
        if(area.Width<=0||area.Height<=0||size.Width<=0||size.Height<=0)return false;
        if(size.Width<=area.Width&&(basket.Collapsed?Grid.HeaderFor(size.Width):size.Height)<=area.Height)return true;
        if(area.Width<Grid.MinWidth||area.Height<(basket.Collapsed?Grid.HeaderFor(Grid.MinWidth):Grid.MinHeight))return false;
        try
        {
            // A collapsed frame can retain its full height for later expansion.
            var available=new Size(area.Width,basket.Collapsed?Math.Max(area.Height,placement.Height):area.Height);
            size=Grid.FitProportional(size,size,available);
            return (basket.Collapsed?Grid.HeaderFor(size.Width):size.Height)<=area.Height;
        }
        catch(InvalidOperationException){return false;}
    }
    internal static DisplayScreen At(Point point,IReadOnlyList<DisplayScreen> screens)
    {
        if(screens.Count==0)throw new InvalidOperationException("目前沒有可用的螢幕。");
        return screens.FirstOrDefault(s=>s.Bounds.Contains(point))??screens.OrderBy(s=>Distance(point,s.Bounds)).First();
    }
    static double Distance(Point point,Rectangle area)
    {double dx=point.X-MathEx.Clamp(point.X,area.Left,area.Right-1),dy=point.Y-MathEx.Clamp(point.Y,area.Top,area.Bottom-1);return dx*dx+dy*dy;}
    internal static bool TryPlace(Basket basket,Rectangle target,DisplayScreen screen,out BasketPlacement result)
    {
        result=new BasketPlacement{X=target.X,Y=target.Y,Width=target.Width,Height=basket.Collapsed&&target.Height==Grid.HeaderFor(target.Width)?basket.Height:target.Height,Monitor=screen.DeviceName};
        if(!TryFit(basket,result,screen.WorkingArea,out var size))return false;
        result.Width=size.Width;result.Height=size.Height;
        result.X=MathEx.Clamp(result.X,screen.WorkingArea.Left,screen.WorkingArea.Right-size.Width);
        result.Y=MathEx.Clamp(result.Y,screen.WorkingArea.Top,screen.WorkingArea.Bottom-(basket.Collapsed?Grid.HeaderFor(size.Width):size.Height));return true;
    }
    internal static bool FindSpace(Basket basket,BasketPlacement placement,Rectangle area,IReadOnlyList<Rectangle> occupied)
    {
        var bounds=placement.Bounds(basket.Collapsed);if(!occupied.Any(r=>r.IntersectsWith(bounds)))return true;
        Point? free=null;double nearest=double.MaxValue;
        for(int y=area.Top;y+bounds.Height<=area.Bottom;y+=16)
        for(int x=area.Left;x+bounds.Width<=area.Right;x+=16)
        {
            var candidate=new Rectangle(x,y,bounds.Width,bounds.Height);if(occupied.Any(r=>r.IntersectsWith(candidate)))continue;
            double distance=Math.Pow((double)x-placement.X,2)+Math.Pow((double)y-placement.Y,2);
            if(distance<nearest){nearest=distance;free=new Point(x,y);}
        }
        if(free==null)return false;placement.X=free.Value.X;placement.Y=free.Value.Y;return true;
    }
    internal static bool TryRecover(IReadOnlyList<Basket> baskets,IReadOnlyList<DisplayScreen> screens,out List<DisplayMove> moves)
    {
        moves=new List<DisplayMove>();if(screens.Count==0)return false;
        // Reserve already valid frames first so a disconnected monitor's frames
        // cannot displace or overlap frames on a monitor that stayed connected.
        var ordered=baskets.OrderBy(b=>screens.Any(s=>s.DeviceName==(b.DisplayHome??BasketPlacement.From(b)).Monitor&&s.WorkingArea.Contains((b.DisplayHome??BasketPlacement.From(b)).Bounds(b.Collapsed)))?0:1).ToArray();
        foreach(var basket in ordered)
        {
            var desired=basket.DisplayHome??BasketPlacement.From(basket);
            var candidates=screens.OrderBy(s=>s.DeviceName==desired.Monitor?0:1).ThenBy(s=>Distance(new Point(desired.X,desired.Y),s.WorkingArea));
            bool found=false;
            foreach(var screen in candidates)
            {
                if(!TryPlace(basket,new Rectangle(desired.X,desired.Y,desired.Width,desired.Height),screen,out var placement))continue;
                var area=screen.WorkingArea;var occupied=moves.Select(m=>m.Placement.Bounds(m.Basket.Collapsed)).ToArray();
                if(!FindSpace(basket,placement,area,occupied))continue;
                bool original=(placement.Monitor==desired.Monitor||desired.Monitor.Length==0)&&placement.X==desired.X&&placement.Y==desired.Y&&placement.Width==desired.Width&&placement.Height==desired.Height;
                moves.Add(new DisplayMove(basket,placement,original?null:desired));found=true;break;
            }
            if(!found){moves.Clear();return false;}
        }
        return true;
    }
}
