using System.Collections.Generic;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;

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

public static Asset ToApiAsset(IAsset mediaAsset, IEnumerable<IAssetFile> mediaAssetFiles, IEnumerable<ILocator> mediaLocators, IStreamingEndpoint streamingEndpoint)
{
    var apiAsset = new Asset { Id = mediaAsset.Id, Name = mediaAsset.Name };
    ILocator streamingLocator = null;
    ILocator sasLocator = null;
    IAssetFile manifestAssetFile = mediaAssetFiles.FirstOrDefault(af => af.Name.EndsWith(IsmFileExtension, StringComparison.OrdinalIgnoreCase));

    if (streamingEndpoint != null)
    {
        streamingLocator = mediaLocators.FirstOrDefault(l => l.Type == LocatorType.OnDemandOrigin);
    }

    if (streamingLocator == null)
    {
        sasLocator = mediaLocators.FirstOrDefault(l => l.Type == Locator​Type.Sas);
    }

    if (manifestAssetFile != null && streamingEndpoint != null && streamingLocator != null)
    {
        apiAsset.BaseStreamingUrl = $"//{streamingEndpoint.HostName}/{streamingLocator.ContentAccessComponent}/{manifestAssetFile.Name}/manifest";
    }

    return apiAsset;
}