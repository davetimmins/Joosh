using ArcGIS.ServiceModel.Common;
using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Operation;
using System;
using System.Collections.Generic;
using System.Linq;
using Nancy;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using ServiceStack.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using Joosh.Proxy;

namespace Joosh.UnifiedSearch
{
    public class SearchModule : NancyModule
    {
        readonly IRootPathProvider _pathProvider;
        static ConcurrentDictionary<String, ArcGISGateway> _arcGISGateways = new ConcurrentDictionary<String, ArcGISGateway>();
        ConcurrentDictionary<String, Joosh.Model.SearchConfigurationData> _configurationData = new ConcurrentDictionary<String, Joosh.Model.SearchConfigurationData>();
        ProxyModule _proxyModule;

        public SearchModule(IRootPathProvider pathProvider, ProxyModule proxyModule)
        {
            _pathProvider = pathProvider;
            ArcGIS.ServiceModel.Serializers.ServiceStackSerializer.Init();
            _proxyModule = proxyModule;

            // Used by ArcGIS Online when adding this service as a custom locator it does a check that it is a valid Esri locator
            // so we will cheat and just return one that we know works
            Get[@"/GeocodeServer", true] = async (x, ct) =>
            {
                var client = new HttpClient();
                var content = await client.GetStringAsync("http://geocode.arcgis.com/arcgis/rest/services/World/geocodeserver?f=json");

                return Response.AsText(content).WithContentType("application/json");
            };

            Get[@"/GeocodeServer/findAddressCandidates", true] = async (x, ct) =>
            {
                return await EsriSearch(ct);
            };

            Get[@"/GeocodeServer/suggest", true] = async (x, ct) =>
            {
                return await EsriSuggest(ct);
            };
        }

        async Task<List<dynamic>> DoSearch(SearchRequest searchRequest, CancellationToken ct)
        {
            String queryString = searchRequest.SearchString;
            if (String.IsNullOrWhiteSpace(queryString)) return null;

            var searchConfig = CheckConfig();

            var results = new List<object>();
            foreach (var queryObject in searchConfig.QuerySearches)
            {
                if (ct.IsCancellationRequested) return null;

                if (!String.IsNullOrEmpty(queryObject.Regex) && !new Regex(queryObject.Regex).IsMatch(queryString)) continue;

                var query = new Query(queryObject.Endpoint.AsEndpoint())
                {
                    Where = String.Format(queryObject.Expression, queryString),
                    OutputSpatialReference = new SpatialReference { Wkid = searchRequest.Wkid ?? searchConfig.OutputWkid },
                    ReturnGeometry = searchRequest.ReturnGeometry
                };
                Debug.WriteLine(String.Format("SearchService where clause for query endpoint {0}, {1}", query.RelativeUrl, query.Where));

                switch (queryObject.GeometryType)
                {
                    case GeometryTypes.Point:
                        var pointResult = await SingleQuery<Point>(ResolveArcGISGateway(queryObject.Endpoint), query, ct);
                        if (pointResult != null) results.AddRange(pointResult);
                        break;
                    case GeometryTypes.Envelope:
                        var extentResult = await SingleQuery<Extent>(ResolveArcGISGateway(queryObject.Endpoint), query, ct);
                        if (extentResult != null) results.AddRange(extentResult);
                        break;
                    case GeometryTypes.MultiPoint:
                        var multiPointResult = await SingleQuery<MultiPoint>(ResolveArcGISGateway(queryObject.Endpoint), query, ct);
                        if (multiPointResult != null) results.AddRange(multiPointResult);
                        break;
                    case GeometryTypes.Polyline:
                        var polylineResult = await SingleQuery<Polyline>(ResolveArcGISGateway(queryObject.Endpoint), query, ct);
                        if (polylineResult != null) results.AddRange(polylineResult);
                        break;
                    case GeometryTypes.Polygon:
                        var polygonResult = await SingleQuery<Polygon>(ResolveArcGISGateway(queryObject.Endpoint), query, ct);
                        if (polygonResult != null) results.AddRange(polygonResult);
                        break;
                };
            }

            foreach (var findObject in searchConfig.FindSearches)
            {
                if (ct.IsCancellationRequested) return null;

                var find = new Find(findObject.Endpoint.AsEndpoint())
                {
                    SearchText = queryString,
                    OutputSpatialReference = new SpatialReference { Wkid = searchRequest.Wkid ?? searchConfig.OutputWkid },
                    ReturnGeometry = searchRequest.ReturnGeometry,
                    SearchFields = findObject.SearchFields.ToList(),
                    LayerIdsToSearch = findObject.LayerIds.ToList()
                };
                Debug.WriteLine(String.Format("SearchService search text for find endpoint {0}, {1}", find.RelativeUrl, find.SearchText));

                var findResults = await SingleFind(ResolveArcGISGateway(findObject.Endpoint), find, ct);
                if (findResults != null) results.AddRange(findResults);
            }

            return results;
        }

