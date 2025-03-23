using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using TL;
using WTelegram;

namespace TelegramCirclesDownloader.Service;

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
        const string openFolder = "Открыть папку загрузок";
        const string download = "Загрузка";
        const string convert = "Конвертер";
        const string exit = "Выход";

        var username = $"{user.first_name} {user.last_name}";
        const string videoDirectory = "videos";

        var style = new Style(Color.MediumOrchid3);

        while (!lifetime.ApplicationStopping.IsCancellationRequested)
        {
            var selectionPrompt = new SelectionPrompt<string>()
                .Title($"Аккаунт: {username}")
                .HighlightStyle(style)
                .AddChoices(openFolder, download, convert, exit);
            var menu = AnsiConsole.Prompt(selectionPrompt);

            try
            {
                switch (menu)
                {
                    case openFolder:
                        Process.Start(new ProcessStartInfo(videoDirectory) { UseShellExecute = true });
                        break;
                    case download:
                        var chats = await client.Messages_GetAllDialogs();
                        var chatDict = chats.chats.ToDictionary(p => p.Key, p => p.Value);

                        var selecting = new SelectionPrompt<string>()
                            .Title("Выберите чат")
                            .HighlightStyle(style)
                            .PageSize(20)
                            .AddChoices(chatDict.Values.Select(chat => $"{chat.Title.EscapeMarkup()} [[{chat.ID}]]"));

                        var selectedEscaped = AnsiConsole.Prompt(selecting);
                        var selectedChat = chatDict.GetChat(selectedEscaped, out var chatId);

                        if (selectedChat == null)
                        {
                            AnsiConsole.MarkupLine($"Чат {selectedEscaped} не найден".MarkupMainColor());
                            Console.ReadKey();
                        }

                        var chatPath = Path.Combine(videoDirectory, chatId.ToString(), "source");
                        if (!Directory.Exists(chatPath))
                        {
                            Directory.CreateDirectory(chatPath);
                        }
                        
                        InputPeer peer = chats.chats[chatId];
                        for (var offsetId = 0;;)
                        {
                            Messages_MessagesBase? messages = null;
                            try
                            {
                                messages = await client.Messages_GetHistory(peer, offsetId);
                                if (messages.Messages.Length == 0) break;
                                foreach (var baseMessage in messages.Messages)
                                {
                                    try
                                    {
                                        if (baseMessage is not Message { media: MessageMediaDocument { document: Document document } }) continue;
                                        if (document.mime_type != "video/mp4") continue;
                                        var filename = $"{document.id}.{document.mime_type[(document.mime_type.IndexOf('/') + 1)..]}";
                                        var filePath = Path.Combine(chatPath, filename);
                                        await using var fileStream = File.Create(filePath);
                                        await client.DownloadFileAsync(document, fileStream);
                                        
                                        AnsiConsole.Write(new TextPath(filePath)
                                            .RootColor(Color.Yellow)
                                            .SeparatorColor(Color.SeaGreen1)
                                            .StemColor(Color.Yellow)
                                            .LeafColor(Color.Green));
                                        
                                        AnsiConsole.Write("       ");
                                        
                                        var size = document.size.GetSizeInMegabytes();
                                        AnsiConsole.Markup(size.MarkupAquaColor());
                                        
                                        
                                        AnsiConsole.WriteLine();
                                    }
                                    catch (Exception e)
                                    {
                                        Log.ForContext<ConsoleMenu>().Error(e, "При чтении сообщения или загрузке возникла ошибка");
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log.ForContext<ConsoleMenu>().Error(e, "При чтении чата возникла ошибка");
                            }
                            if (messages != null)
                            {
                                offsetId = messages.Messages[^1].ID;
                                AnsiConsole.MarkupLine("Небольшая задержка для стабильной работы...".MarkupMainColor());
                                await Task.Delay(3000);
                            }
                        }

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