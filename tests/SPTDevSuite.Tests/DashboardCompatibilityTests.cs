using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SPTarkov.Server.Core.Models.Common;
using SPTDevSuite.Contracts;
using SPTDevSuite.Server;
using SPTDevSuite.Server.Catalog;
using SPTDevSuite.Server.Profiles;
using SPTDevSuite.Server.Security;
using SPTDevSuite.Server.Web;

namespace SPTDevSuite.Tests;

public sealed class DashboardCompatibilityTests
{
    [Fact]
    public async Task OverviewReportsDeclaredModVersion()
    {
        var security = new DashboardSessionSecurity();
        var cookies = IssueCookies(security);
        var state = new FoundationState();
        state.SetCompatibility(CompatibilityPolicy.Evaluate(DevSuiteConstants.RequiredSptVersion));
        var listener = CreateListener(security, state);
        var context = CreateContext(HttpMethods.Get, "/devsuite/api/overview", cookies);

        await listener.HandleAsync(new MongoId("0123456789abcdef01234567"), context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(DevSuiteConstants.ModVersion, response.RootElement.GetProperty("version").GetString());
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task IncompatibleRuntimeRejectsEveryStateChangingMethodBeforeUnlockHandling(string method)
    {
        var security = new DashboardSessionSecurity();
        var cookies = IssueCookies(security);
        var state = new FoundationState();
        var listener = CreateListener(security, state);
        var context = CreateContext(method, "/devsuite/api/unlocks", cookies);
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"modules\":[\"ExamineAllItems\"],\"apply\":true,\"confirmation\":\"APPLY_UNLOCKS\"}"));

        await listener.HandleAsync(new MongoId("0123456789abcdef01234567"), context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Assert.Equal(state.Compatibility.Message, body);
    }

    [Fact]
    public async Task UnlockServiceRejectsIncompatibleRuntimeBeforeProfileDependenciesAreAccessed()
    {
        var state = new FoundationState();
        var service = new UnlockOperationService(
            state,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        var request = new UnlockOperationRequest([UnlockModule.ExamineAllItems], true, "APPLY_UNLOCKS");

        var exception = await Assert.ThrowsAsync<SptCompatibilityException>(() => service.ExecuteAsync(
            new MongoId("0123456789abcdef01234567"), request, CancellationToken.None));

        Assert.Equal(state.Compatibility.Message, exception.Message);
    }

    private static DashboardHttpListener CreateListener(
        DashboardSessionSecurity security,
        FoundationState state) =>
        new(
            security,
            state,
            new ItemCatalogService(),
            Uninitialized<SptProfileOverviewService>(),
            Uninitialized<UnlockOperationService>());

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        IReadOnlyDictionary<string, string> cookies)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.Cookie = string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}"));
        context.Request.Headers[DashboardSessionSecurity.CsrfHeaderName] = cookies[DashboardSessionSecurity.CsrfCookieName];
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static Dictionary<string, string> IssueCookies(DashboardSessionSecurity security)
    {
        var context = new DefaultHttpContext();
        security.IssueCookies(context.Response, false);
        return context.Response.Headers.SetCookie
            .Select(value => value?.Split(';', 2)[0] ?? string.Empty)
            .Select(value => value.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
    }

    private static T Uninitialized<T>() where T : class =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
