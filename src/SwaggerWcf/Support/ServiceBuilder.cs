using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using SwaggerWcf.Attributes;
using SwaggerWcf.Configuration;
using SwaggerWcf.Models;
using SettingElement = SwaggerWcf.Configuration.SettingElement;

namespace SwaggerWcf.Support
{
    internal class ServiceBuilder
    {

        private class ServiceBuildInfo
        {
            public string Name { get; set; }
            public List<Path> Paths { get; set; }
            public List<Type> DefinitionsTypesList { get; set; }
        }

        public static List<Service> Build()
        {
            return BuildService();
        }

        private static List<Service> BuildService()
        {
            const string sectionName = "swaggerwcf";
            SwaggerWcfSection config =
                (SwaggerWcfSection)(ConfigurationManager.GetSection(sectionName) ?? new SwaggerWcfSection());

            List<string> hiddenTags = GetHiddenTags(config);
            List<string> visibleTags = GetVisibleTags(config);
            IReadOnlyDictionary<string, string> settings = GetSettings(config);

            var serviceInfos = BuildPaths(hiddenTags);
            var result = new List<Service>();

            foreach (var serviceInfo in serviceInfos)
            {
                var service = new Service();

                ProcessSettings(service, settings);
                service.Paths = serviceInfo.Value.Paths;
                service.Definitions = DefinitionsBuilder.Process(hiddenTags, visibleTags,
                    serviceInfo.Value.DefinitionsTypesList);
                service.Name = serviceInfo.Value.Name ?? string.Empty;

                result.Add(service);
            }

            return result;
        }

        private static List<string> GetHiddenTags(SwaggerWcfSection config)
        {
            return config.Tags == null
                       ? new List<string>()
                       : config.Tags.OfType<TagElement>()
                               .Where(t => t.Visibile.Equals(false))
                               .Select(t => t.Name)
                               .ToList();
        }

        private static List<string> GetVisibleTags(SwaggerWcfSection config)
        {
            return config.Tags == null
                       ? new List<string>()
                       : config.Tags.OfType<TagElement>()
                               .Where(t => t.Visibile.Equals(true))
                               .Select(t => t.Name)
                               .ToList();
        }

        private static IReadOnlyDictionary<string, string> GetSettings(SwaggerWcfSection config)
        {
            return config.Settings == null
                       ? new Dictionary<string, string>()
                       : config.Settings.OfType<SettingElement>().ToDictionary(se => se.Name, se => se.Value);
        }

        private static void ProcessSettings(Service service, IReadOnlyDictionary<string, string> settings)
        {
            if (settings.ContainsKey("BasePath"))
                service.BasePath = settings["BasePath"];
            if (settings.ContainsKey("Host"))
                service.Host = settings["Host"];

            if (settings.Keys.Any(k => k.StartsWith("Info")))
                service.Info = new Info();
            if (settings.ContainsKey("InfoDescription"))
                service.Info.Description = settings["InfoDescription"];
            if (settings.ContainsKey("InfoVersion"))
                service.Info.Version = settings["InfoVersion"];
            if (settings.ContainsKey("InfoTermsOfService"))
                service.Info.TermsOfService = settings["InfoTermsOfService"];
            if (settings.ContainsKey("InfoTitle"))
                service.Info.Title = settings["InfoTitle"];

            if (settings.Keys.Any(k => k.StartsWith("InfoContact")))
                service.Info.Contact = new InfoContact();
            if (settings.ContainsKey("InfoContactName"))
                service.Info.Contact.Name = settings["InfoContactName"];
            if (settings.ContainsKey("InfoContactUrl"))
                service.Info.Contact.Url = settings["InfoContactUrl"];
            if (settings.ContainsKey("InfoContactEmail"))
                service.Info.Contact.Email = settings["InfoContactEmail"];

            if (settings.Keys.Any(k => k.StartsWith("InfoLicense")))
                service.Info.License = new InfoLicense();
            if (settings.ContainsKey("InfoLicenseUrl"))
                service.Info.License.Url = settings["InfoLicenseUrl"];
            if (settings.ContainsKey("InfoLicenseName"))
                service.Info.License.Name = settings["InfoLicenseName"];
        }

        private static Dictionary<TypeInfo, ServiceBuildInfo> BuildPaths(IList<string> hiddenTags)
        {
            var wcfAssembly = typeof(ServiceContractAttribute).Assembly.GetName().Name;

            // if assembly is from GAC, then we don't need to process it
            // or if assembly doesn't reference System.ServiceModel so it can't have WCF services and we also can safely ignore it
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(
                    assembly =>
                        !assembly.GlobalAssemblyCache ||
                        assembly.GetReferencedAssemblies().Any(a => a.Name != wcfAssembly)).ToArray();

            Dictionary<TypeInfo, ServiceBuildInfo> result = new Dictionary<TypeInfo, ServiceBuildInfo>();

            foreach (Assembly assembly in assemblies)
            {
                IEnumerable<TypeInfo> types;
                try
                {
                    types = assembly.DefinedTypes;
                }
                catch (Exception)
                {
                    // ignore assembly and continue
                    continue;
                }

                foreach (TypeInfo ti in types)
                {
                    SwaggerWcfAttribute da = ti.GetCustomAttribute<SwaggerWcfAttribute>();
                    if (da == null || hiddenTags.Any(ht => ht == ti.AsType().Name))
                        continue;

                    Mapper mapper = new Mapper(hiddenTags);

                    var info = new ServiceBuildInfo();
                    info.DefinitionsTypesList = new List<Type>();
                    info.Name = da.Name;
                    info.Paths = mapper.FindMethods(da.ServicePath, ti.AsType(), info.DefinitionsTypesList).ToList();

                    result.Add(ti, info);
                }
            }

            return result;
        }
    }
}
