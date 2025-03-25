using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using Xabe.FFmpeg;

namespace TelegramCirclesDownloader.Service;

public interface IVideoHandler
{
    Task ConvertToMp4(Queue<FileInfo> filesToConvert);
    Task ConvertTo916(string input, string output, string foreground);
    Task CombineVideosWithChromaKey(string background, string foreground, string chromakeyColor, string output);
}

public class VideoHandler(IHostApplicationLifetime lifetime) : IVideoHandler
{
    public async Task ConvertToMp4(Queue<FileInfo> filesToConvert)
    {
        while (filesToConvert.TryDequeue(out var fileToConvert))
        {
            try
            {
                var outputPath = Path.ChangeExtension(fileToConvert.FullName, ".mp4");

                var mediaInfo = await FFmpeg.GetMediaInfo(fileToConvert.FullName, lifetime.ApplicationStopping);

                IStream? videoStream = mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(VideoCodec.h264);
                IStream? audioStream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(AudioCodec.aac);

                var conversion = FFmpeg.Conversions.New()
                    .AddStream(audioStream, videoStream)
                    .UseMultiThread(true)
                    .SetOutput(outputPath)
                    .SetOverwriteOutput(true);

                var scope = fileToConvert;
                conversion.OnProgress += (_, args) => { AnsiConsole.MarkupLine($"Обработка {scope.Name} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor()); };

                await conversion.Start(lifetime.ApplicationStopping);
                AnsiConsole.MarkupLine($"[{fileToConvert.Name}] успешно обработан".EscapeMarkup().MarkupMainColor());
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to convert file to mp4");
            }
        }
    }
    public async Task ConvertTo916(string input, string output, string foreground)
    {
        var mediaInfo = await FFmpeg.GetMediaInfo(input, lifetime.ApplicationStopping);
        var foregroundInfo = await FFmpeg.GetMediaInfo(foreground, lifetime.ApplicationStopping);
        var foregroundStream = foregroundInfo.VideoStreams.FirstOrDefault();

        if (foregroundStream == null)
        {
            throw new FileLoadException("У фона не обнаружен видео-поток");
        }

        var videoStream = mediaInfo.VideoStreams.FirstOrDefault()?.SetSize(foregroundStream.Width, foregroundStream.Height);
        var audioStream = mediaInfo.AudioStreams.FirstOrDefault();
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
            .AddStream(audioStream)
            .SetOutput(output)
            .AddParameter($"-vf scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2")
            .UseMultiThread(true)
            .AddParameter("-c:v libx264")
            .AddParameter("-c:a aac")
            .SetOverwriteOutput(true);

        conversion.OnProgress += (_, args) => { AnsiConsole.MarkupLine($"Обработка {fileName} [{args.Duration}/{args.TotalLength}][{args.Percent}%]".EscapeMarkup().MarkupMainColor()); };

        await conversion.Start(lifetime.ApplicationStopping);
        AnsiConsole.MarkupLine($"[{fileName}] успешно обработан".EscapeMarkup().MarkupMainColor());
    }

    public async Task CombineVideosWithChromaKey(string background, string foreground, string chromakeyColor, string output)
    {
        var fileName = Path.GetFileName(background);

        var backgroundInfo = await FFmpeg.GetMediaInfo(background, lifetime.ApplicationStopping);
        var greenScreenInfo = await FFmpeg.GetMediaInfo(foreground, lifetime.ApplicationStopping);

        var backgroundStream = backgroundInfo.VideoStreams.FirstOrDefault();
        var greenScreenStream = greenScreenInfo.VideoStreams.FirstOrDefault();

        if (backgroundStream == null || greenScreenStream == null)
        {
            throw new FileLoadException("Один из видеопотоков не найден!");
        }

        var filter = $"[1:v]colorkey=0x{chromakeyColor}:0.3:0.1[a];" +
                     $"[a]trim=duration={(int)backgroundInfo.Duration.TotalSeconds}[a_trim];" +
                     $"[0:v][a_trim]overlay=W-w:0";

        var conversion = FFmpeg.Conversions.New()
            .AddParameter($"-i \"{background}\"")
            .AddParameter($"-i \"{foreground}\"")
            .AddParameter($"-filter_complex \"{filter}\"")
            .AddParameter("-map 0:a")
            .AddParameter("-map 0:v")
            .AddParameter($"-t {(int)backgroundInfo.Duration.TotalSeconds}")
            .AddParameter("-c:v libx264")
            .AddParameter("-c:a copy")
            .UseMultiThread(true)
            .SetOutput(output)
            .SetOverwriteOutput(true);

        conversion.OnProgress += (_, args) => { AnsiConsole.MarkupLine($"Обработка {fileName} [{(int)args.Duration.TotalSeconds}/{(int)args.TotalLength.TotalDays}]".EscapeMarkup().MarkupMainColor()); };

        await conversion.Start(lifetime.ApplicationStopping);
        AnsiConsole.MarkupLine($"[{fileName}] успешно обработан".EscapeMarkup().MarkupMainColor());
    }
}