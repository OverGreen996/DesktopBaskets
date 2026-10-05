

namespace DesktopBaskets;

public sealed class Entry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string? OriginalPath { get; set; }
    public string? Pending { get; set; }
    [Newtonsoft.Json.JsonIgnore] public bool Managed => false;
}

public sealed class Basket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新分類";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = Grid.DefaultWidth;
    public int Height { get; set; } = Grid.DefaultHeight;
    public string Monitor { get; set; } = "";
    public bool Locked { get; set; }
    public bool Collapsed { get; set; }
    public int OpacityPercent {get;set;}=68;
    public int SavedChromeHeight {get;set;}
    public List<Entry> Entries { get; set; } = new();
    [Newtonsoft.Json.JsonIgnore] public Rectangle ScreenBounds => new(X, Y, Width, Collapsed ? Grid.HeaderFor(Width) : Height);
    [Newtonsoft.Json.JsonIgnore] public int Columns => Math.Max(2, (Width - Grid.Side) / Grid.CellWidth);
    [Newtonsoft.Json.JsonIgnore] public int Rows => Math.Max(1, (Height - Grid.VerticalFor(Width)) / Grid.CellHeight);
}

internal static class Grid
{
    public const int Side=96, Header=44, Footer=38, Vertical=128, MinColumns=5, MinRows=1;
    public const int ContentLeft=48, ContentTop=64, ContentBottom=40;
    public static bool Compact(int width)=>width<800;
    public static int HeaderFor(int width)=>Compact(width)?72:Math.Max(Header,(int)Math.Round(width*102.0/1537));
    public static int FooterFor(int width)=>Compact(width)?48:Math.Max(Footer,(int)Math.Round(width*53.0/1537));
    public static int ContentTopFor(int width)=>Compact(width)?16:ContentTop;
    public static int ContentBottomFor(int width)=>Compact(width)?15:ContentBottom;
    public static int VerticalFor(int width)=>HeaderFor(width)+FooterFor(width)+ContentTopFor(width)+ContentBottomFor(width);
    public static int CellWidth {get;private set;}=76;
    public static int CellHeight {get;private set;}=99;
    public static int MinWidth=>MinColumns*CellWidth+Side;
    public static int MinHeight=>MinRows*CellHeight+VerticalFor(MinWidth);
    public static int DefaultWidth=>14*CellWidth+Side;
    public static int DefaultHeight=>4*CellHeight+VerticalFor(DefaultWidth);
    public static void Configure(Size spacing){CellWidth=Math.Max(48,spacing.Width);CellHeight=Math.Max(64,spacing.Height);}
    public static Size Snap(Size size)
    {
        int width=Math.Max(MinColumns,(int)Math.Round((size.Width-Side)/(double)CellWidth))*CellWidth+Side;
        return new Size(width,Math.Max(MinRows,(int)Math.Round((size.Height-VerticalFor(width))/(double)CellHeight))*CellHeight+VerticalFor(width));
    }
    public static Size SnapProportional(Size initial,Size proposed)
    {
        double sw=proposed.Width/(double)initial.Width,sh=proposed.Height/(double)initial.Height;
        double scale=Math.Abs(sw-1)>=Math.Abs(sh-1)?sw:sh;
        scale=Math.Max(scale,Math.Max(MinWidth/(double)initial.Width,MinHeight/(double)initial.Height));
        double targetW=initial.Width*scale,targetH=initial.Height*scale,ratio=initial.Width/(double)initial.Height;
        int rows=Math.Max(MinRows,(int)Math.Round((targetH-VerticalFor((int)targetW))/CellHeight));
        var candidates=new List<Size>();
        for(int r=Math.Max(MinRows,rows-1);r<=rows+1;r++)
        {
            int columns=Math.Max(MinColumns,(int)Math.Round((targetW-Side)/CellWidth));
            for(int c=Math.Max(MinColumns,columns-2);c<=columns+2;c++)
            {int width=c*CellWidth+Side;candidates.Add(new Size(width,r*CellHeight+VerticalFor(width)));}
        }
        return candidates.OrderBy(s=>Math.Abs(Math.Log((s.Width/(double)s.Height)/ratio))*3+Math.Abs(s.Width-targetW)/initial.Width+Math.Abs(s.Height-targetH)/initial.Height).First();
    }
    public static Size FitProportional(Size initial,Size proposed,Size available)
    {
        var size=SnapProportional(initial,proposed);if(size.Width<=available.Width&&size.Height<=available.Height)return size;
        double reduction=Math.Min(available.Width/(double)size.Width,available.Height/(double)size.Height);
        double tw=size.Width*reduction,th=size.Height*reduction,ratio=initial.Width/(double)initial.Height;
        var candidates=new List<Size>();
        for(int c=MinColumns;c*CellWidth+Side<=available.Width;c++)
        {
            int width=c*CellWidth+Side,chrome=VerticalFor(width),maxRows=(available.Height-chrome)/CellHeight;
            if(maxRows<MinRows)continue;
            int ideal=MathEx.Clamp((int)Math.Round((width/ratio-chrome)/CellHeight),MinRows,maxRows);
            for(int r=Math.Max(MinRows,ideal-1);r<=Math.Min(maxRows,ideal+1);r++)candidates.Add(new Size(width,r*CellHeight+chrome));
        }
        if(candidates.Count==0)throw new InvalidOperationException("這個螢幕無法容納完整整理框。請選擇更大的螢幕工作區。");
        return candidates.OrderBy(s=>Math.Abs(Math.Log((s.Width/(double)s.Height)/ratio))*4+Math.Abs(s.Width-tw)/initial.Width+Math.Abs(s.Height-th)/initial.Height).First();
    }
}

