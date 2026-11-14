using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Pagamentos.PagamentoUseCases.ConferirPagamento;

public sealed class ConferirPagamentoValidator : AbstractValidator<ConferirPagamentoRequest>
{
    public ConferirPagamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(PagamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OperacaoId).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorObrigatorio);
            RuleFor(request => request.IdentificadorExterno).NotEmpty().WithMessage(PagamentoValidationMessages.IdentificadorExternoInvalido)
                .MaximumLength(19).WithMessage(PagamentoValidationMessages.IdentificadorExternoInvalido)
                .Must(valor => valor is not null && valor.All(char.IsAsciiDigit)).WithMessage(PagamentoValidationMessages.IdentificadorExternoInvalido);
        });
    }
}
