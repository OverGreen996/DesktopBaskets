using Microsoft.Win32;

namespace DesktopBaskets;

internal sealed class StartupSettings
{
    internal const string RunPath="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    internal const string ValueName="DesktopBaskets";
    readonly string registryPath;
    readonly string executable;
    internal string Command => "\""+executable+"\" --autostart";
    internal StartupSettings(string? executable=null,string? registryPath=null)
    {
        this.executable=Path.GetFullPath(executable??Application.ExecutablePath);
        this.registryPath=registryPath??RunPath;
    }
    internal bool Enabled
    {
        get
        {
            using var key=Registry.CurrentUser.OpenSubKey(registryPath);
            var value=key?.GetValue(ValueName) as string;
            return string.Equals(value?.Trim(),Command,StringComparison.OrdinalIgnoreCase)
                ||string.Equals(value?.Trim(),"\""+executable+"\"",StringComparison.OrdinalIgnoreCase);
        }
    }
    internal void SetEnabled(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(registryPath,true)
            ??throw new InvalidOperationException("無法更新登入啟動設定。");
        if(enabled)key.SetValue(ValueName,Command,RegistryValueKind.String);
        else key.DeleteValue(ValueName,false);
    }
    internal static bool RestoreBaskets(State state,bool autoStart)
    {
        if(!autoStart||state.Enabled||state.Baskets.Count==0)return false;
        state.Enabled=true;return true;
    }
    internal static void WaitForDesktop(Action connect,Action<int> delay,int attempts=30)
    {
        // Only the login launch retries. Stop as soon as Explorer is ready;
        // no timer, thread or periodic check remains after startup.
        for(int attempt=0;attempt<attempts;attempt++)
        {
            try{connect();return;}
            catch when(attempt+1<attempts){delay(1000);}
        }
    }
}
