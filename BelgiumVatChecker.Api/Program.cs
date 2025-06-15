using BelgiumVatChecker.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocumentation();
builder.Services.AddHttpClients();
builder.Services.AddApplicationServices();
builder.Services.AddCorsConfiguration();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwaggerDocumentation();
app.UseApiMiddleware();

app.Run();