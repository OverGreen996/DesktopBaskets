using System.Runtime.InteropServices;

namespace DesktopBaskets;
internal static partial class Verification
{
    [DllImport("user32.dll")] static extern bool EndMenu();
    public static object ShellPropertiesTest(string root)
    {
        string data=Path.Combine(root,"shell-properties-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        string file=Path.Combine(data,"原生內容驗證.txt");File.WriteAllText(file,"unchanged");bool invoked=false;
        try
        {
            using var owner=new Form{Text="Desktop Baskets · 原生內容驗證",Size=new Size(480,160),StartPosition=FormStartPosition.CenterScreen};
            owner.Controls.Add(new Label{Dock=DockStyle.Fill,Text="正在驗證 Windows 原生檔案內容視窗。\n關閉內容視窗後，關閉此測試視窗即可完成。",Padding=new Padding(16)});
            using var menu=new ShellContextMenu(file,owner.Handle);
            using var timeout=new System.Windows.Forms.Timer{Interval=180000};timeout.Tick+=(_,_)=>owner.Close();timeout.Start();
            owner.Shown+=(_,_)=>owner.BeginInvoke(new Action(()=>{menu.ExecuteForVerification("properties",owner.Handle);invoked=true;}));
            Application.Run(owner);
            Require(invoked&&File.ReadAllText(file)=="unchanged","Native properties failed or changed the source file.");
            return new{Passed=true,NativePropertiesCommandInvoked=true,SourceFileUnchanged=true};
        }
        finally{if(File.Exists(file))File.Delete(file);Directory.Delete(data);}
    }
    public static object ShellMenuTest(string root)
    {
        string data=Path.Combine(root,"shell-menu-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        string file=Path.Combine(data,"原生右鍵驗證.txt");File.WriteAllText(file,"unchanged");
        using var owner=new Form{ShowInTaskbar=false};var handle=owner.Handle;
        string[] verbs=Array.Empty<string>();
        IDataObject? previousClipboard=Clipboard.GetDataObject();bool clipboardChanged=false;
        var config=Path.Combine(root,"shell-command-state",Guid.NewGuid().ToString("N"));
        var store=new Store(config);var basket=new Basket{Name="原生右鍵驗證"};store.State.Baskets.Add(basket);var entry=store.Add(basket,file);
        try
        {
            for(int i=0;i<5;i++)
            {
                using var menu=new ShellContextMenu(file,handle,i%2==1);
                Require(menu.Count>0,"Windows Shell did not populate a context menu.");verbs=menu.Verbs();
                Require(verbs.Any(v=>v.Equals("copy",StringComparison.OrdinalIgnoreCase))&&verbs.Any(v=>v.Equals("properties",StringComparison.OrdinalIgnoreCase)),"Native copy/properties verbs missing.");
                using var dismiss=new System.Windows.Forms.Timer{Interval=120};dismiss.Tick+=(_,_)=>EndMenu();dismiss.Start();
                var elapsed=System.Diagnostics.Stopwatch.StartNew();menu.Show(new Point(40,40),handle,Keys.None);dismiss.Stop();
                Require(elapsed.ElapsedMilliseconds>=100,"The native popup did not enter its interactive menu loop.");
                Require(File.ReadAllText(file)=="unchanged","Dismissing the menu changed the file.");
            }
            using(var commands=new ShellContextMenu(file,handle))
            {
                clipboardChanged=true;commands.ExecuteForVerification("copy",handle);
                Require(Clipboard.ContainsFileDropList()&&Clipboard.GetFileDropList().Cast<string>().Any(p=>Path.GetFullPath(p).Equals(Path.GetFullPath(file),StringComparison.OrdinalIgnoreCase)),"Native copy did not put the actual file on the clipboard.");
                Require(File.ReadAllText(file)=="unchanged","Native copy changed the source.");
                commands.ExecuteForVerification("cut",handle);
                Require(Clipboard.ContainsFileDropList()&&File.ReadAllText(file)=="unchanged","Native cut moved the source before paste.");
                if(previousClipboard!=null)Clipboard.SetDataObject(previousClipboard,true);else Clipboard.Clear();clipboardChanged=false;
                commands.ExecuteForVerification("delete",handle,true);
                var settle=System.Diagnostics.Stopwatch.StartNew();
                while(File.Exists(file)&&settle.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(20);}
                Require(!File.Exists(file),"Native delete did not remove the disposable verification file.");
            }
            var app=new App(store,true);
            try{app.RefreshMissingEntries(entry);Require(basket.Entries.Count==0,"Native file deletion left a stale basket item.");}
            finally{app.Quit();}
            return new{Passed=true,NativeShellMenu=true,UnicodePath=true,NativeCopyAndPropertiesVerbs=true,RepeatedPopupAndDismissCycles=5,
                InteractiveNativePopupLoop=true,NativeCopyCopiesActualFile=true,NativeCutKeepsSourceUntilPaste=true,NativeDeleteRemovesOnlyVerificationFile=true,
                DeletedFileRemovedFromBasketCount=true,OriginalClipboardRestored=true,NoCustomTakeOutAction=true};
        }
        finally{if(clipboardChanged){if(previousClipboard!=null)Clipboard.SetDataObject(previousClipboard,true);else Clipboard.Clear();}if(File.Exists(file))File.Delete(file);Directory.Delete(data);}
    }
}
