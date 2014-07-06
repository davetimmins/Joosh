using Joosh.Nancy.MapConfig.Interface;
using Joosh.Nancy.UnifiedSearch.Interface;
using Joosh.Web.Nancy.Modules;
using Microsoft.Owin.Extensions;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Diagnostics;
using Nancy.Pile;
using Nancy.TinyIoc;
using Owin;

namespace Joosh.Web.Nancy
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
            base.ApplicationStartup(container, pipelines);

            ServiceStack.Text.JsConfig.EmitCamelCaseNames = true;
            ServiceStack.Text.JsConfig.IncludeTypeInfo = false;
            ServiceStack.Text.JsConfig.ConvertObjectTypesIntoStringDictionary = true;
            ServiceStack.Text.JsConfig.IncludeNullValues = false;

            container.Register<HomeModule, HomeModule>();
            container.Register<ConfigurationModule, ConfigurationModule>();
            container.Register<SearchModule, SearchModule>();
        }

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