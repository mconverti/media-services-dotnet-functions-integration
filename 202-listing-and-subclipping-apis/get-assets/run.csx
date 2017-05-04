#load "../shared/asset.csx"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Microsoft.WindowsAzure.MediaServices.Client;

public static HttpResponseMessage Run(HttpRequestMessage req, TraceWriter log)
{
    var valuePairs = req.GetQueryNameValuePairs();
    var skip = GetQueryStringIntValue(valuePairs, "skip", 0);
    var take = GetQueryStringIntValue(valuePairs, "take", 10);
    var mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
    var mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");
    var expirationThreshold = DateTime.Now.AddMinutes(5);

    log.Info($"Getting assets from '{mediaServicesAccountName}' account with paging parameters. skip: '{skip}' - take: '{take}'");

    var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
    var mediaAssets = context.Assets.OrderByDescending(a => a.Created).Skip(skip).Take(take).ToArray();
    var mediaAssetIds = mediaAssets.Select(a => a.Id).ToArray();
    var mediaAssetFilesGroups = context.Files.Where(CreateOrExpression<IAssetFile, string>("ParentAssetId", mediaAssetIds)).ToArray().GroupBy(af => af.ParentAssetId);
    var mediaLocatorsGroups = context.Locators.Where(CreateOrExpression<ILocator, string>("AssetId", mediaAssetIds)).Where(l => l.ExpirationDateTime > expirationThreshold).OrderByDescending(a => a.ExpirationDateTime).ToArray().GroupBy(l => l.AssetId);
    var streamingEndpoint = context.StreamingEndpoints.Where(e => e.StreamingEndpointVersion == "2.0" || e.ScaleUnits > 0).ToArray().FirstOrDefault(e => e.State == StreamingEndpointState.Running);

    var apiAssets = mediaAssets
        .Select(
            a =>
            {
                var mediaAssetFilesGroup = mediaAssetFilesGroups.FirstOrDefault(g => g.Key == a.Id);
                var mediaLocatorsGroup = mediaLocatorsGroups.FirstOrDefault(g => g.Key == a.Id);
                return ToApiAsset(a, (mediaAssetFilesGroup != null) ? mediaAssetFilesGroup.AsEnumerable() : new IAssetFile[0], (mediaLocatorsGroup != null) ? mediaLocatorsGroup.AsEnumerable() : new ILocator[0], streamingEndpoint);
            })
        .ToArray();
    log.Info($"Returning '{apiAssets.Length}' assets.");

    return req.CreateResponse(HttpStatusCode.OK, apiAssets);
}

public static int GetQueryStringIntValue(IEnumerable<KeyValuePair<string, string>> keyValuePairs, string key, int defaultValue)
{
    var queryStringValue = defaultValue;

    var pairs = keyValuePairs.Where(vp => vp.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    if (pairs.Count() > 0)
    {
        int value;
        var pair = pairs.First();
        if (int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
        {
            queryStringValue = value;
        }
    }

    return queryStringValue;
}

private static Expression<Func<T, bool>> CreateOrExpression<T, V>(string propertyName, IEnumerable<V> values)
{
    ParameterExpression a = Expression.Parameter(typeof(T), "a");
    Expression exp = Expression.Constant(false);

    foreach (var value in values)
    {
        exp = Expression.OrElse(
            exp,
            Expression.Equal(Expression.Property(a, propertyName), Expression.Constant(value, typeof(V))));
    }

    var predicate = Expression.Lambda<Func<T, bool>>(exp, a);

    return predicate;
}