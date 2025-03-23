using System.Text;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using Spectre.Console;
using TelegramDownloader.Service;
using TL;
using WTelegram;

const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
var logsPath = Path.Combine("logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Spectre(outputTemplate)
    .WriteTo.File($"{logsPath}/errors-.log", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

try
{
    if (!File.Exists(".env"))
    {
        throw new ApplicationException(".env не найден");
    }

    #region env

    Env.Load();
    
    var apiId = Env.GetString("API_ID");
    var apiHash = Env.GetString("API_HASH");
    var phoneNumber = Env.GetString("PHONE_NUMBER");
    var password = Env.GetString("PASSWORD_2FA");

    if (string.IsNullOrEmpty(apiId) || string.IsNullOrEmpty(apiHash) || string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(password))
    {
        throw new ApplicationException("Нужно заполнить .env файл");
    }

    string? ConfigTelegram(string what)
    {
        return what switch
        {
            "api_id" => apiId,
            "api_hash" => apiHash,
            "phone_number" => phoneNumber,
            "verification_code" => AnsiConsole.Prompt(new TextPrompt<string?>("Введите код:".MarkupMainColor())),
            "password" => password,
            _ => null
        };
    }

    #endregion

    if (!Directory.Exists(logsPath))
    {
        Directory.CreateDirectory(logsPath);
    }

    var wTelegramLogs = new StreamWriter(Path.Combine(logsPath, "WTelegram.log"), true, Encoding.UTF8) { AutoFlush = true };
    wTelegramLogs.AutoFlush = true;
    Helpers.Log = (lvl, str) => wTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

    var client = new Client(ConfigTelegram);
    var myself = await client.LoginUserIfNeeded();
    
    var builder = Host.CreateApplicationBuilder();

    builder.Services.AddSingleton<User>(_ => myself);
    builder.Services.AddSingleton<Client>(_ => client);
    builder.Services.AddSerilog();
    builder.Services.AddHostedService<ConsoleMenu>();
    
    var app = builder.Build();

    await app.RunAsync();
}
catch (Exception e)
{
    Log.ForContext<Program>().Fatal(e, "Приложение не может загрузиться");
    Console.ReadKey();
}
finally
{
    await Log.CloseAndFlushAsync();
}