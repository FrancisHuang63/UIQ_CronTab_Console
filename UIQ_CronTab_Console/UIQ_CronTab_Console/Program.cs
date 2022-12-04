using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UIQ_CronTab_Console;
using UIQ_CronTab_Console.Services;
using UIQ_CronTab_Console.Services.Interfaces;
using Microsoft.Extensions.Configuration;

static void Main(string[] args)
{
    using IHost host = Host.CreateDefaultBuilder(args).Build();
    IConfiguration config = new ConfigurationBuilder()
           .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .Build();


    // 1. 建立依賴注入的容器
    var serviceCollection = new ServiceCollection();
    // 2. 註冊服務
    serviceCollection.AddTransient<App>();
    serviceCollection.Configure<ConnectoinStringOption>(config.GetSection("MySqlOptions").GetSection("ConnectionString"));
    serviceCollection.AddScoped<IDataBaseService, MySqlDataBaseNcsUiService>();
    serviceCollection.AddScoped<IDataBaseService, MySqlDataBaseNcsLogService>();
    serviceCollection.AddScoped<IParseLogService, ParseLogService>();
    serviceCollection.AddScoped<IPhaseLogService, PhaseLogService>();
    serviceCollection.AddScoped<IMakeDailyLogService, MakeDailyLogService>();
    serviceCollection.AddScoped<ISshCommandService, SshCommandService>();
    serviceCollection.AddScoped<ILogFileService, LogFileService>();
    // 建立依賴服務提供者
    var serviceProvider = serviceCollection.BuildServiceProvider();

    // 3. 執行主服務
    serviceProvider.GetRequiredService<App>().Run(args);
}