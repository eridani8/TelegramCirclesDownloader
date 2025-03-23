using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using TL;
using WTelegram;

namespace TelegramDownloader.Service;

public class ConsoleMenu(IHostApplicationLifetime lifetime, User user, Client client) : IHostedService
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

        var username = string.IsNullOrEmpty(user.MainUsername) ? user.ID.ToString() : user.MainUsername;

        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            var selectionPrompt = new SelectionPrompt<string>()
                .Title($"Аккаунт: {username}")
                .HighlightStyle(new Style(Color.MediumOrchid3))
                .AddChoices(download, convert, exit);
            var menu = AnsiConsole.Prompt(selectionPrompt);

            try
            {
                switch (menu)
                {
                    case download:
                        var chats = await client.Messages_GetAllDialogs();
                        var chatDict = chats.chats.ToDictionary(p => p.Key, p => p.Value);

                        var selecting = new SelectionPrompt<string>()
                            .Title("Выберите чат")
                            .HighlightStyle(new Style(Color.MediumOrchid3))
                            .PageSize(20)
                            .AddChoices(chatDict.Values.Select(chat => $"{chat.Title.EscapeMarkup()} [[{chat.ID}]]"));

                        var selectedEscaped = AnsiConsole.Prompt(selecting);
                        var selectedChat = chatDict.GetChat(selectedEscaped, out var chatId);

                        if (selectedChat == null)
                        {
                            AnsiConsole.MarkupLine($"Чат {selectedEscaped} не найден".MarkupMainColor());
                            Console.ReadKey();
                        }

                        InputPeer peer = chats.chats[chatId];
                        for (var offsetId = 0;;)
                        {
                            var messages = await client.Messages_GetHistory(peer, offsetId);
                            if (messages.Messages.Length == 0) break;
                            foreach (var baseMessage in messages.Messages)
                            {
                                if (baseMessage is not Message { media: MessageMediaDocument { document: Document document } }) continue;
                                if (document.mime_type != "video/mp4") continue;
                                var filename = $"{document.id}.{document.mime_type[(document.mime_type.IndexOf('/') + 1)..]}";
                            }
                            offsetId = messages.Messages[^1].ID;
                        }

                        // await AnsiConsole.Progress()
                        //     .Columns([
                        //         new ElapsedTimeColumn(),
                        //         new TaskDescriptionColumn(),
                        //         new ProgressBarColumn(),
                        //         new RemainingTimeColumn(),
                        //         new SpinnerColumn(),
                        //     ])
                        //     .StartAsync(async ctx =>
                        //     {
                        //         var task = ctx.AddTask("Поиск и загрузка кружков".MarkupMainColor());
                        //         
                        //         
                        //     });

                        break;
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