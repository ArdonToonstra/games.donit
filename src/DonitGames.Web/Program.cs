using DonitGames.Core.JustOne;
using DonitGames.Core.Rooms;
using DonitGames.Core.Rooms.Echo;
using DonitGames.Core.Undercover;
using DonitGames.Web.Components;
using DonitGames.Web.Rooms;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Forwarded headers.
// Needed both in production (Cloudflare Tunnel) and for local testing through
// a `cloudflared tunnel --url` quick tunnel. KnownNetworks/KnownProxies MUST be
// cleared (KnownIPNetworks in .NET 10): the defaults trust only loopback, but
// inside Docker the peer is the bridge gateway, so the headers would be silently
// dropped and the app would believe the scheme is http — breaking generated
// URLs and Secure cookies.
// Safe here because the container port is published on loopback only, so
// nothing but the host-local tunnel can reach it to spoof the headers.
// ---------------------------------------------------------------------------
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                       | ForwardedHeaders.XForwardedProto
                       | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.ForwardLimit = 2; // Cloudflare edge + cloudflared
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        o.DetailedErrors = builder.Environment.IsDevelopment();

        // Phones lock their screens constantly. Holding the circuit for five
        // minutes means an unlocked phone resumes with byte-identical state
        // instead of reloading and having to reclaim its seat.
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
        o.DisconnectedCircuitMaxRetained = 32; // bounded for a Raspberry Pi
        o.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
    })
    .AddHubOptions(o =>
    {
        // Cloudflare closes an idle proxied WebSocket at ~100s, so the 15s
        // server ping keeps it alive. Never raise either of these past ~90s.
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(40);
        o.HandshakeTimeout = TimeSpan.FromSeconds(30); // mobile networks are slow
        o.EnableDetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddHealthChecks();

// ---------------------------------------------------------------------------
// Room infrastructure (Phase 2). Each game gets its own RoomRegistry<TState>,
// registered under both its concrete type (so shells can inject it directly)
// and the non-generic IRoomRegistry (so RoomJanitor can sweep every game's
// rooms without knowing their state types). Rooms live in memory only — a
// redeploy drops them, by design (docs/DEPLOYMENT.md).
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<SeatCookieService>();
builder.Services.AddSingleton<RoomRegistry<EchoState>>();
builder.Services.AddSingleton<IRoomRegistry>(sp => sp.GetRequiredService<RoomRegistry<EchoState>>());
builder.Services.AddSingleton<IGameSession<EchoState, EchoView>, EchoSession>();

builder.Services.AddSingleton<UndercoverWordCategoryProvider>();
builder.Services.AddSingleton<RoomRegistry<UndercoverState>>();
builder.Services.AddSingleton<IRoomRegistry>(sp => sp.GetRequiredService<RoomRegistry<UndercoverState>>());
builder.Services.AddSingleton<IGameSession<UndercoverState, UndercoverView>, UndercoverSession>();

builder.Services.AddSingleton<JustOneWordBankProvider>();
builder.Services.AddSingleton<RoomRegistry<JustOneState>>();
builder.Services.AddSingleton<IRoomRegistry>(sp => sp.GetRequiredService<RoomRegistry<JustOneState>>());
builder.Services.AddSingleton<IGameSession<JustOneState, JustOneView>, JustOneSession>();

builder.Services.AddHostedService<RoomJanitor>();

// Scoped per circuit: bridges the component that knows its seat to the
// circuit handler, which is the only thing still alive when a circuit is
// finally evicted (CLAUDE.md non-negotiable #4).
builder.Services.AddScoped<CircuitSeatRegistration>();
builder.Services.AddScoped<CircuitHandler, SeatPresenceCircuitHandler>();

var app = builder.Build();

app.UseForwardedHeaders(); // must run before anything reads scheme/host

// Real client IP for logs and any future rate limiting.
app.Use((ctx, next) =>
{
    if (ctx.Request.Headers.TryGetValue("CF-Connecting-IP", out var cf)
        && System.Net.IPAddress.TryParse(cf.ToString(), out var ip))
    {
        ctx.Connection.RemoteIpAddress = ip;
    }

    return next();
});

// *** DELIBERATELY ABSENT: UseHttpsRedirection() and UseHsts(). ***
// The Cloudflare Tunnel speaks plain HTTP to this app, so either one produces
// an infinite redirect loop. Cloudflare's "Always Use HTTPS" handles the edge.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.MapStaticAssets();

// ---------------------------------------------------------------------------
// The pass-and-play Undercover game is a separate Blazor WebAssembly app,
// published into wwwroot/undercover/ with <base href="/undercover/">. It stays
// client-side so its taps remain instant.
//
// ASP.NET Core's static-file middleware does not know every WASM asset type,
// and an unmapped extension is served as a 404. Enumerate them rather than
// switching on ServeUnknownFileTypes.
// ---------------------------------------------------------------------------
var wasmContentTypes = new FileExtensionContentTypeProvider();
wasmContentTypes.Mappings[".wasm"] = "application/wasm";
wasmContentTypes.Mappings[".blat"] = "application/octet-stream";
wasmContentTypes.Mappings[".dat"] = "application/octet-stream";
wasmContentTypes.Mappings[".dll"] = "application/octet-stream";
wasmContentTypes.Mappings[".pdb"] = "application/octet-stream";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = wasmContentTypes });

app.UseAntiforgery();

app.MapHealthChecks("/healthz");

// Deep links inside the WASM app (e.g. /undercover/how-to-play) must serve its
// index.html rather than 404 — the bug the current GitHub Pages deploy has.
// This MUST be registered before MapRazorComponents, or the server router
// claims the path first.
app.MapFallbackToFile("/undercover/{*path:nonfile}", "undercover/index.html");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
