using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;

public static TimeSpan LocatorDuration = TimeSpan.FromDays(10 * 365);

public static void Run(string jobNotification, TraceWriter log)
{
    log.Info($"Job notification received: '{jobNotification}'");
    dynamic notification = JsonConvert.DeserializeObject<dynamic>(jobNotification);
    //dynamic notification = JObject.Parse(jobNotification);
    if (notification.Properties.NewState == "Finished")
    {
        var jobId = (string)notification.Properties.JobId;
        var mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
        var mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

        log.Info($"Getting job '{jobId}' from '{mediaServicesAccountName}' account.");

        var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
        var mediaJob = context.Jobs.Where(j => j.Id == jobId).FirstOrDefault();
        if (mediaJob != null)
        {
            var mediaAsset = mediaJob.OutputMediaAssets.FirstOrDefault();
            
            log.Info($"Publishing output asset '{mediaAsset.Name}' of job '{jobId}' from '{mediaServicesAccountName}' account.");
            
            var mediaPolicy = context.AccessPolicies.Create(mediaAsset.Name, LocatorDuration, AccessPermissions.Read);
            context.Locators.CreateLocator(LocatorType.OnDemandOrigin, mediaAsset, mediaPolicy);
            context.Locators.CreateLocator(LocatorType.Sas, mediaAsset, mediaPolicy);
        }
    }
}