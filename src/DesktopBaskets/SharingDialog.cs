using Newtonsoft.Json.Linq;
using QRCoder;

namespace DesktopBaskets;

internal sealed class SharingDialog : ModernWindow
{
    readonly Sharing sharing;
    readonly ComboBox networks=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=400};
    readonly ComboBox rooms=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=400};
    readonly TextBox code=new(){MaxLength=8,Width=140};
    readonly Label state=new(){Dock=DockStyle.Bottom,Height=52,Padding=new Padding(20,8,20,8),AutoEllipsis=true};
    readonly ListBox devices=new(){Dock=DockStyle.Fill,BorderStyle=BorderStyle.None};
    readonly Button invite,join,revoke;
    bool busy;
    sealed class Option
    {
        public string Label="",Value="",Id="";
        public override string ToString()=>Label;
    }
    public SharingDialog(App app)
    {
        sharing=app.Sharing;Theme.Form(this);Text="共享籃框 · 連線與装置";
        ClientSize=new Size(660,590);MinimumSize=new Size(600,540);ShowInTaskbar=false;
        var tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(18,10)};
        var roomTab=new TabPage("Room 與裝置"){BackColor=Theme.Background,ForeColor=Theme.Text,Padding=new Padding(18)};
        var migrateTab=new TabPage("從 PocketDrop 接手"){BackColor=Theme.Background,ForeColor=Theme.Text,Padding=new Padding(18)};
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=165,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        top.Controls.Add(new Label{Text="同一個 Wi-Fi／區域網路 · 建立 Room 的電腦需保持開啟",AutoSize=true,Margin=new Padding(0,0,0,12)});
        top.Controls.Add(networks);
        var actions=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        actions.Controls.Add(Theme.Button("啟動／套用網路",async(_,_)=>await Run(async()=>
        {
            if(networks.SelectedItem is not Option selected)return;
            await sharing.ApplyNetworkAsync(selected.Value);state.Text="共享已啟動";
        }),true));
        actions.Controls.Add(Theme.Button("停止共享",(_,_)=>{sharing.Stop();RefreshState();}));
        invite=Theme.Button("顯示連線 QR",async(_,_)=>await Run(async()=>
        {
            var value=await sharing.InvokeAsync("invite");using var dialog=new InviteDialog(value);dialog.ShowDialog(this);
        }));actions.Controls.Add(invite);top.Controls.Add(actions);
        top.Controls.Add(new Label{Text="已配對的裝置",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,10,0,0)});
        devices.BackColor=Theme.Panel;devices.ForeColor=Theme.Text;
        revoke=Theme.Button("撤銷選取裝置",async(_,_)=>await Run(async()=>
        {if(devices.SelectedItem is Option selected)await sharing.InvokeAsync("revoke",new{device_id=selected.Id});}));
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=205,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        bottom.Controls.Add(revoke);bottom.Controls.Add(new Label{Text="或加入另一台電腦的 Room",AutoSize=true,ForeColor=Theme.Muted});bottom.Controls.Add(rooms);
        var pairActions=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        pairActions.Controls.Add(Theme.Button("尋找電腦",async(_,_)=>await Run(async()=>
        {
            var list=await sharing.InvokeAsync("nearby");rooms.Items.Clear();
            foreach(var item in list)rooms.Items.Add(new Option{Label=(string?)item["name"]??"Room",Value=(string?)item["endpoint"]??""});
            if(rooms.Items.Count>0)rooms.SelectedIndex=0;state.Text=rooms.Items.Count==0?"正在尋找，請開啟另一台電腦的共享功能後再試。":"請選擇 Room，輸入邀請碼。";
        })));
        pairActions.Controls.Add(code);code.AccessibleName="另一台電腦顯示的八位驗證碼";
        join=Theme.Button("使用驗證碼加入",async(_,_)=>await Run(async()=>
        {
            if(rooms.SelectedItem is not Option selected){state.Text="請先尋找並選擇電腦。";return;}
            await sharing.InvokeAsync("join_code",new{address=selected.Value,code=code.Text.Trim()});code.Clear();
        }));pairActions.Controls.Add(join);bottom.Controls.Add(pairActions);
        var extra=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        extra.Controls.Add(Theme.Button("使用 QR 圖片加入",async(_,_)=>await Run(async()=>
        {
            using var picker=new OpenFileDialog{Title="選擇 PocketDrop／共享籃框邀請 QR 圖片",Filter="圖片|*.png;*.jpg;*.jpeg;*.bmp"};
            if(picker.ShowDialog(this)!=DialogResult.OK)return;
            if(new FileInfo(picker.FileName).Length>10*1024*1024)throw new IOException("QR 圖片請使用 10 MB 以下的檔案。");
            using var bitmap=new Bitmap(picker.FileName);var result=new ZXing.BarcodeReader().Decode(bitmap);
            if(result==null)throw new IOException("圖片中找不到邀請 QR Code。");
            await sharing.InvokeAsync("join_qr",new{qr=result.Text});
        })));
        extra.Controls.Add(Theme.Button("返回本機 Room",async(_,_)=>await Run(async()=>{await sharing.InvokeAsync("leave");})));bottom.Controls.Add(extra);
        roomTab.Controls.Add(devices);roomTab.Controls.Add(bottom);roomTab.Controls.Add(top);
        var migration=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        migration.Controls.Add(new Label{Text="保留原本的手機配對與 Room 身分",Font=Theme.Body(14,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,8,0,16)});
        migration.Controls.Add(new Label{Text="先正常退出 PocketDrop，再複製匯入。\n原程式、原資料與原檔路徑都會保留。\n匯入完成後只開 Desktop Baskets，避免相同 Room 同時執行。\n\n僅能在尚未建立新 Room 時匯入，已有資料不會被覆蓋。",AutoSize=false,Size=new Size(560,140)});
        migration.Controls.Add(Theme.Button("複製匯入 PocketDrop",async(_,_)=>await Run(async()=>
        {
            string? source=Sharing.LegacyDataPath();if(source==null)throw new DirectoryNotFoundException("這個使用者帳號沒有 PocketDrop 資料。");
            sharing.Stop();sharing.ImportLegacy(source);state.Text="已複製匯入，原資料保留。";
            await sharing.EnsureStartedAsync();
        }),true));
        migration.Controls.Add(new Label{Text="一般桌面分類、位置與 LOCK 狀態不受影響。",AutoSize=true,ForeColor=Theme.Muted});migrateTab.Controls.Add(migration);
        tabs.TabPages.Add(roomTab);tabs.TabPages.Add(migrateTab);Controls.Add(tabs);Controls.Add(state);
        Header(this,"SHARE / DEVICES");
        if(sharing.CanImportLegacy)tabs.SelectedTab=migrateTab;
        sharing.Changed+=RefreshState;
        Shown+=async(_,_)=>await Run(async()=>
        {
            var list=await sharing.RequestAsync("interfaces");
            foreach(var item in list)networks.Items.Add(new Option{Label=$"{item["name"]} · {item["address"]}",Value=(string)item["address"]!});
            int selected=networks.Items.Cast<Option>().ToList().FindIndex(i=>i.Value==sharing.Settings.Address);
            if(networks.Items.Count>0)networks.SelectedIndex=Math.Max(0,selected);RefreshState();
        });
        RefreshState();
    }
    void RefreshState()
    {
        if(IsDisposed)return;state.Text=sharing.Status;
        devices.BeginUpdate();devices.Items.Clear();foreach(var d in sharing.Snapshot?.devices??Array.Empty<ShareDevice>())devices.Items.Add(new Option{Label=d.name,Id=d.device_id});devices.EndUpdate();
        invite.Enabled=revoke.Enabled=!busy&&!sharing.Joined;join.Enabled=!busy&&!sharing.Joined;
    }
    async Task Run(Func<Task> action)
    {
        if(busy)return;busy=true;RefreshState();
        try{await action();}
        catch(Exception ex){state.Text=ex.Message;}
        finally{busy=false;if(!IsDisposed){invite.Enabled=revoke.Enabled=!sharing.Joined;join.Enabled=!sharing.Joined;}}
    }
    protected override void Dispose(bool disposing){if(disposing)sharing.Changed-=RefreshState;base.Dispose(disposing);}
    static void Header(ModernWindow window,string title)
    {
        var header=new WindowHeaderPanel{Dock=DockStyle.Top,Height=58,BackColor=Theme.Background};
        header.Paint+=(_,e)=>{FrameArt.Prepare(e.Graphics);FrameArt.Label(e.Graphics,title,new RectangleF(20,12,header.Width-92,30),FrameArt.Display(22),Theme.Text);using var pen=new Pen(Theme.Accent,2);e.Graphics.DrawLine(pen,20,49,70,49);};
        var close=new CaptionButton(window,CaptionAction.Close){AccessibleName="關閉共享設定"};header.Controls.Add(close);
        void Layout(){close.Bounds=new Rectangle(header.Width-52,0,52,44);window.CaptionRegion=header.Bounds;}
        header.Resize+=(_,_)=>Layout();header.LocationChanged+=(_,_)=>Layout();window.Controls.Add(header);Layout();
    }
    internal static bool VerifyInviteQr(string payload)
    {
        using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(payload,QRCodeGenerator.ECCLevel.M);using var qr=new QRCode(data);using var bitmap=qr.GetGraphic(6);
        return new ZXing.BarcodeReader().Decode(bitmap)?.Text==payload;
    }
    internal sealed class InviteDialog : ModernWindow
    {
        readonly System.Windows.Forms.Timer expiry=new(){Interval=1000};
        readonly PictureBox image=new(){SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(290,290)};
        public InviteDialog(JToken invite)
        {
            Theme.Form(this);Text="邀請手機或其他電腦";ClientSize=new Size(380,540);MinimumSize=MaximumSize=new Size(380,540);MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;
            var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(30,18,30,18),FlowDirection=FlowDirection.TopDown,WrapContents=false};
            string code=(string)invite["code"]!;int seconds=(int)invite["expires_seconds"]!;
            layout.Controls.Add(new Label{Text=code,Font=FrameArt.Data(34),ForeColor=Theme.Accent,AutoSize=true});
            layout.Controls.Add(new Label{Text="手機掃描 QR，或輸入上方八位驗證碼",AutoSize=true,ForeColor=Theme.Muted});
            using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode((string)invite["qr"]!,QRCodeGenerator.ECCLevel.M);using var qr=new QRCode(data);image.Image=qr.GetGraphic(6);layout.Controls.Add(image);
            var remaining=new Label{AutoSize=true,ForeColor=Theme.Muted};layout.Controls.Add(remaining);
            layout.Controls.Add(Theme.Button("複製驗證碼",(_,_)=>Theme.Try(()=>Clipboard.SetText(code))));Controls.Add(layout);
            Header(this,"PAIR / CONNECT");
            void UpdateExpiry(){remaining.Text=seconds>0?$"邀請剩餘 {seconds} 秒 · 僅限一次使用":"邀請已過期，請關閉並產生新邀請。";if(seconds<=0){expiry.Stop();image.Visible=false;}}
            expiry.Tick+=(_,_)=>{seconds--;UpdateExpiry();};UpdateExpiry();expiry.Start();
        }
        protected override void Dispose(bool disposing){if(disposing){expiry.Dispose();image.Image?.Dispose();}base.Dispose(disposing);}
    }
}
