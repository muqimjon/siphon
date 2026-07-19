using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Siphon.Media;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddMedia(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference("/docs", o => o.WithTitle("Siphon API"));

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseRateLimiter();
app.MapMedia();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();
