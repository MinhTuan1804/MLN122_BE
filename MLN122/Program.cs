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
                UPDATE ""Questions"" SET ""Content"" = 'Điền vào chỗ trống. C. Mác và Ph. Ăngghen đã nhận xét rằng: ""Giai cấp tư sản, trong quá trình thống trị giai cấp chưa đầy một thế kỉ, đã tạo ra những ... nhiều hơn và đồ sộ hơn ... của tất cả các thế hệ trước kia gộp lại""' WHERE ""QuestionNum"" = 'Câu 237' OR ""QuestionNum"" = '237' OR ""Id"" = 237;
                UPDATE ""Questions"" SET ""Content"" = 'Nội dung về kinh tế chính trị của C. Mác được trình bày tập trung trong tác phẩm nào?' WHERE ""QuestionNum"" = 'Câu 70' OR ""QuestionNum"" = '70' OR ""Id"" = 70;
                UPDATE ""Questions"" SET ""Content"" = 'Ngoài giá trị thì giá cả của thị trường còn phụ thuộc vào những yếu tố nào?' WHERE ""QuestionNum"" = 'Câu 113' OR ""QuestionNum"" = '113' OR ""Id"" = 113;
                UPDATE ""Questions"" SET ""Content"" = 'Lý luận kinh tế chính trị của C. Mác được trình bày tập trung nhất trong tác phẩm nào?' WHERE ""QuestionNum"" = 'Câu 122' OR ""QuestionNum"" = '122' OR ""Id"" = 122;
                UPDATE ""Questions"" SET ""Content"" = 'Xuất khẩu tư bản được coi là đặc điểm của giai đoạn nào?' WHERE ""QuestionNum"" = 'Câu 202' OR ""QuestionNum"" = '202' OR ""Id"" = 202;
                UPDATE ""Questions"" SET ""Content"" = 'Kinh tế - chính trị Mác - Lênin đã kế thừa và phát triển trực tiếp những thành tựu của học thuyết nào?' WHERE ""QuestionNum"" = 'Câu 218' OR ""QuestionNum"" = '218' OR ""Id"" = 218;
                UPDATE ""Questions"" SET ""Content"" = 'Giá trị cá biệt của hàng hoá do yếu tố nào quyết định?' WHERE ""QuestionNum"" = 'Câu 246' OR ""QuestionNum"" = '246' OR ""Id"" = 246;
                UPDATE ""Questions"" SET ""Content"" = 'Cặp phạm trù nào là phát hiện riêng của C. Mác?' WHERE ""QuestionNum"" = 'Câu 307' OR ""QuestionNum"" = '307' OR ""Id"" = 307;
                UPDATE ""Questions"" SET ""Content"" = 'Học thuyết kinh tế nào của C.Mác được coi là hòn đá tảng?' WHERE ""QuestionNum"" = 'Câu 308' OR ""QuestionNum"" = '308' OR ""Id"" = 308;
                UPDATE ""Questions"" SET ""Content"" = 'Mục đích của sản xuất hàng hóa là thỏa mãn nhu cầu của đối tượng nào?' WHERE ""QuestionNum"" = 'Câu 339' OR ""QuestionNum"" = '339' OR ""Id"" = 339;
                UPDATE ""Questions"" SET ""Content"" = 'Tích tụ và tập trung tư bản giống nhau ở điểm nào?' WHERE ""QuestionNum"" = 'Câu 354' OR ""QuestionNum"" = '354' OR ""Id"" = 354;
                UPDATE ""Questions"" SET ""Content"" = 'Sự phát triển của tư bản tài chính dẫn đến sự hình thành của tổ chức/giai tầng nào?' WHERE ""QuestionNum"" = 'Câu 361' OR ""QuestionNum"" = '361' OR ""Id"" = 361;
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
