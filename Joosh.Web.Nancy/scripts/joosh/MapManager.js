
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
    'dojo/dom',
    'dojo/_base/declare',
    'dojo/_base/lang',
    'dojo/domReady!'],
    function (
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
      dom,
      declare,
      lang
    ) {
        return declare("joosh.MapManager", null, {

            map: null,
            popupLayers: null,
            contentTemplate: null,
            popupManager: null,

            constructor: function (params) {
                lang.mixin(this, params);
            },

            _onConfigError: function (error) {
                console.log('ERROR - Loading config: ', error);
            },

            _onConfigSuccess: function (config) {

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

                this.map = new Map('map', config.options);
                this.popupLayers = [];
                if (config.extent)
                    this.map.setExtent(new Extent(config.extent));

                var layers = config.layers.map(function (layer) {
                    var lyr = new layerHash[layer.type](layer.url, layer.options);
                    if (layer.options) {
                        if (layer.options._defExp) {
                            var layerDefinitions = [];
                            layerDefinitions[0] = layer.options._defExp;
                            lyr.setLayerDefinitions(layerDefinitions);
                        }
                        if (layer.options.popups) {
                            this.popupLayers[0] = layer;
                        }
                    }
                    return lyr;
                });

                this.map.on("layers-add-result", function (layersAdded) {

                    if (dom.byId('home-button')) {
                        require(['esri/dijit/HomeButton'], function (HomeButton) {
                            var home = new HomeButton({
                                map: this.map
                            }, 'home-button');
                            home.startup();
                        });
                    }

                    if (dom.byId('locate-button')) {
                        require(['esri/dijit/LocateButton'], function (LocateButton) {
                            var geoLocate = new LocateButton({
                                map: this.map
                            }, "locate-button");
                            geoLocate.startup();
                        });
                    }

                    if (dom.byId('geocoder')) {
                        require(['esri/dijit/Geocoder'], function (Geocoder) {
                            var geocoder = new Geocoder({
                                map: this.map,
                                autoComplete: true,
                                minCharacters: 2,
                                searchDelay: 100,
                                arcgisGeocoder: false,
                                geocoders: [{
                                    url: appConfig.geocoderUrl,
                                    name: "Unified Search",
                                    singleLineFieldName: "SingleLine",
                                    placeholder: "Search cities...",
                                }]
                            }, "geocoder");
                            geocoder.startup();
                        });
                    }

                    if (dom.byId('print-button')) {
                        require(['joosh/Printer', 'esri/tasks/PrintTask'], function (Printer, PrintTask) {
                            var printer = new Printer({
                                map: this.map
                            }, "print-button");
                            printer.initialise(new PrintTask(config.printTask.url, config.printTask.options));
                        });
                    }

                    var scalebar = new Scalebar({
                        map: this.map,
                        scalebarUnit: 'dual'
                    });

                    if (this.popupLayers.length > 0 && this.popupLayers[this.popupLayers.length - 1].options.popups) {
                        require(['joosh/PopupManager'], function (PopupManager) {
                            this.contentTemplate = this.popupLayers[this.popupLayers.length - 1].options.popups[0].content;
                            this.popupManager = new PopupManager({ opLayers: this.popupLayers });
                            this.popupManager.initialisePopupManager();
                            this.popupManager.initialiseMapFunction(this.map);
                        });
                    }
                });

                this.map.addLayers(layers);
            },

            /**
            * Loads the map with the configuration requested
            *
            * @public
            * @param {String} configName
            */
            loadFromService: function (configName) {

                var requestParams = {
                    url: appConfig.contextPath + '/map/' + configName,
                    headers: { 'Accept': 'application/json' },
                    handleAs: 'json'
                };
                esriRequest(requestParams).then(this._onConfigSuccess, this._onConfigError);
            }
        });
    });


