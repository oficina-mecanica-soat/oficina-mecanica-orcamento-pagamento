namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.CancelarOrcamento;

public sealed record CancelarOrcamentoRequest(Guid OrcamentoId, int Versao, Guid OperacaoId, string Motivo);
