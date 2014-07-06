(function (MapManager) {
    'use strict';

    define([
        'esri/map',
        'esri/geometry/Extent',
        'esri/layers/ArcGISTiledMapServiceLayer',
        'esri/layers/ArcGISDynamicMapServiceLayer',
        'esri/layers/FeatureLayer',
        'esri/layers/ArcGISImageServiceLayer',
        'esri/dijit/Scalebar',
        'esri/config',
        'esri/request',
        'esri/urlUtils',
        'esri/tasks/GeometryService',
        'eagle/MapManager',
        'dojo/dom',
        'dojo/domReady!'
    ], function (
          Map,
          Extent,
          ArcGISTiledMapServiceLayer,
          ArcGISDynamicMapServiceLayer,
          FeatureLayer,
          ArcGISImageServiceLayer,
          Scalebar,
          esriConfig,
          esriRequest,
          urlUtils,
          GeometryService,
          MapManager,
          dom
        ) {

        function onConfigError(error) {
            console.log('ERROR - Loading config: ', error);
        }

        function onConfigSuccess(config) {

            delete config._ssl;

            var layerHash = {
                "ArcGISTiledMapServiceLayer": ArcGISTiledMapServiceLayer,
                "ArcGISDynamicMapServiceLayer": ArcGISDynamicMapServiceLayer,
                "ArcGISImageServiceLayer": ArcGISImageServiceLayer,
                "FeatureLayer": FeatureLayer
            };

            if (config.geometryServiceUrl)
                esriConfig.defaults.geometryService = new GeometryService(config.geometryServiceUrl);

            if (config.proxyUrl)
                esriConfig.defaults.io.proxyUrl = config.proxyUrl;

            if (config.proxyRules)
                for (var i = 0, tot = config.proxyRules.length; i < tot; i++) {
                    var rule = config.proxyRules[i];
                    urlUtils.addProxyRule({ urlPrefix: rule.urlPrefix, proxyUrl: rule.proxyUrl });
                }

            MapManager.map = new Map('map', config.options);
            MapManager.popupLayers = [];
            if (config.extent)
                MapManager.map.setExtent(new Extent(config.extent));

            var layers = config.layers.map(function (layer) {
                var lyr = new layerHash[layer.type](layer.url, layer.options);
                if (layer.options) {
                    if (layer.options._defExp) {
                        var layerDefinitions = [];
                        layerDefinitions[0] = layer.options._defExp;
                        lyr.setLayerDefinitions(layerDefinitions);
                    }
                    if (layer.options.popups) {
                        MapManager.popupLayers[0] = layer;
                    }
                }
                return lyr;
            });

            MapManager.map.on("layers-add-result", function (layersAdded) {

                if (dom.byId('home-button')) {
                    require(['esri/dijit/HomeButton'], function (HomeButton) {
                        var home = new HomeButton({
                            map: MapManager.map
                        }, 'home-button');
                        home.startup();
                    });
                }

                if (dom.byId('locate-button')) {
                    require(['esri/dijit/LocateButton'], function (LocateButton) {
                        var geoLocate = new LocateButton({
                            map: MapManager.map
                        }, "locate-button");
                        geoLocate.startup();
                    });
                }

                if (dom.byId('geocoder')) {
                    require(['esri/dijit/Geocoder'], function (Geocoder) {
                        var geocoder = new Geocoder({
                            map: MapManager.map,
                            autoComplete: true,
                            minCharacters: 2,
                            searchDelay: 100,
                            arcgisGeocoder: false,
                            geocoders: [{
                                url: 'http://localhost:63037/GeocodeServer',
                                name: "Eagle Unified Search",
                                singleLineFieldName: "SingleLine",
                                placeholder: "Search cities...",
                            }]
                        }, "geocoder");
                        geocoder.startup();
                    });
                }

                var scalebar = new Scalebar({
                    map: MapManager.map,
                    scalebarUnit: 'dual'
                });

                if (MapManager.popupLayers.length > 0 && MapManager.popupLayers[MapManager.popupLayers.length - 1].options.popups) {
                    require(['eagle/PopupManager'], function (PopupManager) {
                        MapManager.contentTemplate = MapManager.popupLayers[MapManager.popupLayers.length - 1].options.popups[0].content;
                        MapManager.popupManager = new PopupManager({ opLayers: MapManager.popupLayers });
                        MapManager.popupManager.initialisePopupManager();
                        MapManager.popupManager.initialiseMapFunction(MapManager.map);
                    });
                }
            });

            MapManager.map.addLayers(layers);
        }

        return {
            /**
            * Loads the map with the configuration requested
            *
            * @public
            * @param {String} configName
            */
            loadFromService: function (configName) {

                var requestParams = {
                    url: config.contextPath + '/map/' + configName,
                    headers: { 'Accept': 'application/json' },
                    handleAs: 'json'
                };
                esriRequest(requestParams).then(onConfigSuccess, onConfigError);
            }
        }
    });
})(this);


