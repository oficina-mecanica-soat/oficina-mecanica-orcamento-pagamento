using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Enums;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;

public sealed record RegistrarDecisaoOrcamentoRequest(Guid OrcamentoId, int Versao, DecisaoOrcamento Decisao, Guid OperacaoId, Guid? ClienteId);
