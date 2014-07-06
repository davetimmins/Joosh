using Nancy;
using System;

namespace Joosh.Web.Nancy.Modules
{
    public class HomeModule : NancyModule
    {
        public HomeModule()
        {
            Get["/"] = parameters => { return View["index"]; };
        }
    }
}