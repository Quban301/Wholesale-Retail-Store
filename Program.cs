using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)));

builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<ProductSearchService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The built React app is served by this same server, so no CORS is needed.
// (During `npm run dev` the Vite proxy forwards /api, which is also same-origin.)
builder.Services.AddSpaStaticFiles(configuration => configuration.RootPath = "frontend/build");

var app = builder.Build();

// Never leak exception text to API callers; the exception is still logged by the middleware.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Create/upgrade the LocalDB database (and seed data) on startup so `dotnet run` just works.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSpaStaticFiles();

// Don't let the browser cache the SPA shell, so a rebuilt frontend is picked up immediately.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path == "/" || (path?.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ?? false))
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    }
    await next();
});

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Anything that isn't an API route is a client-side React route, so serve the SPA shell.
// The shell lives in the Vite output folder, not wwwroot, so point the fallback there.
var spaRoot = Path.Combine(app.Environment.ContentRootPath, "frontend", "build");
if (Directory.Exists(spaRoot))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(spaRoot) });
}
else
{
    app.Logger.LogWarning("frontend/build not found. Run `npm install && npm run build` in ./frontend to serve the UI.");
}

app.Run();
