using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using TL;
using WTelegram;

namespace TelegramCirclesDownloader.Service;

public class ConsoleMenu(IHostApplicationLifetime lifetime, IHandler handler, User user) : IHostedService
{
    private Task? _task;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _task = Worker();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            lifetime.StopApplication();
            if (_task != null)
            {
                await Task.WhenAny(_task, Task.Delay(Timeout.Infinite, cancellationToken));
            }
        }
        finally
        {
            _task?.Dispose();
        }
    }

    private async Task Worker()
    {
        const string openFolder = "Открыть папку загрузок";
        const string download = "Загрузка";
        const string convert = "Конвертер";
        const string feedback = "Обратная связь";
        const string exit = "Выход";

        var username = $"{user.first_name} {user.last_name}";

        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            var choices = new SelectionPrompt<string>()
                .Title($"Аккаунт: {username}")
                .HighlightStyle(handler.Style)
                .AddChoices(openFolder, download, convert, feedback, exit);
            var prompt = AnsiConsole.Prompt(choices);

            try
            {
                switch (prompt)
                {
                    case openFolder:
                        Process.Start(new ProcessStartInfo(handler.VideoDirectory) { UseShellExecute = true });
                        break;
                    case download:
                        await handler.DownloadCircles();
                        break;
                    case convert:
                        await handler.Converter();
                        break;
                    case feedback:
                        Process.Start(new ProcessStartInfo("https://t.me/eridani_8") { UseShellExecute = true });
                        break;
                    case exit:
                        lifetime.StopApplication();
                        break;
                }
            }
            catch (Exception e)
            {
                Log.ForContext<ConsoleMenu>().Error(e, "При выборе элемента меню возникла ошибка");
            }
        }
    }
}