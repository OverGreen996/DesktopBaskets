namespace DesktopBaskets;

internal static partial class Verification
{
    public static object MembershipTest(string root)
    {
        int assertions=0;
        void Check(bool ok,string message){assertions++;Require(ok,message);}
        string work=Path.GetFullPath(Path.Combine(root,"membership-"+Guid.NewGuid().ToString("N")));
        string desktop=Path.Combine(work,"desktop"),publicDesktop=Path.Combine(work,"public"),settings=Path.Combine(work,"settings");
        Directory.CreateDirectory(desktop);Directory.CreateDirectory(publicDesktop);
        var store=new Store(settings,desktop,publicDesktop);
        var basket=new Basket{Name="原分類",Locked=true};var other=new Basket{Name="其他分類"};
        store.State.Baskets.AddRange(new[]{basket,other});
        string text=Path.Combine(desktop,"保留.txt"),link=Path.Combine(desktop,"程式.lnk"),url=Path.Combine(desktop,"網站.url"),folder=Path.Combine(publicDesktop,"資料夾");
        File.WriteAllText(text,"original");File.WriteAllText(link,"shortcut before installer");File.WriteAllText(url,"[InternetShortcut]");Directory.CreateDirectory(folder);
        var originals=new[]{store.Add(basket,text),store.Add(basket,link),store.Add(basket,url),store.Add(basket,folder)};
        var ids=originals.Select(e=>e.Id).ToArray();var paths=originals.Select(e=>e.Path).ToArray();
        // Simulate pre-fix settings: all prior schema fields, no recovery property.
        var legacy=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(store.StatePath));legacy.Remove("UnavailableEntries");File.WriteAllText(store.StatePath,legacy.ToString());
        store=new Store(settings,desktop,publicDesktop);basket=store.State.Baskets[0];other=store.State.Baskets[1];
        Check(store.State.UnavailableEntries.Count==0&&basket.Entries.Select(e=>e.Id).SequenceEqual(ids),"Pre-fix settings migrate without dropping membership.");
        using(var app=new App(store,true,autoStart:true))
        {
            File.Move(link,link+".replacement");
            app.RefreshMissingEntries();
            Check(basket.Entries.Count==3&&store.State.UnavailableEntries.Count==1,"Unavailable shortcut disappears visually but retains classification.");
            Check(store.State.UnavailableEntries[0].Entry.Id==ids[1]&&store.State.UnavailableEntries[0].BasketId==basket.Id,"Recovery keeps entry identity and exact basket.");
            File.Move(url,url+".replacement");Directory.Move(folder,folder+".offline");
            app.RefreshMissingEntries();
            Check(basket.Entries.Count==1&&store.State.UnavailableEntries.Count==3,"Successive unavailable checks retain user and public desktop items.");
            Check(store.State.UnavailableEntries.OrderBy(e=>e.Index).Select(e=>e.Index).SequenceEqual(new[]{1,2,3}),"Successive absence preserves original item indices.");
            for(int i=0;i<20;i++)store.Save();
            var reopened=new Store(settings,desktop,publicDesktop);
            Check(reopened.State.UnavailableEntries.Count==3&&reopened.State.Baskets[0].Entries.Count==1,"Repeated save and restart do not erase missing membership.");
            var backup=JsonCodec.Deserialize<State>(File.ReadAllText(store.StatePath+".bak"))!;
            Check(backup.UnavailableEntries.Count==3,"Rotating state backup retains recovery records.");
            app.RefreshMissingEntries();
            Check(store.State.UnavailableEntries.Count==3,"Repeated missing events are idempotent.");
            File.Move(url+".replacement",url);app.RefreshMissingEntries();
            Check(basket.Entries.Single(e=>e.Path==url).Id==ids[2]&&store.State.UnavailableEntries.Count==2,"A later item returns first without a duplicate identity.");
            File.Move(link+".replacement",link);Directory.Move(folder+".offline",folder);app.RefreshMissingEntries();
            Check(basket.Entries.Select(e=>e.Id).SequenceEqual(ids),"Restoration in a different order restores the original basket order.");
            Check(basket.Entries.Select(e=>e.Path).SequenceEqual(paths)&&basket.Locked,"Paths and LOCK remain unchanged.");
            Check(File.ReadAllText(text)=="original"&&File.ReadAllText(link)=="shortcut before installer","Organizer does not change source files.");
            Check(store.State.UnavailableEntries.Count==0,"Recovered records leave no duplicate recovery rows.");
            File.Delete(link);app.RefreshMissingSelection(new[]{originals[1]});
            Check(!basket.Entries.Any(e=>e.Path==link)&&store.State.UnavailableEntries.Count==1,"Actual file deletion removes the visible icon and keeps safe metadata only.");
            Check(!File.Exists(link),"Deletion remains a real deletion; organizer never recreates a file.");
            File.WriteAllText(link,"new installer shortcut");
            var restarted=new Store(settings,desktop,publicDesktop);
            Check(restarted.State.Baskets[0].Entries.Select(e=>e.Id).SequenceEqual(ids)&&restarted.State.UnavailableEntries.Count==0,"An installer-recreated shortcut is recovered directly at startup.");
            app.RefreshMissingEntries();
            Check(basket.Entries.Single(e=>e.Path==link).Id==ids[1],"Running app also recovers the same shortcut identity.");
            store.Remove(basket,basket.Entries.Single(e=>e.Path==link));app.RefreshMissingEntries();
            Check(!basket.Entries.Any(e=>e.Path==link)&&store.State.UnavailableEntries.Count==0,"Explicit removal is respected and never automatically reversed.");
            File.Move(url,url+".replacement");app.RefreshMissingEntries();File.Move(url+".replacement",url);
            var moved=store.Add(other,url);app.RefreshMissingEntries();
            Check(moved.Id==ids[2]&&other.Entries.Single().Path==url&&!basket.Entries.Any(e=>e.Path==url),"Explicit reassignment overrides a previous unavailable classification.");
            Check(store.State.UnavailableEntries.Count==0,"Explicit reassignment clears obsolete recovery records.");
            Directory.Move(folder,folder+".offline");app.RefreshMissingEntries();
            store.Delete(basket);Directory.Move(folder+".offline",folder);app.RefreshMissingEntries();
            Check(store.State.UnavailableEntries.Count==0&&store.State.Baskets.Count==1,"Deleting a basket clears its unavailable records and never invents a basket.");
        }
        return new{Passed=true,Assertions=assertions,LegacySettingsPreserved=true,InstallerShortcutReplacementRecovered=true,UnavailablePathsRetainMembership=true,RestartAndRepeatedSaveSafe=true,PublicDesktopAndFoldersCovered=true,OriginalOrderAndPathsPreserved=true,ExplicitRemovalAndReassignmentRespected=true,RealDeletionPreserved=true,UserDesktopUntouched=true};
    }
}
