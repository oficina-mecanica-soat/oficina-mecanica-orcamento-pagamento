using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.SolicitarCobranca;

public sealed record SolicitarCobrancaRequest(Guid OrdemServicoId, Guid OrcamentoId, int Versao, ContextoIntegracao Contexto);
