using FluentAssertions;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Domain.UnitTests.Orcamentos.Factories;

namespace OficinaMecanica.OrcamentoPagamento.Domain.UnitTests.Orcamentos.Aggregates;

public sealed class OrcamentoTests
{
    [Fact]
    public void Dado_SnapshotValido_Quando_Gerar_Entao_DeveCalcularTotalEPreservarIdentidades()
    {
        // Arrange
        var servicoId = Guid.NewGuid();
        var catalogoId = Guid.NewGuid();

        // Act
        var orcamento = OrcamentoTestDataFactory.Criar(servicoId: servicoId, catalogoId: catalogoId);

        // Assert
        orcamento.Id.Should().NotBeEmpty();
        orcamento.Status.Should().Be(StatusOrcamento.AguardandoAprovacao);
        orcamento.ValorTotal.Should().Be(240m);
        orcamento.Servicos.Single().ServicoId.Should().Be(servicoId);
        orcamento.Servicos.Single().ServicoCatalogoId.Should().Be(catalogoId);
        orcamento.Decisao.Should().BeNull();
    }

    [Fact]
    public void Dado_ListaDoProdutor_Quando_AlterarAposGeracao_Entao_SnapshotDevePermanecerCongelado()
    {
        // Arrange
        var servicos = new List<ServicoOrcamento> { ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "Serviço", 10m) };
        var orcamento = Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, servicos, []);

        // Act
        servicos.Clear();

        // Assert
        orcamento.Servicos.Should().HaveCount(1);
        orcamento.ValorTotal.Should().Be(10m);
        ((IList<ServicoOrcamento>)orcamento.Servicos).Invoking(lista => lista.Clear()).Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData(DecisaoOrcamento.Aprovado, StatusOrcamento.Aprovado)]
    [InlineData(DecisaoOrcamento.Recusado, StatusOrcamento.Recusado)]
    public void Dado_OrcamentoPendente_Quando_DecidirERepetir_Entao_DeveAplicarUmaVez(DecisaoOrcamento decisao, StatusOrcamento status)
    {
        // Arrange
        var orcamento = OrcamentoTestDataFactory.Criar();

        // Act
        var primeira = orcamento.RegistrarDecisao(1, decisao);
        var repeticao = orcamento.RegistrarDecisao(1, decisao);

        // Assert
        primeira.Should().BeTrue();
        repeticao.Should().BeFalse();
        orcamento.Status.Should().Be(status);
        orcamento.Decisao.Should().Be(decisao);
    }

    [Fact]
    public void Dado_OrcamentoAprovado_Quando_Compensar_Entao_DevePreservarDecisaoSemAlterarValores()
    {
        // Arrange
        var orcamento = OrcamentoTestDataFactory.Criar();
        orcamento.RegistrarDecisao(1, DecisaoOrcamento.Aprovado);

        // Act
        orcamento.Cancelar(1, "Compensação da OS").Should().BeTrue();
        orcamento.Cancelar(1, "Compensação da OS").Should().BeFalse();
        orcamento.RegistrarDecisao(1, DecisaoOrcamento.Aprovado).Should().BeFalse();

        // Assert
        orcamento.Status.Should().Be(StatusOrcamento.Cancelado);
        orcamento.Decisao.Should().Be(DecisaoOrcamento.Aprovado);
        orcamento.MotivoCancelamento.Should().Be("Compensação da OS");
        orcamento.ValorTotal.Should().Be(240m);
    }

    [Fact]
    public void Dado_Cancelamento_Quando_RepetirComMotivoDivergenteOuDecidir_Entao_DevePreservarEstado()
    {
        // Arrange
        var orcamento = OrcamentoTestDataFactory.Criar();
        orcamento.Cancelar(1, "Recusa");

        // Act
        var repetir = () => orcamento.Cancelar(1, "Outro motivo");
        var decidir = () => orcamento.RegistrarDecisao(1, DecisaoOrcamento.Aprovado);

        // Assert
        repetir.Should().Throw<DomainException>();
        decidir.Should().Throw<DomainException>();
        orcamento.Status.Should().Be(StatusOrcamento.Cancelado);
    }

    [Fact]
    public void Dado_IdentidadeEVersao_Quando_CompararSnapshotReordenado_Entao_DeveReconhecerRepeticao()
    {
        // Arrange
        var a = ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "A", 10m);
        var b = ServicoOrcamento.Criar(Guid.NewGuid(), a.ServicoCatalogoId, "B", 20m);
        var ordem = Guid.NewGuid();
        var cliente = Guid.NewGuid();
        var original = Orcamento.Gerar(ordem, cliente, 1, [a, b], []);

        // Act
        var repetido = Orcamento.Gerar(ordem, cliente, 1, [b, a], []);

        // Assert
        original.PossuiMesmoSnapshot(repetido).Should().BeTrue();
        original.PossuiMesmoSnapshot(Orcamento.Gerar(ordem, cliente, 2, [b, a], [])).Should().BeFalse();
        original.PossuiMesmoSnapshot(Orcamento.Gerar(ordem, Guid.NewGuid(), 1, [b, a], [])).Should().BeFalse();
        original.PossuiMesmoSnapshot(Orcamento.Gerar(ordem, cliente, 1, [a], [])).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Dado_VersaoInvalida_Quando_Gerar_Entao_DeveRejeitar(int versao)
    {
        // Arrange / Act
        var acao = () => OrcamentoTestDataFactory.Criar(versao);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_ReferenciasInvalidas_Quando_Gerar_Entao_NaoDeveAceitarAgregadoInconsistente()
    {
        // Arrange
        var servico = ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "Serviço", 1m);
        var peca = PecaInsumoOrcamento.Criar(Guid.NewGuid(), "Peça", 1, 1m);
        Action[] invalidos =
        [
            () => Orcamento.Gerar(Guid.Empty, Guid.NewGuid(), 1, [servico], []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.Empty, 1, [servico], []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [], []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, null!, []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [servico], null!),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [null!], []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [servico], [null!]),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [servico, servico], []),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1, [servico], [peca, peca]),
            () => Orcamento.Gerar(Guid.NewGuid(), Guid.NewGuid(), 1,
                [ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "Máximo", RegrasSnapshot.ValorMaximo), servico], [])
        ];

        // Act / Assert
        foreach (var acao in invalidos)
            acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_DecisaoOuVersaoDivergente_Quando_Alterar_Entao_DeveRejeitarSemEfeito()
    {
        // Arrange
        var orcamento = OrcamentoTestDataFactory.Criar();
        Action[] invalidos =
        [
            () => orcamento.RegistrarDecisao(2, DecisaoOrcamento.Aprovado),
            () => orcamento.RegistrarDecisao(1, (DecisaoOrcamento)0),
            () => orcamento.Cancelar(2, "Motivo"),
            () => orcamento.Cancelar(1, "")
        ];
        foreach (var acao in invalidos)
            acao.Should().Throw<DomainException>();

        // Act
        orcamento.RegistrarDecisao(1, DecisaoOrcamento.Aprovado);
        var contradizer = () => orcamento.RegistrarDecisao(1, DecisaoOrcamento.Recusado);

        // Assert
        contradizer.Should().Throw<DomainException>();
        orcamento.Status.Should().Be(StatusOrcamento.Aprovado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.001)]
    [InlineData(10000000000000000)]
    public void Dado_ValorNaoRepresentavel_Quando_CriarSnapshot_Entao_DeveRejeitar(decimal valor)
    {
        // Arrange / Act
        var servico = () => ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), "Serviço", valor);
        var peca = () => PecaInsumoOrcamento.Criar(Guid.NewGuid(), "Peça", 1, valor);

        // Assert
        servico.Should().Throw<DomainException>();
        peca.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Dado_DescricaoInvalida_Quando_Criar_Entao_DeveRejeitar(string? descricao)
    {
        // Arrange / Act
        var acao = () => ServicoOrcamento.Criar(Guid.NewGuid(), Guid.NewGuid(), descricao!, 1m);

        // Assert
        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Dado_LimitesDeQuantidadeEDescricao_Quando_CriarSnapshot_Entao_DeveValidarSemOverflow()
    {
        // Arrange
        var id = Guid.NewGuid();
        Action[] invalidos =
        [
            () => ServicoOrcamento.Criar(Guid.Empty, id, "Serviço", 1m),
            () => ServicoOrcamento.Criar(id, Guid.Empty, "Serviço", 1m),
            () => ServicoOrcamento.Criar(id, id, new string('é', 161), 1m),
            () => PecaInsumoOrcamento.Criar(Guid.Empty, "Peça", 1, 1m),
            () => PecaInsumoOrcamento.Criar(id, "Peça", 0, 1m),
            () => PecaInsumoOrcamento.Criar(id, "Peça", 2, RegrasSnapshot.ValorMaximo)
        ];

        // Act / Assert
        foreach (var acao in invalidos)
            acao.Should().Throw<DomainException>();
        ServicoOrcamento.Criar(id, id, " Serviço ", 1m).Descricao.Should().Be("Serviço");
    }
}
