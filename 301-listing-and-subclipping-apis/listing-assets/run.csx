#load "../shared/asset.csx"

#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"

using System;
using System.Linq;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

// Read values from configuration.
private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

public static HttpResponseMessage Run(HttpRequestMessage req, TraceWriter log)
{
    var skip = 0;
    var take = 10;
    log.Info($"Listing assets for '{_mediaServicesAccountName}' account. Skip: '{skip}'. Take: '{take}'.");
    
    var _context = new CloudMediaContext(new MediaServicesCredentials(_mediaServicesAccountName, _mediaServicesAccountKey));
    var results = _context.Assets.OrderByDescending(a => a.Created).Skip(skip).Take(take);
    var assets = results.Select(a => new Asset { Id = a.Id, Name = a.Name });

    return req.CreateResponse(HttpStatusCode.OK, assets);
}
