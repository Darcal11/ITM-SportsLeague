using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SportsLeague.Domain.Entities;
using SportsLeague.Domain.Interfaces.Repositories;
using SportsLeague.Domain.Interfaces.Services;

namespace SportsLeague.Domain.Services;

public class SponsorService : ISponsorService
{
    private readonly ISponsorRepository _sponsorRepository;
    private readonly ITournamentSponsorRepository _tournamentSponsorRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly ILogger<SponsorService> _logger;

    public SponsorService(
        ISponsorRepository sponsorRepository,
        ITournamentSponsorRepository tournamentSponsorRepository,
        ITournamentRepository tournamentRepository,
        ILogger<SponsorService> logger)
    {
        _sponsorRepository = sponsorRepository;
        _tournamentSponsorRepository = tournamentSponsorRepository;
        _tournamentRepository = tournamentRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<Sponsor>> GetAllAsync()
    {
        _logger.LogInformation("Retrieving all sponsors");
        return await _sponsorRepository.GetAllAsync();
    }

    public async Task<Sponsor?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving sponsor with ID: {SponsorId}", id);
        var sponsor = await _sponsorRepository.GetByIdAsync(id);
        if (sponsor == null)
            _logger.LogWarning("Sponsor with ID {SponsorId} not found", id);
        return sponsor;
    }

    public async Task<Sponsor> CreateAsync(Sponsor sponsor)
    {
        // Validate duplicate name
        var nameExists = await _sponsorRepository.ExistsByNameAsync(sponsor.Name);
        if (nameExists)
            throw new InvalidOperationException(
                $"Ya existe un patrocinador con el nombre '{sponsor.Name}'");

        // Validate email format
        if (!IsValidEmail(sponsor.ContactEmail))
            throw new InvalidOperationException(
                $"El formato del correo electrónico '{sponsor.ContactEmail}' no es válido");

        _logger.LogInformation("Creating sponsor: {SponsorName}", sponsor.Name);
        return await _sponsorRepository.CreateAsync(sponsor);
    }

    public async Task UpdateAsync(int id, Sponsor sponsor)
    {
        var existing = await _sponsorRepository.GetByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"No se encontró el patrocinador con ID {id}");

        // Validate duplicate name (exclude the current record)
        var nameExists = await _sponsorRepository.ExistsByNameAsync(sponsor.Name);
        if (nameExists && !existing.Name.ToLower().Equals(sponsor.Name.ToLower()))
            throw new InvalidOperationException(
                $"Ya existe un patrocinador con el nombre '{sponsor.Name}'");

        // Validate email format
        if (!IsValidEmail(sponsor.ContactEmail))
            throw new InvalidOperationException(
                $"El formato del correo electrónico '{sponsor.ContactEmail}' no es válido");

        existing.Name = sponsor.Name;
        existing.ContactEmail = sponsor.ContactEmail;
        existing.Phone = sponsor.Phone;
        existing.WebsiteUrl = sponsor.WebsiteUrl;
        existing.Category = sponsor.Category;

        _logger.LogInformation("Updating sponsor with ID: {SponsorId}", id);
        await _sponsorRepository.UpdateAsync(existing);
    }

    public async Task DeleteAsync(int id)
    {
        var exists = await _sponsorRepository.ExistsAsync(id);
        if (!exists)
            throw new KeyNotFoundException($"No se encontró el patrocinador con ID {id}");

        _logger.LogInformation("Deleting sponsor with ID: {SponsorId}", id);
        await _sponsorRepository.DeleteAsync(id);
    }

    public async Task<TournamentSponsor> LinkToTournamentAsync(int sponsorId, int tournamentId, decimal contractAmount)
    {
        // Validate sponsor exists
        var sponsor = await _sponsorRepository.GetByIdAsync(sponsorId);
        if (sponsor == null)
            throw new KeyNotFoundException(
                $"No se encontró el patrocinador con ID {sponsorId}");

        // Validate tournament exists
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null)
            throw new KeyNotFoundException(
                $"No se encontró el torneo con ID {tournamentId}");

        // Validate ContractAmount > 0
        if (contractAmount <= 0)
            throw new InvalidOperationException(
                "El monto del contrato debe ser mayor a 0");

        // Validate no duplicate link
        var existing = await _tournamentSponsorRepository
            .GetByTournamentAndSponsorAsync(tournamentId, sponsorId);
        if (existing != null)
            throw new InvalidOperationException(
                "Este patrocinador ya está vinculado a ese torneo");

        var tournamentSponsor = new TournamentSponsor
        {
            SponsorId = sponsorId,
            TournamentId = tournamentId,
            ContractAmount = contractAmount,
            JoinedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Linking sponsor {SponsorId} to tournament {TournamentId}",
            sponsorId, tournamentId);

        return await _tournamentSponsorRepository.CreateAsync(tournamentSponsor);
    }

    public async Task UnlinkFromTournamentAsync(int sponsorId, int tournamentId)
    {
        // Validate sponsor exists
        var sponsorExists = await _sponsorRepository.ExistsAsync(sponsorId);
        if (!sponsorExists)
            throw new KeyNotFoundException(
                $"No se encontró el patrocinador con ID {sponsorId}");

        // Validate tournament exists
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null)
            throw new KeyNotFoundException(
                $"No se encontró el torneo con ID {tournamentId}");

        // Validate the link exists
        var link = await _tournamentSponsorRepository
            .GetByTournamentAndSponsorAsync(tournamentId, sponsorId);
        if (link == null)
            throw new KeyNotFoundException(
                "Este patrocinador no está vinculado a ese torneo");

        _logger.LogInformation(
            "Unlinking sponsor {SponsorId} from tournament {TournamentId}",
            sponsorId, tournamentId);

        await _tournamentSponsorRepository.DeleteAsync(link.Id);
    }

    public async Task<IEnumerable<TournamentSponsor>> GetTournamentsBySponsorAsync(int sponsorId)
    {
        var sponsor = await _sponsorRepository.GetByIdAsync(sponsorId);
        if (sponsor == null)
            throw new KeyNotFoundException(
                $"No se encontró el patrocinador con ID {sponsorId}");

        return await _tournamentSponsorRepository.GetBySponsorIdAsync(sponsorId);
    }

    // ── Private helpers ──

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase);
    }
}