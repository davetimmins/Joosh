define([
    "dojo/_base/lang",
    "dojo/_base/declare",
    "dojo/_base/array",
    "dojo/promise/all",
    "dojo/on",
    "dojo/dom-construct",
    "dojo/dom",
    "esri/tasks/query",
    "esri/tasks/QueryTask",
    "esri/dijit/Popup",
    "esri/symbols/SimpleFillSymbol",
    "esri/symbols/SimpleLineSymbol",
    "esri/symbols/SimpleMarkerSymbol",
    "esri/InfoTemplate"],
    function (lang, declare, array, all, on, domConstruct, dom, Query, QueryTask, Popup, SimpleFillSymbol, SimpleLineSymbol, SimpleMarkerSymbol, InfoTemplate) {
        return declare("eagle.PopupManager", null, {
            map: null,
            opLayers: null,
            _queries: null,
            _clickHandle: null,

            constructor: function (params) {
                lang.mixin(this, params);
                this._queries = [];
            },

            initialiseMapFunction: function (map) {
                if (map) {
                    this.map = map;
                    this._clickHandle = this.map.on("click", lang.hitch(this, this._executeQueries));
                }
            },

            initialisePopupManager: function () {
                if (this.opLayers) {
                    array.forEach(this.opLayers, lang.hitch(this, function (opLayer) {
                        if (opLayer.options.popups) {
                            this._setPopupForLayer(opLayer);
                        }
                    }));
                }
            },

            enableDisablePopups: function (eventObject) {
                eventObject.disable == true ? this._disablePopups() : this._enablePopups();
            },

            _enablePopups: function () {
                if (!this._clickHandle) {
                    this._clickHandle = on(this.map, "click", lang.hitch(this, this._executeQueries));
                }
            },

            _disablePopups: function () {
                if (this._clickHandle) {
                    this._clickHandle.remove();
                    this._clickHandle = null;
                }
            },

            _setPopupForLayer: function (layer) {
                var url = layer.url;
                var where = "";
                if (layer.options && layer.options._defExp)
                    where = layer.options._defExp;
                array.forEach(layer.options.popups, lang.hitch(this, function (popup) {
                    this._queries.push({ url: url + "/" + popup.layer.toString(), title: popup.title, content: popup.content });
                }));
            },

            _isLayerVisible: function (queryUrl) {
                for (var j = 0; j < this.map.layerIds.length; j++) {
                    var layer = this.map.getLayer(this.map.layerIds[j]);
                    if (queryUrl.toUpperCase().indexOf(layer.url.toUpperCase()) != -1) {
                        return layer.visible;
                    }
                }
                // not in map
                return false;
            },

            _pointToExtent: function (point) {
                var centerPoint = new esri.geometry.Point
                (point.x, point.y, point.spatialReference);
                var mapWidth = this.map.extent.getWidth();
                //Divide width in map units by width in pixels
                var pixelWidth = mapWidth / this.map.width;
                //Calculate a 10 pixel envelope width (5 pixel tolerance on each side)
                var tolerance = 10 * pixelWidth;
                //Build tolerance envelope and set it as the query geometry
                var queryExtent = new esri.geometry.Extent
                        (1, 1, tolerance, tolerance, point.spatialReference);
                return queryExtent.centerAt(centerPoint);
            },

            _executeQueries: function (e) {
                var queryGeom = this._pointToExtent(e.mapPoint);
                var promises = [];
                var res = [];
                array.forEach(this._queries, lang.hitch(this, function (somequery, indx) {
                    if (this._isLayerVisible(somequery.url)) {
                        var qt = new QueryTask(somequery.url);
                        var q = new Query();
                        q.geometry = queryGeom;
                        if (somequery.where) q.where = somequery.where;
                        q.outFields = ["*"];
                        q.returnGeometry = true;
                        promises.push(qt.execute(q));
                        //do lookup for result index vs real index
                        res.push(indx);
                    }
                }));
                all(promises).then(lang.hitch(this, this._handleQueryResults, queryGeom, res));
            },

            _handleQueryResults: function (mp, indxs, results) {
                // results from deferred lists are returned in the order they were created
                var someResults = [];
                for (var i = 0; i < results.length; i++) {
                    if (!results[i].hasOwnProperty("features"))
                        continue;
                    else {
                        array.forEach(results[i].features, lang.hitch(this, function (feature) {
                            someResults.push(this._showFeature(feature, this._queries[indxs[i]].title, this._queries[indxs[i]].content));
                        }), someResults);
                    }
                }
                if (someResults.length > 0) {

                    this.map.infoWindow.setFeatures(someResults);
                    this.map.infoWindow.show(mp.getCenter());
                }
            },

            _showFeature: function (feature, atitle, someContent) {
                // need to take into account date fields  and domain fields
                var title = atitle;
                var content = someContent;
                var template = new InfoTemplate(atitle, someContent);
                feature.setInfoTemplate(template);
                return feature;
            }
        });
    });