using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Bids;

namespace CarAuction.Application.Services
{
    public class ConflictResolver : IConflictResolver
    {
        public Task ResolveConflictingBidsAsync(List<Bid> allBids, Region region)
        {
            try
            {
                if (!allBids.Any())
                {
                    return Task.CompletedTask;
                }

                var regionBids = allBids.Where(b => b.OriginRegion == region).ToList();

                if (!regionBids.Any())
                    return Task.CompletedTask;

                // o nao uso de metodos LINQ aqui é intencional para evitar criação de listas temporárias
                // criando novas referencias dos objetos Bid na memória
                // ordena os mesmos objetos por data e sequência (diretamente na lista)
                regionBids.Sort((a, b) =>
                {
                    var result = a.CreatedAt.CompareTo(b.CreatedAt);

                    return result != 0
                        ? result
                        : a.Sequence.CompareTo(b.Sequence);
                });

                // marca o primeiro como aceito item da lista como aceito
                regionBids[0].Accept();

                // os demais como rejeitados
                for (int i = 1; i < regionBids.Count; i++)
                    regionBids[i].Reject($"Lost in conflict resolution by region: {regionBids[i].OriginRegion}");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in ResolveConflictingBidsAsync: {ex.Message}");
                throw;
            }
           
        }

        public Task<Bid> DetermineFinalWinnerAsync(List<Bid> allBids)
        {
            try
            {
                var acceptedBids = allBids.Where(b => b.IsAccepted).ToList();

                if (!acceptedBids.Any())
                {
                    return Task.FromResult<Bid>(null);
                }

                // o maior lance aceito vence
                // desempate por timestamp e sequência
                var winner = acceptedBids
                    .OrderByDescending(b => b.Amount)
                    .ThenBy(b => b.CreatedAt)
                    .ThenBy(b => b.Sequence)
                    .First();

                // marca o vencedor como aceito
                winner.Accept();

                // os outros como rejeitados
                foreach (var bid in acceptedBids.Where(b => b != winner))
                {
                    bid.Reject("Lost in global conflict resolution");
                }

                return Task.FromResult(winner);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in DetermineFinalWinnerAsync: {ex.Message}");
                throw;
            }            
        }
    }
}
