using CarAuction.Domain.Abstractions;
using CarAuction.Domain.Abstractions.Repositories;
using CarAuction.Domain.Abstractions.Requests;
using CarAuction.Domain.Abstractions.Results;
using CarAuction.Domain.Models;
using CarAuction.Domain.Models.Auctions;
using CarAuction.Domain.Models.Bids;
using CarAuction.Domain.Models.Partitions;

namespace CarAuction.Application.Services
{
    public class AuctionService : IAuctionService
    {
        private readonly IAuctionRepository _auctionRepository;
        private readonly IBidRepository _bidRepository;
        private readonly IBidOrderingService _bidOrderingService;
        private readonly IRegionCoordinator _regionCoordinator;
        private readonly IConflictResolver _conflictResolver;
        private readonly IPartitionSimulator _partitionSimulator;

        public AuctionService(
            IAuctionRepository auctionRepository,
            IBidRepository bidRepository,
            IBidOrderingService bidOrderingService,
            IRegionCoordinator regionCoordinator,
            IConflictResolver conflictResolver,
            IPartitionSimulator partitionSimulator)
        {
            _auctionRepository = auctionRepository;
            _bidRepository = bidRepository;
            _bidOrderingService = bidOrderingService;
            _regionCoordinator = regionCoordinator;
            _conflictResolver = conflictResolver;
            _partitionSimulator = partitionSimulator;

            _regionCoordinator.PartitionDetected += OnPartitionDetectedHandler;
            _regionCoordinator.PartitionHealed += OnPartitionHealedHandler;
        }

        public async Task<Auction> CreateAuctionAsync(CreateAuctionRequest request)
        {
            try
            {
                var auction = new Auction(
                    request.VehicleId,
                    request.Region,
                    request.StartingPrice,
                    request.ReservePrice,
                    request.StartTime,
                    request.EndTime
                );

                auction.Start();

                await _auctionRepository.AddAsync(auction);

                return auction;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to create auction: {ex.Message}");
                throw;
            }
        }

        public async Task<BidResult> PlaceBidAsync(Guid auctionId, BidRequest request)
        {
            try
            {
                // captura o leilão para o lance o correspondente
                var auction = await _auctionRepository.GetWithBidsAsync(auctionId);

                if (auction is null)
                {
                    return BuildBidResult(false, "Auction not found", null);
                }

                if (auction.State is not AuctionState.Active)
                {
                    return BuildBidResult(false, $"Auction is not active. Current state: {auction.State}", null);
                }

                if (CheckIfAuctionIsHasToBeEnded(auction))
                {
                    auction.End();
                    await _auctionRepository.UpdateAsync(auction);

                    return BuildBidResult(true, "Auction ended successfully", null);
                }

                var currentRegion = await GetCurrentRegionAsync();

                // verifica se o sistema está em estado particionado
                // mas é a onde o usuario esta tentando fazer o lance
                var isCurrentBidRegionPartitioned = await _regionCoordinator.GetPartitionStatusAsync() == PartitionStatus.Partitioned;

                // se a região atual do lance está particionada, retornar erro
                if (isCurrentBidRegionPartitioned)
                {
                    return BuildBidResult(false, "Current region is partitioned. Cannot place bid at this time.", null);
                }

                // verifica se a região do leilão está particionada
                var isAuctionRegionPartitioned = await _regionCoordinator.IsRegionReachableAsync(auction.Region) == false;

                // verifica se o leilão é particionado e se a região atual é diferente da região do leilão
                // esta particionado
                // e a região do leilão é diferente da região atual

                // isso usa o conceito de consistência eventual - AP - Availability + Partition Tolerance
                if (CheckIfAuctionRegionIsPartitionedAndIfAuctionRegionIsDifferentOfCurrentBidRegion(
                    isAuctionRegionPartitioned,
                    auction.Region,
                    currentRegion))
                {
                    return await HandlePartitionedBidAsync(auction, request, currentRegion);
                }

                // a partir daqui, estamos em um cenário consistência forte - CP - Consistency + Partition Tolerance
                if (CheckIfAuctionRegionIsPartitionedAndIfAuctionRegionIsEqualsOfCurrentBidRegion(
                    isAuctionRegionPartitioned,
                    auction.Region,
                    currentRegion))
                {
                    return BuildBidResult(false, "Auction region is partitioned. Cannot place bid at this time.", null);
                }

                // vai garantir que todas operações ocorrem na região do leilão
                return await EnsuringTheAuctionRegionInWorkingWell(auctionId, request, auction, currentRegion);
            }
            catch (Exception ex)
            {
                var messageError = $"Failed to place bid for Auction {auctionId}: {ex.Message}";

                Console.Error.WriteLine(messageError);

                // Retorna resultado padrão de erro
                return BuildBidResult(false, messageError, null);
            }
        }
 
