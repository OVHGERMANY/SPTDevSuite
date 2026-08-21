using System.Net;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers.Http;
using SPTDevSuite.Contracts;
using SPTDevSuite.Server.Catalog;
using SPTDevSuite.Server.Profiles;
using SPTDevSuite.Server.Security;

namespace SPTDevSuite.Server.Web;

[Injectable(TypePriority = 0)]
public sealed class DashboardHttpListener(
    DashboardSessionSecurity security,
    FoundationState foundationState,
    ItemCatalogService catalog,
    SptProfileOverviewService profiles) : IHttpListener
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanHandle(HttpContext context) =>
        context.Request.Path.StartsWithSegments(DevSuiteConstants.RoutePrefix, StringComparison.OrdinalIgnoreCase);

    public async Task HandleAsync(MongoId sessionId, HttpContext context, CancellationToken cancellationToken = default)
    {
        SetSecurityHeaders(context.Response);
        var remoteAddress = context.Connection.RemoteIpAddress;
        if (!DashboardSessionSecurity.IsLoopback(remoteAddress))
        {
            await WriteTextAsync(context, StatusCodes.Status403Forbidden, "Forbidden: SPTDevSuite is loopback-only.", cancellationToken);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (context.Request.Method == HttpMethods.Get
            && (string.Equals(path, DevSuiteConstants.RoutePrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, $"{DevSuiteConstants.RoutePrefix}/", StringComparison.OrdinalIgnoreCase)))
        {
            security.IssueCookies(context.Response, context.Request.IsHttps);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(DashboardPage.Html, cancellationToken);
            await context.Response.CompleteAsync();
            return;
        }

        var sessionToken = context.Request.Cookies[DashboardSessionSecurity.SessionCookieName];
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            var decision = security.ValidateStateChange(
                remoteAddress,
                sessionToken,
                context.Request.Cookies[DashboardSessionSecurity.CsrfCookieName],
                context.Request.Headers[DashboardSessionSecurity.CsrfHeaderName].FirstOrDefault());
            if (decision != SecurityDecision.Allowed)
            {
                await WriteSecurityFailureAsync(context, decision, cancellationToken);
                return;
            }

            await WriteTextAsync(context, StatusCodes.Status405MethodNotAllowed,
                "No state-changing endpoint exists in this foundation milestone.", cancellationToken);
            return;
        }

        var apiDecision = security.ValidateApi(remoteAddress, sessionToken);
        if (apiDecision != SecurityDecision.Allowed)
        {
            await WriteSecurityFailureAsync(context, apiDecision, cancellationToken);
            return;
        }

        if (!foundationState.Compatibility.IsCompatible)
        {
            await WriteTextAsync(context, StatusCodes.Status503ServiceUnavailable,
                foundationState.Compatibility.Message, cancellationToken);
            return;
        }

        switch (path.ToLowerInvariant())
        {
            case "/devsuite/api/overview":
                await WriteJsonAsync(context, new
                {
                    name = "SPTDevSuite",
                    version = "0.1.0",
                    compatibility = foundationState.Compatibility,
                    itemIndexReady = foundationState.ItemIndexReady,
                    indexedItems = catalog.Count,
                    writeCapabilitiesAvailable = foundationState.WriteCapabilitiesAvailable,
                }, cancellationToken);
                break;
            case "/devsuite/api/items":
                await HandleItemsAsync(context, cancellationToken);
                break;
            case "/devsuite/api/profile":
                await HandleProfileAsync(sessionId, context, cancellationToken);
                break;
            case "/devsuite/api/settings":
                await WriteJsonAsync(context, new DashboardSettings(
                    DevSuiteConstants.RequiredSptVersion,
                    DevSuiteConstants.RoutePrefix,
                    true,
                    false,
                    false,
                    DevSuiteConstants.MaximumPageSize), cancellationToken);
                break;
            default:
                await WriteTextAsync(context, StatusCodes.Status404NotFound, "Not found.", cancellationToken);
                break;
        }
    }

    private async Task HandleItemsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var request = context.Request.Query;
        var query = new ItemSearchQuery(
            request["text"].FirstOrDefault(),
            request["id"].FirstOrDefault(),
            request["category"].FirstOrDefault(),
            request["caliber"].FirstOrDefault(),
            ParsePositiveInt(request["page"].FirstOrDefault(), 1),
            ParsePositiveInt(request["pageSize"].FirstOrDefault(), DevSuiteConstants.DefaultPageSize));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await WriteJsonAsync(context, catalog.Search(query, timeout.Token), cancellationToken);
    }

    private async Task HandleProfileAsync(MongoId sessionId, HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            await WriteJsonAsync(context, profiles.GetOverview(sessionId), cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            await WriteTextAsync(context, StatusCodes.Status401Unauthorized,
                "A valid SPT profile session is required.", cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            await WriteTextAsync(context, StatusCodes.Status404NotFound,
                "No PMC profile exists for the current session.", cancellationToken);
        }
    }

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static Task WriteSecurityFailureAsync(HttpContext context, SecurityDecision decision, CancellationToken cancellationToken) =>
        decision switch
        {
            SecurityDecision.Forbidden => WriteTextAsync(context, StatusCodes.Status403Forbidden, "Forbidden.", cancellationToken),
            SecurityDecision.CsrfRejected => WriteTextAsync(context, StatusCodes.Status403Forbidden, "Anti-CSRF validation failed.", cancellationToken),
            _ => WriteTextAsync(context, StatusCodes.Status401Unauthorized, "Dashboard token is missing or invalid.", cancellationToken),
        };

    private static async Task WriteJsonAsync<T>(HttpContext context, T value, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(context.Response.Body, value, JsonOptions, cancellationToken);
        await context.Response.CompleteAsync();
    }

    private static async Task WriteTextAsync(HttpContext context, int statusCode, string text, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(text, cancellationToken);
        await context.Response.CompleteAsync();
    }

    private static void SetSecurityHeaders(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = "default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.XFrameOptions = "DENY";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers.CacheControl = "no-store";
    }
}
