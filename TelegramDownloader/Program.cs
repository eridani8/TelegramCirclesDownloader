using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using TelegramDownloader.Service;

const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
var logsPath = Path.Combine(AppContext.BaseDirectory, "logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Spectre(outputTemplate)
    .WriteTo.File($"{logsPath}/errors-.log", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

try
{
    if (!Directory.Exists(".env"))
    {
        throw new ApplicationException(".env не найден");
    }
    
    Env.Load();
    
    var apiId = Env.GetString("API_ID");
    var apiHash = Env.GetString("API_HASH");
    var phoneNumber = Env.GetString("PHONE_NUMBER");
    var password = Env.GetString("PASSWORD_2FA");
    
    var builder = Host.CreateApplicationBuilder();
    
    builder.Services.AddSerilog();
    builder.Services.AddHostedService<ConsoleMenu>();
    
    var app = builder.Build();

    await app.RunAsync();
}
catch (Exception e)
{
    Log.ForContext<Program>().Fatal(e, "Приложение не может загрузиться");
}
finally
{
    await Log.CloseAndFlushAsync();
}