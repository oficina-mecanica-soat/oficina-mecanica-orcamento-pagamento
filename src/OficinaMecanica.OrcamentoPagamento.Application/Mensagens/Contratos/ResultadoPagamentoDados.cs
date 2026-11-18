namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens.Contratos;

public sealed record ResultadoPagamentoDados(Guid PagamentoId, Guid OrcamentoId, int Versao, decimal Valor);
