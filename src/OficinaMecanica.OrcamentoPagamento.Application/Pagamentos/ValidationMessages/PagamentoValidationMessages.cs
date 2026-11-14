namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.ValidationMessages;

public static class PagamentoValidationMessages
{
    public const string RequestObrigatorio = "A solicitação de pagamento é obrigatória.";
    public const string IdentificadorObrigatorio = "Informe um identificador válido para a operação de pagamento.";
    public const string VersaoInvalida = "A versão do orçamento deve ser maior que zero.";
    public const string ContextoObrigatorio = "O contexto de integração da cobrança é obrigatório.";
    public const string CorrelacaoInvalida = "A correlação deve possuir até 128 caracteres e não pode conter caracteres de controle.";
    public const string IdentificadorExternoInvalido = "O identificador externo deve ser numérico e possuir até 19 dígitos.";
}
