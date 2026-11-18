namespace OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;

public enum StatusPagamento
{
    Solicitado = 1,
    CriacaoEmAndamento = 2,
    AguardandoPagamento = 3,
    Confirmado = 4,
    Recusado = 5
}
