using System.Collections;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.EventStream;
using Serilog;
using Spectre.Console;
using TL;
using Xabe.FFmpeg;

namespace TelegramCirclesDownloader.Service;

public static partial class Extensions
{
    public static async Task ConvertToMp4(Queue<FileInfo> filesToConvert)
    {
        while(filesToConvert.TryDequeue(out var fileToConvert))
        {
            try
            {
                var outputPath = Path.ChangeExtension(fileToConvert.FullName, ".mp4");
            
                var mediaInfo = await FFmpeg.GetMediaInfo(fileToConvert.FullName);
            
                IStream? videoStream = mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.h264);
                IStream? audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.aac);

                var conversion = FFmpeg.Conversions.New()
                    .AddStream(audioStream, videoStream)
                    .SetOutput(outputPath);
            
                //var conversion = await FFmpeg.Conversions.FromSnippet.Convert(fileToConvert.FullName, outputFileName);

                var scope = fileToConvert;
                conversion.OnProgress += (_, args) =>
                {
                    AnsiConsole.MarkupLine($"Обработка {scope.Name} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor());
                };
                
                await conversion.Start();
                AnsiConsole.MarkupLine($"[{fileToConvert.Name}] успешно обработан".EscapeMarkup().MarkupMainColor());
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to convert file to mp4");
            }
        }
    }

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