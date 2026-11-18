using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OficinaMecanica.OrcamentoPagamento.Application.Common;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Interfaces;

namespace OficinaMecanica.OrcamentoPagamento.Application.UnitTests.Orcamentos.Factories;

internal sealed class OrcamentoUseCaseTestDataFactory
{
    public Mock<IOrcamentoRepository> Repository { get; } = new();
    public Mock<IContextoIntegracaoOrcamentoStore> Integracao { get; } = new();
    public Mock<IOutbox> Outbox { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IMapper> Mapper { get; } = new();
    public Mock<ICancelamentoMensagemStore> Cancelamentos { get; } = new();
    public GerarOrcamentoRequest Request
    {
        get;
    } = new(Guid.NewGuid(), Guid.NewGuid(), 1,
        new(Guid.NewGuid(), "correlacao-teste", Guid.NewGuid()),
        [new(Guid.NewGuid(), Guid.NewGuid(), "Serviço", 150m)], [new(Guid.NewGuid(), "Peça", 2, 45m)]);

    public OrcamentoUseCaseTestDataFactory()
    {
        UnitOfWork.Setup(u => u.ExecutarEmTransacaoAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> acao, CancellationToken ct) => acao(ct));
        Mapper.Setup(m => m.Map<OrcamentoResponse>(It.IsAny<Orcamento>()))
            .Returns(new OrcamentoResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "AguardandoAprovacao", null, 240m, [], []));
    }

    public Orcamento CriarOrcamento()
    {
        var s = Request.Servicos.Single();
        var p = Request.PecasInsumos.Single();
        return Orcamento.Gerar(Request.OrdemServicoId, Request.ClienteId, Request.Versao,
            [ServicoOrcamento.Criar(s.ServicoId, s.ServicoCatalogoId, s.Descricao, s.Valor)],
            [PecaInsumoOrcamento.Criar(p.PecaInsumoCatalogoId, p.Descricao, p.Quantidade, p.ValorUnitario)]);
    }

    public GerarOrcamentoUseCase CriarGerarUseCase() =>
        new(Repository.Object, Integracao.Object, Outbox.Object, UnitOfWork.Object, new GerarOrcamentoValidator(), Mapper.Object,
            TimeProvider.System, NullLogger<GerarOrcamentoUseCase>.Instance, Cancelamentos.Object);
}
