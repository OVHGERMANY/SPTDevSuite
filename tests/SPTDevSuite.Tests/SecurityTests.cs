using System.Net;
using Microsoft.AspNetCore.Http;
using SPTDevSuite.Server.Security;

namespace SPTDevSuite.Tests;

public sealed class SecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void LoopbackRequestWithValidTokenIsAccepted(string address)
    {
        var security = new DashboardSessionSecurity();
        var cookies = IssueCookies(security);

        var decision = security.ValidateApi(IPAddress.Parse(address), cookies[DashboardSessionSecurity.SessionCookieName]);

        Assert.Equal(SecurityDecision.Allowed, decision);
    }

    [Fact]
    public void NonLoopbackRequestIsRejected()
    {
        var security = new DashboardSessionSecurity();
        var cookies = IssueCookies(security);

        var decision = security.ValidateApi(IPAddress.Parse("192.168.1.20"), cookies[DashboardSessionSecurity.SessionCookieName]);

        Assert.Equal(SecurityDecision.Forbidden, decision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-token")]
    public void MissingOrInvalidDashboardTokenIsRejected(string? token)
    {
        var decision = new DashboardSessionSecurity().ValidateApi(IPAddress.Loopback, token);

        Assert.Equal(SecurityDecision.Unauthorized, decision);
    }

    [Fact]
    public void StateChangeRequiresMatchingAntiCsrfCookieAndHeader()
    {
        var security = new DashboardSessionSecurity();
        var cookies = IssueCookies(security);
        var session = cookies[DashboardSessionSecurity.SessionCookieName];
        var csrf = cookies[DashboardSessionSecurity.CsrfCookieName];

        Assert.Equal(SecurityDecision.CsrfRejected,
            security.ValidateStateChange(IPAddress.Loopback, session, csrf, "wrong"));
        Assert.Equal(SecurityDecision.CsrfRejected,
            security.ValidateStateChange(IPAddress.Loopback, session, null, csrf));
        Assert.Equal(SecurityDecision.Allowed,
            security.ValidateStateChange(IPAddress.Loopback, session, csrf, csrf));
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
}
