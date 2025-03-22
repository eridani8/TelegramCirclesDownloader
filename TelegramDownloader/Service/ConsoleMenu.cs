using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;

namespace TelegramDownloader.Service;

public class ConsoleMenu(IHostApplicationLifetime lifetime) : IHostedService
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
        const string download = "Загрузка";
        const string convert = "Конвертер";
        const string exit = "Выход";

        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            var selectionPrompt = new SelectionPrompt<string>()
                    .Title("Главное меню")
                    .HighlightStyle(new Style(Color.MediumOrchid3))
                    .AddChoices(download, convert, exit);
            var menu = AnsiConsole.Prompt(selectionPrompt);

            try
            {
                switch (menu)
                {
                    case exit:
                        lifetime.StopApplication();
                        break;
                }
            }
            catch (Exception e)
            {
                Log.ForContext<ConsoleMenu>().Error(e, "При выборе элемента меню возникла ошибка");
                AnsiConsole.WriteException(e);
            }
        }
    }
}