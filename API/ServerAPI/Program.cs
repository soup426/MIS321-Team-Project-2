using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using MulhollandRealEstate.API.Configuration;
using MulhollandRealEstate.API.Data;
using MulhollandRealEstate.API.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var jwtKey = builder.Configuration["Jwt:Key"];
if (!string.IsNullOrWhiteSpace(jwtKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });
    builder.Services.AddAuthorization(options =>
    {
        // Require auth by default when JWT is enabled.
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}

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

if (!string.IsNullOrWhiteSpace(jwtKey))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Serve uploaded images from disk (dev/demo). Put a reverse proxy/CDN in front for production.
var uploadsPath = app.Configuration["Uploads:Path"]
                  ?? Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "uploads"));
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// Serve the vanilla dashboard (Client/) from dotnet (same as TestingAPI).
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
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var ct = cts.Token;

    if (autoMigrate)
        await db.Database.MigrateAsync(ct);
    if (seedEnabled)
        await DbSeeder.SeedAsync(db, app.Environment);

    // Some environments (e.g. Heroku-provisioned DBs) may already have schema created outside EF migrations.
    // Ensure the image table exists so uploads work even if __EFMigrationsHistory isn't present.
    var ensureImages = app.Configuration.GetValue("Uploads:EnsureImagesTable", true);
    if (ensureImages)
    {
        await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `maintenance_request_images` (
  `id` BIGINT NOT NULL AUTO_INCREMENT,
  `maintenance_request_id` BIGINT NOT NULL,
  `storage_key` VARCHAR(300) NOT NULL,
  `file_name` VARCHAR(260) NOT NULL,
  `content_type` VARCHAR(120) NOT NULL,
  `size_bytes` BIGINT NOT NULL,
  `created_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `ix_mri_request` (`maintenance_request_id`)
) ENGINE=InnoDB;", ct);
    }
}

await app.RunAsync();
