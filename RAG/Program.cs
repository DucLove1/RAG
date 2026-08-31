using DotNetEnv;
using Microsoft.Extensions.Options;
using RAG.Class.Config;
using RAG.Extension.DependencyInjection;

// load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();

// Đăng ký bộ mã hóa mở rộng (giúp đọc được các file mã hóa ANSI/Windows cũ)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Toàn bộ stack RAG. Thứ tự đăng ký được khóa bên trong AddRagStack, không phải ở đây.
builder.Services.AddRagStack(builder.Configuration);

builder.Services.Configure<OpenApiConfig>(builder.Configuration.GetSection(OpenApiConfig.SectionName));
builder.Services.AddOpenApi();

var app = builder.Build();

// Phải đặt trước mọi middleware khác để bắt được exception của cả chuỗi phía sau.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(pattern: app.Services.GetRequiredService<IOptions<OpenApiConfig>>().Value.RoutePattern);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
