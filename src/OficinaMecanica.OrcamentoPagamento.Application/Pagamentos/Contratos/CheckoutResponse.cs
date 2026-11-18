namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;

public sealed record CheckoutResponse(
    string IdentificadorExterno,
    Guid Referencia,
    decimal Valor,
    Uri Endereco);
