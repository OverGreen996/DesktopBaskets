using System.Runtime.InteropServices;

namespace DesktopBaskets;
internal static partial class Verification
{
    sealed class ContextRoutingHost : Form
    {
        public int ContextMessages {get;private set;}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x7B){ContextMessages++;m.Result=IntPtr.Zero;return;}
            base.WndProc(ref m);
        }
    }
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    public static object ContextRoutingTest(string root)
    {
        var store=new Store(Path.Combine(root,"context-routing",Guid.NewGuid().ToString("N")));
        var basket=new Basket{Name="右鍵路由驗證",Locked=true,Width=Grid.MinWidth+Grid.CellWidth*4,Height=Grid.MinHeight+Grid.CellHeight*2};
        store.State.Baskets.Add(basket);
        var app=new App(store,true);app.Manager.Hide();
        using var host=new ContextRoutingHost{ShowInTaskbar=false,Size=new Size(basket.Width,basket.Height)};
        using var frame=new BasketWindow(app,basket);
        try
        {
            var origin=host.PointToScreen(Point.Empty);basket.X=origin.X;basket.Y=origin.Y;
            frame.AttachToHost(host.Handle);frame.PerformLayout();
            // These are the actual layered basket controls. The host stands in for
            // Explorer and records any default context-message propagation.
            foreach(var handle in new[]{frame.Viewport.Handle,frame.GlassHandle,frame.Handle})
                foreach(var point in new[]{new IntPtr(-1),new IntPtr((50<<16)|50)})
                    SendMessage(handle,0x7B,handle,point);
            foreach(Control control in frame.Controls)
                SendMessage(control.Handle,0x7B,control.Handle,new IntPtr(-1));
            // A real right-button release generates WM_CONTEXTMENU through the
            // WinForms default window procedure, even with no file selected.
            SendMessage(frame.Viewport.Handle,0x205,IntPtr.Zero,new IntPtr((50<<16)|50));
            Require(host.ContextMessages==0,$"Basket right-click leaked {host.ContextMessages} context messages to its desktop parent.");
            return new{Passed=true,RealBasketControls=true,MouseAndKeyboardContextMessagesConsumed=true,RightButtonReleaseConsumed=true,DesktopParentContextMessages=host.ContextMessages};
        }
        finally{app.Quit();}
    }
    public static object IsolatedShellMenuTest(string root)
    {
        string data=Path.Combine(root,"isolated-shell-menu",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        string file=Path.Combine(data,"獨立原生選單.txt");File.WriteAllText(file,"unchanged");
        using var owner=new Form{Text="Desktop Baskets · 選單執行緒驗證",Size=new Size(360,150),StartPosition=FormStartPosition.CenterScreen};
        var start=new Button{Text="開始原生選單驗證",Dock=DockStyle.Fill};owner.Controls.Add(start);
        var results=new List<object>();Exception? failure=null;Thread? worker=null;
        Action? next=null;next=()=>
        {
            System.Windows.Forms.Timer? dismiss=null;
            worker=ShellContextMenu.StartIsolated(file,new Point(48,48),owner.Handle,Keys.None,(result,error)=>
            {
                dismiss?.Dispose();
                owner.BeginInvoke(new Action(()=>
                {
                    results.Add(result);failure=error;
                    if(error!=null||results.Count==3)owner.Close();else next!();
                }));
            },menu=>menu.BeforeTrackForVerification=()=>
            {
                dismiss=new System.Windows.Forms.Timer{Interval=1500};
                dismiss.Tick+=(_,_)=>{dismiss.Stop();EndMenu();};dismiss.Start();
            });
        };
        start.Click+=(_,_)=>{start.Enabled=false;owner.BeginInvoke(next!);};
        Application.Run(owner);worker?.Join(3000);
        File.WriteAllText(Path.Combine(root,"v049-isolated-shell-menu-observations.json"),JsonCodec.Serialize(results,new JsonOptions{WriteIndented=true}));
        if(failure!=null)throw failure;
        foreach(var observation in results)
        {
            var result=Newtonsoft.Json.Linq.JObject.FromObject(observation);
            Require((uint)result["MenuThreadId"]! !=(uint)result["CommandOwnerThreadId"]!,"Menu still shares the basket/manager UI thread.");
            Require((bool)result["OwnerWasForeground"]!,"Independent menu owner did not obtain foreground.");
            Require((long)result["OpenMilliseconds"]!>=1400,"Independent native menu closed before intentional dismissal.");
        }
        Require(results.Count==3&&File.ReadAllText(file)=="unchanged","Menu cycles incomplete or source changed.");
        return new{Passed=true,SeparateStaMenuThread=true,RepeatedNativePopupCycles=3,MinimumOpenMilliseconds=1400,SourceUnchanged=true,Observations=results};
    }
    public static object BasketMenuUiTest(string root,bool selectionTest=false)
    {
        string data=Path.Combine(root,"basket-menu-ui",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
        var text=Path.Combine(data,"原生文字測試.txt");File.WriteAllText(text,"unchanged");
        var link=Path.Combine(data,"原生捷徑測試.lnk");
        object? automation=null,shortcut=null;
        try
        {
            automation=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            shortcut=automation!.GetType().InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,automation,new object[]{link});
            shortcut!.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,shortcut,new object[]{text});
            shortcut.GetType().InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,shortcut,null);
        }
        finally{if(shortcut!=null)Marshal.ReleaseComObject(shortcut);if(automation!=null)Marshal.ReleaseComObject(automation);}
        var url=Path.Combine(data,"原生網址測試.url");File.WriteAllText(url,"[InternetShortcut]\r\nURL=https://example.com/\r\n");
        var folder=Path.Combine(data,"原生資料夾測試");Directory.CreateDirectory(folder);
        var store=new Store(Path.Combine(data,"config"));
        var basket=new Basket{Name="LOCAL RIGHT CLICK",Locked=true,Width=Grid.MinWidth+Grid.CellWidth*4,Height=Grid.MinHeight+Grid.CellHeight*2};
        store.State.Baskets.Add(basket);foreach(var file in new[]{text,link,url,folder})store.Add(basket,file);
        var destination=new Basket{Name="MULTI DROP",Locked=true,Width=basket.Width,Height=basket.Height};
        if(selectionTest)store.State.Baskets.Add(destination);
        var observations=new List<object>();var app=new App(store,true);app.Manager.Hide();
        app.FileMenuObserved=observation=>{observations.Add(observation);File.WriteAllText(Path.Combine(root,"basket-menu-ui-observations.json"),JsonCodec.Serialize(observations,new JsonOptions{WriteIndented=true}));};
        using var owner=new Form{Text=selectionTest?"Desktop Baskets · 籃框框選本機驗證":"Desktop Baskets · 籃框右鍵本機驗證",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.CenterScreen,Size=new Size(basket.Width,selectionTest?basket.Height*2+24:basket.Height),BackColor=Theme.Background};
        using var frame=new BasketWindow(app,basket);
        using var target=selectionTest?new BasketWindow(app,destination):null;
        var pointerEvents=new List<object>();
        void ObserveSelection()=>File.WriteAllText(Path.Combine(root,"basket-selection-ui-observations.json"),JsonCodec.Serialize(new{SourceObjects=basket.Entries.Count,DestinationObjects=destination.Entries.Count,Selected=frame.Viewport.SelectedEntries.Select(e=>e.Name).ToArray(),Capture=frame.Viewport.Capture,Selecting=frame.Viewport.IsSelecting,PathsUnchanged=new[]{text,link,url,folder}.All(Store.Exists),PointerEvents=pointerEvents.ToArray()},new JsonOptions{WriteIndented=true}));
        frame.Viewport.SelectionChanged+=(_,_)=>ObserveSelection();
        void ObservePointer(string kind,MouseEventArgs e){if(pointerEvents.Count<64)pointerEvents.Add(new{Kind=kind,e.X,e.Y,Button=e.Button.ToString(),frame.Viewport.Capture});frame.Viewport.BeginInvoke(new Action(ObserveSelection));}
        frame.Viewport.MouseDown+=(_,e)=>ObservePointer("down",e);frame.Viewport.MouseUp+=(_,e)=>ObservePointer("up",e);
        frame.Viewport.MouseMove+=(_,e)=>{if(e.Button!=MouseButtons.None)ObservePointer("move",e);};
        if(selectionTest)
        {
            foreach(var control in target!.Controls.Cast<Control>().Concat(new Control[]{target,Control.FromHandle(target.GlassHandle)!}))control.DragDrop+=(_,_)=>{frame.RefreshItems();target.RefreshItems();ObserveSelection();};
        }
        owner.Shown+=(_,_)=>
        {
            using var dpi=new Native.PhysicalDpiScope();
            var point=owner.PointToScreen(Point.Empty);basket.X=point.X;basket.Y=point.Y;
            frame.AttachToHost(owner.Handle);frame.Show();frame.RefreshItems();
            ObserveSelection();
            if(target!=null){destination.X=point.X;destination.Y=point.Y+basket.Height+24;target.AttachToHost(owner.Handle);target.Show();target.RefreshItems();ObserveSelection();}
        };
        owner.FormClosed+=(_,_)=>app.Quit();
        using var timeout=new System.Windows.Forms.Timer{Interval=600000};timeout.Tick+=(_,_)=>owner.Close();timeout.Start();
        try
        {
            owner.Show();app.Manager.BeginInvoke(new Action(()=>app.Manager.Hide()));
            Application.Run(app);
            return new{Completed=true,RealBasketViewport=true,RealLayeredGlassInput=true,MenuObservations=observations.ToArray(),TextSourceUnchanged=File.Exists(text)&&File.ReadAllText(text)=="unchanged"};
        }
        finally{app.Quit();}
    }
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
