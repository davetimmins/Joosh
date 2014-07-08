using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Common;
using ArcGIS.ServiceModel.Operation;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading;
using ServiceStack.Text;
using Joosh.Proxy;

namespace Joosh.UnifiedSearch
{
    internal sealed class ArcGISGateway : PortalGateway
    {
        ProxyModule _proxyModule;

        public ArcGISGateway(String serviceUrl, ProxyModule proxyModule = null)
            : base(serviceUrl)
        {
            _proxyModule = proxyModule;
        }

        public override async Task<QueryResponse<T>> Query<T>(Query queryOptions, CancellationToken ct)
        {
            if (_proxyModule != null && String.IsNullOrWhiteSpace(queryOptions.Token))
            {
                var token = await _proxyModule.GenerateToken(queryOptions.BuildAbsoluteUrl(this.RootUrl));
                if (token != null) queryOptions.Token = token.Value;
            }

            return await Get<QueryResponse<T>, Query>(queryOptions, ct);
        }

        public async Task<FindResponse> DoFind(Find findOptions, CancellationToken ct)
        {
            if (_proxyModule != null && String.IsNullOrWhiteSpace(findOptions.Token))
            {
                var token = await _proxyModule.GenerateToken(findOptions.BuildAbsoluteUrl(this.RootUrl));
                if (token != null) findOptions.Token = token.Value;
            }

            var response = await Get<FindResponse, Find>(findOptions, ct);
            if (ct.IsCancellationRequested || response == null || response.Results == null || !response.Results.Any()) return null;

            foreach (var result in response.Results.Where(r => r.Geometry != null))
            {
                result.Geometry = JsonSerializer.DeserializeFromString(
                    JsonSerializer.SerializeToString(result.Geometry),
                    TypeMap[result.GeometryType]());

                (result.Geometry as IGeometry).SpatialReference = findOptions.OutputSpatialReference;
            }
            return response;
        }

        internal readonly static Dictionary<String, Func<Type>> TypeMap = new Dictionary<String, Func<Type>>
        {
            { GeometryTypes.Point, () => typeof(Point) },
            { GeometryTypes.MultiPoint, () => typeof(MultiPoint) },
            { GeometryTypes.Envelope, () => typeof(Extent) },
            { GeometryTypes.Polygon, () => typeof(Polygon) },
            { GeometryTypes.Polyline, () => typeof(Polyline) }
        };
    }

    public static class FindResultExtensions
    {
        public static List<Feature<IGeometry>> ToFeatures(this FindResult[] findResults)
        {
            var result = new List<Feature<IGeometry>>();
            foreach (var findResult in findResults)
            {
                result.Add(new Feature<IGeometry> { Attributes = findResult.Attributes, Geometry = (IGeometry) findResult.Geometry });
            }
            return result;
        }
    }
}
