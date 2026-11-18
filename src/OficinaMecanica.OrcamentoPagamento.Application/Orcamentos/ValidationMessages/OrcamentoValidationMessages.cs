namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;

public static class OrcamentoValidationMessages
{
    public const string RequestObrigatorio = "Requisição de orçamento é obrigatória.";
    public const string OrcamentoIdObrigatorio = "Identificador do orçamento é obrigatório.";
    public const string OrdemServicoObrigatoria = "Ordem de serviço é obrigatória.";
    public const string ClienteObrigatorio = "Cliente é obrigatório.";
    public const string VersaoInvalida = "Versão deve ser maior que zero.";
    public const string ContextoObrigatorio = "Contexto de integração é obrigatório.";
    public const string SagaObrigatoria = "Identificador da Saga é obrigatório.";
    public const string MensagemObrigatoria = "Identificador da mensagem ou operação é obrigatório.";
    public const string CorrelacaoObrigatoria = "Correlação é obrigatória.";
    public const string CorrelacaoInvalida = "Correlação deve ter até 128 caracteres, sem caracteres de controle.";
    public const string ServicosObrigatorios = "Informe ao menos um serviço.";
    public const string PecasObrigatorias = "Coleção de peças e insumos é obrigatória, mesmo vazia.";
    public const string ItemObrigatorio = "Item do snapshot é obrigatório.";
    public const string ServicoIdObrigatorio = "Identificador da instância do serviço na OS é obrigatório.";
    public const string ServicoCatalogoIdObrigatorio = "Referência do serviço ao catálogo é obrigatória.";
    public const string PecaCatalogoIdObrigatorio = "Referência da peça ou insumo ao catálogo é obrigatória.";
    public const string DescricaoInvalida = "Descrição deve possuir entre 1 e 160 caracteres.";
    public const string ValorInvalido = "Valor deve ser positivo e representável com precisão SQL 18,2.";
    public const string QuantidadeInvalida = "Quantidade deve ser maior que zero.";
    public const string DecisaoInvalida = "Decisão do orçamento inválida.";
    public const string MotivoObrigatorio = "Motivo do cancelamento deve possuir entre 1 e 160 caracteres.";
}
