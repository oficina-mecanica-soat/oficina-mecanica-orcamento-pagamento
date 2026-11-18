using AutoMapper;
using OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.OrcamentoUseCases.Responses;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Aggregates;
using OficinaMecanica.OrcamentoPagamento.Domain.Orcamentos.Entities;

namespace OficinaMecanica.OrcamentoPagamento.Application.Orcamentos.Mappings;

public sealed class OrcamentoMappingProfile : Profile
{
    public OrcamentoMappingProfile()
    {
        CreateMap<Orcamento, OrcamentoResponse>()
            .ForCtorParam(nameof(OrcamentoResponse.Status), opcao => opcao.MapFrom(origem => origem.Status.ToString()))
            .ForCtorParam(nameof(OrcamentoResponse.Decisao), opcao => opcao.MapFrom(origem => origem.Decisao.HasValue ? origem.Decisao.Value.ToString() : null));
        CreateMap<ServicoOrcamento, ServicoOrcamentoResponse>();
        CreateMap<PecaInsumoOrcamento, PecaInsumoOrcamentoResponse>();
    }
}
