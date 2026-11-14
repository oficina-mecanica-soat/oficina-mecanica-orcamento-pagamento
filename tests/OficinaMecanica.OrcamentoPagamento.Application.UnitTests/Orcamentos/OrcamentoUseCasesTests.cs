using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.UnitTests.Orcamentos.Factories;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.Application.UnitTests.Orcamentos;

public sealed class OrcamentoUseCasesTests
{
    [Fact]
    public async Task Dado_CancelamentoEntreConsultaETransacao_Quando_Gerar_Entao_NaoDevePersistirVersaoCancelada()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        dados.Cancelamentos.SetupSequence(r => r.ObterAsync(dados.Request.OrdemServicoId, dados.Request.Versao, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancelamentoRecebido?)null)
            .ReturnsAsync(new CancelamentoRecebido(dados.Request.OrdemServicoId, dados.Request.Versao, dados.Request.Contexto.SagaId, "Cancelamento"));
        // Act
        var resultado = await dados.CriarGerarUseCase().ExecuteAsync(dados.Request);
        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro!.Tipo.Should().Be(TipoErro.RegraNegocio);
        dados.Repository.Verify(r => r.AdicionarAsync(It.IsAny<Domain.Orcamentos.Aggregates.Orcamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Dado_CancelamentoAntecipado_Quando_GerarPorOutroPontoDeEntrada_Entao_NaoDeveReabrirVersao()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        dados.Cancelamentos.Setup(r => r.ObterAsync(dados.Request.OrdemServicoId, dados.Request.Versao, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancelamentoRecebido(dados.Request.OrdemServicoId, dados.Request.Versao, dados.Request.Contexto.SagaId, "Cancelamento"));
        // Act
        var resultado = await dados.CriarGerarUseCase().ExecuteAsync(dados.Request);
        // Assert
        resultado.Sucesso.Should().BeFalse();
        resultado.Erro!.Tipo.Should().Be(TipoErro.RegraNegocio);
        dados.Repository.Verify(r => r.AdicionarAsync(It.IsAny<Domain.Orcamentos.Aggregates.Orcamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(161)]
    public async Task Dado_TextoVazioOuLongo_Quando_Validar_Entao_DeveManterMensagensEmPortugues(int tamanho)
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var texto = new string('a', tamanho);
        var request = dados.Request with
        {
            Contexto = dados.Request.Contexto with { CorrelationId = texto },
            Servicos = [dados.Request.Servicos[0] with { Descricao = texto }],
            PecasInsumos = [dados.Request.PecasInsumos[0] with { Descricao = texto }]
        };

        // Act
        var geracao = await new GerarOrcamentoValidator().ValidateAsync(request);
        var cancelamento = await new CancelarOrcamentoValidator().ValidateAsync(
            new CancelarOrcamentoRequest(Guid.NewGuid(), 1, Guid.NewGuid(), texto));

        // Assert
        geracao.Errors.Where(e => e.PropertyName.EndsWith("Descricao")).Should().HaveCount(2)
            .And.OnlyContain(e => e.ErrorMessage == "Descrição deve possuir entre 1 e 160 caracteres.");
        geracao.Errors.Should().Contain(e => e.PropertyName == "Contexto.CorrelationId"
            && e.ErrorMessage == (tamanho == 0 ? "Correlação é obrigatória." : "Correlação deve ter até 128 caracteres, sem caracteres de controle."));
        cancelamento.Errors.Should().ContainSingle().Which.ErrorMessage.Should()
            .Be("Motivo do cancelamento deve possuir entre 1 e 160 caracteres.");
    }

    [Fact]
    public async Task Dado_SnapshotValido_Quando_Gerar_Entao_DeveConfirmarAgregadoContextoEOutboxNaMesmaUnidade()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        MensagemIntegracao? publicado = null;
        dados.Outbox.Setup(i => i.AdicionarResultadoAsync(It.IsAny<MensagemIntegracao>(), 1, It.IsAny<CancellationToken>()))
            .Callback<MensagemIntegracao, int, CancellationToken>((m, _, _) => publicado = m).Returns(Task.CompletedTask);

        // Act
        var resultado = await dados.CriarGerarUseCase().ExecuteAsync(dados.Request);

        // Assert
        resultado.Sucesso.Should().BeTrue();
        dados.Repository.Verify(r => r.AdicionarAsync(It.Is<Orcamento>(o => o.ValorTotal == 240m), It.IsAny<CancellationToken>()), Times.Once);
        dados.UnitOfWork.Verify(u => u.ExecutarEmTransacaoAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        publicado.Should().NotBeNull();
        publicado!.Tipo.Should().Be("OrcamentoGerado");
        publicado.SagaId.Should().Be(dados.Request.Contexto.SagaId);
        publicado.CausationId.Should().Be(dados.Request.Contexto.MensagemId);
        publicado.Dados.GetProperty("versao").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dado_MesmaVersaoExistente_Quando_Repetir_Entao_DeveRetornarOuConflitarSemPersistir(bool divergente)
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var existente = dados.CriarOrcamento();
        dados.Repository.Setup(r => r.ObterPorOrdemEVersaoAsync(dados.Request.OrdemServicoId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(existente);
        dados.Integracao.Setup(i => i.ObterContextoAsync(existente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dados.Request.Contexto);
        var request = divergente ? dados.Request with { ClienteId = Guid.NewGuid() } : dados.Request;

        // Act
        var resultado = await dados.CriarGerarUseCase().ExecuteAsync(request);

        // Assert
        resultado.Sucesso.Should().Be(!divergente);
        if (divergente)
            resultado.Erro!.Tipo.Should().Be(TipoErro.Conflito);
        dados.UnitOfWork.Verify(u => u.ExecutarEmTransacaoAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dado_RequisicaoInvalida_Quando_Gerar_Entao_DeveRetornarErrosSemConsultarPersistencia()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();

        // Act
        var resultado = await dados.CriarGerarUseCase().ExecuteAsync(dados.Request with
        { OrdemServicoId = Guid.Empty, ClienteId = Guid.Empty, Versao = 0 });

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.Validacao);
        resultado.Erro.Erros.Should().HaveCountGreaterThanOrEqualTo(3);
        dados.Repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dado_ClienteDiferente_Quando_Decidir_Entao_DeveRetornarNaoEncontradoSemMutacao()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var orcamento = dados.CriarOrcamento();
        dados.Repository.Setup(r => r.ObterPorIdAsync(orcamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(orcamento);
        var useCase = new RegistrarDecisaoOrcamentoUseCase(dados.Repository.Object, dados.Integracao.Object, dados.Outbox.Object, dados.UnitOfWork.Object,
            new RegistrarDecisaoOrcamentoValidator(), dados.Mapper.Object, TimeProvider.System, NullLogger<RegistrarDecisaoOrcamentoUseCase>.Instance);

        // Act
        var resultado = await useCase.ExecuteAsync(new(orcamento.Id, 1, DecisaoOrcamento.Aprovado, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        resultado.Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        orcamento.Status.Should().Be(StatusOrcamento.AguardandoAprovacao);
        dados.Integracao.VerifyNoOtherCalls();
        dados.Outbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dado_DecisaoRepetida_Quando_Registrar_Entao_DeveCriarUmaUnicaOutboxComCausacaoDaOperacao()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var orcamento = dados.CriarOrcamento();
        dados.Repository.Setup(r => r.ObterPorIdAsync(orcamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(orcamento);
        dados.Integracao.Setup(i => i.ObterContextoAsync(orcamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dados.Request.Contexto);
        var useCase = new RegistrarDecisaoOrcamentoUseCase(dados.Repository.Object, dados.Integracao.Object, dados.Outbox.Object, dados.UnitOfWork.Object,
            new RegistrarDecisaoOrcamentoValidator(), dados.Mapper.Object, TimeProvider.System, NullLogger<RegistrarDecisaoOrcamentoUseCase>.Instance);
        var request = new RegistrarDecisaoOrcamentoRequest(orcamento.Id, 1, DecisaoOrcamento.Recusado, Guid.NewGuid(), null);

        // Act
        await useCase.ExecuteAsync(request);
        await useCase.ExecuteAsync(request);

        // Assert
        dados.Outbox.Verify(i => i.AdicionarResultadoAsync(It.Is<MensagemIntegracao>(m =>
            m.Tipo == "OrcamentoDecidido" && m.CausationId == request.OperacaoId), 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_CancelamentoRepetido_Quando_Executar_Entao_DeveConfirmarUmaVez()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var orcamento = dados.CriarOrcamento();
        dados.Repository.Setup(r => r.ObterPorIdAsync(orcamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(orcamento);
        dados.Integracao.Setup(i => i.ObterContextoAsync(orcamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(dados.Request.Contexto);
        var useCase = new CancelarOrcamentoUseCase(dados.Repository.Object, dados.Integracao.Object, dados.Outbox.Object, dados.UnitOfWork.Object,
            new CancelarOrcamentoValidator(), dados.Mapper.Object, TimeProvider.System, NullLogger<CancelarOrcamentoUseCase>.Instance);
        var request = new CancelarOrcamentoRequest(orcamento.Id, 1, Guid.NewGuid(), "Recusa");

        // Act
        await useCase.ExecuteAsync(request);
        await useCase.ExecuteAsync(request);

        // Assert
        dados.Outbox.Verify(i => i.AdicionarResultadoAsync(It.Is<MensagemIntegracao>(m => m.Tipo == "OrcamentoCancelado"),
            1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dado_OrcamentoInexistenteOuEntradaInvalida_Quando_ConsultarOuAlterar_Entao_DeveClassificarFalha()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var detalhar = new DetalharOrcamentoUseCase(dados.Repository.Object, new DetalharOrcamentoValidator(), dados.Mapper.Object);
        var decidir = new RegistrarDecisaoOrcamentoUseCase(dados.Repository.Object, dados.Integracao.Object, dados.Outbox.Object, dados.UnitOfWork.Object,
            new RegistrarDecisaoOrcamentoValidator(), dados.Mapper.Object, TimeProvider.System, NullLogger<RegistrarDecisaoOrcamentoUseCase>.Instance);
        var cancelar = new CancelarOrcamentoUseCase(dados.Repository.Object, dados.Integracao.Object, dados.Outbox.Object, dados.UnitOfWork.Object,
            new CancelarOrcamentoValidator(), dados.Mapper.Object, TimeProvider.System, NullLogger<CancelarOrcamentoUseCase>.Instance);

        // Act / Assert
        (await detalhar.ExecuteAsync(new(Guid.Empty, null))).Erro!.Tipo.Should().Be(TipoErro.Validacao);
        (await detalhar.ExecuteAsync(new(Guid.NewGuid(), null))).Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        (await decidir.ExecuteAsync(new(Guid.Empty, 0, 0, Guid.Empty, null))).Erro!.Tipo.Should().Be(TipoErro.Validacao);
        (await decidir.ExecuteAsync(new(Guid.NewGuid(), 1, DecisaoOrcamento.Aprovado, Guid.NewGuid(), null))).Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
        (await cancelar.ExecuteAsync(new(Guid.Empty, 0, Guid.Empty, ""))).Erro!.Tipo.Should().Be(TipoErro.Validacao);
        (await cancelar.ExecuteAsync(new(Guid.NewGuid(), 1, Guid.NewGuid(), "Motivo"))).Erro!.Tipo.Should().Be(TipoErro.NaoEncontrado);
    }

    [Fact]
    public async Task Dado_ColecoesNulasOuElementosInvalidos_Quando_Validar_Entao_DeveAcumularFalhasSemExcecao()
    {
        // Arrange
        var dados = new OrcamentoUseCaseTestDataFactory();
        var validator = new GerarOrcamentoValidator();

        // Act / Assert
        (await validator.ValidateAsync(dados.Request with { Contexto = null!, Servicos = null!, PecasInsumos = null! })).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(dados.Request with { Servicos = [null!], PecasInsumos = [null!] })).IsValid.Should().BeFalse();
        var varios = await validator.ValidateAsync(dados.Request with
        {
            Contexto = new(Guid.Empty, "", Guid.Empty),
            Servicos = [new(Guid.Empty, Guid.Empty, "", 0)],
            PecasInsumos = [new(Guid.Empty, "", 0, 0)]
        });
        varios.Errors.Count.Should().BeGreaterThan(8);
        var mistos = await validator.ValidateAsync(dados.Request with
        {
            Servicos = [null!, new(Guid.Empty, Guid.Empty, "", 0)]
        });
        mistos.Errors.Should().Contain(e => e.PropertyName == "Servicos[0]");
        mistos.Errors.Should().Contain(e => e.PropertyName == "Servicos[1].ServicoCatalogoId");
    }
}
