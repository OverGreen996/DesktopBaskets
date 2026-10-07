using Microsoft.Win32;

namespace DesktopBaskets;

internal static partial class Verification
{
    public static object StartupTest(string root,bool interactive=false)
    {
        string registryPath="Software\\DesktopBasketsVerification\\"+Guid.NewGuid().ToString("N");
        int assertions=0;
        void Check(bool ok,string message){assertions++;Require(ok,message);}
        Directory.CreateDirectory(root);
        var settings=new StartupSettings(Path.Combine(root,"path with spaces","DesktopBaskets.exe"),registryPath);
        try
        {
            Check(!settings.Enabled,"New startup setting defaults to off.");
            using(var key=Registry.CurrentUser.CreateSubKey(registryPath))key.SetValue("OtherApplication","leave untouched");
            settings.SetEnabled(true);
            Check(new StartupSettings(Path.Combine(root,"path with spaces","DesktopBaskets.exe"),registryPath).Enabled,"Setting survives reopening registry.");
            using(var key=Registry.CurrentUser.OpenSubKey(registryPath))
                Check((string?)key?.GetValue(StartupSettings.ValueName)==settings.Command&&settings.Command.EndsWith("\" --autostart"),"Run command quotes the executable and restores baskets.");
            settings.SetEnabled(false);settings.SetEnabled(false);
            using(var key=Registry.CurrentUser.OpenSubKey(registryPath))
                Check(!settings.Enabled&&(string?)key?.GetValue("OtherApplication")=="leave untouched","Disabling preserves unrelated startup applications.");
            using(var key=Registry.CurrentUser.OpenSubKey(registryPath,true))key!.SetValue(StartupSettings.ValueName,settings.Command.Replace(" --autostart",""));
            Check(settings.Enabled,"Legacy installer startup registration is recognized.");
            settings.SetEnabled(true);
            var store=new Store(Path.Combine(root,Guid.NewGuid().ToString("N")));
            var basket=new Basket{Name="啟動設定驗證",Locked=true};
            basket.Entries.Add(new Entry{Name="示範檔案.txt",Path=Path.Combine(root,"示範檔案.txt")});store.State.Baskets.Add(basket);store.Save();
            var before=JsonCodec.Serialize(store.State.Baskets);
            Check(!StartupSettings.RestoreBaskets(store.State,false)&&!store.State.Enabled,"Manual launch keeps a paused desktop paused.");
            Check(StartupSettings.RestoreBaskets(store.State,true)&&store.State.Enabled,"Login launch enables saved baskets even after pausing.");store.Save();
            Check(new Store(store.Root).State.Enabled&&JsonCodec.Serialize(store.State.Baskets)==before,"Login restores membership, paths, lock and geometry unchanged.");
            Check(!StartupSettings.RestoreBaskets(new State(),true),"No baskets are invented for empty settings.");
            int attempts=0,waits=0;
            StartupSettings.WaitForDesktop(()=>{attempts++;if(attempts<3)throw new InvalidOperationException("Explorer starting");},_=>waits++);
            Check(attempts==3&&waits==2,"Delayed Explorer becomes ready and retries stop immediately.");
            attempts=waits=0;bool failed=false;
            try{StartupSettings.WaitForDesktop(()=>{attempts++;throw new InvalidOperationException("Unavailable");},_=>waits++,3);}catch(InvalidOperationException){failed=true;}
            Check(failed&&attempts==3&&waits==2,"Explorer retries have a finite bound.");
            settings.SetEnabled(false);
            using var app=new App(store,true,autoStart:true,startup:settings);
            Check(!app.Manager.Visible,"Login launch does not flash the manager window.");
            app.ShowManager();Application.DoEvents();
            Check(!app.Manager.StartupControl.Checked,"Manager reads the actual Windows startup registration.");
            app.Manager.StartupControl.Checked=true;
            Check(settings.Enabled,"Manager enables Windows startup.");
            app.Manager.StartupControl.Checked=false;
            Check(!settings.Enabled&&store.State.Enabled,"Disabling startup does not pause existing baskets.");
            if(interactive)
            {
                app.Manager.SetStatus("驗證模式 · 此開關只修改獨立測試登錄值，不影響正式啟動設定。");
                Application.Run(app);
            }
            else app.Quit();
            return new{Passed=true,Assertions=assertions,RunCommand=settings.Command,IsolatedRegistry=true,ProductionStartupUntouched=true,LoginRestoresBaskets=true,LoginManagerHidden=true,BoundedExplorerRetry=true};
        }
        finally{Registry.CurrentUser.DeleteSubKeyTree(registryPath,false);}
    }
}
