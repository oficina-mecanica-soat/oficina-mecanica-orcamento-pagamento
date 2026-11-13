using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
using Respawn;
using Testcontainers.MsSql;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Fixtures;

public sealed class OrcamentoPagamentoApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly Dictionary<string, string?> _originais = [];
    private WebApplicationFactory<Program>? _factory;
    private Respawner? _respawner;
    public const string ChaveTeste = "somente-teste-local-nao-usar-em-ambiente-real-2026";
    public string ConnectionString => new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
    { InitialCatalog = "OrcamentoPagamentoTeste" }.ConnectionString;

    public HttpClient CreateClient() => (_factory ?? throw new InvalidOperationException("Fixture não inicializada."))
        .CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public OrcamentoPagamentoDbContext CriarDbContext() =>
        new(new DbContextOptionsBuilder<OrcamentoPagamentoDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        var configuracoes = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = ConnectionString,
            ["Database__ApplyMigrationsOnStartup"] = "false",
            ["Jwt__Issuer"] = "oficina-mecanica-auth",
            ["Jwt__Audience"] = "oficina-mecanica-api",
            ["Jwt__Secret"] = ChaveTeste
        };
        foreach (var (chave, valor) in configuracoes)
        {
            _originais[chave] = Environment.GetEnvironmentVariable(chave);
            Environment.SetEnvironmentVariable(chave, valor);
        }
        _factory = new OrcamentoPagamentoWebApplicationFactory();
        await using var contexto = CriarDbContext();
        await contexto.Database.MigrateAsync();
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["OrcamentoPagamento"]
        });
    }

    public async Task ResetarBancoAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await (_respawner ?? throw new InvalidOperationException("Respawn não inicializado.")).ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _sqlServer.DisposeAsync();
        foreach (var (chave, valor) in _originais)
            Environment.SetEnvironmentVariable(chave, valor);
    }

    private sealed class OrcamentoPagamentoWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
    }
}
