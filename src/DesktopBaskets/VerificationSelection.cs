using System.Reflection;

namespace DesktopBaskets;
internal static partial class Verification
{
    static void Pointer(BasketViewport view,string method,MouseButtons button,Point point)
        =>typeof(BasketViewport).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,new object[]{new MouseEventArgs(button,1,point.X,point.Y,0)});
    static void Key(BasketViewport view,Keys key)
        =>typeof(BasketViewport).GetMethod("OnKeyDown",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,new object[]{new KeyEventArgs(key)});
    public static object SelectionTest(string root)
    {
        string data=Path.GetFullPath(Path.Combine(root,"selection-test",Guid.NewGuid().ToString("N")));Directory.CreateDirectory(data);
        var store=new Store(Path.Combine(data,"config"));
        var basket=new Basket{Name="框選驗證",Width=Grid.MinWidth,Height=Grid.MinHeight+Grid.CellHeight*2,Locked=true};
        var target=new Basket{Name="多選目的分類",Locked=true};store.State.Baskets.AddRange(new[]{basket,target});
        for(int i=0;i<40;i++){string path=Path.Combine(data,$"框選 {i:00}.txt");File.WriteAllText(path,"unchanged");store.Add(basket,path);}
        var original=basket.Entries.ToArray();var app=new App(store,true);app.Manager.Hide();
        using var host=new Form{ShowInTaskbar=false,ClientSize=new Size(basket.Width,basket.Height)};
        using var view=new BasketViewport(app,basket){Dock=DockStyle.Fill};host.Controls.Add(view);host.Show();
        Point Center(int i){var r=view.ItemBounds(i);return new Point(r.Left+r.Width/2,r.Top+r.Height/2);}
        void Down(int i,Keys modifiers=Keys.None)=>view.HandlePointerDown(Center(i),MouseButtons.Left,modifiers);
        void Expect(params int[] indices)=>Require(view.SelectedEntries.Select(e=>e.Id).SequenceEqual(indices.Select(i=>original[i].Id)),"Unexpected selected entries: "+string.Join(",",view.SelectedEntries.Select(e=>e.Name)));
        void Marquee(Point start,Point end,Keys modifiers=Keys.None){view.HandlePointerDown(start,MouseButtons.Left,modifiers);Require(view.IsSelecting,"Blank-space drag did not start selection.");view.UpdateMarquee(end);Pointer(view,"OnMouseUp",MouseButtons.Left,end);Require(!view.IsSelecting&&!view.Capture,"Selection capture leaked after release.");}
        IDataObject? clipboard=Clipboard.GetDataObject();bool changedClipboard=false;
        try
        {
            var first=view.ItemBounds(0);var third=view.ItemBounds(2);
            var start=new Point(first.Left+1,first.Top+1);var end=new Point(third.Right-2,third.Bottom-2);
            Marquee(start,end);Expect(0,1,2);
            view.HandlePointerDown(Center(1),MouseButtons.Right,Keys.None);Expect(0,1,2);
            Require((view.AccessibilityObject.GetChild(2)!.State&AccessibleStates.Selected)!=0,"Multi-selection missing from accessibility.");
            Down(1,Keys.Control);Expect(0,2);Down(4,Keys.Control);Expect(0,2,4);
            Down(1);Pointer(view,"OnMouseUp",MouseButtons.Left,Center(1));Down(4,Keys.Shift);Expect(1,2,3,4);
            Marquee(start,end,Keys.Control);Expect(0,3,4);
            Marquee(end,start);Expect(0,1,2);
            Down(0);Pointer(view,"OnMouseUp",MouseButtons.Left,Center(0));
            view.HandlePointerDown(start,MouseButtons.Left,Keys.None);view.UpdateMarquee(end);Key(view,Keys.Escape);Expect(0);
            Require(!view.IsSelecting&&!view.Capture,"Escape did not release selection capture.");
            view.HandlePointerDown(start,MouseButtons.Left,Keys.None);view.UpdateMarquee(end);view.Capture=false;Expect(0,1,2);
            Require(!view.IsSelecting,"Lost capture left an active selection gesture.");
            Key(view,Keys.Control|Keys.A);Require(view.SelectedEntries.Length==40,"Ctrl+A must include offscreen entries.");
            Key(view,Keys.Escape);Expect();
            view.HandlePointerDown(start,MouseButtons.Left,Keys.None);Pointer(view,"OnMouseUp",MouseButtons.Left,start);
            Key(view,Keys.Right);Expect(0);Key(view,Keys.Control|Keys.Right);Expect(0);Key(view,Keys.Control|Keys.Space);Expect(0,1);
            view.ScrollBy(3);first=view.ItemBounds(15);third=view.ItemBounds(17);
            Marquee(new Point(first.Left+1,first.Top+1),new Point(third.Right-2,third.Bottom-2));Expect(15,16,17);
            // The anchor stays in content coordinates when the user scrolls while selecting.
            view.HandlePointerDown(new Point(first.Left+1,first.Top+1),MouseButtons.Left,Keys.None);
            view.UpdateMarquee(new Point(third.Right-2,third.Bottom-2));view.ScrollBy(1);view.UpdateMarquee(new Point(third.Right-2,third.Bottom-2));
            Pointer(view,"OnMouseUp",MouseButtons.Left,new Point(third.Right-2,third.Bottom-2));Expect(15,16,17,20,21,22);
            Key(view,Keys.Escape);view.ScrollBy(-100);
            Down(1);Pointer(view,"OnMouseUp",MouseButtons.Left,Center(1));Down(2,Keys.Control);Expect(1,2);
            basket.Entries.Remove(original[0]);view.RefreshItems();Expect(1,2);
            basket.Entries.Remove(original[1]);view.RefreshItems();Expect(2);
            basket.Entries.Insert(0,original[0]);basket.Entries.Insert(1,original[1]);view.RefreshItems();
            Down(1,Keys.Control);Expect(1,2);Down(2);Pointer(view,"OnMouseUp",MouseButtons.Left,Center(2));Expect(2);
            Down(1);Pointer(view,"OnMouseUp",MouseButtons.Left,Center(1));Down(2,Keys.Control);
            using(var frame=new BasketWindow(app,target))
            {
                var payload=new DataObject();payload.SetData("DesktopBaskets.Entries",view.SelectedEntries.Select(e=>e.Id).ToArray());
                Require(!payload.GetDataPresent(DataFormats.FileDrop),"Visual selection drag must not move filesystem paths.");
                var args=new DragEventArgs(payload,0,0,0,DragDropEffects.Move,DragDropEffects.None);frame.OnDragEnter(frame,args);
                Require(args.Effect==DragDropEffects.Move,"Multi-file basket drop rejected.");frame.OnDrop(frame,args);
            }
            Require(target.Entries.Select(e=>e.Id).SequenceEqual(new[]{original[1].Id,original[2].Id})&&!basket.Entries.Any(e=>e.Id==original[1].Id||e.Id==original[2].Id),"Multi-file transfer did not move all visual memberships.");
            app.TransferEntries(target,target.Entries.Select(e=>e.Id).ToArray());Require(target.Entries.Count==2,"Dropping on the source basket duplicated entries.");
            string sub=Path.Combine(data,"另一個路徑");Directory.CreateDirectory(sub);string other=Path.Combine(sub,"跨路徑.txt");File.WriteAllText(other,"unchanged");
            foreach(var paths in new[]{new[]{original[1].Path,original[2].Path},new[]{original[2].Path,other}})
            {
                using var menu=new ShellContextMenu(paths,host.Handle);
                Require(menu.SelectionCount==2&&menu.Verbs().Contains("copy"),"Native multi-file menu missing.");
                changedClipboard=true;menu.ExecuteForVerification("copy",host.Handle);
                var settle=System.Diagnostics.Stopwatch.StartNew();
                while(settle.ElapsedMilliseconds<2000&&!Clipboard.GetFileDropList().Cast<string>().OrderBy(p=>p).SequenceEqual(paths.OrderBy(p=>p))){Application.DoEvents();Thread.Sleep(20);}
                Require(Clipboard.ContainsFileDropList()&&Clipboard.GetFileDropList().Cast<string>().OrderBy(p=>p).SequenceEqual(paths.OrderBy(p=>p)),"Native copy did not include exactly the selected files. Expected="+string.Join(";",paths)+" Actual="+string.Join(";",Clipboard.GetFileDropList().Cast<string>()));
            }
            app.RemoveEntries(target,target.Entries.ToArray());Require(target.Entries.Count==0,"Multi-file return/removal was incomplete.");
            // Exercise the actual glass forwarding with message coordinates that
            // deliberately differ from the physical cursor's current position.
            using(var frame=new BasketWindow(app,basket))
            {
                frame.AttachToHost(host.Handle);frame.Show();frame.RefreshItems();
                Point GlassPoint(Point p)=>new(p.X,p.Y+Grid.HeaderFor(basket.Width));
                void Message(int code,Point p){var glassPoint=GlassPoint(p);SendMessage(frame.GlassHandle,code,new IntPtr(code==0x202?0:1),new IntPtr((glassPoint.X&0xffff)|((glassPoint.Y&0xffff)<<16)));}
                first=frame.Viewport.ItemBounds(0);third=frame.Viewport.ItemBounds(1);
                start=new Point(first.Left+1,first.Top+1);end=new Point(third.Right-2,third.Bottom-2);
                Message(0x201,start);Message(0x200,end);Message(0x202,end);
                Require(frame.Viewport.SelectedEntries.Length==2&&!frame.Viewport.IsSelecting&&!frame.Viewport.Capture,"Glass input lost the marquee start or capture release.");
            }
            Require(original.All(e=>File.Exists(e.Path)&&File.ReadAllText(e.Path)=="unchanged"),"Selection changed source paths or contents.");
            return new{Passed=true,RubberBandBothDirections=true,CtrlToggle=true,ShiftRange=true,SelectAllIncludesOffscreen=true,ScrolledSelection=true,ScrollDuringSelection=true,EscapeAndLostCaptureRelease=true,SelectionSurvivesRefreshById=true,MultiFileBasketDrop=true,NativeMultiFileCopySameAndDifferentFolders=true,OriginalPathsAndContentsUnchanged=true,AccessibilityMultiSelection=true,RealGlassUsesMessageCoordinates=true};
        }
        finally{if(changedClipboard){if(clipboard!=null)Clipboard.SetDataObject(clipboard,true);else Clipboard.Clear();}host.Close();app.Quit();}
    }
}
