using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.DetalharOrcamento;

public sealed class DetalharOrcamentoValidator : AbstractValidator<DetalharOrcamentoRequest>
{
    public DetalharOrcamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(OrcamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrcamentoId).NotEmpty().WithMessage(OrcamentoValidationMessages.OrcamentoIdObrigatorio);
            RuleFor(request => request.ClienteId).NotEqual(Guid.Empty).When(request => request.ClienteId.HasValue)
                .WithMessage(OrcamentoValidationMessages.ClienteObrigatorio);
        });
    }
}
