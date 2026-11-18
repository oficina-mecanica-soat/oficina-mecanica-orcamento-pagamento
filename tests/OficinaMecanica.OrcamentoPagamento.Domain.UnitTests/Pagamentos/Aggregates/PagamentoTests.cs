using FluentAssertions;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.Domain.UnitTests.Pagamentos.Aggregates;

public sealed class PagamentoTests
{
    private static Pagamento Criar() => Pagamento.Solicitar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 240m);

    [Fact]
    public void Dado_SolicitacaoValida_Quando_Criar_Entao_DevePreservarIdentidadeEValor()
    {
        // Arrange / Act
        var pagamento = Criar();

        // Assert
        pagamento.Id.Should().NotBeEmpty();
        pagamento.Valor.Should().Be(240m);
        pagamento.Status.Should().Be(StatusPagamento.Solicitado);
        pagamento.CheckoutId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10000000000000000)]
    public void Dado_ValorInvalido_Quando_Solicitar_Entao_DeveRejeitar(decimal valor)
    {
        // Arrange / Act
        var acao = () => Pagamento.Solicitar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, valor);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Dado_IdentidadeOuVersaoInvalida_Quando_Solicitar_Entao_DeveRejeitar(int campo)
    {
        // Arrange / Act
        var acao = () => Pagamento.Solicitar(campo == 0 ? Guid.Empty : Guid.NewGuid(),
            campo == 1 ? Guid.Empty : Guid.NewGuid(), campo == 2 ? Guid.Empty : Guid.NewGuid(), campo == 3 ? 0 : 1, 10m);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_CriacaoIniciada_Quando_Reiniciar_Entao_DeveImpedirOutraTentativa()
    {
        // Arrange
        var pagamento = Criar();
        pagamento.IniciarCriacao();

        // Act
        var acao = () => pagamento.IniciarCriacao();

        // Assert
        acao.Should().Throw<DomainException>();
        pagamento.Status.Should().Be(StatusPagamento.CriacaoEmAndamento);
    }

    [Fact]
    public void Dado_CheckoutRegistrado_Quando_RepetirOuDivergir_Entao_DevePreservarOriginal()
    {
        // Arrange
        var pagamento = Criar();
        var endereco = new Uri("https://sandbox.mercadopago.com.br/checkout/teste");

        // Act
        var criado = pagamento.RegistrarCheckout("preferencia", endereco, 240m);
        var repetido = pagamento.RegistrarCheckout("preferencia", endereco, 240m);
        var divergente = () => pagamento.RegistrarCheckout("outra", endereco, 240m);

        // Assert
        criado.Should().BeTrue();
        repetido.Should().BeFalse();
        divergente.Should().Throw<DomainException>();
        pagamento.Status.Should().Be(StatusPagamento.AguardandoPagamento);
    }

    [Theory]
    [InlineData("http://checkout.test", "valido", 240)]
    [InlineData("https://usuario:senha@checkout.test", "valido", 240)]
    [InlineData("https://checkout.test", "", 240)]
    [InlineData("https://checkout.test", "valido", 239)]
    public void Dado_CheckoutInvalido_Quando_Registrar_Entao_DevePreservarSolicitacao(string endereco, string id, decimal valor)
    {
        // Arrange
        var pagamento = Criar();

        // Act
        var acao = () => pagamento.RegistrarCheckout(id, new Uri(endereco), valor);

        // Assert
        acao.Should().Throw<DomainException>();
        pagamento.Status.Should().Be(StatusPagamento.Solicitado);
        pagamento.CheckoutId.Should().BeNull();
    }

    [Fact]
    public void Dado_PagamentoRecusado_Quando_ConfirmarOutroPagamento_Entao_DevePermitirAprovacaoSemRegredirDepois()
    {
        // Arrange
        var pagamento = Criar();
        pagamento.Recusar("11", 240m).Should().BeTrue();
        pagamento.Recusar("12", 240m).Should().BeFalse();

        // Act
        var confirmado = pagamento.Confirmar("13", 240m);
        var repetido = pagamento.Confirmar("13", 240m);
        var recusado = pagamento.Recusar("14", 240m);
        pagamento.RegistrarCheckout("preferencia", new Uri("https://checkout.test"), 240m);

        // Assert
        confirmado.Should().BeTrue();
        repetido.Should().BeFalse();
        recusado.Should().BeFalse();
        pagamento.Status.Should().Be(StatusPagamento.Confirmado);
        pagamento.IdentificadorPagamentoProvedor.Should().Be("13");
        var duplicidadeFinanceira = () => pagamento.Confirmar("15", 240m);
        duplicidadeFinanceira.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(true, "", 240)]
    [InlineData(false, "", 240)]
    [InlineData(true, "10", 239)]
    [InlineData(false, "10", 239)]
    public void Dado_ResultadoInvalido_Quando_Registrar_Entao_DeveRejeitar(bool confirmar, string id, decimal valor)
    {
        // Arrange
        var pagamento = Criar();

        // Act
        Action acao = () =>
        {
            if (confirmar)
            {
                pagamento.Confirmar(id, valor);
            }
            else
            {
                pagamento.Recusar(id, valor);
            }
        };

        // Assert
        acao.Should().Throw<DomainException>();
        pagamento.Status.Should().Be(StatusPagamento.Solicitado);
    }
}
