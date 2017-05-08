#r "Newtonsoft.Json"
#load "../shared/subclip.csx"
#load "../shared/mediaHelpers.csx"

using System.IO;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public const string SubclippingJobsQueueName = "subclippingjobsqueue";
public const string SubclippingAzureFunctionNotificationEndpointName = "SubclippingAzureFunctionNotificationEndpoint";

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    var subclip = await req.Content.ReadAsAsync<Subclip>();
    if (string.IsNullOrWhiteSpace(subclip.SourceAssetId))
    {
        log.Info("Invalid subclip payload: 'SourceAssetId' not found.");
        return req.CreateResponse(HttpStatusCode.BadRequest, "The 'SourceAssetId' field is required.");
    }

    if (string.IsNullOrWhiteSpace(subclip.Name))
    {
        log.Info("Invalid subclip payload: 'Name' not found.");
        return req.CreateResponse(HttpStatusCode.BadRequest, "The 'Name' field is required.");
    }

    if (subclip.Start >= subclip.End)
    {
        log.Info("Invalid subclip payload: 'End' is not greater than 'Start'.");
        return req.CreateResponse(HttpStatusCode.BadRequest, "The 'End' field must be greater than the 'Start' field.");
    }

    if (string.IsNullOrWhiteSpace(subclip.EncodingProfile))
    {
        subclip.EncodingProfile = "H264 Multiple Bitrate 720p";
    }

    log.Info($"Started processing subclip: '{JsonConvert.SerializeObject(subclip)}'");

    var mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
    var mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");
    var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
    var mediaAsset = context.Assets.Where(a => a.Id == subclip.SourceAssetId).FirstOrDefault();

    if (mediaAsset == null)
    {
        log.Info($"Source asset '{subclip.SourceAssetId}' not found in '{mediaServicesAccountName}' account.");
        return req.CreateResponse(HttpStatusCode.BadRequest, $"Source asset '{subclip.SourceAssetId}' not found");
    }

    var notificationEndpoint = context.NotificationEndPoints.Where(e => e.Name == SubclippingAzureFunctionNotificationEndpointName).FirstOrDefault();
    if (notificationEndpoint == null)
    {            
        log.Info($"Creating notification endpoint '{SubclippingAzureFunctionNotificationEndpointName}' in '{mediaServicesAccountName}' account for '{SubclippingJobsQueueName}' queue.");
        notificationEndpoint = await context.NotificationEndPoints.CreateAsync(SubclippingAzureFunctionNotificationEndpointName, NotificationEndPointType.AzureQueue, SubclippingJobsQueueName).ConfigureAwait(false);
    }

    var mediaProcessor = GetLatestMediaProcessorByName(context, "Media Encoder Standard");
    var subclippingPreset = BuildSubclippingPreset(subclip.Start, subclip.End - subclip.Start, subclip.EncodingProfile);
    var subclippingJob = context.Jobs.Create($"Subclipping job for '{mediaAsset.Id}'");
    var subclippingTask = subclippingJob.Tasks.AddNew($"Subclipping task for '{mediaAsset.Id}'", mediaProcessor, subclippingPreset, TaskOptions.None);
        
    subclippingTask.InputAssets.Add(mediaAsset);
    subclippingTask.OutputAssets.AddNew(subclip.Name, AssetCreationOptions.None);
    subclippingJob.JobNotificationSubscriptions.AddNew(NotificationJobState.FinalStatesOnly, notificationEndpoint);
    
    await subclippingJob.SubmitAsync().ConfigureAwait(false);
    log.Info($"Subclipping job submitted '{subclippingJob.Id}' in '{mediaServicesAccountName}' account.");

    return req.CreateResponse(HttpStatusCode.Created, new { JobId = subclippingJob.Id });
}

public static string BuildSubclippingPreset(double start, double duration, string encodingProfile)
{
    var presetPath = Path.Combine(@"shared/presets", $"{encodingProfile}.json");
    dynamic preset = JObject.Parse(File.ReadAllText(presetPath));
    
    // TODO
    var subclippingInfo = new JObject();
    subclippingInfo.Add("StartTime", new JValue(FormatTime(start)));
    subclippingInfo.Add("Duration", new JValue(FormatTime(duration)));
    preset.Add("Sources", new JArray(subclippingInfo));
    
    return JsonConvert.SerializeObject(preset);;
}

public static string FormatTime(double timeInSeconds) {
    var d = Math.Floor(timeInSeconds / 86400);
    timeInSeconds -= (d * 86400);
    var h = Math.Floor(timeInSeconds / 3600) % 24;
    timeInSeconds -= (h * 3600);
    var m = Math.Floor(timeInSeconds / 60) % 60;
    timeInSeconds -= (m * 60);
    var s = Math.Floor(timeInSeconds);
    timeInSeconds -= s;
    var hs = Math.Floor(timeInSeconds * 100);

    var returnVal = string.Empty;
    returnVal = returnVal + (d > 0 ? $"{d}." : string.Empty);
    returnVal = returnVal + (h < 10 ? $"0{h}:" : $"{h}:");
    returnVal = returnVal + (m < 10 ? $"0{m}:" : $"{m}:");
    returnVal = returnVal + (s < 10 ? $"0{s}" : $"{s}");
    if (hs > 0) {
        returnVal = returnVal + $".{hs}";
    }

    return returnVal;
}