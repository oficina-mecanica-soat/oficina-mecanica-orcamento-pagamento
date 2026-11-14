using FluentAssertions;
using Microsoft.Extensions.Options;
using OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Options;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Pagamentos.Services;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Scenarios;

public sealed class ValidadorAssinaturaMercadoPagoTests
{
    private const string HashEsperado = "9af8f9b136f35b35c2938a77fd515b29732c3297d48a8b90136aa57f4cbc2036";
    private const string AssinaturaValida = "ts=1742505638683,v1=" + HashEsperado;

    [Theory]
    [InlineData(AssinaturaValida)]
    [InlineData("v1=" + HashEsperado + ", ts=1742505638683")]
    [InlineData("ts=1742505638683,v1=9AF8F9B136F35B35C2938A77FD515B29732C3297D48A8B90136AA57F4CBC2036")]
    public void Dado_AssinaturaDoManifestoConhecido_Quando_Validar_Entao_DeveAutenticarOrigem(string assinatura)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        var validador = new ValidadorAssinaturaMercadoPago(Options.Create(dados.Configuracao));

        // Act
        var valida = validador.Validar(assinatura, "requisicao-teste-123", "123456");

        // Assert
        valida.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ts=1742505638683")]
    [InlineData("v1=" + HashEsperado)]
    [InlineData("ts=1742505638683,v1=" + HashEsperado + ",ts=1742505638683")]
    [InlineData("ts=1742505638683,v1=" + HashEsperado + ",v1=" + HashEsperado)]
    [InlineData("ts=1742505638683,v1=" + HashEsperado + ",extra=1")]
    [InlineData("ts=1742505638683,v1=" + HashEsperado + ",invalido")]
    [InlineData("ts=letras,v1=" + HashEsperado)]
    [InlineData("ts=00000000000000,v1=" + HashEsperado)]
    [InlineData("ts=1742505638683,v1=00")]
    [InlineData("ts=1742505638683,v1=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    [InlineData("ts=1742505638684,v1=" + HashEsperado)]
    public void Dado_AssinaturaAusenteMalformadaOuAdulterada_Quando_Validar_Entao_DeveRecusar(string? assinatura)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        var validador = new ValidadorAssinaturaMercadoPago(Options.Create(dados.Configuracao));

        // Act / Assert
        validador.Validar(assinatura, "requisicao-teste-123", "123456").Should().BeFalse();
    }

    [Theory]
    [InlineData("outra-requisicao", "123456")]
    [InlineData("requisicao-teste-123", "999999")]
    [InlineData("requisicao;ts:1", "123456")]
    [InlineData(null, "123456")]
    [InlineData("requisicao-teste-123", null)]
    [InlineData("requisicao-teste-123", "../pagamento")]
    [InlineData("requisicao-teste-123", "12345678901234567890")]
    public void Dado_RequisicaoOuRecursoDivergente_Quando_Validar_Entao_AssinaturaNaoDeveServirParaOutroPagamento(string? requisicaoId, string? pagamentoId)
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        var validador = new ValidadorAssinaturaMercadoPago(Options.Create(dados.Configuracao));

        // Act / Assert
        validador.Validar(AssinaturaValida, requisicaoId, pagamentoId).Should().BeFalse();
    }

    [Fact]
    public void Dado_IntegracaoDesabilitadaOuAssinaturaExcessiva_Quando_Validar_Entao_DeveRecusar()
    {
        // Arrange
        var dados = new MercadoPagoTestDataFactory();
        var desabilitado = new ValidadorAssinaturaMercadoPago(Options.Create(new MercadoPagoOptions()));
        var habilitado = new ValidadorAssinaturaMercadoPago(Options.Create(dados.Configuracao));

        // Act / Assert
        desabilitado.Validar(AssinaturaValida, "requisicao-teste-123", "123456").Should().BeFalse();
        habilitado.Validar(new string('X', 257), "requisicao-teste-123", "123456").Should().BeFalse();
        habilitado.Validar(AssinaturaValida, new string('X', 129), "123456").Should().BeFalse();
    }
}
