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

namespace Joosh.Nancy.UnifiedSearch
{
    internal sealed class ArcGISGateway : PortalGateway
    {
        public ArcGISGateway(String serviceUrl)
            : base(serviceUrl)
        { }

        public async Task<FindResponse> DoFind(Find findOptions, CancellationToken ct)
        {
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
