using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

public sealed record GerarOrcamentoRequest(Guid OrdemServicoId, Guid ClienteId, int Versao,
    ContextoIntegracao Contexto, ServicoOrcamentoRequest[] Servicos, PecaInsumoOrcamentoRequest[] PecasInsumos);
public sealed record ServicoOrcamentoRequest(Guid ServicoId, Guid ServicoCatalogoId, string Descricao, decimal Valor);
public sealed record PecaInsumoOrcamentoRequest(Guid PecaInsumoCatalogoId, string Descricao, int Quantidade, decimal ValorUnitario);
