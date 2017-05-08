using System;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;

private static IMediaProcessor GetLatestMediaProcessorByName(CloudMediaContext context, string mediaProcessorName)
{
    var processor = context.MediaProcessors.Where(p => p.Name == mediaProcessorName).ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();
    if (processor == null)
    {
        throw new ArgumentException($"Unknown media processor: '{mediaProcessorName}'");
    }

    return processor;
}