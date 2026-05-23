using Microsoft.Extensions.Logging;
using SportsLeague.Domain.Entities;
using SportsLeague.Domain.Enums;
using SportsLeague.Domain.Helpers;
using SportsLeague.Domain.Interfaces.Repositories;
using SportsLeague.Domain.Interfaces.Services;

namespace SportsLeague.Domain.Services;

public class MatchLineupService : IMatchLineupService
{
    private readonly IMatchLineupRepository _lineupRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly MatchValidationHelper _validationHelper;
    private readonly ILogger<MatchLineupService> _logger;

    public MatchLineupService(
        IMatchLineupRepository lineupRepository,
        IMatchRepository matchRepository,
        IPlayerRepository playerRepository,
        MatchValidationHelper validationHelper,
        ILogger<MatchLineupService> logger)
    {
        _lineupRepository = lineupRepository;
        _matchRepository = matchRepository;
        _playerRepository = playerRepository;
        _validationHelper = validationHelper;
        _logger = logger;
    }

    public async Task<MatchLineup> AddPlayerAsync(int matchId, MatchLineup lineup)
    {
        // V1 — El partido debe existir
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match == null)
            throw new KeyNotFoundException(
                $"No se encontró el partido con ID {matchId}");

        // V6 — El partido debe estar en estado Scheduled
        if (match.Status != MatchStatus.Scheduled)
            throw new InvalidOperationException(
                "Solo se pueden registrar alineaciones en partidos Scheduled");

        // V2 — El jugador debe existir
        var player = await _playerRepository.GetByIdAsync(lineup.PlayerId);
        if (player == null)
            throw new KeyNotFoundException(
                $"No se encontró el jugador con ID {lineup.PlayerId}");

        // V3 — El jugador debe pertenecer al HomeTeam o AwayTeam
        if (player.TeamId != match.HomeTeamId && player.TeamId != match.AwayTeamId)
            throw new InvalidOperationException(
                "El jugador no pertenece a ninguno de los equipos del partido");

        // V4 — El jugador no puede estar registrado dos veces en la misma alineación
        var alreadyRegistered = await _lineupRepository
            .ExistsByMatchAndPlayerAsync(matchId, lineup.PlayerId);
        if (alreadyRegistered)
            throw new InvalidOperationException(
                "El jugador ya está registrado en la alineación de este partido");

        // V5 — Máximo 11 titulares por equipo por partido (solo aplica a IsStarter = true)
        if (lineup.IsStarter)
        {
            var starterCount = await _lineupRepository
                .CountStartersByMatchAndTeamAsync(matchId, player.TeamId, isStarter: true);

            if (starterCount >= 11)
                throw new InvalidOperationException(
                    "El equipo ya tiene 11 titulares registrados en este partido");
        }

        lineup.MatchId = matchId;

        _logger.LogInformation(
            "Adding player {PlayerId} to lineup of match {MatchId} — Starter: {IsStarter}, Position: {Position}",
            lineup.PlayerId, matchId, lineup.IsStarter, lineup.Position);

        return await _lineupRepository.CreateAsync(lineup);
    }

    public async Task<IEnumerable<MatchLineup>> GetLineupByMatchAsync(int matchId)
    {
        // V1 — El partido debe existir
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match == null)
            throw new KeyNotFoundException(
                $"No se encontró el partido con ID {matchId}");

        _logger.LogInformation("Retrieving full lineup for match {MatchId}", matchId);
        return await _lineupRepository.GetByMatchAsync(matchId);
    }

    public async Task<IEnumerable<MatchLineup>> GetLineupByMatchAndTeamAsync(int matchId, int teamId)
    {
        // V1 — El partido debe existir
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match == null)
            throw new KeyNotFoundException(
                $"No se encontró el partido con ID {matchId}");

        // Verificar que el equipo sea uno de los dos del partido
        if (teamId != match.HomeTeamId && teamId != match.AwayTeamId)
            throw new InvalidOperationException(
                "El equipo no participa en este partido");

        _logger.LogInformation(
            "Retrieving lineup for match {MatchId} — team {TeamId}", matchId, teamId);
        return await _lineupRepository.GetByMatchAndTeamAsync(matchId, teamId);
    }

    public async Task DeletePlayerAsync(int matchId, int lineupId)
    {
        var lineup = await _lineupRepository.GetByIdAsync(lineupId);
        if (lineup == null || lineup.MatchId != matchId)
            throw new KeyNotFoundException(
                $"No se encontró el registro de alineación con ID {lineupId}");

        _logger.LogInformation(
            "Removing lineup entry {LineupId} from match {MatchId}", lineupId, matchId);
        await _lineupRepository.DeleteAsync(lineupId);
    }
}