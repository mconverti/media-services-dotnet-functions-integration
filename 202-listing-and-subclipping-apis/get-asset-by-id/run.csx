#load "../shared/asset.csx"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;

public static HttpResponseMessage Run(HttpRequestMessage req, string assetId, TraceWriter log)
{
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
    var streamingEndpoints = context.StreamingEndpoints.ToArray();

    var apiAsset = ToApiAsset(mediaAsset, mediaAssetFiles, mediaLocators, streamingEndpoints);

    log.Info($"Returning '{assetId}' asset from '{mediaServicesAccountName}' account.");

    return req.CreateResponse(HttpStatusCode.OK, apiAsset);
}
