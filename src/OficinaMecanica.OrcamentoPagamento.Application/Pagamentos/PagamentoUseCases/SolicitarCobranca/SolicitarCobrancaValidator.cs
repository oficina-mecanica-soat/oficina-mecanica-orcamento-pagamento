using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.SolicitarCobranca;

public sealed class SolicitarCobrancaValidator : AbstractValidator<SolicitarCobrancaRequest>
{
    public SolicitarCobrancaValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(PagamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrdemServicoId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
            RuleFor(request => request.OrcamentoId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
            RuleFor(request => request.Versao).GreaterThan(0).WithMessage(PagamentoValidationMessages.VersaoInvalida);
            RuleFor(request => request.Contexto).NotNull().WithMessage(PagamentoValidationMessages.ContextoObrigatorio);
            When(request => request.Contexto is not null, () =>
            {
                RuleFor(request => request.Contexto.SagaId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
                RuleFor(request => request.Contexto.MensagemId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
                RuleFor(request => request.Contexto.CorrelationId).NotEmpty().WithMessage(PagamentoValidationMessages.CorrelacaoInvalida)
                    .MaximumLength(128).WithMessage(PagamentoValidationMessages.CorrelacaoInvalida)
                    .Must(valor => valor is not null && !valor.Any(char.IsControl)).WithMessage(PagamentoValidationMessages.CorrelacaoInvalida);
            });
        });
    }
}
