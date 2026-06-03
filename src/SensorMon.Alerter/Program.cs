using SensorMon.Alerter;
using StackExchange.Redis;

// Worker Service host: no web server, no DB — this service only reads Valkey and POSTs
// to ntfy. Mirrors the structure of SensorMon.Worker so the two are easy to compare.
var builder = Host.CreateApplicationBuilder(args);

// Map config into AlerterOptions. Keys are kept flat (Valkey:Connection, Ntfy:*,
// Alert:*) so they line up with the env vars in the shared k8s ConfigMap/Secret —
// notably Valkey__Connection is the SAME key the worker reads.
builder.Services.Configure<AlerterOptions>(o =>
{
    o.ValkeyConnection = builder.Configuration.GetValue("Valkey:Connection", "localhost:6379")!;
    o.NtfyBaseUrl = builder.Configuration.GetValue("Ntfy:BaseUrl", "https://ntfy.sh")!;
    o.NtfyTopic = builder.Configuration.GetValue("Ntfy:Topic", "")!;
    o.CooldownSeconds = builder.Configuration.GetValue("Alert:CooldownSeconds", 300);
    o.ConsumerName = builder.Configuration.GetValue("Alerter:ConsumerName", "alerter-0")!;
});

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
