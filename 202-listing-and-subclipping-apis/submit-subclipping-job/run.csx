#load "../shared/subclip.csx"

#r "Newtonsoft.Json"

using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;
using Newtonsoft.Json;

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

    // TODO

    return req.CreateResponse(HttpStatusCode.Created);
}
