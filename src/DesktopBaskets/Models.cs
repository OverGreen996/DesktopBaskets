

namespace DesktopBaskets;

public sealed class Entry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    string? displayName,displaySourceName,displaySourcePath;
    [Newtonsoft.Json.JsonIgnore] public string DisplayName
    {
        get
        {
            if(displayName!=null&&displaySourceName==Name&&displaySourcePath==Path)return displayName;
            displaySourceName=Name;displaySourcePath=Path;displayName=Name;
            string extension=System.IO.Path.GetExtension(Path);
            // Cache only the label. Shell operations continue using the full path.
            if(new[]{".lnk",".url",".website",".appref-ms"}.Contains(extension,StringComparer.OrdinalIgnoreCase)
                &&Name.EndsWith(extension,StringComparison.OrdinalIgnoreCase)&&!Directory.Exists(Path))
                displayName=Name.Substring(0,Name.Length-extension.Length);
            return displayName;
        }
    }
    public string? OriginalPath { get; set; }
    public string? Pending { get; set; }
    [Newtonsoft.Json.JsonIgnore] public bool Managed => false;
}

public sealed class Basket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "新分類";
    public string Kind {get;set;}="local";
    [Newtonsoft.Json.JsonIgnore] public bool Shared=>Kind=="share";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = Grid.DefaultWidth;
    public int Height { get; set; } = Grid.DefaultHeight;
    public string Monitor { get; set; } = "";
    public bool Locked { get; set; }
    public bool Collapsed { get; set; }
    public int OpacityPercent {get;set;}=68;
    public int SavedChromeHeight {get;set;}
    public BasketPlacement? DisplayHome {get;set;}
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
    public List<UnavailableEntry> UnavailableEntries {get;set;}=new();
    public List<IconBackup> Icons { get; set; } = new();
}