public sealed class IconBackup
{
    public string Key { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int LastX { get; set; }
    public int LastY { get; set; }
    public bool Hidden {get;set;}
}

public sealed class State
{
    public int Revision {get;set;}
    public DateTimeOffset ModifiedUtc {get;set;}=DateTimeOffset.UtcNow;
    public int Version { get; set; } = 2;
    public int GridWidth {get;set;}=96;
    public int GridHeight {get;set;}=96;
    public int GridVertical {get;set;}=90;
    public int GridSide {get;set;}=36;
    public int FrameLayoutVersion {get;set;}
    public bool Enabled { get; set; }
    public bool? OriginalAutoArrange { get; set; }
    public bool? OriginalSnapToGrid { get; set; }
    public List<Basket> Baskets { get; set; } = new();
    public List<IconBackup> Icons { get; set; } = new();
}

public sealed class Store
{
    public string Root { get; }
    public string DesktopRoot { get; }
    public string PublicDesktopRoot { get; }
    public string StatePath => System.IO.Path.Combine(Root, "state.json");
    public State State { get; private set; }
    public Store(string? root=null,string? desktopRoot=null,string? publicDesktopRoot=null)
    {
        Root=root??System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopBaskets");
        DesktopRoot=desktopRoot??Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        PublicDesktopRoot=publicDesktopRoot??Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        Directory.CreateDirectory(Root);
        if(File.Exists(StatePath))
        {
            try{State=JsonCodec.Deserialize<State>(File.ReadAllText(StatePath))??throw new InvalidDataException("設定內容是空的。");}
            catch(Exception ex){throw new InvalidDataException("無法讀取設定，原始檔已保留："+StatePath,ex);}
            if(State.Version!=1&&State.Version!=2)throw new InvalidDataException("不支援此設定檔版本。");
            if(State.Baskets.SelectMany(b=>b.Entries).Any(e=>e.OriginalPath!=null||e.Pending!=null))
                throw new InvalidDataException("舊版資料含搬檔紀錄，為保護原始路徑已停止載入。請先還原舊版資料。");
            State.Version=2;
            foreach(var basket in State.Baskets)
            {
                int columns=Math.Max(2,(basket.Width-State.GridSide)/Math.Max(48,State.GridWidth));
                int rows=Math.Max(1,(basket.Height-(basket.SavedChromeHeight>0?basket.SavedChromeHeight:State.GridVertical))/Math.Max(64,State.GridHeight));
                if(!basket.Locked){basket.Width=Math.Max(Grid.MinColumns,columns)*Grid.CellWidth+Grid.Side;basket.Height=Math.Max(Grid.MinRows,rows)*Grid.CellHeight+Grid.VerticalFor(basket.Width);}
                basket.OpacityPercent=MathEx.Clamp(basket.OpacityPercent,40,100);
            }
            if(State.GridVertical!=Grid.Vertical||State.FrameLayoutVersion<1)
            {
                // Keep the saved cell counts when the taller reference header is introduced.
                // Only resolve new collisions caused by the extra header height.
                var placed=State.Baskets.Where(b=>b.Locked).Select(b=>b.ScreenBounds).ToList();
                foreach(var basket in State.Baskets.Where(b=>!b.Locked))
                {
                    var area=Screen.FromRectangle(basket.ScreenBounds).WorkingArea;
                    if(placed.Any(r=>r.IntersectsWith(basket.ScreenBounds))||!area.Contains(basket.ScreenBounds))
                    {
                        var candidates=new List<Point>();
                        for(int py=area.Top+16;py+basket.ScreenBounds.Height<=area.Bottom;py+=16)
                        for(int px=area.Left+16;px+basket.Width<=area.Right;px+=16)
                            if(!placed.Any(r=>r.IntersectsWith(new Rectangle(px,py,basket.Width,basket.ScreenBounds.Height))))candidates.Add(new Point(px,py));
                        if(candidates.Count>0){var point=candidates.OrderBy(p=>Math.Pow(p.X-basket.X,2)+Math.Pow(p.Y-basket.Y,2)).First();basket.X=point.X;basket.Y=point.Y;}
                    }
                    placed.Add(basket.ScreenBounds);
                }
            }
        }
        else State=new State();
        State.GridWidth=Grid.CellWidth;State.GridHeight=Grid.CellHeight;State.GridVertical=Grid.Vertical;State.GridSide=Grid.Side;State.FrameLayoutVersion=1;
    }
    public void Save()
    {
        State.Revision++;State.ModifiedUtc=DateTimeOffset.UtcNow;
        foreach(var basket in State.Baskets)basket.SavedChromeHeight=Grid.VerticalFor(basket.Width);
        string temp=StatePath+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {JsonCodec.Serialize(stream,State,new JsonOptions{WriteIndented=true});stream.Flush(true);}
        if(File.Exists(StatePath))File.Replace(temp,StatePath,StatePath+".bak");else File.Move(temp,StatePath);
    }
    public static bool Exists(string path)=>File.Exists(path)||Directory.Exists(path);
    public Entry Add(Basket basket,string path)
    {
        path=System.IO.Path.GetFullPath(path);
        if(!Exists(path))throw new FileNotFoundException("找不到檔案。",path);
        var existing=State.Baskets.SelectMany(b=>b.Entries).FirstOrDefault(e=>string.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase));
        if(existing!=null){Transfer(basket,existing);return existing;}
        var entry=new Entry{Name=System.IO.Path.GetFileName(path),Path=path};
        basket.Entries.Add(entry);Save();return entry;
    }
    public void Remove(Basket basket,Entry entry){basket.Entries.Remove(entry);Save();}
    public void Delete(Basket basket){State.Baskets.Remove(basket);Save();}
    public void Transfer(Basket target,Entry entry)
    {
        foreach(var basket in State.Baskets)basket.Entries.Remove(entry);
        target.Entries.Add(entry);Save();
    }
}
public record LayoutIcon(string Key, Point Position);
public static class LayoutPlanner
{
    public static Rectangle Footprint(Point p, Size spacing) => new(p.X - 10, p.Y - 10, spacing.Width + 20, spacing.Height + 20);
    public static Dictionary<string, Point> Plan(IReadOnlyList<LayoutIcon> icons, IReadOnlyList<Rectangle> baskets,
        IReadOnlyList<Rectangle> workAreas, Size spacing,HashSet<string>? forceMove=null)
    {
        spacing = new Size(Math.Max(48, spacing.Width), Math.Max(64, spacing.Height));
        var result = new Dictionary<string, Point>();
        var occupied = new List<Rectangle>();
        var displaced = new List<LayoutIcon>();
        foreach (var icon in icons)
        {
            var box = Footprint(icon.Position, spacing);
            if (baskets.Any(b => b.IntersectsWith(box))||forceMove?.Contains(icon.Key)==true) displaced.Add(icon);
            else occupied.Add(box);
        }
        // Candidate cells use a generous footprint, including labels and a safety gutter.
        foreach (var icon in displaced)
        {
            var candidates = new List<Point>();
            foreach (var area in workAreas)
            for (int x = area.Left + 10; x + spacing.Width + 10 <= area.Right; x += spacing.Width + 20)
            for (int y = area.Top + 10; y + spacing.Height + 10 <= area.Bottom; y += spacing.Height + 20)
            {
                var p = new Point(x, y);
                var box = Footprint(p, spacing);
                if (!baskets.Any(b => b.IntersectsWith(box)) && !occupied.Any(b => b.IntersectsWith(box))) candidates.Add(p);
            }
            if (candidates.Count == 0) throw new InvalidOperationException("桌面空間不足：請縮小整理籃、分類更多圖示，或減少整理籃。這次位置沒有套用。");
            var best = candidates.OrderBy(p => Math.Pow((double)p.X - icon.Position.X, 2) + Math.Pow((double)p.Y - icon.Position.Y, 2)).First();
            result.Add(icon.Key, best);
            occupied.Add(Footprint(best, spacing));
        }
        return result;
    }
}
