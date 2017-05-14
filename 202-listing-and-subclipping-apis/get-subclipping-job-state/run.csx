using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;

public const string JobIdPrefix = "nb:jid:UUID:";

public static HttpResponseMessage Run(HttpRequestMessage req, string jobId, TraceWriter log)
{
    var mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
    var mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

    if (!jobId.StartsWith(JobIdPrefix, StringComparison.OrdinalIgnoreCase))
    {
        jobId = $"{JobIdPrefix}{jobId}";
    }

    log.Info($"Getting job '{jobId}' from '{mediaServicesAccountName}' account.");

    var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
    var mediaJob = context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();

    if (mediaJob == null)
    {
        log.Info($"Job '{jobId}' not found in '{mediaServicesAccountName}' account.");
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    log.Info($"Returning '{jobId}' job from '{mediaServicesAccountName}' account.");

    return req.CreateResponse(HttpStatusCode.OK, new { Id = mediaJob.Id, Name = mediaJob.Name, State = mediaJob.State.ToString() });
}
