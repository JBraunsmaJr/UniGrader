// See https://aka.ms/new-console-template for more information

using UniGrader;
using UniGrader.Models;
using UniGrader.Platforms;
using UniGrader.Shared.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Join(Util.PlatformDataPath, "appsettings.json"), true);

builder.Services.AddOptions();
builder.Services.AddLogging();

PlatformConfig platformConfig = new();
builder.Configuration.Bind("PlatformConfig", platformConfig);
builder.Services.AddSingleton(x => platformConfig);
builder.Services.AddSingleton<QaPlatform>();

var app = builder.Build();
var platform = app.Services.GetRequiredService<QaPlatform>();
await Util.SetExecutionPolicy(true);
await platform.Run();

Console.WriteLine("Grading Complete...");