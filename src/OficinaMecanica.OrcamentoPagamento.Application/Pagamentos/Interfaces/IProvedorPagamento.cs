using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;

public interface IProvedorPagamento
{
    Task<CheckoutResponse> CriarCheckoutAsync(CriarCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutResponse?> ConsultarCheckoutPorReferenciaAsync(Guid referencia, CancellationToken cancellationToken = default);
    Task<PagamentoProvedorResponse> ConsultarPagamentoAsync(string identificadorExterno, CancellationToken cancellationToken = default);
}
