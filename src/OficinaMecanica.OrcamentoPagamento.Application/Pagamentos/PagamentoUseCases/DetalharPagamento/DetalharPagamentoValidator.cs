using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.DetalharPagamento;

public sealed class DetalharPagamentoValidator : AbstractValidator<DetalharPagamentoRequest>
{
    public DetalharPagamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(PagamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.PagamentoId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
            RuleFor(request => request.ClienteId).NotEqual(Guid.Empty).When(request => request.ClienteId.HasValue)
                .WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
        });
    }
}
