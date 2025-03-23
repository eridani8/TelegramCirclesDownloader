using Microsoft.Extensions.Options;
using Serilog;
using Spectre.Console;
using Sprache;
using TL;
using WTelegram;

namespace TelegramCirclesDownloader.Service;

public class Handler(Client client, IOptions<AppSettings> settings)
{
    public const string VideoDirectory = "videos";
    public readonly Style Style = new(Color.MediumOrchid3);
    private const string Tab = "       ";

    public async Task DownloadCircles()
    {
        var chats = await client.Messages_GetAllDialogs();
        var chatDict = chats.chats
            .ToDictionary(p => p.Key, p => p.Value);

        var selecting = new SelectionPrompt<string>()
            .Title("Выберите чат")
            .HighlightStyle(Style)
            .PageSize(20)
            .AddChoices(chatDict.Values.Select(chat => $"{chat.Title.EscapeMarkup()} [[{chat.ID}]]"));

        var selectedEscaped = AnsiConsole.Prompt(selecting);
        var selectedChat = chatDict.GetChat(selectedEscaped, out var chatId);

        if (selectedChat == null)
        {
            AnsiConsole.MarkupLine($"Чат {selectedEscaped} не найден".MarkupMainColor());
            Console.ReadKey();
        }

        var chatPath = Path.Combine(VideoDirectory, chatId.ToString(), "source");
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

                        AnsiConsole.Write(Tab);

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
                await Task.Delay(TimeSpan.FromSeconds(settings.Value.GetPagesDelay).Milliseconds);
            }
        }
    }
}