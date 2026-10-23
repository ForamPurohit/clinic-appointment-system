using ClinicAppointment.API.Data;
using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.API.Services;

public interface ISpecializationService
{
    Task<IReadOnlyList<SpecializationDto>> GetAllAsync();
    Task<SpecializationDto> CreateAsync(SpecializationRequest request);
    Task<SpecializationDto> UpdateAsync(int id, SpecializationRequest request);
    Task DeleteAsync(int id);
}

public class SpecializationService : ISpecializationService
{
    private readonly AppDbContext _db;

    public SpecializationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SpecializationDto>> GetAllAsync() =>
        await _db.Specializations
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SpecializationDto(s.Id, s.Name, s.Description, s.Doctors.Count(d => d.IsActive)))
            .ToListAsync();

    public async Task<SpecializationDto> CreateAsync(SpecializationRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.Specializations.AnyAsync(s => s.Name == name))
        {
            throw new ConflictException($"Specialization '{name}' already exists.");
        }

        var entity = new Specialization { Name = name, Description = request.Description };
        _db.Specializations.Add(entity);
        await _db.SaveChangesAsync();

        return new SpecializationDto(entity.Id, entity.Name, entity.Description, 0);
    }

    public async Task<SpecializationDto> UpdateAsync(int id, SpecializationRequest request)
    {
        var entity = await _db.Specializations.Include(s => s.Doctors).FirstOrDefaultAsync(s => s.Id == id)
                     ?? throw new NotFoundException("Specialization not found.");

        var name = request.Name.Trim();
        if (await _db.Specializations.AnyAsync(s => s.Name == name && s.Id != id))
        {
            throw new ConflictException($"Specialization '{name}' already exists.");
        }

        entity.Name = name;
        entity.Description = request.Description;
        await _db.SaveChangesAsync();

        return new SpecializationDto(entity.Id, entity.Name, entity.Description, entity.Doctors.Count(d => d.IsActive));
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Specializations.FindAsync(id)
                     ?? throw new NotFoundException("Specialization not found.");

        if (await _db.Doctors.AnyAsync(d => d.SpecializationId == id))
        {
            throw new BadRequestException("Reassign this specialization's doctors before deleting it.");
        }

        _db.Specializations.Remove(entity);
        await _db.SaveChangesAsync();
    }
}
