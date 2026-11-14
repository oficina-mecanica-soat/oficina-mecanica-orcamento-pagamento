namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;

public sealed record PagamentoProvedorResponse(
    string IdentificadorExterno,
    Guid Referencia,
    decimal Valor,
    string Moeda,
    StatusPagamentoProvedor Status);
