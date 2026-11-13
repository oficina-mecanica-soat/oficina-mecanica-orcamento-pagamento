using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Configuration;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Middlewares;
using OficinaMecanica.OrcamentoPagamento.Application;
using OficinaMecanica.OrcamentoPagamento.Infrastructure;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddApiObservability();
builder.Services.AddApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OrcamentoPagamentoDbContext>().Database.MigrateAsync();
}
app.UseApiMiddlewares();
app.MapApiEndpoints();
await app.RunAsync();

#pragma warning disable ASP0027
public partial class Program
{
    protected Program() { }
}
#pragma warning restore ASP0027
