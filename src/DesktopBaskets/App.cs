namespace DesktopBaskets;

internal sealed class App : ApplicationContext
{
    public Store Store { get; }
    public IconCache Icons { get; }=new();
    public ManagerWindow Manager { get; }
    internal StartupSettings Startup { get; }
    readonly DesktopLayout layout;
    readonly Dictionary<string,BasketWindow> windows=new();
    internal IReadOnlyCollection<BasketWindow> DesktopWindows=>windows.Values;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer debounce=new(){Interval=300};
    readonly System.Windows.Forms.Timer idle=new(){Interval=30000};
    readonly WakeFilter wakeFilter;
    public bool IsStandby {get;private set;}
    public const int StandbyAfterSeconds=30;
    DesktopEvents? events;
    readonly List<FileSystemWatcher> watchers=new();
    int completedEventsSeen,completedEventsRelevant;
    public int DesktopEventsSeen => completedEventsSeen+(events?.Seen??0);
    public int DesktopEventsRelevant => completedEventsRelevant+(events?.Relevant??0);
    internal int DesktopCheckCount {get;private set;}
    public int VisualPaintCount=>windows.Values.Sum(w=>w.ChromePaintCount+w.GlassPaintCount+w.Viewport.PaintCount);
    bool applying,quitting,desktopRefreshed;
    bool fileMenuPending,fileMenuOpen;
    internal Action<object>? FileMenuObserved;
    bool hideVerificationWindows;
    internal void HideVerificationWindows(){hideVerificationWindows=true;foreach(var w in windows.Values)w.Hide();Manager.Hide();}
    public App(Store store,bool smoke=false,bool autoStart=false,StartupSettings? startup=null)
    {
        Store=store;Startup=startup??new StartupSettings();layout=new DesktopLayout(store);Manager=new ManagerWindow(this);
        Icons.ImageReady+=path=>{foreach(var window in windows.Values)if(!window.IsDisposed)window.Viewport.RefreshImage(path);};
        Theme.ErrorOwner=Manager;
        // Upgrade the installer's older command to the explicit login mode.
        if(!smoke&&Startup.Enabled)Theme.Try(()=>Startup.SetEnabled(true));
        wakeFilter=new WakeFilter(this);Application.AddMessageFilter(wakeFilter);
        MainForm=Manager;
        var menu=new ContextMenuStrip();
        menu.Items.Add("開啟分類管理",null,(_,_)=>ShowManager());
        menu.Items.Add("新增分類",null,(_,_)=>NewBasket());
        menu.Items.Add("啟用／暫停桌面",null,(_,_)=>Toggle());
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出並還原圖示",null,(_,_)=>Quit());
        tray=new NotifyIcon{Icon=Theme.AppIcon,Text="Desktop Baskets · 桌面分類",Visible=!smoke,ContextMenuStrip=menu};
        tray.DoubleClick+=(_,_)=>ShowManager();
        debounce.Tick+=(_,_)=>{debounce.Stop();CheckDesktop();};
        idle.Tick+=(_,_)=>EnterStandby();idle.Start();
        if(!smoke)WatchDesktop();
        if(autoStart)_=Manager.Handle;else Manager.Show();
        if(!smoke)
        {
            try {layout.Restore();} // recover interrupted session before establishing a new one
            catch(Exception ex){Store.State.Enabled=false;Store.Save();ShowManager();Manager.SetStatus("上次的桌面位置還原失敗，備份仍保留。",true);Theme.Error(ex);}
            if(Store.State.Enabled)try{ApplyLayout();}catch(Exception ex){ShowManager();Manager.SetStatus("籃框開啟失敗，設定與原始檔案保留。",true);Theme.Error(ex);}
            else Manager.SetStatus("準備就緒 · 新增分類會自動放上桌面。關閉管理視窗後可從系統匣開啟。");
        }
    }
    internal void SetAutoStart(bool enabled)
    {
        Wake();
        try
        {
            Startup.SetEnabled(enabled);
            Manager.SetStatus(enabled?"已設定 · 登入 Windows 後自動啟動並開啟籃子，管理視窗留在系統匣。":"已關閉登入自動啟動 · 目前的籃子仍保持原狀。");
        }
        catch(Exception ex){Manager.SetStatus("無法更新登入啟動設定："+ex.Message,true);Theme.Error(ex);}
        finally{Manager.RefreshStartup();}
    }
    public void ShowManager(){Wake();Manager.Show();if(Manager.WindowState!=FormWindowState.Normal)Manager.CaptionCommand(0xF120);Manager.Activate();}
    public void Wake()
    {
        if(quitting)return;
        idle.Stop();idle.Start();
        if(!IsStandby)return;
        IsStandby=false;tray.Text="Desktop Baskets · 桌面分類";
        foreach(var window in windows.Values)window.RefreshStatus();
        Manager.SetStatus(Store.State.Enabled?"已喚醒 · 桌面圖示自動避讓 · 格數縮放／滾輪捲動":"已喚醒 · 新增分類會自動放上桌面");
    }
    public void EnterStandby()
    {
        idle.Stop();if(IsStandby||quitting)return;
        if(applying||fileMenuOpen||fileMenuPending||windows.Values.Any(w=>w.Viewport.IsSelecting)){idle.Start();return;}
        IsStandby=true;Icons.Clear();
        foreach(var window in windows.Values)window.RefreshStatus();
        tray.Text="Desktop Baskets · 微待命";
        Manager.SetStatus("微待命 · 點擊、拖曳或捲動會直接喚醒 · 桌面變動仍會自動避讓");
        // One trim per idle transition. Do not repeatedly trim or suspend the message loop.
        GC.Collect(2,GCCollectionMode.Forced);GC.WaitForPendingFinalizers();
        using(var process=System.Diagnostics.Process.GetCurrentProcess())TrimWorkingSet(process.Handle);
    }
    [System.Runtime.InteropServices.DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);
    static void TrimWorkingSet(IntPtr handle){try{EmptyWorkingSet(handle);}catch(EntryPointNotFoundException){}}
    sealed class WakeFilter : IMessageFilter
    {
        readonly App app;public WakeFilter(App app){this.app=app;}
        public bool PreFilterMessage(ref Message message)
        {
            bool interaction=message.Msg is 0x100 or 0x104 or 0x201 or 0x203 or 0x204 or 0x206 or 0x207 or 0x20A or 0x20B or 0x20E or 0x114 or 0x115;
            if(message.Msg==0x200&&message.WParam.ToInt64()!=0)interaction=true;
            if(interaction)app.Wake();return false;
        }
    }
    public void NewBasket()
    {
        ShowManager();var area=Screen.FromControl(Manager).WorkingArea;
        // Reserve space on the right first; overlapped icons will be displaced automatically.
        var model=new Basket{Name="新分類",X=area.Right-356,Y=area.Top+24,Monitor=Screen.FromControl(Manager).DeviceName};
        var rects=Store.State.Baskets.Select(b=>b.ScreenBounds).ToArray();
        bool found=false;
        for(int y=area.Top+24;y+model.Height<=area.Bottom&&!found;y+=model.Height+16)
        for(int x=area.Right-model.Width-24;x>=area.Left;x-=model.Width+16)
        {
            var rect=new Rectangle(x,y,model.Width,model.Height);
            if(rects.Any(r=>r.IntersectsWith(rect)))continue;
            model.X=x;model.Y=y;found=true;break;
        }
        if(!found){Theme.Error(new InvalidOperationException("沒有足夠空間容納新分類。請先縮小其他分類籃。"));return;}
        using var dialog=new BasketDialog(model);
        if(dialog.ShowDialog(Manager)!=DialogResult.OK)return;
        bool previouslyEnabled=Store.State.Enabled;
        Store.State.Baskets.Add(model);Store.State.Enabled=true;
        try {ApplyLayout();Store.Save();Refresh(model.Id);}
        catch(Exception ex)
        {
            Store.State.Baskets.Remove(model);Store.State.Enabled=previouslyEnabled;
            RecoverLayout();Refresh();Theme.Error(ex);
        }
    }
    public void EditBasket(Basket basket)
    {
        ShowManager();var old=CopyGeometry(basket);
        using var dialog=new BasketDialog(basket);
        if(dialog.ShowDialog(Manager)!=DialogResult.OK)return;
        try {if(Store.State.Enabled)ApplyLayout();Store.Save();Refresh(basket.Id);}
        catch(Exception ex){RestoreGeometry(basket,old);RecoverLayout();Refresh();Theme.Error(ex);}
    }
    static Basket CopyGeometry(Basket b)=>new(){Name=b.Name,X=b.X,Y=b.Y,Width=b.Width,Height=b.Height,Collapsed=b.Collapsed,Locked=b.Locked,Monitor=b.Monitor,OpacityPercent=b.OpacityPercent};
    static void RestoreGeometry(Basket b,Basket old){b.Name=old.Name;b.X=old.X;b.Y=old.Y;b.Width=old.Width;b.Height=old.Height;b.Collapsed=old.Collapsed;b.Locked=old.Locked;b.Monitor=old.Monitor;b.OpacityPercent=old.OpacityPercent;}
    public void Place(Basket basket,Rectangle target,bool resized)
    {
        if(basket.Locked)return;
        var old=CopyGeometry(basket);var screen=Screen.FromRectangle(target);var area=screen.WorkingArea;
        if(resized){var size=Grid.FitProportional(old.ScreenBounds.Size,target.Size,area.Size);basket.Width=size.Width;basket.Height=size.Height;}
        basket.X=MathEx.Clamp(target.X,area.Left,area.Right-basket.Width);
        basket.Y=MathEx.Clamp(target.Y,area.Top,area.Bottom-(basket.Collapsed?Grid.HeaderFor(basket.Width):basket.Height));basket.Monitor=screen.DeviceName;
        try {ApplyLayout();Store.Save();Refresh(basket.Id);}
        catch(Exception ex){RestoreGeometry(basket,old);RecoverLayout();Refresh();Theme.Error(ex);}
    }
    public Rectangle Magnetize(Basket basket,Rectangle proposed)=>Magnet.Move(proposed,Store.State.Baskets.Where(b=>b!=basket).Select(b=>b.ScreenBounds));
    public void Collapse(Basket basket)
    {
        if(basket.Locked)return;
        basket.Collapsed=!basket.Collapsed;
        try {if(Store.State.Enabled)ApplyLayout();Store.Save();Refresh(basket.Id);}
        catch(Exception ex){basket.Collapsed=!basket.Collapsed;RecoverLayout();Theme.Error(ex);}
    }
    public void ToggleLock(Basket basket)
    {
        basket.Locked=!basket.Locked;Store.Save();Refresh(basket.Id);
    }
    public void DeleteBasket(Basket basket)
    {
        // Removing a basket restores collected files; references are only detached.
        Theme.Try(()=>{Store.Delete(basket);if(Store.State.Enabled){if(Store.State.Baskets.Count==0)Toggle();else ApplyLayout();}Refresh();});
    }
    public void PickDesktop(Basket basket)
    {
        ShowManager();using var picker=new DesktopPicker();
        if(picker.ShowDialog(Manager)==DialogResult.OK)AddPaths(basket,picker.SelectedPaths);
    }
    public void AddPaths(Basket basket,IEnumerable<string> paths)
    {
        var errors=new List<string>();
        foreach(var path in paths)try{Store.Add(basket,path);}catch(Exception ex){errors.Add(System.IO.Path.GetFileName(path)+"："+ex.Message);}
        if(Store.State.Enabled)try{ApplyLayout();}catch(Exception ex){errors.Add(ex.Message);}
        Refresh(basket.Id);
        if(errors.Count>0)Theme.Error(new IOException(string.Join("\n",errors)));
    }
    public void RemoveEntry(Basket basket,Entry entry)
        =>RemoveEntries(basket,new[]{entry});
    public void RemoveEntries(Basket basket,IEnumerable<Entry> entries)
    {
        var ids=new HashSet<string>(entries.Select(e=>e.Id));if(ids.Count==0)return;
        Theme.Try(()=>{basket.Entries.RemoveAll(e=>ids.Contains(e.Id));Store.Save();if(Store.State.Enabled)ApplyLayout();Refresh(basket.Id);});
    }
    public bool CanReturnToDesktop(Entry entry)=>new[]{Store.DesktopRoot,Store.PublicDesktopRoot}
        .Any(root=>string.Equals(Path.GetDirectoryName(entry.Path),root.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase));
    public bool IsDesktopDrop(Point point)
    {
        using var dpi=new Native.PhysicalDpiScope();
        if(!Store.State.Enabled||Store.State.Baskets.Any(b=>b.ScreenBounds.Contains(point))||!Native.PhysicalScreens().Any(s=>s.WorkingArea.Contains(point)))return false;
        var shell=layout.Shell;var target=Native.WindowFromPoint(new Native.POINT(point));
        return target==shell.List||target==shell.Host||Native.IsChild(shell.List,target);
    }
    public void ReturnEntryToDesktop(string id,Point point)
        =>ReturnEntriesToDesktop(new[]{id},point);
    public void ReturnEntriesToDesktop(IEnumerable<string> ids,Point point)
    {
        var chosen=new HashSet<string>(ids);
        var entries=Store.State.Baskets.SelectMany(b=>b.Entries).Where(e=>chosen.Contains(e.Id)).ToArray();
        if(entries.Length==0||entries.Any(e=>!CanReturnToDesktop(e))||!Store.State.Enabled)return;
        var backups=Store.State.Baskets.ToDictionary(b=>b,b=>b.Entries.ToArray());
        foreach(var basket in Store.State.Baskets)basket.Entries.RemoveAll(e=>chosen.Contains(e.Id));
        var positions=new Dictionary<string,Point>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<entries.Length;i++)positions[entries[i].Path]=new Point(point.X,point.Y+i*Grid.CellHeight);
        try{ApplyLayout(false,positions);Store.Save();Refresh();}
        catch(Exception ex){foreach(var backup in backups){backup.Key.Entries.Clear();backup.Key.Entries.AddRange(backup.Value);}RecoverLayout();Store.Save();Refresh();Theme.Error(ex);}
    }
    public void TransferEntry(Basket target,string id)
        =>TransferEntries(target,new[]{id});
    public void TransferEntries(Basket target,IEnumerable<string> ids)
    {
        var chosen=new HashSet<string>(ids);
        var entries=Store.State.Baskets.Where(b=>b!=target).SelectMany(b=>b.Entries).Where(e=>chosen.Contains(e.Id)).ToArray();
        if(entries.Length==0)return;
        var movingIds=new HashSet<string>(entries.Select(e=>e.Id));
        Theme.Try(()=>{foreach(var basket in Store.State.Baskets)basket.Entries.RemoveAll(e=>movingIds.Contains(e.Id));target.Entries.AddRange(entries);Store.Save();Refresh(target.Id);});
    }
    public void ShowEntryMenu(Entry entry,Point screenPoint)
        =>ShowEntriesMenu(new[]{entry},screenPoint);
    public void ShowEntriesMenu(IEnumerable<Entry> selection,Point screenPoint)
    {
        var entries=selection.GroupBy(e=>e.Id).Select(g=>g.First()).ToArray();if(entries.Length==0)return;
        if(fileMenuPending||fileMenuOpen||quitting||Manager.IsDisposed)return;
        fileMenuPending=true;var modifiers=Control.ModifierKeys;
        // Finish the originating mouse-up (including glass forwarding and capture
        // cleanup) before starting the native menu's nested message loop.
        Manager.BeginInvoke(new Action(()=>
        {
            fileMenuPending=false;if(quitting||Manager.IsDisposed)return;
            var existing=entries.Where(e=>Store.Exists(e.Path)).ToArray();
            if(existing.Length!=entries.Length)RefreshMissingSelection(entries);
            if(existing.Length==0)return;
            fileMenuOpen=true;idle.Stop();debounce.Stop();
            var owner=Manager.Handle;
            ShellContextMenu.ShowOnUiThread(existing.Select(e=>e.Path),screenPoint,owner,modifiers,(observation,error)=>
            {
                if(quitting||Manager.IsDisposed)return;
                try{Manager.BeginInvoke(new Action(()=>
                {
                    if(quitting)return;
                    try
                    {
                        FileMenuObserved?.Invoke(observation);
                        // Bounded local diagnostics contain menu timings and HWND/thread
                        // lifecycle only, never filenames or file contents.
                        try{var log=Path.Combine(Store.Root,"native-menu-diagnostics.jsonl");
                            if(File.Exists(log)&&new FileInfo(log).Length>65536)File.WriteAllText(log,"");
                            File.AppendAllText(log,JsonCodec.Serialize(new{Utc=DateTimeOffset.UtcNow,Menu=observation})+Environment.NewLine);}catch{}
                        if(error!=null)Theme.Error(error);
                        RefreshMissingSelection(entries);
                    }
                    finally{fileMenuOpen=false;Wake();ScheduleCheck();}
                }));}catch(InvalidOperationException){ /* UI shutdown already completed. */ }
            });
        }));
    }
    internal void RefreshMissingEntries(Entry? menuEntry=null)
        =>RefreshMissingSelection(menuEntry==null?Array.Empty<Entry>():new[]{menuEntry});
    internal void RefreshMissingSelection(IEnumerable<Entry> entries)
    {
        var ids=new HashSet<string>(entries.Select(e=>e.Id));
        bool changed=false;
        foreach(var basket in Store.State.Baskets)
            changed|=basket.Entries.RemoveAll(e=>(ids.Contains(e.Id)||CanReturnToDesktop(e))&&!Store.Exists(e.Path))>0;
        if(changed){Store.Save();Refresh();}
    }
    public ContextMenuStrip BasketMenu(Basket basket)
    {
        var menu=new ContextMenuStrip();menu.Items.Add("分類設定／重新命名",null,(_,_)=>EditBasket(basket));
        menu.Items.Add(basket.Locked?"解鎖位置與大小":"鎖定位置與大小",null,(_,_)=>ToggleLock(basket));
        menu.Items.Add(basket.Collapsed?"展開":"收合",null,(_,_)=>Collapse(basket)).Enabled=!basket.Locked;
        menu.Items.Add("加入桌面檔案",null,(_,_)=>PickDesktop(basket));menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("移除分類，圖示顯示回桌面",null,(_,_)=>DeleteBasket(basket));
        // ToolStrip raises Closed before dispatching the clicked item's action.
        menu.Closed+=(_,_)=>Manager.BeginInvoke(new Action(()=>menu.Dispose()));return menu;
    }
    public void Toggle()
    {
        if(Store.State.Enabled)
        {
            foreach(var window in windows.Values)window.Hide();events?.Dispose();events=null;debounce.Stop();
            try{layout.Restore();Store.State.Enabled=false;Store.Save();Refresh();Manager.SetStatus("已暫停 · 桌面圖示已還原 · 原始檔案路徑全程保持不變");}
            catch(Exception ex){Manager.SetStatus("還原未完成，位置備份仍保留。",true);Theme.Error(ex);}
        }
        else
        {
            Store.State.Enabled=true;
            try {ApplyLayout();Store.Save();Refresh();}
            catch(Exception ex){Store.State.Enabled=false;RecoverLayout();Refresh();Theme.Error(ex);}
        }
    }
    void ValidateGeometry()
    {
        var baskets=Store.State.Baskets;
        foreach(var b in baskets)
        {
            if(!Native.PhysicalScreens().Any(s=>s.WorkingArea.Contains(b.ScreenBounds)))throw new InvalidOperationException("分類「"+b.Name+"」超出螢幕工作區。請在分類設定調整位置或大小。");
            if(b.Width<(b.Locked?2*Grid.CellWidth+Grid.Side:Grid.MinWidth)||b.Height<(b.Locked?Grid.Header+40:Grid.MinHeight))throw new InvalidOperationException($"分類籃太小。完整邊框至少需要 {Grid.MinColumns} × {Grid.MinRows} 格。");
        }
        for(int i=0;i<baskets.Count;i++)for(int j=i+1;j<baskets.Count;j++)
            if(baskets[i].ScreenBounds.IntersectsWith(baskets[j].ScreenBounds))throw new InvalidOperationException("分類籃位置重疊，請移到其他位置。桌面圖示尚未改動。");
    }
    void ApplyLayout(bool preserveManagedPositions=false,IReadOnlyDictionary<string,Point>? desktopTargets=null)
    {
        using var dpi=new Native.PhysicalDpiScope();
        applying=true;debounce.Stop();
        try
        {
            ValidateGeometry();layout.Apply(Store.State.Baskets.Select(b=>b.ScreenBounds).ToArray(),preserveManagedPositions,desktopTargets);
            foreach(var id in windows.Keys.Where(k=>!Store.State.Baskets.Any(b=>b.Id==k)).ToArray()){windows[id].Dispose();windows.Remove(id);}
            foreach(var b in Store.State.Baskets)
            {
                if(!windows.TryGetValue(b.Id,out var window)||window.IsDisposed){window=new BasketWindow(this,b);windows[b.Id]=window;}
                // Existing frames stay visible while only their contents change.
                // Complete icon avoidance before attaching/showing a new or moved frame.
                window.Attach(layout.Shell);if(!hideVerificationWindows&&!window.Visible)window.Show();
            }
            if(events!=null){completedEventsSeen+=events.Seen;completedEventsRelevant+=events.Relevant;events.Dispose();}events=new DesktopEvents(layout.Shell,ScheduleCheck);
            if(watchers.Count==0)WatchDesktop();
            Manager.SetStatus("原始路徑不變  /  格數縮放 · 磁吸對齊 · 框外桌面可正常操作");
        }
        catch {foreach(var window in windows.Values)window.Hide();throw;}
        finally {applying=false;}
    }
    void RecoverLayout()
    {
        try {if(Store.State.Enabled)ApplyLayout();else layout.Restore();Store.Save();}
        catch(Exception ex){foreach(var w in windows.Values)w.Hide();Manager.SetStatus(ex.Message,true);}
    }
    void ScheduleCheck(bool refreshed=false)
    {
        if(applying||quitting||!Store.State.Enabled)return;
        desktopRefreshed|=refreshed;
        debounce.Stop();debounce.Start();
    }
    void WatchDesktop()
    {
        foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}.Distinct())
        {
            if(!Directory.Exists(root))continue;
            var watcher=new FileSystemWatcher(root){NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite|NotifyFilters.Size,SynchronizingObject=Manager};
            watcher.Created+=(_,_)=>ScheduleCheck(true);watcher.Deleted+=(_,_)=>ScheduleCheck(true);
            watcher.Changed+=(_,change)=>
            {
                Icons.Invalidate(change.FullPath);
                if(!IsStandby)foreach(var window in windows.Values)if(!window.IsDisposed)window.Viewport.RefreshImage(change.FullPath);
            };
            watcher.Renamed+=(_,change)=>
            {
                bool updated=false;
                foreach(var entry in Store.State.Baskets.SelectMany(b=>b.Entries))
                {
                    if(string.Equals(entry.Path,change.OldFullPath,StringComparison.OrdinalIgnoreCase))
                    {entry.Path=change.FullPath;entry.Name=System.IO.Path.GetFileName(change.FullPath);updated=true;}
                    else if(entry.Path.StartsWith(change.OldFullPath+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    {entry.Path=change.FullPath+entry.Path.Substring(change.OldFullPath.Length);updated=true;}
                }
                foreach(var backup in Store.State.Icons.Where(b=>string.Equals(b.Key,change.OldFullPath,StringComparison.OrdinalIgnoreCase)))backup.Key=change.FullPath;
                if(updated){Store.Save();Refresh();}ScheduleCheck(true);
            };
            watcher.Error+=(_,_)=>ScheduleCheck();watcher.EnableRaisingEvents=true;watchers.Add(watcher);
        }
    }
    void CheckDesktop()
    {
        DesktopCheckCount++;
        using var dpi=new Native.PhysicalDpiScope();
        if(!Store.State.Enabled||applying)return;
        if(fileMenuOpen||fileMenuPending)return; // Completion schedules one deferred check.
        try
        {
            if(!layout.Shell.IconsVisible){foreach(var w in windows.Values)w.Hide();return;}
            RefreshMissingEntries();
            var icons=layout.Shell.ReadIcons();
            bool blocked;bool refresh=desktopRefreshed;desktopRefreshed=false;
            try
            {
                var rects=Store.State.Baskets.Select(b=>layout.Shell.ToView(b.ScreenBounds)).ToArray();
                var spacing=layout.Shell.Spacing;
                var assigned=new HashSet<string>(Store.State.Baskets.SelectMany(b=>b.Entries).Select(e=>e.Path),StringComparer.OrdinalIgnoreCase);
                blocked=(refresh&&Store.State.Icons.Any(b=>icons.Any(i=>i.Key==b.Key&&i.Position!=new Point(b.LastX,b.LastY))))||icons.Any(i=>assigned.Contains(i.Key)
                    ?Native.PhysicalScreens().Any(s=>layout.Shell.ToView(s.Bounds).IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing)))
                    :rects.Any(r=>r.IntersectsWith(LayoutPlanner.Footprint(i.Position,spacing))))
                    ||Store.State.Icons.Any(b=>b.Hidden&&!assigned.Contains(b.Key))
                    ||(refresh&&layout.NeedsCompaction(icons));
                if(!blocked){layout.Shell.CleanAssignedSelection(icons,assigned);if(!refresh)layout.RememberUserPositions(icons);}
            }
            finally {foreach(var i in icons)i.Dispose();}
            if(blocked)ApplyLayout(refresh);else if(!hideVerificationWindows)foreach(var w in windows.Values)w.Show();
        }
        catch(Exception ex){foreach(var w in windows.Values)w.Hide();Manager.SetStatus("桌面整理已暫停："+ex.Message,true);}
    }
    public void Reconnect()
    {
        events?.Dispose();events=null;
        foreach(var w in windows.Values)w.Dispose();windows.Clear();
        foreach(var watcher in watchers)watcher.Dispose();watchers.Clear();
        if(Store.State.Enabled)RecoverLayout();
    }
    public void DisplayChanged()
    {
        foreach(var b in Store.State.Baskets)
        {
            var screen=Screen.AllScreens.FirstOrDefault(s=>s.DeviceName==b.Monitor)??Screen.PrimaryScreen!;
            if(b.Width>screen.WorkingArea.Width||b.Height>screen.WorkingArea.Height)
            {var fit=Grid.FitProportional(new Size(b.Width,b.Height),new Size(b.Width,b.Height),screen.WorkingArea.Size);b.Width=fit.Width;b.Height=fit.Height;}
            b.X=MathEx.Clamp(b.X,screen.WorkingArea.Left,screen.WorkingArea.Right-b.Width);
            b.Y=MathEx.Clamp(b.Y,screen.WorkingArea.Top,screen.WorkingArea.Bottom-(b.Collapsed?Grid.HeaderFor(b.Width):b.Height));
        }
        if(Store.State.Enabled)RecoverLayout();Refresh();
    }
    public void Refresh(string? selected=null)
    {
        foreach(var window in windows.Values)if(!window.IsDisposed)window.RefreshItems();
        Manager.RefreshState(selected);Icons.Prune(Store.State.Baskets.SelectMany(b=>b.Entries).Select(e=>e.Path));
    }
    public void Quit()
    {
        if(quitting)return;quitting=true;debounce.Stop();idle.Stop();events?.Dispose();events=null;
        foreach(var w in windows.Values)w.Hide();
        try{layout.Restore();}
        catch(Exception ex){quitting=false;Manager.SetStatus("還原未完成。位置備份已保留，可重試退出。",true);Theme.Error(ex);return;}
        Manager.Exiting=true;tray.Visible=false;
        if(Theme.ErrorOwner==Manager)Theme.ErrorOwner=null;
        foreach(var w in windows.Values)w.Dispose();windows.Clear();
        Application.RemoveMessageFilter(wakeFilter);tray.Dispose();debounce.Dispose();idle.Dispose();Icons.Dispose();layout.Dispose();Manager.Close();ExitThread();
    }
}
