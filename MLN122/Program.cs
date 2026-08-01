using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MLN122.Data;
using MLN122.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MLN122 Quiz API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Database=mln122_db;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Services
builder.Services.AddScoped<JwtService>();
var secretKey = builder.Configuration["Jwt:Secret"] ?? "SuperSecretKeyForMLN122QuizWebsiteWithMinimum256BitsLengthHere!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MLN122QuizAPI",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MLN122QuizClient",
        ClockSkew = TimeSpan.Zero
    };
});

// CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Auto-migrate & seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        var env = services.GetRequiredService<IWebHostEnvironment>();
        
        // Ensure Database Created / Migrated
        await context.Database.EnsureCreatedAsync();

        // Safely add any new columns to existing Users table
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastQuestionIndex"" integer NOT NULL DEFAULT 0;
                ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastStudyMode"" text NOT NULL DEFAULT 'flashcard';
                ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastFilterType"" text NOT NULL DEFAULT 'all';
                UPDATE ""Questions"" SET ""Content"" = 'Theo C. Mác, hàng hoá là sản phẩm lao động được sản xuất ra nhằm mục đích gì?' WHERE ""QuestionNum"" = 'Câu 165' OR ""QuestionNum"" = '165' OR ""Id"" = 165;
                UPDATE ""Questions"" SET ""Content"" = 'Theo C.Mác, khối lượng giá trị thặng dư là gì?' WHERE ""QuestionNum"" = 'Câu 166' OR ""QuestionNum"" = '166' OR ""Id"" = 166;
                UPDATE ""Questions"" SET ""Content"" = 'Theo C. Mác, yếu tố nào sau đây là hàng hóa đặc biệt?' WHERE ""QuestionNum"" = 'Câu 506' OR ""QuestionNum"" = '506' OR ""Id"" = 506;
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine("DB Migration note: " + ex.Message);
        }
        
        // Seed 539 questions
        await DataSeeder.SeedAsync(context, env);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
