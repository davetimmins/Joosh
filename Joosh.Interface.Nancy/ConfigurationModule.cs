using Joosh.Model;
using Nancy;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Joosh.MapConfig
{
    public class ConfigurationModule : NancyModule
    {
        ConcurrentDictionary<String, MapConfigurationData> _configurationData = new ConcurrentDictionary<String, MapConfigurationData>();

        public ConfigurationModule(IRootPathProvider pathProvider)
        {
            Get[@"/map/{role}"] = req =>
            {
                if (String.IsNullOrWhiteSpace(req.Role)) return HttpStatusCode.BadRequest;

                MapConfigurationData result;
                if (_configurationData.TryGetValue(req.Role, out result)) return result;

                var configFile = new FileInfo(Path.Combine(pathProvider.GetRootPath(), "bin", "Json", String.Format("{0}.json", req.Role)));
                if (!configFile.Exists) throw new FileNotFoundException(configFile.Name);
                String json = File.ReadAllText(configFile.FullName);
                if (String.IsNullOrWhiteSpace(json))
                    return HttpStatusCode.InternalServerError;

                result = ServiceStack.Text.JsonSerializer.DeserializeFromString<MapConfigurationData>(json);
                if (_configurationData.TryAdd(req.Role, result)) return result;

                return null;
            };
        }
    }
}
