using System.Text.RegularExpressions;
using Serilog;
using Spectre.Console;
using TL;
using Xabe.FFmpeg;

namespace TelegramCirclesDownloader.Service;

public static partial class Extensions
{
    public static async Task ConvertToMp4(Queue<FileInfo> filesToConvert, CancellationToken cancellationToken)
    {
        while(filesToConvert.TryDequeue(out var fileToConvert))
        {
            try
            {
                var outputPath = Path.ChangeExtension(fileToConvert.FullName, ".mp4");
            
                var mediaInfo = await FFmpeg.GetMediaInfo(fileToConvert.FullName, cancellationToken);
            
                IStream? videoStream = mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.h264_nvenc);
                IStream? audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.aac);

                var conversion = FFmpeg.Conversions.New()
                    .AddStream(audioStream, videoStream)
                    .UseMultiThread(true)
                    .SetOutput(outputPath);
            
                var scope = fileToConvert;
                conversion.OnProgress += (_, args) =>
                {
                    AnsiConsole.MarkupLine($"Обработка {scope.Name} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor());
                };
                
                await conversion.Start(cancellationToken);
                AnsiConsole.MarkupLine($"[{fileToConvert.Name}] успешно обработан".EscapeMarkup().MarkupMainColor());
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to convert file to mp4");
            }
        }
    }
    
    public static async Task ConvertTo916(this string input, string output, CancellationToken cancellationToken)
    {
        var mediaInfo = await FFmpeg.GetMediaInfo(input, cancellationToken);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        var fileName = Path.GetFileName(input);

        if (videoStream == null)
        {
            throw new FileLoadException("Видео поток не найден!");
        }
        
        var width = videoStream.Width;
        var height = (int)(width * 16.0 / 9.0);
        
        if (height < videoStream.Height)
        {
            height = videoStream.Height;
            width = (int)(height * 9.0 / 16.0);
        }
        
        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(output)
            .AddParameter($"-vf scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2")
            .UseMultiThread(true)
            .AddParameter("-c:v h264_nvenc")
            .SetOverwriteOutput(true);
        
        conversion.OnProgress += (_, args) =>
        {
            AnsiConsole.MarkupLine($"Обработка {fileName} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor());
        };

        await conversion.Start(cancellationToken);
        AnsiConsole.MarkupLine($"[{fileName}] успешно обработан".EscapeMarkup().MarkupMainColor());
    }

    public static async Task CombineVideosWithChromaKey(this string background, string foreground, string output, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(background);
        
        var backgroundInfo = await FFmpeg.GetMediaInfo(background, cancellationToken);
        var greenScreenInfo = await FFmpeg.GetMediaInfo(foreground, cancellationToken);
        
        var backgroundStream = backgroundInfo.VideoStreams.FirstOrDefault();
        var greenScreenStream = greenScreenInfo.VideoStreams.FirstOrDefault();

        if (backgroundStream == null || greenScreenStream == null)
        {
            throw new FileLoadException("Один из видеопотоков не найден!");
        }
        
        const string filter = "[1:v]chromakey=0x00FF00:0.1:0.2[fg]; " +
                              "[fg][0:v]scale2ref=w=oh*mdar:h=ih[fg_scaled][bg]; " +
                              "[bg][fg_scaled]overlay=(main_w-overlay_w)/2:(main_h-overlay_h)/2[out]";
        
        
        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i {background}")
            .AddParameter($"-i {foreground}")
            .AddParameter($"-filter_complex \"{filter}\"")
            .AddParameter("-map \"[out]\" -map 1:a")
            .UseMultiThread(true)
            .AddParameter("-c:v h264_nvenc")
            .SetOutput(output)
            .SetOverwriteOutput(true);

        conversion.OnProgress += (_, args) =>
        {
            AnsiConsole.MarkupLine($"Обработка {fileName} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor());
        };

        await conversion.Start(cancellationToken);
        AnsiConsole.MarkupLine($"[{fileName}] успешно обработан".EscapeMarkup().MarkupMainColor());
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