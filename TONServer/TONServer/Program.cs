using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer
{
    public class Program
    {
        /// <summary> Чтобы запустить приложение ASP.NET Core, необходим объект IHost, в рамках которого развертывается веб-приложение. Для создания IHost применяется объект IHostBuilder. </summary>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Непосредственно создание IHostBuilder производится с помощью метода Host.CreateDefaultBuilder(args).
        /// <para>Данный метод выполняет ряд задач:</para>
        /// <list type="bullet">
        /// <item><description>Устанавливает корневой каталог (для этого используется свойство Directory.GetCurrentDirectory).</description></item>
        /// <item><description>Устанавливает конфигурацию хоста. Для этого загружаются переменные среды с префиксом "DOTNET_" и аргументы командной строки.</description></item>
        /// <item><description>Устанавливает конфигурацию приложения. Для этого загружается содержимое из файлов appsettings.json</description></item>
        /// </list>
        /// <para>Далее вызывается метод ConfigureWebHostDefaults(). Этот метод призван выполнять конфигурацию параметров хоста, а именно:</para>
        /// <list type="bullet">
        /// <item><description>Загружает конфигурацию из переменных среды с префиксом "ASPNETCORE_"</description></item>
        /// <item><description>Запускает и настраивает веб-сервер Kestrel, в рамках которого будет разворачиваться приложение</description></item>
        /// <item><description>Добавляет компонент Host Filtering, который позволяет настраивать адреса для веб-сервера Kestrel</description></item>
        /// <item><description>Если переменная окружения ASPNETCORE_FORWARDEDHEADERS_ENABLED равна true, добавляет компонент Forwarded Headers, который позволяет считывать из запроса заголовки "X-Forwarded-"</description></item>
        /// <item><description>Если для работы приложения требуется IIS, то данный метод также обеспечивает интеграцию с IIS</description></item>
        /// </list>
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
