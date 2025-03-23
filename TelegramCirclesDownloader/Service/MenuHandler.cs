using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Spectre.Console;
using TL;
using WTelegram;

namespace TelegramCirclesDownloader.Service;

public interface IMenuHandler
{
    string VideoDirectory { get; }
    string Tab { get; }
    Style Style { get; }
    Task DownloadCircles();
    Task Converter();
    Task ConvertChromaVideos();
    Task ConvertCircles();
}

public class MenuHandler(Client client, IOptions<AppSettings> settings, IHostApplicationLifetime lifetime) : IMenuHandler
{
    public string VideoDirectory => "videos";
    private const string ChromaVideoDirectory = "chroma_videos";
    public Style Style { get; } = new(Color.MediumOrchid3);
    public string Tab => "       ";

    public async Task DownloadCircles()
    {
        var chats = await client.Messages_GetAllDialogs();
        var chatDict = chats.chats
            .ToDictionary(p => p.Key, p => p.Value);

        var choices = new SelectionPrompt<string>()
            .Title("Выберите чат")
            .HighlightStyle(Style)
            .PageSize(20)
            .AddChoices(chatDict.Values.Select(chat => $"{chat.Title.EscapeMarkup()} [[{chat.ID}]]"));

        var prompt = AnsiConsole.Prompt(choices);
        var selectedChat = chatDict.GetChat(prompt, out var chatId);

        if (selectedChat == null)
        {
            AnsiConsole.MarkupLine($"Чат {prompt} не найден".MarkupMainColor());
            Console.ReadKey();
            return;
        }

        var path = Path.Combine(VideoDirectory, chatId.ToString(), "source");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        InputPeer peer = chats.chats[chatId];
        for (var offsetId = 0;;)
        {
            if (lifetime.ApplicationStopping.IsCancellationRequested) break;
            Messages_MessagesBase? messages = null;
            try
            {
                messages = await client.Messages_GetHistory(peer, offsetId);
                if (messages.Messages.Length == 0) break;
                foreach (var baseMessage in messages.Messages)
                {
                    try
                    {
                        if (lifetime.ApplicationStopping.IsCancellationRequested) break;
                        if (baseMessage is not Message { media: MessageMediaDocument { document: Document document } }) continue;
                        if (!document.attributes.Any(a => a is DocumentAttributeVideo attributeVideo && (attributeVideo.flags & DocumentAttributeVideo.Flags.round_message) != 0))
                        {
                            continue;
                        }
                        var filename = $"{document.id}.{document.mime_type[(document.mime_type.IndexOf('/') + 1)..]}";
                        var filePath = Path.Combine(path, filename);
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
    
    public async Task Converter()
    {
        const string circleVideos = "Кружки";
        const string chromaVideos = $"/{ChromaVideoDirectory}/ mov -> mp4";
        
        var choices = new SelectionPrompt<string>()
            .Title("Что будем конвертировать?")
            .HighlightStyle(Style)
            .AddChoices(circleVideos, chromaVideos);
        var prompt = AnsiConsole.Prompt(choices);

        switch (prompt)
        {
            case circleVideos:
                await ConvertCircles();
                break;
            case chromaVideos:
                await ConvertChromaVideos();
                break;
        }
    }

    public async Task ConvertChromaVideos()
    {
        var files = ChromaVideoDirectory
            .ToDirectoryInfo()
            .EnumerateFiles("*.mov");

        await Extensions.ConvertToMp4(new Queue<FileInfo>(files));
    }
    
    public async Task ConvertCircles()
    {
        var chats = await client.Messages_GetAllDialogs();
        var chatDict = chats.chats
            .ToDictionary(p => p.Key, p => p.Value);
        
        var dirs = VideoDirectory
            .ToDirectoryInfo()
            .GetDirectories();

        var selecting = new SelectionPrompt<string>()
            .Title("Выберите чат")
            .HighlightStyle(Style)
            .PageSize(20)
            .AddChoices(dirs.Select(dir => $"{chatDict.GetChat($"[[{dir.Name}]]", out var dirChatId)} [[{dirChatId}]]"));
        
        var selectedEscaped = AnsiConsole.Prompt(selecting);
        var selectedChat = chatDict.GetChat(selectedEscaped, out var chatId);

        if (selectedChat == null)
        {
            AnsiConsole.MarkupLine($"Чат {selectedEscaped} не найден".MarkupMainColor());
            Console.ReadKey();
            return;
        }

        var rootDir = Path.Combine(VideoDirectory, chatId.ToString());
        var sourceDir = Path.Combine(rootDir, "source");
        var convertedDir = Path.Combine(rootDir, "converted");
        var combinedDir = Path.Combine(rootDir, "combined");

        if (!Directory.Exists(convertedDir))
        {
            Directory.CreateDirectory(convertedDir);
        }
        
        var files = sourceDir
            .ToDirectoryInfo()
            .EnumerateFiles("*.mp4");
        
        foreach (var fileInfo in files)
        {
            try
            {
                var converted = $"{convertedDir}\\{fileInfo.Name}";
                await fileInfo.FullName.ConvertTo916(converted);
                await Path.GetFullPath(converted).CombineVideosWithChromaKey(@"F:\dev\kwork\TelegramCirclesDownloader\TelegramCirclesDownloader\bin\Debug\net9.0\chroma_videos\foreground.mp4", $"{combinedDir}\\{fileInfo.Name}");
            }
            catch (Exception e)
            {
                Log.ForContext<MenuHandler>().Error(e, "Ошибка обработки видео: {File}", fileInfo.FullName);
            }
        }
    }
}