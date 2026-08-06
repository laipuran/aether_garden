using aether_garden_be.Modules;
using aether_garden_be.Options;
using aether_garden_be.Services.Content;
using aether_garden_be.Services.Github;
using aether_garden_be.Services.Music;
using aether_garden_be.Services.Profile;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

builder.Services.Configure<GithubOptions>(builder.Configuration.GetSection("Github"));
builder.Services.Configure<ContentOptions>(builder.Configuration.GetSection("Content"));
builder.Services.Configure<InternalAuthOptions>(builder.Configuration.GetSection("InternalAuth"));
builder.Services.Configure<FeatureOptions>(builder.Configuration.GetSection("Features"));
builder.Services.Configure<MusicOptions>(builder.Configuration.GetSection("Music"));

var frontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<MarkdownContentService>();
builder.Services.AddSingleton<IContentProvider>(sp => sp.GetRequiredService<MarkdownContentService>());
builder.Services.AddSingleton<IContentReloadService>(sp => sp.GetRequiredService<MarkdownContentService>());
builder.Services.AddSingleton<StaticProfileProvider>();
builder.Services.AddSingleton<GithubOverviewService>();
builder.Services.AddSingleton<IAppleMusicService, AppleMusicService>();
builder.Services.AddSingleton<AppleMusicDevTokenProvider>();
builder.Services.AddSingleton<NeteaseMusicService>();

builder.Services.AddSingleton<IEndpointModule, ProfileModule>();
builder.Services.AddSingleton<IEndpointModule, BlogModule>();
builder.Services.AddSingleton<IEndpointModule, NotesModule>();
builder.Services.AddSingleton<IEndpointModule, GithubModule>();
builder.Services.AddSingleton<IEndpointModule, MusicModule>();
builder.Services.AddSingleton<IEndpointModule, InternalModule>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");

await app.Services.GetRequiredService<IContentReloadService>().ReloadAsync();

var features = app.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;
var modules = app.Services.GetServices<IEndpointModule>();

foreach (var module in modules)
{
    if (module.IsEnabled(features))
    {
        module.MapEndpoints(app);
    }
}

app.Run();
