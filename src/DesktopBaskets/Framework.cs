// Use the Windows-included .NET Framework runtime: no browser or separate resident runtime.
using System.Text;
using Newtonsoft.Json;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
namespace DesktopBaskets
{
    internal sealed class JsonOptions { public bool WriteIndented { get; set; } }
    internal static class JsonCodec
    {
        public static string Serialize<T>(T value,JsonOptions? options=null) => JsonConvert.SerializeObject(value,options?.WriteIndented==true?Formatting.Indented:Formatting.None);
        public static void Serialize<T>(Stream stream,T value,JsonOptions? options=null)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(Serialize(value,options));stream.Write(bytes,0,bytes.Length);
        }
        public static T? Deserialize<T>(string text) => JsonConvert.DeserializeObject<T>(text,new JsonSerializerSettings{MaxDepth=64});
    }
}
namespace DesktopBaskets
{
    internal static class MathEx { public static int Clamp(int value,int min,int max) => Math.Max(min,Math.Min(max,value)); }
}
