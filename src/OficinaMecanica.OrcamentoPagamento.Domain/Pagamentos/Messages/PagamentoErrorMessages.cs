namespace OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Messages;

public static class PagamentoErrorMessages
{
    public const string IdentificadorInvalido = "Os identificadores do pagamento devem ser válidos.";
    public const string VersaoInvalida = "A versão do orçamento deve ser maior que zero.";
    public const string ValorInvalido = "O valor do pagamento deve ser positivo e possuir até duas casas decimais.";
    public const string ValorDivergente = "O valor informado pelo provedor diverge do valor da cobrança.";
    public const string CheckoutInvalido = "Os dados do checkout são inválidos.";
    public const string CheckoutDivergente = "Já existe outro checkout associado a este pagamento.";
    public const string ConfirmacaoDivergente = "Já existe outra confirmação associada a este pagamento.";
    public const string TransicaoInvalida = "O estado atual não permite iniciar outra criação de cobrança.";
    public const string ReferenciaExternaInvalida = "A identificação do pagamento no provedor é inválida.";
}
