using BitAggregator.Application;
using BitAggregator.Infrastructure;
using BitAggregator.Api.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

const string blazorCorsPolicy = "BlazorFrontend";

builder.Services.AddCors(options =>
{
    options.AddPolicy(blazorCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5180")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "BitAggregator API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors(blazorCorsPolicy);

app.MapControllers();

app.Run();

public partial class Program;