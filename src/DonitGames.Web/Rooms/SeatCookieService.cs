namespace DonitGames.Web.Rooms;

public sealed record SeatCookie(string RoomCode, Guid SeatId);

/// <summary>
/// Reads/writes the <c>dg_seat</c> cookie that lets a phone rejoin its seat after a reload —
/// the mechanism the 8-minute reconnect row in docs/DEPLOYMENT.md depends on.
/// </summary>
public sealed class SeatCookieService
{
    private const string CookieName = "dg_seat";

    public void Set(HttpResponse response, string roomCode, Guid seatId)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(CookieName, $"{roomCode}:{seatId:D}", new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            // Mirrors the request scheme rather than hardcoding true: dotnet run --urls
            // http://0.0.0.0:5000 (the documented LAN phone testing loop in CLAUDE.md) is plain
            // HTTP, and a hardcoded Secure=true would silently drop the cookie there. Behind the
            // Cloudflare Tunnel the forwarded-headers middleware makes IsHttps true, so this
            // still ends up Secure in production.
            Secure = response.HttpContext.Request.IsHttps,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddHours(12),
        });
    }

    public SeatCookie? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        var separator = value.IndexOf(':');
        if (separator <= 0 || !Guid.TryParse(value[(separator + 1)..], out var seatId))
        {
            return null;
        }

        return new SeatCookie(value[..separator], seatId);
    }

    public void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(CookieName);
    }
}
