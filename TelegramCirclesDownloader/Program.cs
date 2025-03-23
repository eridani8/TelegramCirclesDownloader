using System.Text;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Spectre;
using Spectre.Console;
using TelegramCirclesDownloader.Service;
using TL;
using WTelegram;

const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}";
var logsPath = Path.Combine("logs");

if (!Directory.Exists(logsPath))
{
    Directory.CreateDirectory(logsPath);
}

if (!Directory.Exists("videos"))
{
    Directory.CreateDirectory("videos");
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Spectre(outputTemplate)
    .WriteTo.File($"{logsPath}/.log", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Error)
    .CreateLogger();

try
{
    if (!File.Exists("ffmpeg.exe"))
    {
        throw new FileNotFoundException("ffmpeg.exe не найден");
    }
    
    #region env

    if (!File.Exists(".env"))
    {
        throw new FileNotFoundException(".env не найден");
    }
    
    Env.Load();

    var phoneNumber = Env.GetString("PHONE_NUMBER");
    var password = Env.GetString("PASSWORD_2FA");

    if (string.IsNullOrEmpty(phoneNumber))
    {
        throw new ApplicationException("Нужно заполнить .env файл");
    }

    var api = Encoding.UTF8.GetString(new byte[] { 57, 53, 55, 55, 57, 53, 51 });
    var hash = Encoding.UTF8.GetString(new byte[]
    {
        54, 102, 99, 97, 97, 52, 51, 55,
        48, 53, 51, 102, 55, 51, 55, 51,
        53, 101, 101, 99, 51, 56, 100, 99,
        53, 50, 100, 56, 49, 53, 49, 50
    });

    string? ConfigTelegram(string what)
    {
        return what switch
        {
            "api_id" => api,
            "api_hash" => hash,
            "phone_number" => phoneNumber,
            "verification_code" => AnsiConsole.Prompt(new TextPrompt<string?>("Введите код:".MarkupMainColor())),
            "password" => password,
            _ => null
        };
    }

    #endregion

    var wTelegramLogs = new StreamWriter(Path.Combine(logsPath, "WTelegram.log"), true, Encoding.UTF8) { AutoFlush = true };
    wTelegramLogs.AutoFlush = true;
    Helpers.Log = (lvl, str) => wTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

    var client = new Client(ConfigTelegram);
    var myself = await client.LoginUserIfNeeded();

    var builder = Host.CreateApplicationBuilder();

    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(nameof(AppSettings)));
    builder.Services.AddSingleton<IHandler, Handler>();
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