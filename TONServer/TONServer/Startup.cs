using Libs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.WebEncoders;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace TONServer
{
    /// <summary> 
    ///  Startup      ASP.NET Core.     ,  ,    ,       middleware.
    /// <para>     ,   ConfigureServices()     Configure().      ASP.NET.</para>
    /// <para>    ,        IWebHostEnvironment (   ,    )  IConfiguration (  ),      .  ,         IWebHostEnvironment,         </para>
    /// </summary>
    public class Startup
    {
        public IConfiguration conf { get; }
        public IWebHostEnvironment env { get; }

        public Startup(IConfiguration conf, IWebHostEnvironment env)
        {
            this.conf = conf;
            this.env = env;
        }

        /// <summary>   ConfigureServices()  ,   . </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            _Singleton.ConnectionString = conf.GetConnectionString(env.IsDevelopment() ? "conn" : "conn1");
            services.AddDbContext<_DbContext>(options => options.UseSqlServer(_Singleton.ConnectionString));

            services.Configure<WebEncoderOptions>(options =>
            {
                options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
            });

            //   
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => //CookieAuthenticationOptions
                {
                    options.LoginPath = new PathString("/account/login");
                    options.ExpireTimeSpan = TimeSpan.FromDays(365);
                });

            //     MVC,         .
            services.AddControllersWithViews();
            //     IDistributedCache,  ASP.NET Core    IDistributedCache,    .
            services.AddDistributedMemoryCache(); services.AddSession();

            services.AddSignalR();
        }

        /// <summary>
        ///  Configure ,     .    .     Configure       ,     ConfigureServices        (, IWebHostEnvironment).
        /// <para>   ASP.NET Core    .        .      HTTP-     .   ,     ,  middleware.  middleware         ,       .   middleware        ,       .  middleware      Run, Map  Use  IApplicationBuilder,     Configure()  Startup.         ( inline ),       .    middleware   RequestDelegate,        : public delegate Task RequestDelegate(HttpContext context);</para>
        /// <para> Configure        Startup,   middleware           .            .</para>
        /// <para> Run        middleware  .  ,    Run,           .  Use    middleware,    ,            middleware.  Map (   MapXXX())        ,       . </para>
        /// <para>     Run/Use/Map      middleware    ,         UseMiddleware(). </para>
        /// </summary>
        /// <param name="app">  ,   ,    IApplicationBuilder.</param>
        /// <param name="env">    ,    ,    .</param>
        public async void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Helper.AppName = Assembly.GetExecutingAssembly().GetName().Name;
            Helper.PathLog = env.WebRootPath + "\\";
            Helper.PathLog = Helper.PathLog.Replace("\\", "/");
            _Singleton.Development = env.IsDevelopment();

            app.Use(async (context, next) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                context.Items["sw"] = sw;
                await next.Invoke();
            });

            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<_DbContext>())
                {
                    context.Database.Migrate();
                }
            }

            //    
            app.UseDeveloperExceptionPage();
            // Strict-Transport-Security  ,        https,     http.
            if (!env.IsDevelopment()) app.UseHsts();
            //           https (    SSL)
            app.UseHttpsRedirection();
            //   
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "text/html"
            });
            //    
            app.UseSession();

            app.UseAuthentication();
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                RequestCultureProviders = new List<IRequestCultureProvider> { new AcceptLanguageHeaderRequestCultureProvider() }
            });

            // app.UseRouting()    ,         .
            app.UseRouting();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto
            });

            app.UseAuthorization();

            //  ,    
            app.UseEndpoints(endpoints =>
            {
                //  ,         .
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=index}/{action=index}/{id?}");
                endpoints.MapHub<_Hub>("/bot");
            });

            await StartJob<Job>(1000);
        }

        public async Task StartJob<T>(double msecs) where T : IJob
        {
            string id = Helper.RandomString();
            var scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            var job = JobBuilder.Create<T>().WithIdentity(id).Build();
            var trigger = TriggerBuilder.Create().WithIdentity(id).WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMilliseconds(msecs)).RepeatForever()).Build();
            await scheduler.ScheduleJob(job, trigger);
            await scheduler.Start();
        }
    }
}