        public async Task<Auction> GetAuctionAsync(Guid auctionId, ConsistencyLevel consistency)
        {
            try
            {
                // consistência forte - dados atualizados - CP
                if (consistency == ConsistencyLevel.Strong)
                {
                    return await _auctionRepository.GetWithBidsAsync(auctionId);
                }

                // consistência eventual - dados podem estar desatualizados - AP
                return await _auctionRepository.GetByIdAsync(auctionId);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to get auction {auctionId}: {ex.Message}");
                throw;
            }
        }

        public async Task<ReconciliationResult> ReconcileAuctionAsync(Guid auctionId)
        {
            try
            {
                // carrega o leilão com os lances existentes
                var auction = await _auctionRepository.GetWithBidsAsync(auctionId);

                if (auction is null
                    || auction.State != AuctionState.Paused)
                {
                    return new ReconciliationResult { Success = false };
                }

                // retorna os lances feitos durante a partição, mas tenham sido feitos antes do prazo final do leilão
                var partitionBids = await _bidRepository.GetBidsMadeDuringPartitionWithinAuctionDeadlineAsync(auctionId, auction.EndTime);

                // se não houver lances particionados para reconciliar
                if (!partitionBids.Any())
                {
                    if (CheckIfAuctionIsHasToBeEnded(auction))
                    {
                        auction.End();
                    }
                    else
                    {
                        auction.Resume();
                    }

                    await _auctionRepository.UpdateAsync(auction);

                    return new ReconciliationResult
                    {
                        Success = true,
                        BidsReconciled = 0,
                        WinnerId = auction.WinningBidderId,
                        Price = auction.CurrentPrice
                    };
                }

                // faz junção dos lances normais + particionados
                var allBids = auction.Bids
                    .Concat(partitionBids)
                    .OrderBy(b => b.CreatedAt)
                    .ThenBy(b => b.Sequence)
                    .ToList();

                // resolve conflitos locais - atualiza os lances por referência 
                // por regiao de origem dos lances
                foreach (var region in Enum.GetValues(typeof(Region)).Cast<Region>())
                {
                    await _conflictResolver.ResolveConflictingBidsAsync(allBids, region);
                }

                // determina vencedor final com base nas regras (valor -> tempo -> sequência)
                var winningBid = await _conflictResolver.DetermineFinalWinnerAsync(allBids);

                // Atualiza estado do leilão, se houver vencedor
                if (winningBid is not null)
                {
                    auction.UpdateWinningBid(winningBid.Amount, winningBid.BidderId);
                }

                if (CheckIfAuctionIsHasToBeEnded(auction))
                {
                    auction.End();
                }
                else
                {
                    auction.Resume();
                }

                await _auctionRepository.UpdateAsync(auction);

                // Retorna resultado para fins de auditoria
                return new ReconciliationResult
                {
                    Success = true,
                    BidsReconciled = allBids.Count,
                    WinnerId = winningBid?.BidderId,
                    Price = winningBid?.Amount
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to reconcile auction {auctionId}: {ex.Message}");
                return new ReconciliationResult { Success = false };
            }
        }

        private async Task<BidResult> EnsuringTheAuctionRegionInWorkingWell(
            Guid auctionId,
            BidRequest request,
            Auction auction,
            Region currentRegion)
        {
            return await _regionCoordinator.ExecuteInRegionAsync(auction.Region, async () =>
            {
                // usado para garantir a ordem dos lances mesmo - vai pegar sempre o próximo e atualizar a sequencia
                var sequence = await _bidOrderingService.GetNextBidSequenceAsync(auctionId);

                var bid = new Bid(auctionId, request.BidderId, request.Amount, currentRegion, sequence);

                // valida o valor do lance em relação ao leilão atual e a ordem dos lances
                var acceptance = await _bidOrderingService.ValidateBidOrderAsync(auctionId, bid);

                // se o lance não for válido, rejeita e retorna o motivo
                if (!acceptance.IsValid)
                {
                    bid.Reject(acceptance.Reason);

                    // registra o lance rejeitado
                    await _bidRepository.AddAsync(bid);

                    return BuildBidResult(false, acceptance.Reason, bid);
                }

                // tenta colocar o lance no leilão
                var placed = auction.TryPlaceBid(bid);

                // se não conseguiu colocar o lance, rejeita e informa o motivo
                if (!placed)
                {
                    bid.Reject("Bid amount must be higher than current price");

                    // registra o lance rejeitado
                    await _bidRepository.AddAsync(bid);

                    return BuildBidResult(false, "Bid amount must be higher than current price", bid);
                }

                // aceita o lance
                bid.Accept();

                // registra o lance aceito
                await _bidRepository.AddAsync(bid);

                // atualiza o leilão com o novo lance
                await _auctionRepository.UpdateAsync(auction);

                return BuildBidResult(true, "Bid placed successfully", bid);
            });
        }

        private static bool CheckIfAuctionRegionIsPartitionedAndIfAuctionRegionIsDifferentOfCurrentBidRegion(
            bool isPartitioned,
            Region auctionRegion,
            Region currentRegion)
                => isPartitioned && auctionRegion != currentRegion;

        private static bool CheckIfAuctionRegionIsPartitionedAndIfAuctionRegionIsEqualsOfCurrentBidRegion(
            bool isPartitioned,
            Region auctionRegion,
            Region currentRegion)
                => isPartitioned && auctionRegion == currentRegion;

        private async Task<BidResult> HandlePartitionedBidAsync(
            Auction auction,
            BidRequest request,
            Region currentRegion)
        {
            try
            {                
                // usado para garantir a ordem dos lances mesmo - vai pegar sempre o próximo e atualizar a sequencia
                var sequence = await _bidOrderingService.GetNextBidSequenceAsync(auction.Id);

                var bid = new Bid(auction.Id, request.BidderId, request.Amount, currentRegion, sequence);

                // marca o leilao para reconciliação posterior
                auction.Pause();

                // marca o lance como durante a partição
                bid.MarkAsDuringPartition();

                await _bidRepository.AddAsync(bid);

                // atualiza o status da partição e garante persistência e eventos
                await _regionCoordinator.AddPartitionAsync(currentRegion, auction.Region);

                return BuildBidResult(true, "Bid queued for reconciliation after partition heals", bid);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error handling partitioned bid: {ex.Message}. AuctionId: {auction.Id}");
                throw;
            }
        }

        private async Task<Region> GetCurrentRegionAsync()
        {
            return await _partitionSimulator.GetCurrentRegionAsync();
        }

        private async void OnPartitionDetectedHandler(object sender, PartitionEventArgs e)
        {
            try
            {
                Console.WriteLine($"Partition detected between regions {e.OriginBidRegion} and {e.AuctionRegion} at {e.CreatedAt}.");

                var activeAuctions = await _auctionRepository.GetActiveAuctionsByRegionAsync(e.AuctionRegion);

                foreach (var auction in activeAuctions)
                {
                    // Se o leilão estiver na região isolada na partição, pausar o leilão para evitar novos lances
                    if (auction.Region == e.AuctionRegion)
                    {
                        if (auction.State == AuctionState.Active)
                        {
                            auction.Pause();

                            await _auctionRepository.UpdateAsync(auction);

                            Console.WriteLine($"Auction {auction.Id} paused due to partition {auction.Region}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erro no handler de partição detectada: {ex.Message}");
            }
        }

        private async void OnPartitionHealedHandler(object sender, PartitionEventArgs e)
        {
            try
            {
                // log no proprio console que a partição entre as regiões foi curada
                Console.WriteLine($"Partition healed between regions {e.OriginBidRegion} and {e.AuctionRegion} at {e.CreatedAt}");

                await _regionCoordinator.UpdatePartitionByAuctionRegionAsync(e.AuctionRegion, PartitionStatus.Reconciling);

                //continuar daqui 
                //ver como faco para apanhar o evento de partição correto, penso que deve ser baseado no auction region
                //com isso
                //marcar o evento de partição como reconcialiando

                // Obter lista de leilões impactados            
                var affectedAuctions = await _auctionRepository.GetAuctionsThatNeedsReconciliationByRegionAsync(e.AuctionRegion);

                // Para cada leilão, realiza o processo de reconciliação para sincronizar os bids locais e remotos
                foreach (var auction in affectedAuctions)
                {
                    var reconciliationResult = await ReconcileAuctionAsync(auction.Id);

                    // Log resultado ou atualiza estados conforme necessário
                    Console.WriteLine($"Reconciled auction {auction.Id} with result: {reconciliationResult.Success}");
                }

                await _regionCoordinator.UpdatePartitionByAuctionRegionAsync(e.AuctionRegion, PartitionStatus.Resolved);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Erro durante OnPartitionHealedHandler: {ex.Message}");

                await _regionCoordinator.UpdatePartitionByAuctionRegionAsync(e.AuctionRegion, PartitionStatus.Partitioned);
            }
        }

        private static TimeZoneInfo GetTimeZoneForRegion(Region region) => region switch
        {
            Region.USEast => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
            Region.EUWest => TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"),
            _ => TimeZoneInfo.Utc
        };

        private static bool CheckIfAuctionIsHasToBeEnded(Auction auction)
        {
            TimeZoneInfo auctionTimeZone = GetTimeZoneForRegion(auction.Region);

            DateTime currentTimeInAuctionRegion = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, auctionTimeZone);

            return auction.EndTime < currentTimeInAuctionRegion;
        }

        private static BidResult BuildBidResult(bool success, string message, Bid? bid) => new()
        {
            Success = success,
            Message = message,
            Bid = bid
        };
    }
}
