using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;

public const int MaxAssetPageSize = 24;

public const string AssetIdPrefix = "nb:cid:UUID:";

public const string Mp3FileExtension = ".mp3";
public const string WabFileExtension = ".wav";
public const string WmaFileExtension = ".wma";
public const string M4aFileExtension = ".m4a";
public const string PcmFileExtension = ".pcm";
public const string F4aFileExtension = ".f4a";
public const string MkaFileExtension = ".mka";
public const string OggFileExtension = ".ogg";
public const string IsmFileExtension = ".ism";
public const string IsmcFileExtension = ".ismc";
public const string IsmvFileExtension = ".ismv";
public const string IsmaFileExtension = ".isma";
public const string M3u8FileExtension = ".m3u8";
public const string Mp4FileExtension = ".mp4";
public const string JpgFileExtension = ".jpg";
public const string PngFileExtension = ".png";
public const string BmpFileExtension = ".bmp";
public const string KayakFileExtension = ".kayak";
public const string GraphFileExtension = ".graph";
public const string XenioFileExtension = ".xenio";
public const string WorkflowFileExtension = ".workflow";
public const string ZeniumFileExtension = ".zenium";
public const string BlueprintFileExtension = ".blueprint";

public class Asset
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string Type { get; set; }

    public string BaseStreamingUrl { get; set; }

    public IEnumerable<File> Files { get; set; }
}

public class File
{
    public string Name { get; set; }

    public long Size { get; set; }

    public string DownloadUrl { get; set; }
}

public static Asset ToApiAsset(IAsset mediaAsset, IEnumerable<IAssetFile> mediaAssetFiles, IEnumerable<ILocator> mediaLocators, IEnumerable<IStreamingEndpoint> streamingEndpoints)
{
    var apiAsset = new Asset { Id = mediaAsset.Id, Name = mediaAsset.Name, Type = GetAssetType(mediaAssetFiles), Files = new File[0] };
    var expirationThreshold = DateTime.UtcNow.AddMinutes(5);
    var streamingEndpoint = streamingEndpoints.Where(e => e.StreamingEndpointVersion == "2.0" || e.ScaleUnits > 0).FirstOrDefault(e => e.State == StreamingEndpointState.Running);
    var streamingLocator = default(ILocator);
    mediaLocators = mediaLocators.Where(l => l.ExpirationDateTime > expirationThreshold).OrderByDescending(a => a.ExpirationDateTime).ToArray();
    if (streamingEndpoint != null)
    {
        streamingLocator = mediaLocators.FirstOrDefault(l => l.Type == LocatorType.OnDemandOrigin);
    }

    var sasLocator = default(ILocator);
    if (streamingLocator == null)
    {
        sasLocator = mediaLocators.FirstOrDefault(l => l.Type == Locator​Type.Sas);
    }

    var manifestAssetFile = mediaAssetFiles.FirstOrDefault(af => af.Name.EndsWith(IsmFileExtension, StringComparison.OrdinalIgnoreCase));
    if (manifestAssetFile != null && streamingEndpoint != null && streamingLocator != null)
    {
        apiAsset.BaseStreamingUrl = $"//{streamingEndpoint.HostName}/{streamingLocator.ContentAccessComponent}/{manifestAssetFile.Name}/manifest";
    }

    apiAsset.Files = mediaAssetFiles.Select(af => ToApiFile(af, streamingLocator, sasLocator, streamingEndpoint)).ToArray();

    return apiAsset;
}

public static File ToApiFile(IAssetFile mediaAssetFile, ILocator streamingLocator, ILocator sasLocator, IStreamingEndpoint streamingEndpoint)
{
    var apiFile = new File { Name = mediaAssetFile.Name, Size = mediaAssetFile.ContentFileSize };

    if (streamingLocator != null && streamingLocator != null)
    {
        apiFile.DownloadUrl = $"//{streamingEndpoint.HostName}/{streamingLocator.ContentAccessComponent}/{mediaAssetFile.Name}";
    }
    else if (sasLocator != null)
    {
        apiFile.DownloadUrl = $"{sasLocator.BaseUri}/{mediaAssetFile.Name}{sasLocator.ContentAccessComponent}";
    }

    return apiFile;
}

public static string GetAssetType(IEnumerable<IAssetFile> mediaAssetFiles)
{
    var assetType = "Unknown";
    var mp4FileCount = mediaAssetFiles.Count(af => af.Name.EndsWith(Mp4FileExtension, StringComparison.OrdinalIgnoreCase));
    if (mediaAssetFiles.Any(af => af.Name.EndsWith(IsmFileExtension, StringComparison.OrdinalIgnoreCase)))
    {
        var hasIsmvOrIsma = mediaAssetFiles.Any(af => af.Name.EndsWith(IsmvFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(IsmaFileExtension, StringComparison.OrdinalIgnoreCase));
        if (mediaAssetFiles.Any(af => af.Name.EndsWith(IsmcFileExtension, StringComparison.OrdinalIgnoreCase)) && hasIsmvOrIsma)
        {
            assetType = "Smooth Streaming";
        }
        else if (mp4FileCount > 0)
        {
            assetType = (mp4FileCount == 1) ? "Single MP4" : "Multi-Bitrate MP4";
        }
        else if (mediaAssetFiles.Any(af => af.Name.EndsWith(M3u8FileExtension, StringComparison.OrdinalIgnoreCase)))
        {
            assetType = "HLS Streaming";
        }
        else if (!hasIsmvOrIsma)
        {
            assetType = "Live Archive";
        }
    }
    else if (mediaAssetFiles.Any(af => af.Name.EndsWith(JpgFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(PngFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(BmpFileExtension, StringComparison.OrdinalIgnoreCase)))
    {
        assetType = "Thumbnail";
    }
    else if (mp4FileCount == 1)
    {
        assetType = "Single MP4";
    }
    else if (mediaAssetFiles.Any(af => af.Name.EndsWith(Mp3FileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(WabFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(WmaFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(M4aFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(PcmFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(F4aFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(MkaFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(OggFileExtension, StringComparison.OrdinalIgnoreCase)))
    {
        assetType = "Audio";
    }
    else if ((mediaAssetFiles.Count() == 1) && (mediaAssetFiles.Any(af => af.Name.EndsWith(KayakFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(GraphFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(XenioFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(WorkflowFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(ZeniumFileExtension, StringComparison.OrdinalIgnoreCase) || af.Name.EndsWith(BlueprintFileExtension, StringComparison.OrdinalIgnoreCase))))
    {
        assetType = "Encoder Configuration";
    }

    return assetType;
}
