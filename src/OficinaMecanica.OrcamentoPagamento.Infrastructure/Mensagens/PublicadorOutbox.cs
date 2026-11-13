using Microsoft.EntityFrameworkCore;
using OficinaMecanica.OrcamentoPagamento.Application.Mensagens;
using OficinaMecanica.OrcamentoPagamento.Infrastructure.Persistence;
namespace OficinaMecanica.OrcamentoPagamento.Infrastructure.Mensagens;

public sealed class PublicadorOutbox
{
    private readonly OrcamentoPagamentoDbContext _dbContext;
    private readonly ITransporteMensagem _transporte;
    private readonly TimeProvider _relogio;
    public PublicadorOutbox(OrcamentoPagamentoDbContext dbContext, ITransporteMensagem transporte, TimeProvider relogio)
    { _dbContext = dbContext; _transporte = transporte; _relogio = relogio; }
    public async Task<bool> PublicarProximaAsync(CancellationToken cancellationToken = default)
    {
        var agora = _relogio.GetUtcNow();
        var registros = _dbContext.Set<RegistroOutbox>();
        // Preservar a ordem pendente: não ultrapassar uma publicação ainda incerta.
        var pendente = await registros.AsNoTracking().Where(r => r.PublicadoEm == null).OrderBy(r => r.CriadoEm).ThenBy(r => r.Id).FirstOrDefaultAsync(cancellationToken);
        if (pendente is null || pendente.LeaseAte > agora || pendente.ProximaTentativaEm > agora) { return false; }
        var responsavel = Guid.NewGuid();
        var alterados = await registros.Where(r => r.Id == pendente.Id && r.PublicadoEm == null && (r.LeaseAte == null || r.LeaseAte <= agora))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Responsavel, responsavel).SetProperty(r => r.LeaseAte, agora.AddMinutes(1)), cancellationToken);
        if (alterados == 0) { return false; }
        try
        {
            await _transporte.PublicarAsync("OrdemServico", pendente.Corpo, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            var tentativa = pendente.Tentativas + 1;
            await registros.Where(r => r.Id == pendente.Id && r.Responsavel == responsavel && r.PublicadoEm == null)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Responsavel, (Guid?)null).SetProperty(r => r.LeaseAte, (DateTimeOffset?)null)
                    .SetProperty(r => r.Tentativas, tentativa).SetProperty(r => r.ProximaTentativaEm, _relogio.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(tentativa, 8))))), cancellationToken);
            return false;
        }
        // Falha após envio mantém corpo e ID para reentrega; nunca confirma antecipadamente.
        var confirmados = await registros.Where(r => r.Id == pendente.Id && r.Responsavel == responsavel && r.PublicadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.PublicadoEm, _relogio.GetUtcNow()).SetProperty(r => r.Responsavel, (Guid?)null)
                .SetProperty(r => r.LeaseAte, (DateTimeOffset?)null), cancellationToken);
        return confirmados == 1;
    }
}
