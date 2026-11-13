using System.Text.Json;
using FluentAssertions;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
namespace OficinaMecanica.OrcamentoPagamento.Application.UnitTests.Mensagens;

public sealed class ContratoMensagemTests
{
    private static MensagemIntegracao Valida() => new(Guid.NewGuid(), "GerarOrcamento", 1, Guid.NewGuid(), Guid.NewGuid(), "correlacao",
        Guid.NewGuid(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { versao = 1 }));
    [Theory]
    [InlineData("mensagemId")]
    [InlineData("sagaId")]
    [InlineData("ordemServicoId")]
    [InlineData("causationId")]
    [InlineData("versao")]
    [InlineData("correlationId")]
    [InlineData("ocorridoEm")]
    [InlineData("dados")]
    public void Dado_EnvelopeSemCampoObrigatorio_Quando_Inspecionar_Entao_DeveRejeitar(string campo)
    {
        // Arrange
        var campos = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonContrato.Serializar(Valida()))!;
        campos.Remove(campo);
        // Act
        var ler = () => JsonContrato.LerEnvelope(JsonContrato.Serializar(campos));
        // Assert
        ler.Should().Throw<ContratoMensagemException>();
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    public void Dado_JsonInvalido_Quando_Inspecionar_Entao_DeveRejeitar(string json)
    {
        // Act / Assert
        FluentActions.Invoking(() => JsonContrato.LerEnvelope(json)).Should().Throw<ContratoMensagemException>();
    }
    [Fact]
    public void Dado_CampoExtraOuNumeralComoTexto_Quando_Inspecionar_Entao_DeveRejeitar()
    {
        // Arrange
        var json = JsonContrato.Serializar(Valida());
        // Act / Assert
        FluentActions.Invoking(() => JsonContrato.LerEnvelope(json[..^1] + ",\"token\":\"proibido\"}")).Should().Throw<ContratoMensagemException>();
        FluentActions.Invoking(() => JsonContrato.LerEnvelope(json.Replace("\"versao\":1", "\"versao\":\"1\""))).Should().Throw<ContratoMensagemException>();
    }
    [Fact]
    public void Dado_TraceInvalido_Quando_Inspecionar_Entao_DeveDescartarSemAlterarHashDeNegocio()
    {
        // Arrange
        var mensagem = Valida();
        var dados = new { versao = 1 };
        var contexto = new Dictionary<string, string> { ["traceparent"] = "00-00000000000000000000000000000000-00f067aa0ba902b7-01", ["token"] = "descartar" };
        // Act
        var filtrada = JsonContrato.LerEnvelope(JsonContrato.Serializar(mensagem with { TraceContext = contexto }));
        // Assert
        filtrada.TraceContext.Should().BeNull();
        JsonContrato.Hash(filtrada, dados).Should().Be(JsonContrato.Hash(mensagem, dados));
    }
    [Fact]
    public void Dado_TraceValidoComBaggage_Quando_Filtrar_Entao_DevePreservarSomenteTraceparent()
    {
        // Arrange
        var contexto = new Dictionary<string, string>
        {
            ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            ["baggage"] = "descartar"
        };
        // Act / Assert
        ContextoTrace.Filtrar(contexto).Should().ContainSingle().Which.Key.Should().Be("traceparent");
    }
    [Fact]
    public void Dado_OffsetNaoUtcOuCorrelacaoInvalida_Quando_Inspecionar_Entao_DeveRejeitar()
    {
        // Arrange
        var mensagem = Valida();
        // Act / Assert
        FluentActions.Invoking(() => JsonContrato.LerEnvelope(JsonContrato.Serializar(mensagem with { OcorridoEm = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(-3)) })))
            .Should().Throw<ContratoMensagemException>();
        FluentActions.Invoking(() => JsonContrato.LerEnvelope(JsonContrato.Serializar(mensagem with { CorrelationId = "invalida\n" })))
            .Should().Throw<ContratoMensagemException>();
    }
}
