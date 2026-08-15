using Qdrant.Client;
using RAG.Class;
using RAG.Class.Config;
using RAG.Extension;
using RAG.Interface;
using DotNetEnv;

// load environment variables from .env file
Env.Load();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Set up configuration to read from appsettings.json and environment variables
builder.Services.Configure<QDrantConfig>(builder.Configuration.GetSection(QDrantConfig.SectionName));
builder.Services.Configure<GroqConfig>(builder.Configuration.GetSection(GroqConfig.SectionName));
builder.Services.Configure<GeminiEmbeddingModelConfig>(builder.Configuration.GetSection(GeminiEmbeddingModelConfig.SectionName));


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddLLM(builder.Configuration);
builder.Services.AddEmbeddingModel(builder.Configuration);
// Đăng ký QdrantClient (Sử dụng gRPC)
builder.Services.AddQdrant(builder.Configuration);

// Đăng ký Service của bạn
builder.Services.AddSingleton<RAGPipline>();
builder.Services.AddSingleton<RagConfig>();
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
