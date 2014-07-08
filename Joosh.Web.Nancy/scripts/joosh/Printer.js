define([
    'esri/tasks/PrintTask',
    'esri/tasks/PrintParameters',
    'esri/tasks/PrintTemplate',
    'dojo/_base/lang',
    'dojo/_base/declare',
    'dojo/on',
    'dojo/dom'],
    function (
        PrintTask,
        PrintParameters,
        PrintTemplate,
        lang,
        declare,
        on,
        dom
    ) {
        return declare("joosh.Printer", null, {
            map: null,
            printTask: null,
            srcNodeRef: null,

            constructor: function (params, srcNodeRef) {
                lang.mixin(this, params);
                this.srcNodeRef = dom.byId(srcNodeRef);
            },

            initialise: function (printTask) {
                if (printTask) {
                    this.printTask = printTask;

                    this.printTask.on('complete', function (result) {
                        window.open(result.result.url);
                    });

                    this.printTask.on('error', function (error) {
                        alert(error.message);
                    });

                    on(this.srcNodeRef, 'click', lang.hitch(this, this._print));
                }
            },

            _print: function () {

                if (!this.printTask) return;

                var printParams = new PrintParameters();
                printParams.template = new PrintTemplate();
                printParams.template.layout = printParams.template.label = "A4 Landscape";
                printParams.template.format = "PDF";
                printParams.template.exportOptions = {
                    dpi: 300
                };
                printParams.template.layoutOptions = {
                    titleText: 'Joosh print',
                    scalebarUnit: "Kilometers",
                    copyrightText: "Something here",
                    authorText: 'Me'
                };
                printParams.map = this.map;

                this.printTask.execute(printParams);
            }
        });
    });