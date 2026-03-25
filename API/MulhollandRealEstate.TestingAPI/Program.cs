using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MulhollandRealEstate.API.Configuration;
using MulhollandRealEstate.API.Data;
using MulhollandRealEstate.API.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var connectionString = MysqlConnectionResolver.Resolve(builder.Configuration);
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddScoped<ITriageService, TriageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Serve the vanilla dashboard (Client/) from dotnet.
var clientPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "Client"));
if (Directory.Exists(clientPath))
{
    var fp = new PhysicalFileProvider(clientPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fp });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fp });
}

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", false);
    var seedEnabled = app.Configuration.GetValue("Database:SeedEnabled", false);
    if (autoMigrate)
        await db.Database.MigrateAsync();
    if (seedEnabled)
        await DbSeeder.SeedAsync(db, app.Environment);
}

await app.RunAsync();
