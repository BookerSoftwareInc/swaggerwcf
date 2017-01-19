using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using SwaggerWcf.Models;
using SwaggerWcf.Support;
using Path = System.IO.Path;

namespace SwaggerWcf
{
    public delegate Stream GetFileCustomDelegate(string filename, out string contentType, out long contentLength);

    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class SwaggerWcfEndpoint : ISwaggerWcfEndpoint
    {
        private static List<Service> Services { get; set; }
        private static int _initialized;

        public SwaggerWcfEndpoint()
        {
            Init();
        }

        public static event EventHandler<List<Service>> OnGenerationCompleted;

        public static void Init()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
                return;

            Services = ServiceBuilder.Build();

			OnGenerationCompleted?.Invoke(null, Services);
        }

        public static void SetCustomZip(Stream customSwaggerUiZipStream)
        {
            if (customSwaggerUiZipStream != null)
                Support.StaticContent.SetArchiveCustom(customSwaggerUiZipStream);
        }

        public static void SetCustomGetFile(GetFileCustomDelegate getFileCustom)
        {
            Support.StaticContent.GetFileCustom = getFileCustom;
        }

        public static void Configure(Info info)
        {
            Init();
            foreach (var service in Services)
            {
                service.Info = info;
            }
        }

        public Stream GetSwaggerFile()
        {
            return GetSwaggerFile(null);
        }

        public Stream GetSwaggerFile(string name)
        {
            WebOperationContext woc = WebOperationContext.Current;

            Service service;
            if (string.IsNullOrEmpty(name))
            {
                service = Services.First();
            }
            else
            {
                var serviceName = Path.GetFileNameWithoutExtension(name);

                service =
                    Services.FirstOrDefault(s => string.Equals(s.Name, serviceName, StringComparison.CurrentCultureIgnoreCase));

                if (service == null)
                {
                    woc.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                    return null;
                }
            }

            woc.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            woc.OutgoingResponse.ContentType = "application/json";

            // we always return first swagger
            return new MemoryStream(Encoding.UTF8.GetBytes(Serializer.Process(service)));
        }

        public Stream StaticContent(string content)
        {
            WebOperationContext woc = WebOperationContext.Current;

            if (woc == null)
                return Stream.Null;

            if (string.IsNullOrWhiteSpace(content))
            {
                string swaggerUrl = woc.IncomingRequest.UriTemplateMatch.BaseUri.LocalPath + "/swagger.json";
                woc.OutgoingResponse.StatusCode = HttpStatusCode.Redirect;
                woc.OutgoingResponse.Location = "index.html?url=" + swaggerUrl;
                return null;
            }

            string filename = content.Contains("?")
                ? content.Substring(0, content.IndexOf("?", StringComparison.Ordinal))
                : content;

            if (filename.EndsWith(".json", StringComparison.CurrentCultureIgnoreCase))
            {
                return GetSwaggerFile(filename);
            }

            string contentType;
            long contentLength;
            Stream stream = Support.StaticContent.GetFile(filename, out contentType, out contentLength);

            if (stream == Stream.Null)
            {
                woc.OutgoingResponse.StatusCode = HttpStatusCode.NotFound;
                return null;
            }

            woc.OutgoingResponse.StatusCode = HttpStatusCode.OK;
            woc.OutgoingResponse.ContentLength = contentLength;
            woc.OutgoingResponse.ContentType = contentType;

            return stream;
        }
    }
}
