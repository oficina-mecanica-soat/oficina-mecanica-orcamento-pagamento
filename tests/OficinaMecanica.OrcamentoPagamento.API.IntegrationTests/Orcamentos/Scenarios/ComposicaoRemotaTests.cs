using System.Net;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SQS;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OficinaMecanica.OrcamentoPagamento.API.Extensions.Configuration;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Hosting;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Orcamentos.Scenarios;

public sealed class ComposicaoRemotaTests
{
    private const string Conta = "123456789012";
    private const string FilaEntrada = "https://sqs.us-east-1.amazonaws.com/123456789012/orcamento.fifo";
    private const string FilaOs = "https://sqs.us-east-1.amazonaws.com/123456789012/os.fifo";

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345678901x")]
    public void Dado_ContaInvalida_Quando_Validar_Entao_DeveRejeitar(string conta)
    {
        // Arrange / Act
        var tentativa = () => IdentidadeAcademica.ValidarConta(conta);

        // Assert
        tentativa.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("123456789012", "arn:aws:sts::123456789012:assumed-role/LabRole/sessao", 200, true)]
    [InlineData("999999999999", "arn:aws:sts::123456789012:assumed-role/LabRole/sessao", 200, false)]
    [InlineData("123456789012", "arn:aws:sts::123456789012:assumed-role/OutraRole/sessao", 200, false)]
    [InlineData("123456789012", "arn:aws:sts::123456789012:assumed-role/LabRole/", 200, false)]
    [InlineData("123456789012", "arn:aws:iam::123456789012:user/usuario", 200, false)]
    [InlineData("123456789012", "arn:aws:sts::123456789012:assumed-role/LabRole/sessao", 500, false)]
    public async Task Dado_IdentidadeDoWorkload_Quando_Validar_Entao_DeveExigirContaELabRole(string conta, string arn, int status, bool valida)
    {
        // Arrange
        var sts = new Mock<IAmazonSecurityTokenService>();
        sts.Setup(client => client.GetCallerIdentityAsync(It.IsAny<GetCallerIdentityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetCallerIdentityResponse { Account = conta, Arn = arn, HttpStatusCode = (HttpStatusCode)status });

        // Act / Assert
        if (valida)
        {
            await IdentidadeAcademica.ValidarAsync(sts.Object, Conta);
        }
        else
        {
            var tentativa = () => IdentidadeAcademica.ValidarAsync(sts.Object, Conta);
            await tentativa.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public void Dado_EntradaIgualAResultados_Quando_Compor_Entao_DeveRejeitarCicloNaMesmaFila()
    {
        // Arrange
        var services = new ServiceCollection();
        var sqs = new Mock<IAmazonSQS>();

        // Act
        var tentativa = () => services.AddProcessamentoRemoto(new(FilaEntrada, FilaEntrada), sqs.Object);

        // Assert
        tentativa.Should().Throw<ArgumentException>();
        sqs.VerifyNoOtherCalls();
    }

    [Fact]
    public void Dado_ComposicaoJaConfigurada_Quando_RegistrarNovamente_Entao_DeveRejeitar()
    {
        // Arrange
        var services = new ServiceCollection();
        var sqs = new Mock<IAmazonSQS>();
        services.AddProcessamentoRemoto(new(FilaEntrada, FilaOs), sqs.Object);

        // Act
        var tentativa = () => services.AddProcessamentoRemoto(new(FilaEntrada, FilaOs), sqs.Object);

        // Assert
        tentativa.Should().Throw<InvalidOperationException>();
        sqs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dado_ProcessamentoDesabilitado_Quando_Inicializar_Entao_NaoDeveRegistrarClientesOuWorkersRemotos()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProcessamentoRemoto:Habilitado"] = "false"
        });

        // Act
        using var clientes = await builder.AddApiProcessingAsync();

        // Assert
        clientes.Should().BeNull();
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAmazonSQS));
    }

    [Theory]
    [InlineData("ProcessamentoRemoto:ContaEsperada", "123")]
    [InlineData("ProcessamentoRemoto:Regiao", "regiao-inexistente")]
    [InlineData("ProcessamentoRemoto:FilaEntradaUrl", "https://sqs.us-east-1.amazonaws.com/123456789012/standard")]
    [InlineData("ProcessamentoRemoto:FilaEntradaUrl", "https://sqs.us-west-2.amazonaws.com/123456789012/orcamento.fifo")]
    [InlineData("ProcessamentoRemoto:FilaResultadosOsUrl", "https://sqs.us-east-1.amazonaws.com/999999999999/os.fifo")]
    public async Task Dado_ConfiguracaoIncompativel_Quando_Habilitar_Entao_DeveRejeitarAntesDeConsultarIdentidade(string chave, string valor)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        var configuracao = new Dictionary<string, string?>
        {
            ["ProcessamentoRemoto:Habilitado"] = "true",
            ["ProcessamentoRemoto:ContaEsperada"] = Conta,
            ["ProcessamentoRemoto:Regiao"] = "us-east-1",
            ["ProcessamentoRemoto:FilaEntradaUrl"] = FilaEntrada,
            ["ProcessamentoRemoto:FilaResultadosOsUrl"] = FilaOs
        };
        configuracao[chave] = valor;
        builder.Configuration.AddInMemoryCollection(configuracao);

        // Act
        var tentativa = () => builder.AddApiProcessingAsync();

        // Assert
        await tentativa.Should().ThrowAsync<ArgumentException>();
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAmazonSQS));
    }
}
