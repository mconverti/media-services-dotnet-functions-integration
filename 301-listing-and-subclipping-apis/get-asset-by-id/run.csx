#load "../shared/asset.csx"

#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;

public static HttpResponseMessage Run(HttpRequestMessage req, string assetId, TraceWriter log)
{
    var expirationThreshold = DateTime.Now.AddMinutes(5);
    var mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
    var mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

    log.Info($"Getting asset '{assetId}' from '{mediaServicesAccountName}' account.");

    var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
    var mediaAsset = context.Assets.Where(a => a.Id == assetId).FirstOrDefault();

    if (mediaAsset == null)
    {
        log.Info($"Asset '{assetId}' not found in '{mediaServicesAccountName}' account.");
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    var mediaAssetFiles = mediaAsset.AssetFiles.ToArray();
    var mediaLocators = mediaAsset.Locators.ToArray();
    var streamingEndpoint = context.StreamingEndpoints.Where(e => e.StreamingEndpointVersion == "2.0" || e.ScaleUnits > 0).ToArray().FirstOrDefault(e => e.State == StreamingEndpointState.Running);

    var apiAsset = ToApiAsset(mediaAsset, mediaAssetFiles, mediaLocators, streamingEndpoint);

    log.Info($"Returning '{assetId}' asset.");

    return req.CreateResponse(HttpStatusCode.OK, apiAsset);
}
