using System.Diagnostics;
using System.Drawing.Imaging;

namespace DesktopBaskets;

internal static partial class Verification
{
    public static object ThumbnailTest(string root,bool interactive=false)
    {
        root=Path.Combine(root,"thumbnails-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Grid.Configure(new Size(76,99));
        var specs=new[]{
            (Name:"風景.png",Size:new Size(1200,600),Format:ImageFormat.Png),
            (Name:"直幅.jpg",Size:new Size(300,900),Format:ImageFormat.Jpeg),
            (Name:"透明.png",Size:new Size(160,160),Format:ImageFormat.Png),
            (Name:"色塊.bmp",Size:new Size(320,200),Format:ImageFormat.Bmp),
            (Name:"圖像.gif",Size:new Size(240,180),Format:ImageFormat.Gif),
            (Name:"素材.tiff",Size:new Size(400,240),Format:ImageFormat.Tiff)
        };
        foreach(var spec in specs)
        {
            using var image=new Bitmap(spec.Size.Width,spec.Size.Height);
            using(var g=Graphics.FromImage(image))
            {
                g.Clear(spec.Name=="透明.png"?Color.Transparent:Color.FromArgb(35,83,116));
                using var teal=new SolidBrush(Color.FromArgb(66,200,157));
                g.FillEllipse(teal,spec.Size.Width/4,spec.Size.Height/4,spec.Size.Width/2,spec.Size.Height/2);
                using var yellow=new SolidBrush(Color.FromArgb(255,218,60));
                g.FillRectangle(yellow,spec.Size.Width/2,spec.Size.Height/2,spec.Size.Width/4,spec.Size.Height/4);
            }
            image.Save(Path.Combine(root,spec.Name),spec.Format);
        }
        string corrupt=Path.Combine(root,"損壞.png"),text=Path.Combine(root,"文字.txt"),missing=Path.Combine(root,"不存在.jpg");
        File.WriteAllText(corrupt,"Not an image");File.WriteAllText(text,"Original text");
        var paths=specs.Select(s=>Path.Combine(root,s.Name)).Concat(new[]{text,corrupt,missing}).ToArray();
        var baseline=paths.Where(File.Exists).ToDictionary(p=>p,p=>File.ReadAllBytes(p));
        var store=new Store(Path.Combine(root,"settings"));
        var basket=new Basket{Name="IMAGE PREVIEW",Width=9*Grid.CellWidth+Grid.Side,Height=3*Grid.CellHeight+Grid.VerticalFor(9*Grid.CellWidth+Grid.Side),Locked=true};
        store.State.Baskets.Add(basket);foreach(var path in paths)basket.Entries.Add(new Entry{Name=Path.GetFileName(path),Path=path});store.Save();
        using var app=new App(store,true);
        using var frame=new BasketWindow(app,basket){TopLevel=true,StartPosition=FormStartPosition.CenterScreen,Size=basket.ScreenBounds.Size};
        app.Manager.Hide();app.Icons.ImageReady+=frame.Viewport.RefreshImage;frame.Show();
        void Pump(Func<bool> ready,string message)
        {
            var clock=Stopwatch.StartNew();
            while(!ready()&&clock.ElapsedMilliseconds<15000){Application.DoEvents();Thread.Sleep(10);}
            Application.DoEvents();Require(ready(),message+"; cache="+app.Icons.Count+", pending="+app.Icons.PendingCount+", previews="+string.Join(",",specs.Select(s=>s.Name+":"+app.Icons.HasThumbnail(Path.Combine(root,s.Name))+":"+app.Icons.Failure(Path.Combine(root,s.Name)))));
        }
        try
        {
            Application.DoEvents();
            frame.Viewport.Refresh();
            Pump(()=>specs.All(s=>app.Icons.HasThumbnail(Path.Combine(root,s.Name))),"Supported image did not acquire a real Shell thumbnail");
            Pump(()=>app.Icons.PendingCount==0&&!app.Icons.WorkerRunning,"Thumbnail worker did not stop after loading");
            foreach(var spec in specs)
            {
                var image=app.Icons.Get(Path.Combine(root,spec.Name),app.Manager);
                Require(image.Width<=64&&image.Height<=48,"Thumbnail exceeded bitmap memory bound");
                Require(Math.Abs(image.Width/(double)image.Height-spec.Size.Width/(double)spec.Size.Height)<.12,"Thumbnail aspect ratio changed");
                using var file=new FileStream(Path.Combine(root,spec.Name),FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            }
            Require(!app.Icons.HasThumbnail(text)&&!app.Icons.HasThumbnail(corrupt)&&!app.Icons.HasThumbnail(missing),"Invalid/non-image file was marked as thumbnail");
            Require(app.Icons.Get(Path.Combine(root,"透明.png")).GetPixel(0,0).A<10,"Transparent preview lost its alpha channel");
            foreach(var pair in baseline)Require(File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value),"Thumbnail extraction changed an original file");
            var first=frame.Viewport.ItemBounds(0);
            var box=new Rectangle(first.X+4+(first.Width-8-48)/2,first.Y+8,48,36);
            var fit=IconCache.Fit(app.Icons.Get(paths[0]).Size,box);
            using(var screenshot=new Bitmap(frame.Width,frame.Height))
            {
                frame.DrawToBitmap(screenshot,new Rectangle(Point.Empty,screenshot.Size));
                using var content=new Bitmap(frame.Viewport.Width,frame.Viewport.Height);
                frame.Viewport.DrawToBitmap(content,new Rectangle(Point.Empty,content.Size));
                Require(content.GetPixel(fit.X+fit.Width/2,fit.Y+fit.Height/2).ToArgb()!=Theme.Panel.ToArgb(),"Basket did not draw image preview in file cell");
                screenshot.Save(Path.Combine(root,"basket-image-preview.png"));
            }
            // A file edited in-place must not remain stuck on its first preview.
            string update=Path.Combine(root,"updated.png");
            void Solid(Color color){using var bitmap=new Bitmap(100,50);using(var g=Graphics.FromImage(bitmap))g.Clear(color);bitmap.Save(update,ImageFormat.Png);}
            Solid(Color.Red);app.Icons.Get(update,app.Manager);
            Pump(()=>app.Icons.HasThumbnail(update),"Initial preview failed");
            Require(app.Icons.Get(update).GetPixel(20,10).R>200,"Initial preview pixels incorrect");
            Solid(Color.Lime);File.SetLastWriteTimeUtc(update,DateTime.UtcNow.AddMinutes(1));app.Icons.Invalidate(update);app.Icons.Get(update,app.Manager);
            Pump(()=>app.Icons.HasThumbnail(update),"Edited preview failed to reload");
            Require(app.Icons.Get(update).GetPixel(20,10).G>200,"Edited preview kept stale pixels");
            // Scroll-sized request storms and cancellation cannot exceed bounds
            // or refill a cleared standby cache with late worker completions.
            for(int i=0;i<96;i++)
            {
                string path=Path.Combine(root,$"scroll-{i}.png");File.Copy(paths[0],path);
                app.Icons.Get(path,app.Manager);
                Require(app.Icons.Count<=IconCache.Limit&&app.Icons.PendingCount<=IconCache.Limit,"Thumbnail queue/cache exceeded bound");
            }
            app.Icons.Clear();
            Pump(()=>!app.Icons.WorkerRunning,"Cleared cache worker continued running");
            Require(app.Icons.Count==0&&app.Icons.PendingCount==0,"Late completion refilled standby cache");
            frame.Viewport.Refresh();
            Pump(()=>specs.All(s=>app.Icons.HasThumbnail(Path.Combine(root,s.Name))),"Thumbnails did not return after cache clear");
            Pump(()=>app.Icons.PendingCount==0&&!app.Icons.WorkerRunning,"Worker stayed active on a warm cache");
            int count=app.Icons.Count;for(int i=0;i<10;i++){frame.Viewport.Refresh();Application.DoEvents();}
            Require(app.Icons.Count==count&&app.Icons.PendingCount==0&&!app.Icons.WorkerRunning,"Warm repaints restarted background extraction");
            if(interactive)
            {
                frame.Hide();frame.ShowInTaskbar=true;frame.Text="Desktop Baskets · 圖片縮圖驗證";
                frame.Show();frame.Activate();Application.Run(frame);
            }
            return new{Passed=true,Formats=new[]{"PNG","JPEG","BMP","GIF","TIFF"},AspectRatioPreserved=true,TransparentPngPreserved=true,RealBasketViewportPreview=true,OriginalPathsAndBytesPreserved=true,NoFileLocks=true,CorruptAndMissingFilesFallback=true,EditedFileReloads=true,CacheLimit=IconCache.Limit,PendingLimit=IconCache.Limit,StandbyDropsLateResults=true,WarmRepaintsDoNotStartWorker=true,WorkerExitsWhenIdle=true,Preview=Path.Combine(root,"basket-image-preview.png")};
        }
        finally{app.Quit();}
    }
}
