using SensorMon.Alerter;
using StackExchange.Redis;

// Worker Service host: no web server, no DB — this service only reads Valkey and POSTs
// to ntfy. Mirrors the structure of SensorMon.Worker so the two are easy to compare.
var builder = Host.CreateApplicationBuilder(args);

// Bind the "Alerter" config section (appsettings.json / env vars Alerter__*).
builder.Services.Configure<AlerterOptions>(builder.Configuration.GetSection("Alerter"));

// HttpClient for the ntfy POST.
builder.Services.AddHttpClient("ntfy");

// Shared Valkey connection (singleton multiplexer). AbortOnConnectFail = false so the
// service starts even if Valkey is briefly unavailable and reconnects on its own.
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var opts = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<AlerterOptions>>().Value;
    var config = ConfigurationOptions.Parse(opts.ValkeyConnection);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddSingleton<NtfyClient>();
builder.Services.AddHostedService<AlerterWorker>();

var host = builder.Build();
host.Run();
