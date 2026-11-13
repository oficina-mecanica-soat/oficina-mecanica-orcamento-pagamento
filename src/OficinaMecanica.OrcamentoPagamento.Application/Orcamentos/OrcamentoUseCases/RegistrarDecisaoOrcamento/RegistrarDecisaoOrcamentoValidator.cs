using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.RegistrarDecisaoOrcamento;

public sealed class RegistrarDecisaoOrcamentoValidator : AbstractValidator<RegistrarDecisaoOrcamentoRequest>
{
    public RegistrarDecisaoOrcamentoValidator()
    {
        RuleFor(request => request).NotNull().WithMessage(OrcamentoValidationMessages.RequestObrigatorio);
        When(request => request is not null, () =>
        {
            RuleFor(request => request.OrcamentoId).NotEmpty().WithMessage(OrcamentoValidationMessages.OrcamentoIdObrigatorio);
            RuleFor(request => request.Versao).GreaterThan(0).WithMessage(OrcamentoValidationMessages.VersaoInvalida);
            RuleFor(request => request.OperacaoId).NotEmpty().WithMessage(OrcamentoValidationMessages.MensagemObrigatoria);
            RuleFor(request => request.Decisao).IsInEnum().WithMessage(OrcamentoValidationMessages.DecisaoInvalida);
            RuleFor(request => request.ClienteId).NotEqual(Guid.Empty).When(request => request.ClienteId.HasValue)
                .WithMessage(OrcamentoValidationMessages.ClienteObrigatorio);
        });
    }
}
