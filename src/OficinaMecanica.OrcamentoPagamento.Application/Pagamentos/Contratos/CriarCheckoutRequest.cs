namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;

public sealed record CriarCheckoutRequest(
    Guid Referencia,
    string Descricao,
    decimal Valor);
