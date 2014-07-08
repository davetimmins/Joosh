using ArcGIS.ServiceModel.Operation;
using Nancy;
using Newtonsoft.Json.Linq;
using ServiceStack.Text;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Joosh.Proxy
{
    public class ProxyModule : NancyModule
    {
        readonly TokenProviderManager _tokenProviderManager;

        public ProxyModule(IRootPathProvider pathProvider)
        {
            ArcGIS.ServiceModel.Serializers.JsonDotNetSerializer.Init();
            _tokenProviderManager = new TokenProviderManager(pathProvider.GetRootPath());

            Post[@"/getToken/{UrlForToken}", true] = async (x, ct) =>
            {
                return await GenerateToken(x.UrlForToken);
            };

            Get[@"/proxyPrint", true] = async (x, ct) =>
            {
                return await DoPrint();
            };

            Post[@"/proxyPrint", true] = async (x, ct) =>
            {
                return await DoPrint();
            };

            Get[@"/proxy", true] = async (x, ct) =>
            {
                return await DoProxy();
            };

            Post[@"/proxy", true] = async (x, ct) =>
            {
                return await DoProxy();
            };

            Get[@"/proxy/simple", true] = async (x, ct) =>
            {
                return await DoSimpleProxy();
            };

            Post[@"/proxy/simple", true] = async (x, ct) =>
            {
                return await DoSimpleProxy();
            };
        }

        public async Task<Token> GenerateToken(String urlForToken)
        {
            if (String.IsNullOrWhiteSpace(urlForToken)) return null;

            return await _tokenProviderManager.GenerateToken(urlForToken);
        }

        public async Task<Response> DoPrint()
        {
            String uri = GetRequestPath();

            var data = String.Equals(base.Context.Request.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)
                ? base.Context.Request.Query as Nancy.DynamicDictionary
                : base.Context.Request.Form as Nancy.DynamicDictionary;

            if (!data.ContainsKey("Web_Map_as_JSON"))
                throw new InvalidOperationException("No webmap data was passed in");
            var webMapAsJson = data["Web_Map_as_JSON"];

            data["Web_Map_as_JSON"] = await _tokenProviderManager.AddTokenForMatchedPrintUrls(webMapAsJson);
            return await DoRequestAsGet(uri);
        }

        public async Task<Response> DoProxy()
        {
            string uri = GetRequestPath();

            if (!uri.Contains("token="))
            {
                var token = await GenerateToken(uri);
                if (token == null) throw new InvalidOperationException("No token generated for '{0}'".Fmt(uri));
                uri += uri.Contains("?") ? "&token=" + token.Value : "?token=" + token.Value;
            }

            return await DoRequestAsGet(uri);
        }

        public async Task<Response> DoSimpleProxy()
        {
            return await DoRequestAsGet(GetRequestPath());
        }

        async Task<Response> DoRequestAsGet(String uri, String format = "json")
        {
            var data = String.Equals(base.Context.Request.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)
                ? base.Context.Request.Query as Nancy.DynamicDictionary
                : base.Context.Request.Form as Nancy.DynamicDictionary;

            String callback = null;
            if (data != null)
            {
                if (data.ContainsKey("f"))
                    format = data["f"];
                if (data.ContainsKey("callback"))
                    callback = data["callback"];
            }
            String url = GetRequestPath() + "?";
            var keyArray = data.Keys.ToArray();
            for (var i = 1; i < keyArray.Length; i++)
                url += keyArray[i] + "=" + data[keyArray[i]] + "&";
            url = url.Trim('&');

            if (!url.Contains("token="))
            {
                var token = await GenerateToken(url);
                if (token != null)
                    url += url.Contains("?") ? "&token=" + token.Value : "?token=" + token.Value;
            }

            switch (format)
            {
                case "json":
                    if (!url.Contains("f="))
                        url += url.Contains("?") ? "&f=json" : "?f=json";
                    return StripDuplicateCallback(url.GetJsonFromUrl(), callback);
                case "image":
                    String imageFormat = data["format"];
                    return Response.FromStream(new MemoryStream(url.GetBytesFromUrl()), MimeTypes.GetMimeType("." + imageFormat.Replace("8", "").Replace("24", "").Replace("32", "")));

                default:
                    return StripDuplicateCallback(url.GetStringFromUrl(), callback);
            }
        }

        String GetRequestPath()
        {
            var data = base.Context.Request.Query as Nancy.DynamicDictionary;
            if (data == null) return null;

            String url = data.Keys.ToArray()[0].UrlDecode();
            return (url.IndexOf("?") > -1) ? url.SafeSubstring(0, url.IndexOf("?")) : url;
        }

        Response StripDuplicateCallback(String result, String match)
        {
            if (String.IsNullOrWhiteSpace(match)) return result;

            var firstIndex = result.IndexOf(match);
            var lastIndex = result.LastIndexOf(match);
            var same = firstIndex != result.LastIndexOf(match) && firstIndex != -1;
            if (same) return result;
            // if we get this far then we want jsonp so use Response.AsJson
            result = result.SafeSubstring(result.IndexOf('(') + 1).TrimEnd(new[] { ';', ')' });
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(result);
            return Response.AsJson(obj);
        }
    }
}
