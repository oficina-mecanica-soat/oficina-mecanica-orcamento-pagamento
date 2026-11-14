namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;

public sealed class ProvedorPagamentoException : Exception
{
    public ProvedorPagamentoException(TipoFalhaProvedorPagamento tipo)
        : base("Não foi possível concluir a operação com o provedor de pagamento.")
    {
        Tipo = tipo;
    }

    public TipoFalhaProvedorPagamento Tipo { get; }
}
