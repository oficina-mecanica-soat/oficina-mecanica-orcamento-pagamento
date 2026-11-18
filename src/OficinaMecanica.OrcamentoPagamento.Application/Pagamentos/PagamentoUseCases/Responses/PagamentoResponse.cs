namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.Responses;

public sealed record PagamentoResponse
{
    public Guid Id { get; init; }
    public Guid OrdemServicoId { get; init; }
    public Guid OrcamentoId { get; init; }
    public Guid ClienteId { get; init; }
    public int Versao { get; init; }
    public decimal Valor { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? EnderecoCheckout { get; init; }
}
