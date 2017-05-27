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

    if (take >= MaxAssetPageSize) {
        var errorMessage = $"Take filter parameter must be lower than '{MaxAssetPageSize}' and current value is '{take}'.";
        log.Error($"Bad Request. {errorMessage}");
        return req.CreateResponse(HttpStatusCode.BadRequest, new { Error = errorMessage });
    }

    log.Info($"Getting assets from '{mediaServicesAccountName}' account with paging parameters. skip: '{skip}' - take: '{take}'");

    var context = new CloudMediaContext(new MediaServicesCredentials(mediaServicesAccountName, mediaServicesAccountKey));
    var mediaAssets = context.Assets.OrderByDescending(a => a.Created).Skip(skip).Take(take).ToArray();
    var mediaAssetIds = mediaAssets.Select(a => a.Id).ToArray();
    var mediaAssetFilesGroups = context.Files.Where(CreateOrExpression<IAssetFile, string>("ParentAssetId", mediaAssetIds)).ToArray().GroupBy(af => af.ParentAssetId);
    var mediaLocatorsGroups = context.Locators.Where(CreateOrExpression<ILocator, string>("AssetId", mediaAssetIds)).ToArray().GroupBy(l => l.AssetId);
    var streamingEndpoints = context.StreamingEndpoints.ToArray();

    log.Info($"Getting assets total count from '{mediaServicesAccountName}' account.");
    var apiAssetsTotalCount = context.Assets.Count();

    var apiAssets = mediaAssets
        .Select(
            a =>
            {
                var mediaAssetFilesGroup = mediaAssetFilesGroups.FirstOrDefault(g => g.Key == a.Id);
                var mediaLocatorsGroup = mediaLocatorsGroups.FirstOrDefault(g => g.Key == a.Id);
                return ToApiAsset(a, (mediaAssetFilesGroup != null) ? mediaAssetFilesGroup.AsEnumerable() : new IAssetFile[0], (mediaLocatorsGroup != null) ? mediaLocatorsGroup.AsEnumerable() : new ILocator[0], streamingEndpoints);
            })
        .ToArray();
    log.Info($"Returning '{apiAssets.Length}' assets out of '{apiAssetsTotalCount}' from '{mediaServicesAccountName}' account.");

    return req.CreateResponse(HttpStatusCode.OK, new {
        Data = apiAssets,
        Total = apiAssetsTotalCount,
        Skip = skip
    });
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