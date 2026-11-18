using FluentValidation;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.ValidationMessages;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.GerarOrcamento;

public sealed class PecaSnapshotValidator : AbstractValidator<PecaInsumoOrcamentoRequest>
{
    public PecaSnapshotValidator()
    {
        RuleFor(item => item.PecaInsumoCatalogoId).NotEmpty().WithMessage(OrcamentoValidationMessages.PecaCatalogoIdObrigatorio);
        RuleFor(item => item.Descricao).NotEmpty().WithMessage(OrcamentoValidationMessages.DescricaoInvalida).MaximumLength(RegrasSnapshot.DescricaoMaxima).WithMessage(OrcamentoValidationMessages.DescricaoInvalida);
        RuleFor(item => item.Quantidade).GreaterThan(0).WithMessage(OrcamentoValidationMessages.QuantidadeInvalida);
        RuleFor(item => item.ValorUnitario).Must(valor => valor > 0 && valor <= RegrasSnapshot.ValorMaximo && decimal.Round(valor, 2) == valor)
            .WithMessage(OrcamentoValidationMessages.ValorInvalido);
    }
}
