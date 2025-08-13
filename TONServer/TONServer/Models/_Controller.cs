using Libs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer
{
    public class _Controller : Controller
    {
        public _DbContext db { get { return (_DbContext)HttpContext.RequestServices.GetService(typeof(_DbContext)); } }
        public IHubContext<_Hub> hub { get { return (IHubContext<_Hub>)HttpContext.RequestServices.GetService(typeof(IHubContext<_Hub>)); } }
        public IWebHostEnvironment env { get { return (IWebHostEnvironment)HttpContext.RequestServices.GetService(typeof(IWebHostEnvironment)); } }
        public User user;
        public Lang lang;
        public string session;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_Singleton.Hub == null) _Singleton.Hub = hub;
            _Singleton.WebRootPath = env.WebRootPath;
            _Singleton.Host = Request.Host.ToString();
            if (User.Identity.IsAuthenticated)
            {
                user = GetSession<User>("user");
                if (user == null)
                {
                    user = db.Users.Find(Helper.IntParse(User.Identity.Name));
                    SetSession("user", user);
                }
                ViewBag.user = user;
            }

            var route = Request.Path.Value;
            if (route.StartsWith("/admin"))
            {
                if (user == null || user.Role != "admin")
                {
                    if (Request.Method == "GET") { Response.Redirect(Url.Action("index", "index")); return; }
                    else throw new Exception("Access denied");
                }
            }

            if (!HttpContext.Session.Keys.Contains("lang"))
            {
                SetSession("session", Helper.RandomString(20));
                SetSession("lang", LangRep.Lang(user, Request, out var langS));
                SetSession("langS", langS);
                SetSession("lds", LangRep.LangDescs());
            }
            lang = GetSession<Lang>("lang");
            session = GetSession<string>("session");
            ViewBag.lang = lang;
            ViewBag.session = session;
            ViewBag.langS = GetSession<string>("langS");
            ViewBag.lds = GetSession<List<LangDesc>>("lds");

            base.OnActionExecuting(context);
        }

        protected void SetSession<T>(string key, T value)
        {
            HttpContext.Session.SetString(key, JsonConvert.SerializeObject(value));
        }

        protected T GetSession<T>(string key)
        {
            var value = HttpContext.Session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }

        public static string GetLeftPart(HttpRequest request)
        {
            return $"{request.Scheme}://{request.Host.Value}";
        }

        protected string RenderPartialViewToString(string viewName, object model = null)
        {
            if (string.IsNullOrEmpty(viewName)) viewName = ControllerContext.ActionDescriptor.DisplayName;

            ViewData.Model = model;

            using (StringWriter sw = new StringWriter())
            {
                var engine = (ICompositeViewEngine)HttpContext.RequestServices.GetService(typeof(ICompositeViewEngine));
                ViewEngineResult viewResult = engine.FindView(ControllerContext, viewName, false);

                ViewContext viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    sw,
                    new HtmlHelperOptions() //Added this parameter in
                );

                //Everything is async now!
                var t = viewResult.View.RenderAsync(viewContext);
                t.Wait();

                return sw.GetStringBuilder().ToString();
            }
        }
    }
}
