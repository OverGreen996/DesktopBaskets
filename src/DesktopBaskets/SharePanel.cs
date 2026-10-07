using Newtonsoft.Json.Linq;

namespace DesktopBaskets;

internal sealed class SharePanel : Panel
{
    readonly App app;
    readonly Basket basket;
    readonly Sharing sharing;
    readonly FlowLayoutPanel navigation=new(){Dock=DockStyle.Top,Height=40,WrapContents=false};
    readonly TextBox text=new(){Dock=DockStyle.Fill,Multiline=true,AcceptsReturn=true,AcceptsTab=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None};
    readonly ListView files=new(){Dock=DockStyle.Fill,View=View.Details,HeaderStyle=ColumnHeaderStyle.None,FullRowSelect=true,MultiSelect=true,HideSelection=false,BorderStyle=BorderStyle.None};
    readonly Panel textPage=new(){Dock=DockStyle.Fill},filePage=new(){Dock=DockStyle.Fill};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=28,ForeColor=Theme.Muted,AutoEllipsis=true};
    readonly FlowLayoutPanel textActions=new(){Dock=DockStyle.Bottom,Height=44,WrapContents=false},fileActions=new(){Dock=DockStyle.Bottom,Height=44,WrapContents=false};
    readonly Button send,download,cancel;
    readonly System.Windows.Forms.Timer draftSave=new(){Interval=450};
    readonly ImageList images=new(){ImageSize=new Size(32,32),ColorDepth=ColorDepth.Depth32Bit};
    bool rendering,busy;
    string lastFiles="";
    long displayedRevision=-1;
    public int RefreshCount {get;private set;}
    internal TextBox TextControl=>text;
    internal ListView FileControl=>files;
    internal Button SendControl=>send;
    internal bool ActionsWithinPage=>textPage.ClientRectangle.Contains(textActions.Bounds)&&filePage.ClientRectangle.Contains(fileActions.Bounds)&&!text.Bounds.IntersectsWith(textActions.Bounds)&&!files.Bounds.IntersectsWith(fileActions.Bounds);
    public SharePanel(App app,Basket basket)
    {
        this.app=app;this.basket=basket;sharing=app.Sharing;BackColor=Theme.Panel;ForeColor=Theme.Text;Font=Theme.Body();
        AutoScroll=true;AutoScrollMinSize=new Size(0,210);
        AccessibleName="共享文字與檔案";AllowDrop=true;
        navigation.Controls.Add(Small("文字",(_,_)=>Page(false)));
        navigation.Controls.Add(Small("檔案",(_,_)=>Page(true)));
        navigation.Controls.Add(Small("裝置 / QR",(_,_)=>app.ShowSharingSettings()));
        send=Small("分享文字",async(_,_)=>await Run(async()=>
        {
            string outgoing=text.Text;await sharing.InvokeAsync("text",new{content=outgoing});
            if(text.Text==outgoing){sharing.Settings.Dirty=false;sharing.Settings.Draft="";sharing.Save();}
            Render();status.Text="文字已分享";
        }),true);
        textActions.Controls.Add(send);textActions.Controls.Add(Small("複製",(_,_)=>Theme.Try(()=>{if(text.Text.Length>0)Clipboard.SetText(text.Text);})));
        text.BackColor=Color.FromArgb(29,33,36);text.ForeColor=Theme.Text;text.Font=Theme.Body(11);
        text.Text=sharing.Settings.Dirty?sharing.Settings.Draft:sharing.Snapshot?.text??"";
        text.AccessibleName="共享文字；貼上後按分享文字送出";
        text.TextChanged+=(_,_)=>
        {
            if(rendering)return;sharing.Settings.Draft=text.Text;sharing.Settings.Dirty=true;
            status.Text="尚未分享的草稿";draftSave.Stop();draftSave.Start();
        };
        draftSave.Tick+=(_,_)=>{draftSave.Stop();Theme.Try(sharing.Save);};
        textPage.Controls.Add(text);textPage.Controls.Add(textActions);
        files.BackColor=Color.FromArgb(29,33,36);files.ForeColor=Theme.Text;files.Font=Theme.Body();
        files.AccessibleName="共享檔案；本機檔案使用 Windows 原生右鍵";
        files.SmallImageList=images;app.Icons.ImageReady+=ImageReady;
        files.Columns.Add("檔案",260);files.Columns.Add("來源與大小",160);
        download=Small("開啟／下載",async(_,_)=>await OpenSelected());
        cancel=Small("取消下載",async(_,_)=>await Run(async()=>{await sharing.InvokeAsync("cancel");},false));cancel.Visible=false;
        fileActions.Controls.Add(download);
        fileActions.Controls.Add(Small("加入",(_,_)=>
        {
            using var picker=new OpenFileDialog{Multiselect=true,Title="加入共享檔案（原檔路徑保留）"};
            if(picker.ShowDialog(app.Manager)==DialogResult.OK)app.AddPaths(basket,picker.FileNames);
        }));
        fileActions.Controls.Add(Small("解除共享",async(_,_)=>await RemoveSelected()));fileActions.Controls.Add(cancel);
        filePage.Controls.Add(files);filePage.Controls.Add(fileActions);
        Controls.Add(filePage);Controls.Add(textPage);Controls.Add(navigation);Controls.Add(status);
        files.Resize+=(_,_)=>{files.Columns[0].Width=Math.Max(100,files.ClientSize.Width-174);};
        files.DoubleClick+=async(_,_)=>await OpenSelected();
        files.MouseUp+=(_,e)=>{if(e.Button==MouseButtons.Right)NativeMenu(Native.PhysicalCursor);};
        files.KeyDown+=async(_,e)=>
        {
            if(e.Control&&e.KeyCode==Keys.A){foreach(ListViewItem item in files.Items)item.Selected=true;e.Handled=true;}
            else if(e.KeyCode==Keys.Enter){e.Handled=true;await OpenSelected();}
            else if(e.KeyCode==Keys.Apps||(e.Shift&&e.KeyCode==Keys.F10)){e.Handled=true;NativeMenu(files.PointToScreen(new Point(20,30)));}
        };
        files.ItemDrag+=(_,_)=>DragSelected();
        foreach(Control target in new Control[]{this,files,text,textPage,filePage,navigation})
        {
            target.AllowDrop=true;target.DragEnter+=appDragEnter;target.DragDrop+=appDrop;
        }
        foreach(var control in new Control[]{navigation,status,textPage,filePage,text,files,textActions,fileActions})control.Dock=DockStyle.None;
        SizeChanged+=(_,_)=>LayoutContent();
        sharing.Changed+=Render;sharing.Progress+=OnProgress;Page(false);LayoutContent();Render();
        Button Small(string label,EventHandler action,bool primary=false)
        {var button=Theme.Button(label,action,primary);button.MinimumSize=new Size(66,32);button.Height=32;button.Padding=new Padding(8,2,8,2);button.Margin=new Padding(0,0,6,0);button.Font=Theme.Body(9);return button;}
    }
    internal void Page(bool file){filePage.Visible=file;textPage.Visible=!file;LayoutContent();}
    void LayoutContent()
    {
        // Explicit geometry avoids changing Dock order when switching pages.
        // Small frames scroll the entire content instead of clipping actions.
        int width=Math.Max(1,ClientSize.Width),height=Math.Max(210,ClientSize.Height);
        var offset=AutoScrollPosition;
        navigation.Bounds=new Rectangle(offset.X,offset.Y,width,40);
        status.Bounds=new Rectangle(offset.X,offset.Y+height-28,width,28);
        textPage.Bounds=filePage.Bounds=new Rectangle(offset.X,offset.Y+40,width,height-68);
        int body=Math.Max(1,height-112);
        text.Bounds=files.Bounds=new Rectangle(0,0,width,body);
        textActions.Bounds=fileActions.Bounds=new Rectangle(0,body,width,44);
    }
    void appDragEnter(object? sender,DragEventArgs e)
    {
        if(e.Data?.GetDataPresent(DataFormats.FileDrop)==true||e.Data?.GetDataPresent("DesktopBaskets.Entries")==true)e.Effect=DragDropEffects.Copy;
    }
    void appDrop(object? sender,DragEventArgs e)
    {
        app.Wake();if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)app.AddPaths(basket,paths);
        else if(e.Data?.GetData("DesktopBaskets.Entries") is string[] ids)app.TransferEntries(basket,ids);
        Page(true);
    }
    ShareFile[] Selected()=>files.SelectedItems.Cast<ListViewItem>().Select(i=>(ShareFile)i.Tag!).ToArray();
    void NativeMenu(Point location)
    {
        var selected=Selected();if(selected.Length==0)return;
        var paths=selected.Select(sharing.PathFor).ToArray();
        if(paths.Any(p=>p==null)){status.Text="遠端檔案先下載，即可使用 Windows 原生右鍵。";return;}
        app.ShowEntriesMenu(selected.Select((f,i)=>new Entry{Name=f.name,Path=paths[i]!}),location);
    }
    async Task OpenSelected()
    {
        var file=Selected().FirstOrDefault();if(file==null)return;
        await Run(async()=>
        {
            var path=sharing.PathFor(file);
            if(path!=null){Theme.Open(path);return;}
            cancel.Visible=true;status.Text="準備下載…";
            try{path=await sharing.DownloadAsync(file);status.Text="已下載 · "+file.name;}
            finally{cancel.Visible=false;}
        });
    }
    async Task RemoveSelected()
    {
        var chosen=Selected();if(chosen.Length==0)return;
        if(sharing.Joined&&chosen.Any(f=>!sharing.LocalPaths.ContainsKey(f.file_id))){status.Text="其他裝置的共享紀錄由建立 Room 的電腦管理。";return;}
        await Run(async()=>
        {
            foreach(var file in chosen)
            {
                string? path=sharing.PathFor(file);
                if(sharing.Joined)await sharing.InvokeAsync("unshare_paths",new{paths=new[]{path}});
                else await sharing.InvokeAsync("remove",new{file_id=file.file_id});
                if(path!=null){var entry=basket.Entries.FirstOrDefault(e=>string.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase));if(entry!=null)app.RemoveEntry(basket,entry);}
            }
            status.Text="已解除共享，原檔保留。";
        });
    }
    void DragSelected()
    {
        var selected=Selected();var paths=selected.Select(sharing.PathFor).ToArray();
        if(paths.Length==0||paths.Any(p=>p==null)){status.Text="遠端檔案先下載，即可拖出。";return;}
        var entries=paths.Select(p=>basket.Entries.FirstOrDefault(e=>string.Equals(e.Path,p,StringComparison.OrdinalIgnoreCase))).ToArray();
        var data=new DataObject();data.SetData(DataFormats.FileDrop,paths!);
        if(entries.All(e=>e!=null)){data.SetData("DesktopBaskets.Entries",entries.Select(e=>e!.Id).ToArray());}
        files.DoDragDrop(data,DragDropEffects.Copy|DragDropEffects.Link);
        var point=Native.PhysicalCursor;
        if(entries.All(e=>e!=null&&app.CanReturnToDesktop(e))&&app.IsDesktopDrop(point))
        {
            app.ReturnEntriesToDesktop(entries.Select(e=>e!.Id),point);
            if(entries.All(e=>!basket.Entries.Contains(e!)))_=Run(async()=>{await sharing.InvokeAsync("unshare_paths",new{paths});status.Text="已移出共享籃框 · 原檔路徑保留";});
        }
    }
    async Task Run(Func<Task> action,bool exclusive=true)
    {
        if(exclusive&&busy)return;
        if(exclusive){busy=true;send.Enabled=download.Enabled=false;}
        try{app.Wake();await action();}
        catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}
        finally{if(exclusive){busy=false;if(!IsDisposed)send.Enabled=download.Enabled=true;}}
    }
    void OnProgress(JObject progress)
    {
        status.Text=$"下載 {Bytes((long?)progress["done"]??0)} / {Bytes((long?)progress["total"]??0)} · {Bytes((long?)progress["speed"]??0)}/s";
    }
    static string Bytes(long bytes)=>bytes<1024?$"{bytes} B":bytes<1024*1024?$"{bytes/1024.0:0.#} KB":bytes<1024L*1024*1024?$"{bytes/(1024.0*1024):0.#} MB":$"{bytes/(1024.0*1024*1024):0.#} GB";
    public void Render()
    {
        if(IsDisposed)return;RefreshCount++;
        var snapshot=sharing.Snapshot;rendering=true;
        try
        {
            if(snapshot!=null&&snapshot.revision!=displayedRevision)
            {
                if(!sharing.Settings.Dirty)text.Text=snapshot.text;
                displayedRevision=snapshot.revision;
            }
            if(!busy)status.Text=sharing.Settings.Dirty?"尚未分享的草稿 · "+sharing.Status:sharing.Status;
            string next=Newtonsoft.Json.JsonConvert.SerializeObject(snapshot?.files);
            if(next==lastFiles)return;lastFiles=next;
            var chosen=new HashSet<string>(Selected().Select(f=>f.file_id));files.BeginUpdate();
            try
            {
                files.Items.Clear();
                images.Images.Clear();
                foreach(var file in snapshot?.files??Array.Empty<ShareFile>())
                {
                    var path=sharing.PathFor(file);
                    var row=new ListViewItem(new Entry{Name=file.name,Path=path??file.name}.DisplayName){Tag=file,Selected=chosen.Contains(file.file_id)};
                    if(path!=null&&File.Exists(path)&&images.Images.Count<IconCache.Limit){images.Images.Add(file.file_id,app.Icons.Get(path,this));row.ImageKey=file.file_id;}
                    row.SubItems.Add(file.available?$"{file.origin} · {Bytes(file.size)}":"來源離線或檔案已變更");files.Items.Add(row);
                }
            }
            finally{files.EndUpdate();}
        }
        finally{rendering=false;}
    }
    void ImageReady(string path)
    {
        foreach(ListViewItem item in files.Items)
        {
            var file=(ShareFile)item.Tag!;
            if(!string.Equals(sharing.PathFor(file),path,StringComparison.OrdinalIgnoreCase))continue;
            int index=images.Images.IndexOfKey(file.file_id);if(index>=0)images.Images[index]=app.Icons.Get(path,this);
        }
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x7B){m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
    protected override void Dispose(bool disposing)
    {
        if(disposing){app.Icons.ImageReady-=ImageReady;sharing.Changed-=Render;sharing.Progress-=OnProgress;draftSave.Stop();draftSave.Dispose();images.Dispose();if(sharing.Settings.Dirty)sharing.Save();}
        base.Dispose(disposing);
    }
}
