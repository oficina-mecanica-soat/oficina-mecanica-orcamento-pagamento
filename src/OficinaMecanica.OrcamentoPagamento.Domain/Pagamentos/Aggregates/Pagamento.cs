using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Enums;
using OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Messages;
using OficinaMecanica.OrcamentoPagamento.Domain.Shared.Exceptions;

namespace OficinaMecanica.OrcamentoPagamento.Domain.Pagamentos.Aggregates;

public sealed class Pagamento
{
    private Pagamento()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Guid OrcamentoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public int Versao { get; private set; }
    public decimal Valor { get; private set; }
    public StatusPagamento Status { get; private set; }
    public string? CheckoutId { get; private set; }
    public string? EnderecoCheckout { get; private set; }
    public string? IdentificadorPagamentoProvedor { get; private set; }

    public static Pagamento Solicitar(Guid ordemServicoId, Guid orcamentoId, Guid clienteId, int versao, decimal valor)
    {
        if (ordemServicoId == Guid.Empty || orcamentoId == Guid.Empty || clienteId == Guid.Empty)
        {
            throw new DomainException(PagamentoErrorMessages.IdentificadorInvalido);
        }
        if (versao <= 0)
        {
            throw new DomainException(PagamentoErrorMessages.VersaoInvalida);
        }
        if (valor <= 0 || valor > 9999999999999999.99m || decimal.Round(valor, 2) != valor)
        {
            throw new DomainException(PagamentoErrorMessages.ValorInvalido);
        }

        return new Pagamento
        {
            Id = Guid.CreateVersion7(),
            OrdemServicoId = ordemServicoId,
            OrcamentoId = orcamentoId,
            ClienteId = clienteId,
            Versao = versao,
            Valor = valor,
            Status = StatusPagamento.Solicitado
        };
    }

    public void IniciarCriacao()
    {
        if (Status != StatusPagamento.Solicitado)
        {
            throw new DomainException(PagamentoErrorMessages.TransicaoInvalida);
        }
        Status = StatusPagamento.CriacaoEmAndamento;
    }

    public bool RegistrarCheckout(string identificador, Uri endereco, decimal valor)
    {
        ExigirValor(valor);
        if (string.IsNullOrWhiteSpace(identificador) || identificador.Length > 200
            || endereco is null || !endereco.IsAbsoluteUri || endereco.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(endereco.UserInfo) || endereco.AbsoluteUri.Length > 2048)
        {
            throw new DomainException(PagamentoErrorMessages.CheckoutInvalido);
        }
        if (CheckoutId is not null)
        {
            if (CheckoutId != identificador || EnderecoCheckout != endereco.AbsoluteUri)
            {
                throw new DomainException(PagamentoErrorMessages.CheckoutDivergente);
            }
            return false;
        }

        CheckoutId = identificador;
        EnderecoCheckout = endereco.AbsoluteUri;
        if (Status is StatusPagamento.Solicitado or StatusPagamento.CriacaoEmAndamento)
        {
            Status = StatusPagamento.AguardandoPagamento;
        }
        return true;
    }

    public bool Confirmar(string identificadorPagamentoProvedor, decimal valor)
    {
        ExigirValor(valor);
        ExigirIdentificadorExterno(identificadorPagamentoProvedor);
        if (Status == StatusPagamento.Confirmado)
        {
            if (IdentificadorPagamentoProvedor != identificadorPagamentoProvedor)
            {
                throw new DomainException(PagamentoErrorMessages.ConfirmacaoDivergente);
            }
            return false;
        }

        IdentificadorPagamentoProvedor = identificadorPagamentoProvedor;
        Status = StatusPagamento.Confirmado;
        return true;
    }

    public bool Recusar(string identificadorPagamentoProvedor, decimal valor)
    {
        ExigirValor(valor);
        ExigirIdentificadorExterno(identificadorPagamentoProvedor);
        if (Status is StatusPagamento.Confirmado or StatusPagamento.Recusado)
        {
            return false;
        }

        IdentificadorPagamentoProvedor = identificadorPagamentoProvedor;
        Status = StatusPagamento.Recusado;
        return true;
    }

    private void ExigirValor(decimal valor)
    {
        if (Valor != valor)
        {
            throw new DomainException(PagamentoErrorMessages.ValorDivergente);
        }
    }

    private static void ExigirIdentificadorExterno(string identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador) || identificador.Length > 200)
        {
            throw new DomainException(PagamentoErrorMessages.ReferenciaExternaInvalida);
        }
    }
}
