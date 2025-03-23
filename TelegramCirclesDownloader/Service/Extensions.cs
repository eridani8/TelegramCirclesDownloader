using System.Text.RegularExpressions;
using TL;

namespace TelegramCirclesDownloader.Service;

public static partial class Extensions
{
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