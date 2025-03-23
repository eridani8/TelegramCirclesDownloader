using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.EventStream;
using Serilog;
using TL;

namespace TelegramCirclesDownloader.Service;

public static partial class Extensions
{
    public static async Task CallFfmpeg(this FileInfo fileInfo, string command, string outputPath)
    {
        var cmd = Cli.Wrap("ffmpeg")
            .WithWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory)
            .WithArguments($"""-i "{fileInfo.FullName}" {command} "{outputPath}" """);

        await foreach (var cmdEvent in cmd.ListenAsync())
        {
            switch (cmdEvent)
            {
                case StandardOutputCommandEvent stdOut:
                    Log.ForContext<Handler>().Information(stdOut.Text);
                    break;
                case StandardErrorCommandEvent stdErr:
                    Log.ForContext<Handler>().Information(stdErr.Text);
                    break;
            }
        }
    }
    
    public static string GetSizeInMegabytes(this long bytes)
    {
        var sizeInMb = bytes / (1024.0 * 1024.0);
        return $"{sizeInMb:0.##} MB";
    }
    
    public static string MarkupAquaColor(this string str)
    {
        return $"[aquamarine1]{str}[/]";
    }
    
    public static string MarkupMainColor(this string str)
    {
        return $"[mediumorchid3]{str}[/]";
    }

    public static ChatBase? GetChat(this Dictionary<long, ChatBase> dict, string str, out long chatId)
    {
        chatId = -1;
        var match = ChatRegex().Match(str);
        if (match.Success && long.TryParse(match.Groups[1].Value, out chatId) && dict.TryGetValue(chatId, out var selectedChat))
        {
            return selectedChat;
        }
        return null;
    }
    
    [GeneratedRegex(@"\[\[(\d+)\]\]$")]
    private static partial Regex ChatRegex();
}