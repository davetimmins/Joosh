﻿using Joosh.MapConfig;
using Joosh.UnifiedSearch;
using Joosh.Web.Modules;
using Microsoft.Owin.Extensions;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Diagnostics;
using Nancy.Pile;
using Nancy.TinyIoc;
using Owin;
using System;

namespace Joosh.Web
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseNancy();
            app.UseStageMarker(PipelineStage.MapHandler);
        }
    }

    public class CustomBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            CustomStatusCode.AddCode(404);
            CustomStatusCode.AddCode(500);

            base.ApplicationStartup(container, pipelines);

            ServiceStack.Text.JsConfig.EmitCamelCaseNames = true;
            ServiceStack.Text.JsConfig.IncludeTypeInfo = false;
            ServiceStack.Text.JsConfig.ConvertObjectTypesIntoStringDictionary = true;
            ServiceStack.Text.JsConfig.IncludeNullValues = false;

            container.Register<HomeModule, HomeModule>();
            container.Register<ConfigurationModule, ConfigurationModule>();
            container.Register<SearchModule, SearchModule>();

#if !DEBUG
            DiagnosticsHook.Disable(pipelines);
#endif

            pipelines.AfterRequest.AddItemToEndOfPipeline((ctx) =>
            {
                if (ctx.Response.StatusCode == HttpStatusCode.InternalServerError) return;

                ctx.Response.Headers.Add("X-Frame-Options", "deny");
                ctx.Response.Headers.Add("X-Download-Options", "noopen");
                ctx.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                ctx.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            });
        }

#if DEBUG
        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration { Password = @"admin" }; }
        }
#endif

        protected override void ConfigureConventions(NancyConventions nancyConventions)
        {
            base.ConfigureConventions(nancyConventions);

            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("content"));
            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("images"));
            nancyConventions.StaticContentsConventions.Add(StaticContentConventionBuilder.AddDirectory("scripts"));

            nancyConventions.StaticContentsConventions.AddStylesBundle("styles.css", true,
                new[]
                {
                    "content/*.css"
                });

            nancyConventions.StaticContentsConventions.AddScriptsBundle("scripts.js", true,
                new[]
                {
                    "scripts/run.js"
                });
        }
    }
}