using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Contratos;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Exceptions;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.Interfaces;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;

namespace OficinaMecanica.OrcamentoPagamento.API.IntegrationTests.Pagamentos.Factories;

internal sealed class EstadoProvedorPagamentoTeste
{
    public CheckoutResponse? Checkout { get; set; }
    public PagamentoProvedorResponse? Pagamento { get; set; }
    public bool FalharAposCriacao { get; set; }
    public bool Indisponivel { get; set; }
    public int Criacoes { get; set; }
    public int ConsultasPagamento { get; set; }
    public Func<CancellationToken, Task>? AntesConsultarCheckout { get; set; }
}

internal sealed class ProvedorPagamentoTeste : IProvedorPagamento
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    private readonly EstadoProvedorPagamentoTeste _estado;

    public ProvedorPagamentoTeste(OrcamentoPagamentoDbContext dbContext, EstadoProvedorPagamentoTeste estado)
    {
        _dbContext = dbContext;
        _estado = estado;
    }

    public async Task<CheckoutResponse> CriarCheckoutAsync(CriarCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ExigirChamadaForaDeTransacao();
        await using var verificacao = new OrcamentoPagamentoDbContext(new DbContextOptionsBuilder<OrcamentoPagamentoDbContext>()
                .UseSqlServer(_dbContext.Database.GetConnectionString()).Options);
        var intencaoPersistida = await verificacao.Pagamentos.FindAsync([request.Referencia], cancellationToken);
        intencaoPersistida.Should().NotBeNull();
        intencaoPersistida!.Status.Should().Be(Domain.Pagamentos.Enums.StatusPagamento.CriacaoEmAndamento);
        _estado.Criacoes++;
        _estado.Checkout = new("preferencia-teste", request.Referencia, request.Valor, new Uri("https://sandbox.mercadopago.com.br/checkout/teste"));
        if (_estado.FalharAposCriacao)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.ResultadoIncerto);
        }
        return _estado.Checkout;
    }

    public async Task<CheckoutResponse?> ConsultarCheckoutPorReferenciaAsync(Guid referencia, CancellationToken cancellationToken = default)
    {
        ExigirChamadaForaDeTransacao();
        if (_estado.AntesConsultarCheckout is not null)
        {
            await _estado.AntesConsultarCheckout(cancellationToken);
        }
        return _estado.Checkout;
    }

    public Task<PagamentoProvedorResponse> ConsultarPagamentoAsync(string identificadorExterno, CancellationToken cancellationToken = default)
    {
        ExigirChamadaForaDeTransacao();
        _estado.ConsultasPagamento++;
        return Task.FromResult(_estado.Pagamento ?? throw new InvalidOperationException("Configure o pagamento do teste."));
    }

    private void ExigirChamadaForaDeTransacao()
    {
        _dbContext.Database.CurrentTransaction.Should().BeNull();
        if (_estado.Indisponivel)
        {
            throw new ProvedorPagamentoException(TipoFalhaProvedorPagamento.ConfiguracaoInvalida);
        }
    }
}
