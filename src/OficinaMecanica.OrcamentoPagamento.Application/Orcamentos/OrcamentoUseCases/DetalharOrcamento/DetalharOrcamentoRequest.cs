namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;

public sealed record DetalharOrcamentoRequest(Guid OrcamentoId, Guid? ClienteId);
