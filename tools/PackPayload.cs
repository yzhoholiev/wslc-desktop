using System.Formats.Tar;
using System.IO.Compression;

if (args is not [var appDirectory, var payloadPath])
{
    Console.Error.WriteLine("Usage: dotnet run PackPayload.cs -- <published app directory> <payload file>");
    return 1;
}

var options = new ZstandardCompressionOptions { Quality = ZstandardCompressionOptions.MaxQuality };
using FileStream payload = File.Create(payloadPath);
using var compressed = new ZstandardStream(payload, options, false);
TarFile.CreateFromDirectory(appDirectory, compressed, false);
return 0;
