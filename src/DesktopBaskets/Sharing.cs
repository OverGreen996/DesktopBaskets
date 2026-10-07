using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;

namespace DesktopBaskets;

internal sealed class ShareFile
{
    public string file_id="",name="",origin="";
    public long size {get;set;}
    public bool available {get;set;}
}
internal sealed class ShareDevice {public string device_id="",name="";}
internal sealed class ShareSnapshot
{
    public string text="",room_name="",device_id="",room_id="",endpoint="";
    public long revision {get;set;}
    public ShareFile[] files=Array.Empty<ShareFile>();
    public ShareDevice[] devices=Array.Empty<ShareDevice>();
}
internal sealed class ShareSettings
{
    public string Address="";
    public string Draft="";
    public bool Dirty;
    public Dictionary<string,string> Downloads=new();
}

// The helper is our child, uses inherited stdio and has no browser runtime.
// Credentials and full source paths never leave this local process boundary.
internal sealed class Sharing : IDisposable
{
    readonly Control owner;
    readonly object gate=new();
    readonly Dictionary<long,TaskCompletionSource<JToken>> pending=new();
    readonly SemaphoreSlim startGate=new(1,1);
    Process? process;
    StreamWriter? input;
    long nextId;
    bool disposed;
    string? lastState;
    public string Root {get;}
    public string Inbox {get;}
    public ShareSettings Settings {get;private set;}
    public ShareSnapshot? Snapshot {get;private set;}
    public Dictionary<string,string> LocalPaths {get;private set;}=new();
    public bool Joined {get;private set;}
    internal string SelfId {get;private set;}="";
    public bool Online {get;private set;}=true;
    public bool Running=>process!=null&&!process.HasExited;
    public string Status {get;private set;}="共享尚未啟動";
    public event Action? Changed;
    public event Action<JObject>? Progress;
    string SettingsPath=>Path.Combine(Root,"settings.json");
    internal string DataPath=>Path.Combine(Root,"room");
    internal string HelperPath {get;set;}=Path.Combine(AppContext.BaseDirectory,"DesktopBaskets.Share.exe");
    public Sharing(Control owner,string root,string? inbox=null)
    {
        this.owner=owner;Root=Path.Combine(root,"sharing");
        Inbox=inbox??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","DesktopBaskets");
        Directory.CreateDirectory(Root);
        Settings=File.Exists(SettingsPath)?Newtonsoft.Json.JsonConvert.DeserializeObject<ShareSettings>(File.ReadAllText(SettingsPath))??new():new();
    }
    public void Save()
    {
        string temp=SettingsPath+".tmp";
        File.WriteAllText(temp,Newtonsoft.Json.JsonConvert.SerializeObject(Settings,Newtonsoft.Json.Formatting.Indented),new UTF8Encoding(false));
        if(File.Exists(SettingsPath))File.Replace(temp,SettingsPath,SettingsPath+".bak");else File.Move(temp,SettingsPath);
    }
    void Ui(Action action)
    {
        if(disposed||owner.IsDisposed)return;
        if(owner.InvokeRequired){if(owner.IsHandleCreated)owner.BeginInvoke(new Action(()=>{if(!disposed&&!owner.IsDisposed)action();}));}
        else action();
    }
    void StartProcess()
    {
        if(disposed)throw new ObjectDisposedException(nameof(Sharing));
        lock(gate)
        {
            if(Running)return;
            if(!File.Exists(HelperPath))throw new FileNotFoundException("找不到共享元件，請重新安裝完整版本。",HelperPath);
            var info=new ProcessStartInfo(HelperPath){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            info.Arguments="--data "+Quote(DataPath)+" --downloads "+Quote(Inbox);
            var child=new Process{StartInfo=info,EnableRaisingEvents=true};
            child.OutputDataReceived+=(_,e)=>{if(e.Data!=null)Receive(e.Data);};
            // Errors are deliberately not dumped to the UI or a public log (pairing data may be sensitive).
            child.ErrorDataReceived+=(_,_)=>{};
            child.Exited+=(_,_)=>{if(ReferenceEquals(process,child))FailPending("共享連線已停止");};
            process=child;
            try {child.Start();input=new StreamWriter(child.StandardInput.BaseStream,new UTF8Encoding(false)){AutoFlush=true};child.BeginOutputReadLine();child.BeginErrorReadLine();}
            catch{process=null;child.Dispose();throw;}
        }
    }
    static string Quote(string path)=>"\""+path.Replace("\"","\\\"")+"\"";
    internal Task<JToken> RequestAsync(string op,object? args=null,int timeoutSeconds=30)
    {
        StartProcess();
        var command=args==null?new JObject():JObject.FromObject(args);
        long id=Interlocked.Increment(ref nextId);command["id"]=id;command["op"]=op;
        var completion=new TaskCompletionSource<JToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(gate)
        {
            if(pending.Count>=16)throw new InvalidOperationException("共享正在處理其他操作，請稍候。");
            pending[id]=completion;
            try{input!.WriteLine(command.ToString(Newtonsoft.Json.Formatting.None));}
            catch{pending.Remove(id);throw;}
        }
        return AwaitResponse();
        async Task<JToken> AwaitResponse()
        {
            using var timeoutCancellation=new CancellationTokenSource();
            var timeout=Task.Delay(TimeSpan.FromSeconds(timeoutSeconds),timeoutCancellation.Token);
            if(await Task.WhenAny(completion.Task,timeout)!=completion.Task)
            {lock(gate)pending.Remove(id);throw new TimeoutException("共享操作逾時，請檢查連線後重試。");}
            timeoutCancellation.Cancel();
            return await completion.Task;
        }
    }
    void Receive(string line)
    {
        try
        {
            var message=JObject.Parse(line);
            if(message["id"]?.Type==JTokenType.Integer)
            {
                long id=(long)message["id"]!;TaskCompletionSource<JToken>? completion;
                lock(gate){pending.TryGetValue(id,out completion);pending.Remove(id);}
                if(completion==null)return;
                if((bool?)message["ok"]==true)completion.TrySetResult(message["result"]??JValue.CreateNull());
                else completion.TrySetException(new IOException((string?)message["error"]??"共享操作失敗"));
            }
            else switch((string?)message["event"])
            {
                case "state":Ui(()=>Accept(message["payload"]!));break;
                case "room-online":Ui(()=>{bool value=(bool?)message["payload"]==true;if(value==Online)return;Online=value;UpdateStatus();Changed?.Invoke();});break;
                case "download-progress":Ui(()=>Progress?.Invoke((JObject)message["payload"]!));break;
            }
        }
        catch(Exception ex) when(ex is Newtonsoft.Json.JsonException||ex is InvalidCastException){ }
    }
    void FailPending(string error)
    {
        TaskCompletionSource<JToken>[] jobs;
        lock(gate){jobs=pending.Values.ToArray();pending.Clear();}
        foreach(var job in jobs)job.TrySetException(new IOException(error));
        Ui(()=>{Online=false;Status=error;Changed?.Invoke();});
    }
    public void Accept(JToken state)
    {
        if(state["snapshot"]==null)return;
        string serialized=state.ToString(Newtonsoft.Json.Formatting.None);
        if(lastState==serialized)return;lastState=serialized;
        Snapshot=state["snapshot"]!.ToObject<ShareSnapshot>();Joined=(string?)state["mode"]=="joined";
        SelfId=(string?)state["self_id"]??"";
        LocalPaths=state["local_paths"]?.ToObject<Dictionary<string,string>>()??new();
        UpdateStatus();Changed?.Invoke();
    }
    void UpdateStatus()=>Status=!Online?"來源離線 · 配對保留，正在重新連線":Snapshot==null?"共享尚未啟動":$"{(Joined?"已加入":"本機")} Room · {Snapshot.devices.Length} 台裝置";
    public async Task EnsureStartedAsync()
    {
        await startGate.WaitAsync();
        try
        {
            if(Running&&Snapshot!=null)return;
            var networks=await RequestAsync("interfaces");
            var network=networks.FirstOrDefault(n=>(string?)n["address"]==Settings.Address)??networks.OrderBy(n=>((string?)n["name"]??"").IndexOf("virtual",StringComparison.OrdinalIgnoreCase)>=0?1:0).FirstOrDefault();
            if(network==null)throw new IOException("請連接 Wi-Fi 或乙太網路，再按啟動共享。");
            Settings.Address=(string)network["address"]!;Save();
            Accept(await RequestAsync("start",new{address=Settings.Address}));Online=true;UpdateStatus();Changed?.Invoke();
        }
        catch(Exception ex){Status=ex.Message;Changed?.Invoke();throw;}
        finally{startGate.Release();}
    }
    public async Task<JToken> InvokeAsync(string op,object? args=null,int timeoutSeconds=30)
    {
        await EnsureStartedAsync();var result=await RequestAsync(op,args,timeoutSeconds);Accept(result);return result;
    }
    internal async Task ApplyNetworkAsync(string address)
    {
        await startGate.WaitAsync();
        try
        {
            var result=await RequestAsync("start",new{address});
            Settings.Address=address;Save();Online=true;Accept(result);UpdateStatus();Changed?.Invoke();
        }
        finally{startGate.Release();}
    }
    public string? PathFor(ShareFile file)=>LocalPaths.TryGetValue(file.file_id,out var local)?local:Settings.Downloads.TryGetValue(file.file_id,out var downloaded)&&File.Exists(downloaded)?downloaded:null;
    public async Task<string> DownloadAsync(ShareFile file)
    {
        var result=await InvokeAsync("download",new{file_id=file.file_id},86400);
        string path=(string)result["path"]!;Settings.Downloads[file.file_id]=path;Save();Changed?.Invoke();return path;
    }
    public void Stop()
    {
        Process? child;lock(gate){child=process;process=null;}
        if(child==null)return;
        try{if(!child.HasExited){input?.WriteLine("{\"op\":\"shutdown\"}");input?.Close();if(!child.WaitForExit(3000))child.Kill();}}
        catch(InvalidOperationException){}catch(IOException){}
        finally{input?.Dispose();input=null;child.Dispose();}
        Snapshot=null;lastState=null;Status="共享已停止";FailPending(Status);
    }
    internal static string? LegacyDataPath()=>new[]{Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData}
        .Select(folder=>Path.Combine(Environment.GetFolderPath(folder),"local.pocketdrop.m0","phone-trial"))
        .FirstOrDefault(path=>File.Exists(Path.Combine(path,"phone-trial.sqlite")));
    internal bool CanImportLegacy=>!Directory.Exists(Path.Combine(DataPath,"phone-trial"))&&LegacyDataPath()!=null;
    internal void ImportLegacy(string source)
    {
        if(Running)throw new InvalidOperationException("請先停止共享，再匯入 PocketDrop。");
        if(Process.GetProcesses().Any(p=>{try{return p.ProcessName.IndexOf("pocketdrop",StringComparison.OrdinalIgnoreCase)>=0;}catch{return false;}finally{p.Dispose();}}))
            throw new InvalidOperationException("請先正常退出 PocketDrop，避免兩個程式同時使用同一個 Room。");
        string target=Path.Combine(DataPath,"phone-trial");
        if(Directory.Exists(target))throw new InvalidOperationException("已建立共享資料，為保留現有 Room，不能覆蓋匯入。");
        if(!Directory.Exists(source))throw new DirectoryNotFoundException("找不到 PocketDrop 資料。");
        string staging=Path.Combine(Root,"import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            foreach(string file in Directory.GetFiles(source)){var targetFile=Path.Combine(staging,Path.GetFileName(file));File.Copy(file,targetFile,false);}
            Directory.CreateDirectory(DataPath);Directory.Move(staging,target);
        }
        catch{if(Directory.Exists(staging))Directory.Delete(staging,true);throw;}
    }
    public void Dispose(){if(disposed)return;disposed=true;Stop();}
}
