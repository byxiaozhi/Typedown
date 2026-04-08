using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;

namespace System.Runtime.CompilerServices
{
    public static class IsExternalInit { }
}

namespace Typedown.Core
{
    public static class Config
    {
        public static JsonSerializerSettings EditorJsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy(true, true)
            },
            MaxDepth = 256
        };

        public static string GetLocalFolderPath()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public static string AppName => "Typedown";
    }
}
