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
    /// Класс Startup является входной точкой в приложение ASP.NET Core. Этот класс производит конфигурацию приложения, настраивает сервисы, которые приложение будет использовать, устанавливает компоненты для обработки запроса или middleware.
    /// <para>При запуске приложения сначала срабатывает конструктор, затем метод ConfigureServices() и в конце метод Configure(). Эти методы вызываются средой выполнения ASP.NET.</para>
    /// <para>Можно создать конструктор без параметров, а можно в качестве параметров передать сервисы IWebHostEnvironment (передает информацию о среде, в которой запускается приложение) и IConfiguration (передает конфигурацию приложения), которые доступны для приложения по умолчанию. К примеру, можно получить доступный для приложения по умолчанию сервис IWebHostEnvironment, сохранить его в переменную и использовать при обработке запроса</para>
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


            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            });
            app.UseForwardedHeaders();

            app.UseAuthentication();
        public void ConfigureServices(IServiceCollection services)
        {
            _Singleton.ConnectionString = conf.GetConnectionString(env.IsDevelopment() ? "conn" : "conn1");
            services.AddDbContext<_DbContext>(options => options.UseSqlServer(_Singleton.ConnectionString));

            services.Configure<WebEncoderOptions>(options =>
            {
                options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
            });

            // установка конфигурации подключения
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => //CookieAuthenticationOptions
                {
                    options.LoginPath = new PathString("/account/login");
                    options.ExpireTimeSpan = TimeSpan.FromDays(365);
                });

            //Добавляет только те сервисы фреймворка MVC, которые позволяют использовать контроллеры и представления и связанную функциональность.
            services.AddControllersWithViews();
            //Все сессии работают поверх объекта IDistributedCache, и ASP.NET Core предоставляет встроенную реализацию IDistributedCache, которую мы можем использовать.
            services.AddDistributedMemoryCache(); services.AddSession();

            services.AddSignalR();
        }

        /// <summary>
        /// Метод Configure устанавливает, как приложение будет обрабатывать запрос. Этот метод является обязательным. В принципе в метод Configure в качестве параметра может передаваться любой сервис, который зарегистрирован в методе ConfigureServices или который регистрируется для приложения по умолчанию (например, IWebHostEnvironment).
        /// <para>Обработка запроса в ASP.NET Core устроена по принципу конвейера. Сначала данные запроса получает первый компонент в конвейере. После обработки он передает данные HTTP-запроса второму компоненту и так далее. Эти компоненты конвейера, которые отвечают за обработку запроса, называются middleware. Компонент middleware может либо передать запрос далее следующему в конвейере компоненту, либо выполнить обработку и закончить работу конвейера. Также компонент middleware в конвейере может выполнять обработку запроса как до, так и после следующего в конвейере компонента. Компоненты middleware конфигурируются с помощью методов расширений Run, Map и Use объекта IApplicationBuilder, который передается в метод Configure() класса Startup. Каждый компонент может быть определен как анонимный метод (встроенный inline компонент), либо может быть вынесен в отдельный класс. Для создания компонентов middleware используется делегат RequestDelegate, который выполняет некоторое действие и принимает контекст запроса: public delegate Task RequestDelegate(HttpContext context);</para>
        /// <para>Метод Configure выполняется один раз при создании объекта класса Startup, и компоненты middleware создаются один раз и живут в течение всего жизненного цикла приложения. То есть для последующей обработки запросов используются одни и те же компоненты.</para>
        /// <para>Метод Run представляет собой простейший способ для добавления компонентов middleware в конвейер. Однако компоненты, определенные через метод Run, не вызывают никакие другие компоненты и дальше обработку запроса не передают. Метод Use также добавляет компоненты middleware, которые также обрабатывают запрос, но в нем может быть вызван следующий в конвейере запроса компонент middleware. Метод Map (и методы расширения MapXXX()) применяется для сопоставления пути запроса с определенным делегатом, который будет обрабатывать запрос по этому пути. </para>
        /// <para>Кроме использования делегатов в методах Run/Use/Map мы можем создавать свои компоненты middleware в виде отдельных классов, которые затем добавляются в конвейер с помощью метода UseMiddleware(). </para>
        /// </summary>
        /// <param name="app">Для установки компонентов, которые обрабатывают запрос, используются методы объекта IApplicationBuilder.</param>
        /// <param name="env">Позволяет получить информацию о среде, в которой запускается приложение, и взаимодействовать с ней.</param>
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

            //Выводит подробные сообщения об ошибках
            app.UseDeveloperExceptionPage();
            //Заголовок Strict-Transport-Security сообщает браузеру, что к приложению надо обращаться по протоколу https, а не по протоколу http.
            if (!env.IsDevelopment()) app.UseHsts();
            //Добавляет для проекта переадресацию на тот же ресурс только по протоколу https (если приложение имеет поддержку SSL)
            app.UseHttpsRedirection();
            //Добавляет поддержку статических файлов
            app.UseStaticFiles(new StaticFileOptions
            {
                ServeUnknownFileTypes = true,
                DefaultContentType = "text/html"
            });
            //Добавляет механизм работы с сессиями
            app.UseSession();

            app.UseAuthentication();
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                RequestCultureProviders = new List<IRequestCultureProvider> { new AcceptLanguageHeaderRequestCultureProvider() }
            });

            //Вызов app.UseRouting() добавляет некоторые возможности маршрутизации, благодаря чему приложение может соотносить запросы с определенными маршрутами.
            app.UseRouting();

            app.UseAuthorization();

            //Позволяет определить маршруты, которые будут обрабатываться приложением
            app.UseEndpoints(endpoints =>
            {
                //Встраивается система маршрутизации, которая позволяет связать приходящие от пользователей запросы с контроллерами.
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