// File.Exists can be false while an installer replaces a shortcut, a drive is
// offline, or permissions are unavailable. Preserve membership independently
// of availability; only explicit basket operations relinquish ownership.
public sealed class UnavailableEntry
{
    public string BasketId {get;set;}="";
    public int Index {get;set;}
    public Entry Entry {get;set;}=new();
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
            State.UnavailableEntries??=new();
            RestoreAvailableEntries();
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
    public bool RestoreAvailableEntries()
    {
        bool changed=false;
        foreach(var saved in State.UnavailableEntries.OrderBy(e=>e.Index).ToArray())
        {
            var basket=State.Baskets.FirstOrDefault(b=>b.Id==saved.BasketId);
            if(basket==null){State.UnavailableEntries.Remove(saved);changed=true;continue;}
            if(!Exists(saved.Entry.Path))continue;
            if(!State.Baskets.SelectMany(b=>b.Entries).Any(e=>e.Id==saved.Entry.Id||string.Equals(e.Path,saved.Entry.Path,StringComparison.OrdinalIgnoreCase)))
                basket.Entries.Insert(MathEx.Clamp(saved.Index,0,basket.Entries.Count),saved.Entry);
            State.UnavailableEntries.Remove(saved);changed=true;
        }
        return changed;
    }
    public bool RefreshAvailability(Func<Entry,bool> inspect)
    {
        bool changed=RestoreAvailableEntries();
        foreach(var basket in State.Baskets)
        {
            var entries=basket.Entries.ToArray();
            var previous=State.UnavailableEntries.Where(e=>e.BasketId==basket.Id).OrderBy(e=>e.Index).ToArray();
            for(int index=0;index<entries.Length;index++)
            {
                var entry=entries[index];if(!inspect(entry)||Exists(entry.Path))continue;
                int originalIndex=index;
                foreach(var earlier in previous)if(earlier.Index<=originalIndex)originalIndex++;
                if(!State.UnavailableEntries.Any(e=>e.Entry.Id==entry.Id))
                    State.UnavailableEntries.Add(new UnavailableEntry{BasketId=basket.Id,Index=originalIndex,Entry=entry});
                basket.Entries.Remove(entry);changed=true;
            }
        }
        return changed;
    }
    public Entry Add(Basket basket,string path)
    {
        path=System.IO.Path.GetFullPath(path);
        if(!Exists(path))throw new FileNotFoundException("找不到檔案。",path);
        var existing=State.Baskets.SelectMany(b=>b.Entries).FirstOrDefault(e=>string.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase));
        if(existing!=null){Transfer(basket,existing);return existing;}
        var saved=State.UnavailableEntries.FirstOrDefault(e=>string.Equals(e.Entry.Path,path,StringComparison.OrdinalIgnoreCase));
        var entry=saved?.Entry??new Entry{Name=System.IO.Path.GetFileName(path),Path=path};
        State.UnavailableEntries.RemoveAll(e=>e.Entry.Id==entry.Id||string.Equals(e.Entry.Path,path,StringComparison.OrdinalIgnoreCase));
        basket.Entries.Add(entry);Save();return entry;
    }
    public void Remove(Basket basket,Entry entry){basket.Entries.Remove(entry);State.UnavailableEntries.RemoveAll(e=>e.Entry.Id==entry.Id);Save();}
    public void Delete(Basket basket){State.Baskets.Remove(basket);State.UnavailableEntries.RemoveAll(e=>e.BasketId==basket.Id);Save();}
    public void Transfer(Basket target,Entry entry)
    {
        foreach(var basket in State.Baskets)basket.Entries.Remove(entry);
        State.UnavailableEntries.RemoveAll(e=>e.Entry.Id==entry.Id);
        target.Entries.Add(entry);Save();
    }
}
public record LayoutIcon(string Key, Point Position);
public static class LayoutPlanner
{
    public static Rectangle Footprint(Point p, Size spacing) => new(p.X - 10, p.Y - 10, spacing.Width + 20, spacing.Height + 20);
    // Explorer spacing already includes its label area. Adjacent native cells may
    // share our extra basket-clearance gutter without overlapping each other.
    public static Rectangle Cell(Point p,Size spacing)=>new(p,spacing);
    public static Point GridAnchor(IReadOnlyList<LayoutIcon> reference,Rectangle area,Size spacing)
    {
        var local=reference.Where(i=>area.Contains(i.Position)).Select(i=>i.Position).ToArray();
        int Phase(IEnumerable<int> values,int step,int fallback)
        {
            var groups=values.Select(n=>((n%step)+step)%step).GroupBy(n=>n).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).ToArray();
            return groups.Length==0?fallback:groups[0].Key;
        }
        // Explorer's actual positions reveal the native grid phase, including
        // its icon padding. Do not invent a second grid with extra cell gaps.
        return new Point(area.Left+Phase(local.Select(p=>p.X-area.Left),spacing.Width,Math.Max(0,(spacing.Width-48)/2)),
            area.Top+Phase(local.Select(p=>p.Y-area.Top),spacing.Height,2));
    }
    public static Dictionary<string,Point> Compact(IReadOnlyList<LayoutIcon> icons,IReadOnlyList<Rectangle> baskets,
        IReadOnlyList<Rectangle> workAreas,Size spacing,IReadOnlyList<LayoutIcon>? gridReference=null)
    {
        var result=new Dictionary<string,Point>(StringComparer.OrdinalIgnoreCase);
        if(icons.Count==0)return result;
        if(workAreas.Count==0)throw new InvalidOperationException("沒有可用的桌面工作區。");
        spacing=new Size(Math.Max(48,spacing.Width),Math.Max(64,spacing.Height));
        int Monitor(Point p)
        {
            for(int i=0;i<workAreas.Count;i++)if(workAreas[i].Contains(p))return i;
            return Enumerable.Range(0,workAreas.Count).OrderBy(i=>
            {
                var area=workAreas[i];double dx=p.X-MathEx.Clamp(p.X,area.Left,area.Right-1),dy=p.Y-MathEx.Clamp(p.Y,area.Top,area.Bottom-1);
                return dx*dx+dy*dy;
            }).First();
        }
        var groups=icons.Select((icon,index)=>new{Icon=icon,Index=index,Monitor=Monitor(icon.Position)}).GroupBy(i=>i.Monitor);
        foreach(var group in groups)
        {
            var area=workAreas[group.Key];var start=GridAnchor(gridReference??icons,area,spacing);
            var ordered=group.OrderBy(i=>i.Icon.Position.X).ThenBy(i=>i.Icon.Position.Y).ThenBy(i=>i.Index).Select(i=>i.Icon).ToArray();
            int index=0;
            for(int x=start.X;x+spacing.Width<=area.Right&&index<ordered.Length;x+=spacing.Width)
            for(int y=start.Y;y+spacing.Height<=area.Bottom&&index<ordered.Length;y+=spacing.Height)
            {
                var point=new Point(x,y);
                if(baskets.Any(b=>b.IntersectsWith(Footprint(point,spacing))))continue;
                var icon=ordered[index++];if(icon.Position!=point)result.Add(icon.Key,point);
            }
            if(index<ordered.Length)throw new InvalidOperationException("桌面空間不足：請縮小整理籃、分類更多圖示，或減少整理籃。這次位置沒有套用。");
        }
        return result;
    }
    public static Dictionary<string,Point> FillVacancies(IReadOnlyList<LayoutIcon> icons,IEnumerable<Point> vacancies,
        IReadOnlyList<Rectangle> baskets,IReadOnlyList<Rectangle> workAreas,Size spacing)
    {
        var positions=icons.ToDictionary(i=>i.Key,i=>i.Position,StringComparer.OrdinalIgnoreCase);
        var result=new Dictionary<string,Point>(StringComparer.OrdinalIgnoreCase);
        bool Before(Point a,Point b)=>a.X<b.X||(a.X==b.X&&a.Y<b.Y);
        foreach(var area in workAreas)
        {
            var holes=new HashSet<Point>(vacancies.Where(p=>area.Contains(Cell(p,spacing))));
            while(holes.Count>0)
            {
                var hole=holes.OrderBy(p=>p.X).ThenBy(p=>p.Y).First();holes.Remove(hole);
                var cell=Cell(hole,spacing);
                if(baskets.Any(b=>b.IntersectsWith(Footprint(hole,spacing)))||positions.Values.Any(p=>cell.IntersectsWith(Cell(p,spacing))))continue;
                var next=positions.Where(i=>area.Contains(Cell(i.Value,spacing))&&Before(hole,i.Value)
                    &&!baskets.Any(b=>b.IntersectsWith(Footprint(i.Value,spacing))))
                    .OrderBy(i=>i.Value.X).ThenBy(i=>i.Value.Y).FirstOrDefault();
                if(next.Key==null)continue;
                var freed=next.Value;positions[next.Key]=hole;result[next.Key]=hole;holes.Add(freed);
            }
        }
        return result;
    }
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
