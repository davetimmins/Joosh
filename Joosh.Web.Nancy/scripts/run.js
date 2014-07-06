(function () {
    'use strict';

    require({
        async: true,
        parseOnLoad: true,
        packages: [
        {
            name: 'eagle',
            location: config.contextPath + '/scripts/eagle',
        }]
    });

    require(["eagle/MapManager"], function (MapManager) {

        function getParameterByName(name) {
            name = name.replace(/[\[]/, "\\[").replace(/[\]]/, "\\]");
            var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
                results = regex.exec(location.search);
            return results == null ? "" : decodeURIComponent(results[1].replace(/\+/g, " "));
        }

        MapManager.loadFromService(getParameterByName('map') || 'default');
    });
}).call(this);