        async Task<SuggestGeocodeResponse> EsriSuggest(CancellationToken ct)
        {
            var queryString = base.Context.Request.Query["text"];
            if (String.IsNullOrWhiteSpace(queryString)) return null;

            var searchConfig = CheckConfig();

            var response = new SuggestGeocodeResponse();
            var results = await DoSearch(new SearchRequest { SearchString = queryString, ReturnGeometry = false }, ct);
            if (ct.IsCancellationRequested || results == null || !results.Any()) return response;

            var suggestions = new List<Suggestion>();
            // convert results to SuggestGeocodeResponse since that is what the Esri control expects
            foreach (var result in results)
            {
                Dictionary<String, object> resultAttributes = result.Attributes;
                suggestions.Add(new Suggestion
                {
                    Text = resultAttributes[searchConfig.ReturnFields.Intersect(resultAttributes.Select(r => r.Key)).FirstOrDefault()].ToString()
                });
            }
            response.Suggestions = suggestions.ToArray();
            return response;
        }

        async Task<SingleInputCustomGeocodeResponse> EsriSearch(CancellationToken ct)
        {
            var queryString = base.Context.Request.Query["SingleLine"];
            var outSR = base.Context.Request.Query["outSR"];
            int? wkid = null;

            if (!String.IsNullOrWhiteSpace(outSR))
            {
                var sr = JsonSerializer.DeserializeFromString<SpatialReference>(outSR);
                wkid = sr.LatestWkid ?? sr.Wkid;
            }
            if (String.IsNullOrWhiteSpace(queryString)) return null;

            var searchConfig = CheckConfig();

            var results = await DoSearch(new SearchRequest { SearchString = queryString, Wkid = wkid }, ct);
            if (ct.IsCancellationRequested || results == null || !results.Any()) return new SingleInputCustomGeocodeResponse();

            var response = new SingleInputCustomGeocodeResponse { SpatialReference = results.First().Geometry.SpatialReference };
            var candidates = new List<Candidate>();
            // convert results to SingleInputCustomGeocodeResponse since that is what the Esri control expects
            foreach (var result in results)
            {
                var candidate = new Candidate { Score = 100, Attributes = new Dictionary<String, object> { { "Loc_name", "Eagle Unified Search" } } };
                Dictionary<String, object> resultAttributes = result.Attributes;
                candidate.Location = result.Geometry.GetCenter();
                candidate.Address = resultAttributes[searchConfig.ReturnFields.Intersect(resultAttributes.Select(r => r.Key)).FirstOrDefault()].ToString();
                candidates.Add(candidate);
            }
            response.Candidates = candidates.ToArray();
            return response;
        }

        async Task<List<Feature<T>>> SingleQuery<T>(ArcGISGateway gateway, Query query, CancellationToken ct) where T : IGeometry
        {
            if (gateway == null) return null;

            var queryResult = await gateway.Query<T>(query, ct);
            if (!ct.IsCancellationRequested && queryResult != null && queryResult.Features != null)
            {
                foreach (var feature in queryResult.Features)
                    if (feature != null && feature.Geometry != null) feature.Geometry.SpatialReference = queryResult.SpatialReference;
                return queryResult.Features.ToList();
            }

            return null;
        }

        async Task<List<Feature<IGeometry>>> SingleFind(ArcGISGateway gateway, Find find, CancellationToken ct)
        {
            if (gateway == null) return null;

            var findResult = await gateway.DoFind(find, ct);
            if (findResult != null && findResult.Results != null && findResult.Results.Any())
                return findResult.Results.ToFeatures();

            return null;
        }

        Joosh.Model.SearchConfigurationData CheckConfig()
        {
            Joosh.Model.SearchConfigurationData result;
            // currently only supporting one config but just in case we expand in the future
            if (_configurationData.TryGetValue("searchConfig", out result)) return result;

            System.Diagnostics.Debug.WriteLine("Loading search configuration from file");
            var configFile = new FileInfo(Path.Combine(_pathProvider.GetRootPath(), "bin", "Json", "searchConfig.json"));
            if (!configFile.Exists) throw new FileNotFoundException(configFile.Name);
            String json = File.ReadAllText(configFile.FullName);
            result = JsonSerializer.DeserializeFromString<Joosh.Model.SearchConfigurationData>(json);
            if (_configurationData.TryAdd("searchConfig", result)) return result;

            return null;
        }

        ArcGISGateway ResolveArcGISGateway(String url)
        {
            if (String.IsNullOrWhiteSpace(url)) return null;
            Debug.WriteLine("Resolving ArcGISGateway for " + url);

            ArcGISGateway arcGISGateway;
            if (_arcGISGateways.TryGetValue(url, out arcGISGateway)) return arcGISGateway;

            arcGISGateway = new ArcGISGateway(url, _proxyModule);
            Debug.WriteLine("Created ArcGISGateway for url " + url);
            if (_arcGISGateways.TryAdd(url, arcGISGateway)) return arcGISGateway;

            return null;
        }
    }

    public class SearchRequest
    {
        public SearchRequest()
        {
            ReturnGeometry = true;
        }

        public string SearchString { get; set; }

        public int? Wkid { get; set; }

        public bool ReturnGeometry { get; set; }
    }
}
