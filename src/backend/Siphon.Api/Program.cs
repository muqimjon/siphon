using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Siphon.Accounts;
using Siphon.Media;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddMedia(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddAccounts(builder.Configuration);

var app = builder.Build();
app.MigrateAccounts();
app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference("/reference", o => o.WithTitle("Siphon API"));

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/reference")).ExcludeFromDescription();
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapMedia();
app.MapAccounts();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();
