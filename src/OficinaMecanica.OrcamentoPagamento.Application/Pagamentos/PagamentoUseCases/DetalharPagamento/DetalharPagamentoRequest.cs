namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;

public sealed record DetalharPagamentoRequest(Guid PagamentoId, Guid? ClienteId = null);
