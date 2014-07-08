using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Joosh.MapConfig
{
    public class MapConfigurationData
    {
        public String Role { get; set; }

        public Extent Extent { get; set; }

        public dynamic Options { get; set; }

        public String GeometryServiceUrl { get; set; }

        public String ProxyUrl { get; set; }

        public List<ProxyRule> ProxyRules { get; set; }

        public PrintTask PrintTask { get; set; }

        public List<MapLayer> Layers { get; set; }

        public ViewerOptions ViewerOptions { get; set; }
    }

    public class ViewerOptions
    {
        public String ViewerTitle { get; set; }

        public String ViewerSubTitle { get; set; }
    }

    public class ProxyRule
    {
        public String UrlPrefix { get; set; }

        public String ProxyUrl { get; set; }
    }

    public class MapLayer
    {
        public String Type { get; set; }

        public String Url { get; set; }

        public dynamic Options { get; set; }
    }

    public static class MapLayerType
    {
        public const String ArcGISDynamicMapServiceLayer = "ArcGISDynamicMapServiceLayer";
        public const String ArcGISTiledMapServiceLayer = "ArcGISTiledMapServiceLayer";
        public const String FeatureLayer = "FeatureLayer";
        public const String ArcGISImageServiceLayer = "ArcGISImageServiceLayer";
    }

    [DataContract]
    public class Extent
    {
        [DataMember(Order = 5)]
        public SpatialReference SpatialReference { get; set; }

        [DataMember(Name = "xmin", Order = 1)]
        public double XMin { get; set; }

        [DataMember(Name = "xmax", Order = 3)]
        public double XMax { get; set; }

        [DataMember(Name = "ymin", Order = 2)]
        public double YMin { get; set; }

        [DataMember(Name = "ymax", Order = 4)]
        public double YMax { get; set; }
    }

    public class SpatialReference
    {
        public int Wkid { get; set; }
    }

    public class PrintTask
    {
        public String Url { get; set; }

        public dynamic Options { get; set; }
    }
}
