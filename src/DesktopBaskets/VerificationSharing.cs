using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace DesktopBaskets;
internal static partial class Verification
{
    static T ShareWait<T>(Task<T> task,int seconds=90)
    {
        var watch=Stopwatch.StartNew();
        while(!task.IsCompleted){if(watch.Elapsed.TotalSeconds>seconds)throw new TimeoutException("共享驗證逾時");Application.DoEvents();Thread.Sleep(10);}
        return task.GetAwaiter().GetResult();
    }
    static void ShareWait(Task task,int seconds=90)=>ShareWait(Complete(task),seconds);
    static async Task<bool> Complete(Task task){await task;return true;}
    static void ShareUntil(Func<bool> condition,int seconds=20)
    {var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed.TotalSeconds>seconds)throw new TimeoutException("共享狀態未更新");Application.DoEvents();Thread.Sleep(30);}}
    static void SharePump(int milliseconds)
    {var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(20);}}
    public static object SharingTest(string root)
    {
        string work=Path.Combine(Path.GetFullPath(root),"sharing-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var store=new Store(Path.Combine(work,"host"));var basket=new Basket{Name="共享驗證",Kind="share",Locked=true};store.State.Baskets.Add(basket);
        using var app=new App(store,true);app.HideVerificationWindows();
        using var host=new Form{ClientSize=new Size(900,520)};_ = host.Handle;
        using var panel=new SharePanel(app,basket){Dock=DockStyle.Fill};host.Controls.Add(panel);_ = panel.Handle;_ = panel.FileControl.Handle;
        using var client=new Sharing(app.Manager,Path.Combine(work,"client"),Path.Combine(work,"client-downloads"));
        int assertions=0;void Check(bool value,string message){Require(value,message);assertions++;}
        try
        {
            host.Show();panel.Page(false);Application.DoEvents();Check(panel.ActionsWithinPage&&panel.TextControl.Visible,"Shared text actions were clipped or hidden.");
            panel.Page(true);Application.DoEvents();Check(panel.ActionsWithinPage&&panel.FileControl.Visible,"File page switch damaged layout.");
            host.ClientSize=new Size(476,150);Application.DoEvents();Check(panel.ActionsWithinPage&&panel.AutoScrollMinSize.Height>host.ClientSize.Height,"Small shared frame cannot scroll its actions.");
            host.ClientSize=new Size(900,520);panel.Page(false);host.Hide();
            ShareWait(app.Sharing.EnsureStartedAsync());Check(app.Sharing.Running&&app.Sharing.Snapshot!=null,"Native sharing engine did not start.");
            string roomId=app.Sharing.Snapshot!.room_id;
            string unicode="籃框接管 PocketDrop ✓ 中文文字\r\nhttps://example.invalid/測試";
            ShareWait(app.Sharing.InvokeAsync("text",new{content=unicode}));
            Check(app.Sharing.Snapshot!.text==unicode,"Unicode text changed across the stdio/LAN boundary.");
            Check(panel.TextControl.Text==unicode,"Basket text did not reflect shared text.");
            panel.TextControl.Text="尚未送出的草稿";
            ShareWait(app.Sharing.InvokeAsync("text",new{content="收到的新文字"}));
            Check(panel.TextControl.Text=="尚未送出的草稿"&&app.Sharing.Settings.Dirty,"Incoming update overwrote a draft.");
            app.Sharing.Settings.Dirty=false;app.Sharing.Settings.Draft="";app.Sharing.Save();
            string file=Path.Combine(work,"原始位置-模型.stl");File.WriteAllText(file,"solid 中文測試\nend",new System.Text.UTF8Encoding(false));
            string initialHash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(file)));
            ShareWait(app.AddSharedPaths(basket,new[]{file,work}));
            Check(basket.Entries.Count==1&&basket.Entries[0].Path==file&&File.Exists(file),"Sharing changed the original file path or accepted a directory.");
            Check(app.Sharing.Snapshot!.files.Length==1,"Unexpected shared file count.");
            ShareWait(app.AddSharedPaths(basket,new[]{file}));Check(app.Sharing.Snapshot!.files.Length==1,"Repeated drop created duplicate metadata.");
            var shared=app.Sharing.Snapshot!.files[0];Check(app.Sharing.PathFor(shared)==file,"Local shared file lost its native Shell path.");
            string downloaded=ShareWait(app.Sharing.DownloadAsync(shared));
            Check(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(downloaded)),"Downloaded content differs from the original.");
            Check(File.Exists(file)&&Path.GetFullPath(downloaded).StartsWith(Path.GetFullPath(app.Sharing.Inbox),StringComparison.OrdinalIgnoreCase),"Download moved the source or escaped the inbox.");
            ShareWait(client.EnsureStartedAsync());
            var invite=ShareWait(app.Sharing.InvokeAsync("invite"));
            Check(SharingDialog.VerifyInviteQr((string)invite["qr"]!),"PC invitation QR cannot be decoded with its exact pairing payload.");
            ShareWait(client.InvokeAsync("join_code",new{address=app.Sharing.Snapshot!.endpoint,code=(string)invite["code"]!}));
            Check(client.Joined&&client.Snapshot!.room_id==roomId,"Another native client could not join the Room.");
            ShareWait(client.InvokeAsync("text",new{content="手機協定相容測試 · 來自另一台裝置"}));
            ShareUntil(()=>app.Sharing.Snapshot?.text=="手機協定相容測試 · 來自另一台裝置");Check(true,"Client text did not reach the basket.");
            string remote=Path.Combine(work,"來源端-only.txt");File.WriteAllText(remote,"metadata only; download on demand");
            int payloads=Directory.Exists(app.Sharing.Inbox)?Directory.GetFiles(app.Sharing.Inbox,"*",SearchOption.AllDirectories).Length:0;
            ShareWait(client.InvokeAsync("files_add",new{paths=new[]{remote}}));
            ShareUntil(()=>app.Sharing.Snapshot!.files.Any(f=>f.name==Path.GetFileName(remote)));
            Check(Directory.GetFiles(app.Sharing.Inbox,"*",SearchOption.AllDirectories).Length==payloads,"Metadata sharing eagerly copied file contents.");
            ShareUntil(()=>client.Snapshot!.files.Any(f=>f.name==Path.GetFileName(remote)&&client.PathFor(f)==remote));Check(true,"Joined source could not resolve its own original path.");
            var remoteView=app.Sharing.Snapshot!.files.First(f=>f.name==Path.GetFileName(remote));
            string fetched=ShareWait(app.Sharing.DownloadAsync(remoteView));Check(File.ReadAllBytes(fetched).SequenceEqual(File.ReadAllBytes(remote)),"Room relay download failed.");
            ShareWait(client.InvokeAsync("unshare_paths",new{paths=new[]{remote}}));
            ShareUntil(()=>!app.Sharing.Snapshot!.files.Any(f=>f.file_id==remoteView.file_id));Check(File.Exists(remote),"Unsharing a joined source deleted its original file.");
            ShareWait(client.InvokeAsync("files_add",new{paths=new[]{remote}}));
            ShareUntil(()=>app.Sharing.Snapshot!.files.Any(f=>f.name==Path.GetFileName(remote)&&f.available));
            remoteView=app.Sharing.Snapshot!.files.First(f=>f.name==Path.GetFileName(remote));
            SharePump(3500);int renders=panel.RefreshCount;SharePump(6000);
            Check(panel.RefreshCount==renders,"Unchanged network heartbeat repainted the shared basket.");
            client.Stop();ShareUntil(()=>app.Sharing.Snapshot!.files.Any(f=>f.file_id==remoteView.file_id&&!f.available),20);
            Check(true,"Offline source did not become unavailable.");
            ShareWait(client.EnsureStartedAsync());ShareUntil(()=>app.Sharing.Snapshot!.files.Any(f=>f.file_id==remoteView.file_id&&f.available));
            Check(client.Joined&&client.Snapshot!.room_id==roomId,"Restart lost pairing or changed Room.");
            ShareWait(app.Sharing.InvokeAsync("revoke",new{device_id=client.SelfId}));
            bool rejected=false;try{ShareWait(client.InvokeAsync("text",new{content="must fail"}));}catch(IOException){rejected=true;}
            Check(rejected,"Revoked device could still change shared text.");
            Check(Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(file)))==initialHash,"Tests modified original file contents.");
            client.Stop();app.Sharing.Stop();Check(!client.Running&&!app.Sharing.Running,"Owned helper stayed running after stop.");
            Check(File.Exists(file)&&File.Exists(remote),"Stopping sharing deleted original files.");
            string migration=Path.Combine(app.Sharing.DataPath,"phone-trial");
            var legacyHashes=Directory.GetFiles(migration).ToDictionary(Path.GetFileName,p=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p))));
            using var imported=new Sharing(app.Manager,Path.Combine(work,"import"));imported.ImportLegacy(migration);
            Check(Directory.GetFiles(migration).All(p=>legacyHashes[Path.GetFileName(p)]==Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p)))),"Migration modified legacy data.");
            ShareWait(imported.EnsureStartedAsync());Check(imported.Snapshot!.room_id==roomId&&imported.Snapshot.text=="手機協定相容測試 · 來自另一台裝置","Import lost Room identity or data.");imported.Stop();
            bool overwrite=false;try{imported.ImportLegacy(migration);}catch(InvalidOperationException){overwrite=true;}
            Check(overwrite,"Migration overwrote an existing Room.");
            ShareWait(app.Sharing.EnsureStartedAsync());app.Quit();Check(!app.Sharing.Running,"Exiting the application left the owned sharing helper alive.");
            return new{Passed=true,Assertions=assertions,OriginalPathsPreserved=true,NoEagerFileCopies=true,NoUnchangedUiRefresh=true,NativeEngineWithoutWebView=true,
                DesktopStateIsolated=true,PhysicalAndroidTestPending=true,Work=work};
        }
        finally{client.Stop();app.Sharing.Stop();app.Quit();}
    }
    public static void SharingDemo(string root)
    {
        string work=Path.Combine(Path.GetFullPath(root),"share-demo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var store=new Store(Path.Combine(work,"settings"));
        var host=new Form{Text="Desktop Baskets 0.5.0 · 共享籃框驗證",FormBorderStyle=FormBorderStyle.None,ClientSize=new Size(1040,650),Location=new Point(160,140),StartPosition=FormStartPosition.Manual,BackColor=Theme.Background,ShowInTaskbar=true};
        var basket=new Basket{Name="共享 / SHARE",Kind="share",Width=1040,Height=650,X=host.Left,Y=host.Top,Locked=true};store.State.Baskets.Add(basket);
        var app=new App(store,true);app.Manager.Hide();
        var frame=new BasketWindow(app,basket);host.Shown+=async(_,_)=>
        {
            frame.AttachToHost(host.Handle);frame.Show();
            try
            {
                await app.Sharing.EnsureStartedAsync();await app.Sharing.InvokeAsync("text",new{content="Desktop Baskets 共享籃框\r\n文字與檔案，在桌面上就能分享。"});
                string sample=Path.Combine(work,"共享示範.txt");File.WriteAllText(sample,"This is a generated test fixture, not a personal file.");
                await app.AddSharedPaths(basket,new[]{sample});
            }
            catch(Exception ex){app.Manager.SetStatus(ex.Message,true);}
        };
        host.FormClosed+=(_,_)=>{frame.Dispose();app.Quit();};host.Show();Application.Run(app);
    }
    public static void SharingSettingsDemo(string root,bool qr)
    {
        string work=Path.Combine(Path.GetFullPath(root),"settings-demo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        using var app=new App(new Store(work),true);app.Manager.Hide();ShareWait(app.Sharing.EnsureStartedAsync());
        using ModernWindow dialog=qr?new SharingDialog.InviteDialog(ShareWait(app.Sharing.InvokeAsync("invite"))):new SharingDialog(app);
        dialog.ShowInTaskbar=true;dialog.FormClosed+=(_,_)=>app.Quit();Application.Run(dialog);
    }
}
