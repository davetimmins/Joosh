using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Operation;
using Nancy.Helpers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Joosh.Proxy
{
    internal class TokenProviderManager
    {
        const String MapServerMatch = "/MapServer";
        const String FeatureServerMatch = "/FeatureServer";
        const String ImageServerMatch = "/ImageServer";
        String[] MatchTests = new[] { MapServerMatch, FeatureServerMatch, ImageServerMatch };
        static ConcurrentDictionary<String, ProxyTokenProvider> _tokenProviders = new ConcurrentDictionary<String, ProxyTokenProvider>();
        static TokenProviderConfiguration _config;

        public TokenProviderManager(String rootPath)
        {
            var configFile = new FileInfo(Path.Combine(rootPath, "bin", "Json", "tokenProviderConfig.json"));
            if (!configFile.Exists) throw new FileNotFoundException(configFile.Name);
            String json = File.ReadAllText(configFile.FullName);
            if (String.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("No data exists for token provider configuration at bin/Json/tokenProviderConfig.json");
            _config = JsonConvert.DeserializeObject<TokenProviderConfiguration>(json);
            foreach (var tp in _config.Providers)
            {
                ResolveTokenProvider(tp.Url);
            }
        }

        public async Task<Token> GenerateToken(String url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;

            var tokenProvider = ResolveTokenProvider(HttpUtility.UrlDecode(url));
            if (tokenProvider == null) return null;

            var token = await tokenProvider.CheckGenerateToken(System.Threading.CancellationToken.None);

            // keep track of the token and automatically request it rather than waiting for the user
            // only doing this as we can't use proper async here
            var expiryDate = token.Expiry.FromUnixTime();
            var timer = new System.Threading.Timer(
            (e) =>
            {
                if (e == null) return;
                GenerateToken(e.ToString());
            },
            HttpUtility.UrlDecode(url), ((int)expiryDate.Subtract(DateTime.UtcNow).TotalMilliseconds) + 1, System.Threading.Timeout.Infinite);

            return token;
        }

        internal async Task<String> AddTokenForMatchedPrintUrls(String requestUrl)
        {
            if (String.IsNullOrWhiteSpace(requestUrl)) return requestUrl;

            var partToReplace = (requestUrl.Contains("Web_Map_as_JSON=")) ? requestUrl.Substring(requestUrl.IndexOf("Web_Map_as_JSON=")) : requestUrl;

            var result = partToReplace.Clone().ToString();
            foreach (var provider in _tokenProviders)
            {
                var matchStart = (provider.Key.IndexOf('?') > -1) ? provider.Key.Substring(0, provider.Key.IndexOf('?')) : provider.Key;
                if (!result.Contains(matchStart)) continue;
                var token = await GenerateToken(provider.Key);

                if (token == null) continue;

                foreach (var matchValue in MatchTests)
                {
                    var startIndex = 0;
                    while (result.IndexOf(matchStart, startIndex) > -1 && result.IndexOf(matchValue, startIndex) > -1)
                    {
                        startIndex += result.IndexOf(matchValue, startIndex + result.IndexOf(matchStart) + matchValue.Length);
                        result = result.Insert(startIndex + matchValue.Length, "?token=" + token.Value);
                    }
                }
            }

            return requestUrl.Replace(partToReplace, result);
        }

        ProxyTokenProvider ResolveTokenProvider(String url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            Debug.WriteLine("Resolving token provider for " + url);

            foreach (var matchValue in MatchTests)
            {
                if (url.IndexOf(matchValue) > -1) url = url.Substring(0, url.IndexOf(matchValue) + matchValue.Length);
            }

            Debug.WriteLine("token provider url truncated to " + url);
            ProxyTokenProvider tokenProvider;
            if (_tokenProviders.TryGetValue(url, out tokenProvider)) return tokenProvider;

            var config = _config.Providers.FirstOrDefault(c => url.ToLower().StartsWith(c.Url.ToLower()));
            if (config == null) return null;

            tokenProvider = new ProxyTokenProvider(config);
            Debug.WriteLine("Created TokenProvider for url " + url);
            if (_tokenProviders.TryAdd(url, tokenProvider)) return tokenProvider;

            return null;
        }
    }

    internal class ProxyTokenProvider : TokenProvider
    {
        public ProxyTokenProvider(TokenProviderRule config)
            : base(config.Url, config.Username, config.Password)
        {
            TokenRequest.DontForceHttps = true;
            TokenRequest.Client = null;
            TokenRequest.Referer = null;
        }
    }

    [DataContract]
    public class TokenProviderConfiguration
    {
        [DataMember(Name = "providers")]
        public List<TokenProviderRule> Providers { get; set; }
    }

    [DataContract]
    public class TokenProviderRule
    {
        [DataMember(Name = "url")]
        public String Url { get; set; }
        [DataMember(Name = "user")]
        public String Username { get; set; }
        [DataMember(Name = "pw")]
        public String Password { get; set; }
    }
}
