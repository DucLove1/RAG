using RAG.Extension;
using DotNetEnv;

// load environment variables from .env file
Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers();

// Các provider LLM được đăng ký dạng Keyed Services (Groq / Gemini) trong AddLLM.
builder.Services.AddLLM(builder.Configuration);
builder.Services.AddEmbeddingModel(builder.Configuration);

// Đăng ký QdrantClient (Sử dụng gRPC)
builder.Services.AddQdrant(builder.Configuration);

// Node chuẩn hóa câu hỏi người dùng (viết tắt, sai chính tả, thiếu dấu)
builder.Services.AddQueryNormalization(builder.Configuration);

// Pipeline RAG + cấu hình prompt/chunking
builder.Services.AddRagPipeline(builder.Configuration);

// Đăng ký bộ mã hóa mở rộng (giúp đọc được các file mã hóa ANSI/Windows cũ)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(pattern: "/openapi");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
