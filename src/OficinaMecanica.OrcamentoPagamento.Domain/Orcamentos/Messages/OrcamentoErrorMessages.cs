namespace OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Messages;

public static class OrcamentoErrorMessages
{
    public const string IdentificadorObrigatorio = "Identificador do orçamento ou da referência é obrigatório.";
    public const string VersaoInvalida = "Versão do orçamento deve ser maior que zero.";
    public const string DescricaoInvalida = "Descrição do snapshot deve possuir entre 1 e 160 caracteres.";
    public const string ValorInvalido = "Valor deve ser positivo e representável com precisão SQL 18,2.";
    public const string QuantidadeInvalida = "Quantidade deve ser maior que zero.";
    public const string ServicosObrigatorios = "Orçamento deve possuir ao menos um serviço.";
    public const string ReferenciasDuplicadas = "Snapshot não pode conter referências duplicadas.";
    public const string ColecaoInvalida = "Coleção do snapshot não pode ser nula ou conter elementos nulos.";
    public const string DecisaoInvalida = "Decisão do orçamento inválida.";
    public const string TransicaoInvalida = "Orçamento não permite esta alteração no estado atual.";
    public const string ResultadoDivergente = "Repetição do orçamento possui dados divergentes.";
    public const string OrcamentoNaoEncontrado = "Orçamento não encontrado.";
}
