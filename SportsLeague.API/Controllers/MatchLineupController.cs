using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using SportsLeague.API.DTOs.Request;
using SportsLeague.API.DTOs.Response;
using SportsLeague.Domain.Entities;
using SportsLeague.Domain.Interfaces.Services;

namespace SportsLeague.API.Controllers;

[ApiController]
[Route("api/match/{matchId}/lineup")]
public class MatchLineupController : ControllerBase
{
    private readonly IMatchLineupService _lineupService;
    private readonly IMapper _mapper;

    public MatchLineupController(
        IMatchLineupService lineupService,
        IMapper mapper)
    {
        _lineupService = lineupService;
        _mapper = mapper;
    }

    /// <summary>Agrega un jugador a la alineación del partido.</summary>
    [HttpPost]
    public async Task<ActionResult<MatchLineupDto>> AddPlayer(
        int matchId, CreateMatchLineupDto dto)
    {
        try
        {
            var lineup = _mapper.Map<MatchLineup>(dto);
            var created = await _lineupService.AddPlayerAsync(matchId, lineup);

            // Recargar con Navigation Properties para mapear PlayerName y TeamName
            var fullLineup = await _lineupService.GetLineupByMatchAsync(matchId);
            var createdFull = fullLineup.FirstOrDefault(l => l.Id == created.Id);

            return CreatedAtAction(
                nameof(GetLineup),
                new { matchId },
                _mapper.Map<MatchLineupDto>(createdFull));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Obtiene la alineación completa del partido.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MatchLineupDto>>> GetLineup(int matchId)
    {
        try
        {
            var lineup = await _lineupService.GetLineupByMatchAsync(matchId);
            return Ok(_mapper.Map<IEnumerable<MatchLineupDto>>(lineup));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Obtiene la alineación de un equipo específico dentro del partido.</summary>
    [HttpGet("team/{teamId}")]
    public async Task<ActionResult<IEnumerable<MatchLineupDto>>> GetLineupByTeam(
        int matchId, int teamId)
    {
        try
        {
            var lineup = await _lineupService.GetLineupByMatchAndTeamAsync(matchId, teamId);
            return Ok(_mapper.Map<IEnumerable<MatchLineupDto>>(lineup));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Elimina un jugador de la alineación.</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePlayer(int matchId, int id)
    {
        try
        {
            await _lineupService.DeletePlayerAsync(matchId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}