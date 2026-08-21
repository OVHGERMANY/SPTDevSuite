using System.Net;
using System.Security.Cryptography;
using System.Text;
using SPTarkov.DI.Annotations;

namespace SPTDevSuite.Server.Security;

public enum SecurityDecision
{
    Allowed,
    Forbidden,
    Unauthorized,
    CsrfRejected,
}
[Injectable(InjectionType.Singleton)]
public sealed class DashboardSessionSecurity
{
    public const string SessionCookieName = "SPTDevSuite.Session";
    public const string CsrfCookieName = "SPTDevSuite.Csrf";
    public const string CsrfHeaderName = "X-SPTDevSuite-CSRF";

    private readonly string _sessionToken = GenerateToken();
    private readonly string _csrfToken = GenerateToken();

    public string CsrfToken => _csrfToken;

    public static bool IsLoopback(IPAddress? address) => address is not null && IPAddress.IsLoopback(address);

    public SecurityDecision ValidateApi(IPAddress? remoteAddress, string? presentedToken)
    {
        if (!IsLoopback(remoteAddress))
        {
            return SecurityDecision.Forbidden;
        }

        return FixedEquals(_sessionToken, presentedToken) ? SecurityDecision.Allowed : SecurityDecision.Unauthorized;
    }

    public SecurityDecision ValidateStateChange(
        IPAddress? remoteAddress,
        string? presentedToken,
        string? csrfCookie,
        string? csrfHeader)
    {
        var apiDecision = ValidateApi(remoteAddress, presentedToken);
        if (apiDecision != SecurityDecision.Allowed)
        {
            return apiDecision;
        }

        return FixedEquals(_csrfToken, csrfCookie) && FixedEquals(_csrfToken, csrfHeader)
            ? SecurityDecision.Allowed
            : SecurityDecision.CsrfRejected;
    }

    public void IssueCookies(HttpResponse response, bool secure)
    {
        response.Cookies.Append(SessionCookieName, _sessionToken, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = secure,
            Path = "/devsuite",
        });
        response.Cookies.Append(CsrfCookieName, _csrfToken, new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = secure,
            Path = "/devsuite",
        });
    }

    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static bool FixedEquals(string expected, string? actual)
    {
        if (actual is null)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
