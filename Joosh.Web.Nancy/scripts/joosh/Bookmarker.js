define([
    'esri/geometry/Extent',
    'dojo/_base/lang',
    'dojo/_base/declare',
    'dojo/on',
    'dojo/_base/json',
    'dojo/dom'],
    function (
        Extent,
        lang,
        declare,
        on,
        dojo,
        dom
    ) {
        return declare('joosh.Bookmarker', null, {
            map: null,

            constructor: function (params) {
                lang.mixin(this, params);

                if (this.map) {

                    this.map.on('load', function () {
                        if (window.location.hash) {
                            var hashExtent = dojo.fromJson('{' + window.location.hash.replace('#', '') + '}');
                            var savedExtent = new Extent({ xmin: hashExtent.xmin, ymin: hashExtent.ymin, xmax: hashExtent.xmax, ymax: hashExtent.ymax, spatialReference: this.map.spatialReference });
                            this.map.setExtent(savedExtent);
                        }

                        this.map.on('extent-change', function (e) {
                            window.location.hash = "xmin:" + this.map.extent.xmin +
                                ",ymin:" + this.map.extent.ymin +
                                ",xmax:" + this.map.extent.xmax +
                                ",ymax:" + this.map.extent.ymax;
                        });
                    });
                }
            }
        });
    });