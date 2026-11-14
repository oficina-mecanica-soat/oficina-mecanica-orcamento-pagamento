using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Scenarios;

public sealed class ConfiguracaoMercadoPagoTests
{
    [Fact]
    public async Task Dado_AmbienteSemCredenciais_Quando_ResolverIntegracao_Entao_DevePermanecerDesabilitadaSemEnviarHttp()
    {
        // Arrange
        await using var services = new ServiceCollection()
            .AddPagamentosInfrastructure(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var cliente = services.GetRequiredService<IProvedorPagamento>();

        // Act / Assert
        services.GetRequiredService<IValidadorNotificacaoPagamento>().Validar(null, null, null).Should().BeFalse();
        (await FluentActions.Awaiting(() => cliente.ConsultarPagamentoAsync("123456")).Should().ThrowAsync<ProvedorPagamentoException>())
            .Which.Tipo.Should().Be(TipoFalhaProvedorPagamento.ConfiguracaoInvalida);
    }

    [Theory]
    [InlineData("MercadoPago:AccessToken", "")]
    [InlineData("MercadoPago:AccessToken", "token com espaço")]
    [InlineData("MercadoPago:WebhookSecret", "")]
    [InlineData("MercadoPago:RecebedorId", "0")]
    [InlineData("MercadoPago:UrlNotificacao", "http://oficina.example.com/notificacao")]
    [InlineData("MercadoPago:UrlNotificacao", "https://localhost/notificacao")]
    [InlineData("MercadoPago:UrlNotificacao", "https://usuario:senha@oficina.example.com/notificacao")]
    [InlineData("MercadoPago:UrlNotificacao", "https://oficina.example.com/notificacao#fragmento")]
    public void Dado_AtivacaoIncompleta_Quando_ValidarConfiguracao_Entao_DeveFalharSemExporCredenciais(string campo, string valor)
    {
        // Arrange
        var configuracao = ConfiguracaoValida();
        configuracao[campo] = valor;
        using var services = new ServiceCollection().AddPagamentosInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(configuracao).Build()).BuildServiceProvider();

        // Act
        var ler = () => services.GetRequiredService<IOptions<MercadoPagoOptions>>().Value;

        // Assert
        var erro = ler.Should().Throw<OptionsValidationException>().Which;
        erro.Message.Should().NotContain("token-apenas-teste").And.NotContain("chave-apenas-teste");
    }

    [Fact]
    public void Dado_ConfiguracaoCompleta_Quando_ResolverPortas_Entao_DeveComporAdaptadoresSemRequisicoes()
    {
        // Arrange
        using var services = new ServiceCollection().AddPagamentosInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(ConfiguracaoValida()).Build()).BuildServiceProvider();

        // Act / Assert
        services.GetRequiredService<IOptions<MercadoPagoOptions>>().Value.PossuiConfiguracaoValida().Should().BeTrue();
        services.GetRequiredService<IProvedorPagamento>().Should().NotBeNull();
        services.GetRequiredService<IValidadorNotificacaoPagamento>().Should().NotBeNull();
    }

    private static Dictionary<string, string?> ConfiguracaoValida()
    {
        return new()
        {
            ["MercadoPago:Ativo"] = "true",
            ["MercadoPago:AccessToken"] = "token-apenas-teste",
            ["MercadoPago:WebhookSecret"] = "chave-apenas-teste",
            ["MercadoPago:RecebedorId"] = "42",
            ["MercadoPago:UrlNotificacao"] = "https://oficina.example.com/notificacao"
        };
    }
}
