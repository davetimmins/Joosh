(function () {
    'use strict';

    require({
        async: true,
        parseOnLoad: true,
        packages: [
        {
            name: 'joosh',
            location: appConfig.contextPath + '/scripts/joosh',
        }]
    });

    require(["joosh/MapManager"], function (MapManager) {

        function getParameterByName(name) {
            name = name.replace(/[\[]/, "\\[").replace(/[\]]/, "\\]");
            var regex = new RegExp("[\\?&]" + name + "=([^&#]*)"),
                results = regex.exec(location.search);
            return results == null ? "" : decodeURIComponent(results[1].replace(/\+/g, " "));
        }

        new MapManager().loadFromService(getParameterByName('map') || 'default');
    });
}).call(this);
