using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopBaskets;

// Bitmaps belong to the UI thread. One transient STA extracts thumbnails; it
// exits when its bounded queue empties and never polls during standby.
internal sealed class IconCache : IDisposable
{
    internal const int Limit=64;
    sealed class CachedImage
    {
        public Bitmap Image;
        public bool Thumbnail,Attempted;
        public string Failure="";
        public CachedImage(Bitmap image){Image=image;}
    }
    sealed class Request
    {
        public readonly string Path;
        public readonly CachedImage Entry;
        public readonly Control Owner;
        public Bitmap? Result;
        public string Failure="";
        public Request(string path,CachedImage entry,Control owner){Path=path;Entry=entry;Owner=owner;}
    }
    readonly Dictionary<string,CachedImage> cache=new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<string> order=new();
    readonly object gate=new();
    readonly Dictionary<string,Request> pending=new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<Request> queue=new();
    bool workerRunning,disposed;
    public event Action<string>? ImageReady;
    public int Count=>cache.Count;
    internal int PendingCount{get{lock(gate)return pending.Count;}}
    internal bool WorkerRunning{get{lock(gate)return workerRunning;}}
    public bool HasThumbnail(string path)=>cache.TryGetValue(path,out var entry)&&entry.Thumbnail;
    internal string Failure(string path)=>cache.TryGetValue(path,out var entry)?entry.Failure:"not cached";
    static readonly HashSet<string> ImageExtensions=new(StringComparer.OrdinalIgnoreCase)
        {".png",".jpg",".jpeg",".jpe",".jfif",".bmp",".gif",".tif",".tiff",".webp",".heic",".heif",".avif",".svg",".ico",".dng",".raw",".cr2",".nef"};
    public Bitmap Get(string path,Control? owner=null)
    {
        if(!cache.TryGetValue(path,out var entry))
        {
            while(cache.Count>=Limit&&order.Count>0)Invalidate(order.Dequeue());
            entry=new CachedImage(ReadIcon(path));cache[path]=entry;order.Enqueue(path);
        }
        if(!entry.Attempted&&owner!=null&&!owner.IsDisposed&&owner.IsHandleCreated&&ImageExtensions.Contains(Path.GetExtension(path)))
        {
            lock(gate)
            {
                if(!disposed&&!pending.ContainsKey(path)&&pending.Count<Limit)
                {
                    var request=new Request(path,entry,owner);pending.Add(path,request);queue.Enqueue(request);
                    if(!workerRunning)
                    {
                        workerRunning=true;
                        var thread=new Thread(Extract){IsBackground=true,Name="Desktop Baskets thumbnails",Priority=ThreadPriority.BelowNormal};
                        thread.SetApartmentState(ApartmentState.STA);thread.Start();
                    }
                }
            }
        }
        return entry.Image;
    }
    bool IsCurrent(Request request)=>!disposed&&pending.TryGetValue(request.Path,out var current)&&ReferenceEquals(current,request);
    void Extract()
    {
        while(true)
        {
            Request request;
            lock(gate)
            {
                if(queue.Count==0){workerRunning=false;return;}
                request=queue.Dequeue();if(!IsCurrent(request))continue;
            }
            Bitmap? image=ReadThumbnail(request.Path,out var failure);
            lock(gate)
            {
                if(!IsCurrent(request)){image?.Dispose();continue;}
                request.Result=image;request.Failure=failure;
            }
            try
            {
                request.Owner.BeginInvoke(new Action(()=>Complete(request)));
            }
            catch(InvalidOperationException)
            {
                lock(gate){if(IsCurrent(request)){pending.Remove(request.Path);request.Result?.Dispose();request.Result=null;}}
            }
        }
    }
    void Complete(Request request)
    {
        bool current;Bitmap? image;
        lock(gate){current=IsCurrent(request);if(current)pending.Remove(request.Path);image=request.Result;request.Result=null;}
        if(!current||!cache.TryGetValue(request.Path,out var entry)||!ReferenceEquals(entry,request.Entry)){image?.Dispose();return;}
        entry.Attempted=true;
        entry.Failure=request.Failure;
        if(image==null)return; // Unsupported/corrupt images keep their normal icon.
        entry.Image.Dispose();entry.Image=image;entry.Thumbnail=true;ImageReady?.Invoke(request.Path);
    }
    public void Invalidate(string path)
    {
        bool tracked=cache.ContainsKey(path);
        lock(gate)tracked|=pending.ContainsKey(path);
        if(!tracked)return;
        if(cache.TryGetValue(path,out var entry)){cache.Remove(path);entry.Image.Dispose();}
        lock(gate)
        {
            if(pending.TryGetValue(path,out var request)){request.Result?.Dispose();request.Result=null;pending.Remove(path);}
            var remaining=queue.Where(r=>!string.Equals(r.Path,path,StringComparison.OrdinalIgnoreCase)).ToArray();
            queue.Clear();foreach(var remainingRequest in remaining)queue.Enqueue(remainingRequest);
        }
        // Remove stale order entries so re-adding a file cannot evict it early.
        var live=order.Where(k=>cache.ContainsKey(k)).ToArray();order.Clear();foreach(var key in live)order.Enqueue(key);
    }
    public void Prune(IEnumerable<string> paths)
    {
        var live=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);
        foreach(var key in cache.Keys.Where(k=>!live.Contains(k)).ToArray())Invalidate(key);
    }
    public void Clear()
    {
        lock(gate){foreach(var request in pending.Values){request.Result?.Dispose();request.Result=null;}pending.Clear();queue.Clear();}
        foreach(var entry in cache.Values)entry.Image.Dispose();cache.Clear();order.Clear();
    }
    public void Dispose(){lock(gate)disposed=true;Clear();}
    internal static Rectangle Fit(Size image,Rectangle box)
    {
        double scale=Math.Min((double)box.Width/image.Width,(double)box.Height/image.Height);
        int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
        return new Rectangle(box.X+(box.Width-width)/2,box.Y+(box.Height-height)/2,width,height);
    }
    static Bitmap ReadIcon(string path)
    {
        SHGetFileInfo(path,0,out var info,(uint)Marshal.SizeOf<SHFILEINFO>(),0x100);
        if(info.Icon==IntPtr.Zero)return SystemIcons.Application.ToBitmap();
        try{using var icon=Icon.FromHandle(info.Icon);return icon.ToBitmap();}
        finally{DestroyIcon(info.Icon);}
    }
    static Bitmap? ReadThumbnail(string path,out string failure)
    {
        failure="";
        object? factory=null;IntPtr handle=IntPtr.Zero;
        try
        {
            var iid=typeof(IShellItemImageFactory).GUID;
            int created=SHCreateItemFromParsingName(Path.GetFullPath(path),IntPtr.Zero,ref iid,out factory);
            if(created<0||factory==null){failure="CreateItem: "+created.ToString("X8");return null;}
            // THUMBNAILONLY: never mistake a generic Shell icon for a preview.
            int fetched=((IShellItemImageFactory)factory).GetImage(new NativeSize{Width=64,Height=48},0x8,out handle);
            if(fetched<0||handle==IntPtr.Zero){failure="GetImage: "+fetched.ToString("X8");return null;}
            using var source=CopyShellBitmap(handle);
            var bounds=Fit(source.Size,new Rectangle(0,0,64,48));
            var result=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb);
            try
            {
                using var graphics=Graphics.FromImage(result);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source,new Rectangle(0,0,result.Width,result.Height));return result;
            }
            catch{result.Dispose();throw;}
        }
        catch(Exception error){failure=error.GetType().Name+": "+error.Message;return null;} // Shell thumbnail providers are optional.
        finally
        {
            if(handle!=IntPtr.Zero)DeleteObject(handle);
            if(factory!=null&&Marshal.IsComObject(factory))Marshal.ReleaseComObject(factory);
        }
    }
    static Bitmap CopyShellBitmap(IntPtr handle)
    {
        // FromHbitmap drops the alpha channel. Copy the Shell's 32-bit DIB
        // instead so transparent PNGs blend into the basket's glass surface.
        GetObject(handle,Marshal.SizeOf<NativeBitmap>(),out var native);
        int width=native.Width,height=Math.Abs(native.Height);
        if(width<=0||height<=0||width>512||height>512)throw new InvalidOperationException("Invalid Shell thumbnail dimensions");
        var info=new BitmapInfo{Header=new BitmapHeader{Size=40,Width=width,Height=-height,Planes=1,Bits=32}};
        var bytes=new byte[checked(width*height*4)];
        IntPtr dc=CreateCompatibleDC(IntPtr.Zero);
        try{if(GetDIBits(dc,handle,0,(uint)height,bytes,ref info,0)!=(uint)height)throw new InvalidOperationException("Cannot read Shell thumbnail pixels");}
        finally{if(dc!=IntPtr.Zero)DeleteDC(dc);}
        // Older providers produce an RGB bitmap with zero in every alpha byte.
        bool alpha=false;for(int i=3;i<bytes.Length;i+=4)if(bytes[i]!=0){alpha=true;break;}
        if(!alpha)for(int i=3;i<bytes.Length;i+=4)bytes[i]=255;
        var result=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        try
        {
            var data=result.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
            try{for(int y=0;y<height;y++)Marshal.Copy(bytes,y*width*4,IntPtr.Add(data.Scan0,y*data.Stride),width*4);}
            finally{result.UnlockBits(data);}
            return result;
        }
        catch{result.Dispose();throw;}
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeSize{public int Width,Height;}
    [StructLayout(LayoutKind.Sequential)]struct NativeBitmap{public int Type,Width,Height,WidthBytes;public ushort Planes,Bits;public IntPtr Pixels;}
    [StructLayout(LayoutKind.Sequential)]struct BitmapHeader{public uint Size;public int Width,Height;public ushort Planes,Bits;public uint Compression,ImageSize;public int XPixels,YPixels;public uint ColorsUsed,ColorsImportant;}
    [StructLayout(LayoutKind.Sequential)]struct BitmapInfo{public BitmapHeader Header;public uint Color;}
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory{[PreserveSig]int GetImage(NativeSize size,uint flags,out IntPtr bitmap);}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SHFILEINFO
    {
        public IntPtr Icon;public int Index;public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object? item);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(string path,uint attributes,out SHFILEINFO info,uint size,uint flags);
    [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll",EntryPoint="GetObjectW")]static extern int GetObject(IntPtr handle,int size,out NativeBitmap bitmap);
    [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")]static extern uint GetDIBits(IntPtr dc,IntPtr bitmap,uint start,uint lines,[Out]byte[] bits,ref BitmapInfo info,uint usage);
}
