namespace OficinaMecanica.OrcamentoPagamento.Application.Mensagens;

public sealed class ContratoMensagemException : Exception
{
    public ContratoMensagemException(string codigo) : base(codigo) { }
}
