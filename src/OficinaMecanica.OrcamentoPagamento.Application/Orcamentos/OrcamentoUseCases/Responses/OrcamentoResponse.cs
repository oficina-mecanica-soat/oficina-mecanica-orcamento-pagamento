namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;

public sealed record OrcamentoResponse(Guid Id, Guid OrdemServicoId, Guid ClienteId, int Versao,
    string Status, string? Decisao, decimal ValorTotal, IReadOnlyCollection<ServicoOrcamentoResponse> Servicos,
    IReadOnlyCollection<PecaInsumoOrcamentoResponse> PecasInsumos);
public sealed record ServicoOrcamentoResponse(Guid ServicoId, Guid ServicoCatalogoId, string Descricao, decimal Valor);
public sealed record PecaInsumoOrcamentoResponse(Guid PecaInsumoCatalogoId, string Descricao, int Quantidade, decimal ValorUnitario, decimal ValorTotal);
