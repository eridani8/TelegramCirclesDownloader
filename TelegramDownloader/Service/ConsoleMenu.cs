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

    private Task Worker()
    {
        const string auth = "Авторизация";
        const string download = "Загрузка";
        const string convert = "Конвертер";
        const string exit = "Выход";
        
        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            var menu = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .AddChoices(
                        auth,
                        download,
                        convert,
                        exit
                    ));
            
            try
            {
                switch (menu)
                {
                    case exit:
                        lifetime.StopApplication();
                        break;
                }
            }
            catch(Exception e)
            {
                Log.ForContext<ConsoleMenu>().Error(e, "При выборе элемента меню возникла ошибка");
                AnsiConsole.WriteException(e);
            }
        }
        
        return Task.CompletedTask;
    }
}