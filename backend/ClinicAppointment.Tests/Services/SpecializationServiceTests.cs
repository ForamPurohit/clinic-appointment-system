using ClinicAppointment.API.DTOs;
using ClinicAppointment.API.Exceptions;
using ClinicAppointment.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ClinicAppointment.Tests.Services;

/// <summary>Covers UC-11 (manage specialties).</summary>
public class SpecializationServiceTests
{
    private static SpecializationRequest Request(string name, string? description = null) =>
        new() { Name = name, Description = description };

    [Fact]
    public async Task GetAll_SortedByName_CountsOnlyActiveDoctors()
    {
        using var data = new TestData();

        var list = await data.Specializations().GetAllAsync();

        Assert.Equal(new[] { "Cardiology", "General Medicine" }, list.Select(s => s.Name));
        Assert.Equal(0, list[0].DoctorCount);
        Assert.Equal(1, list[1].DoctorCount); // the inactive doctor isn't counted
    }

    [Fact]
    public async Task Create_NewName_IsSavedTrimmed()
    {
        using var data = new TestData();

        var dto = await data.Specializations().CreateAsync(Request("  Neurology  ", "Brain and nerves"));

        Assert.Equal("Neurology", dto.Name);
        Assert.Equal(0, dto.DoctorCount);
        await using var check = data.NewContext();
        Assert.True(await check.Specializations.AnyAsync(s => s.Name == "Neurology"));
    }

    [Fact]
    public async Task Create_ExistingName_ThrowsConflict()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<ConflictException>(() => data.Specializations().CreateAsync(Request(" Cardiology ")));
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        using var data = new TestData();

        var dto = await data.Specializations().UpdateAsync(data.Cardiology.Id, Request("Cardiology & Vascular", "Heart and vessels"));

        Assert.Equal("Cardiology & Vascular", dto.Name);
        Assert.Equal("Heart and vessels", dto.Description);
    }

    [Fact]
    public async Task Update_KeepingSameName_IsAllowed()
    {
        using var data = new TestData();

        var dto = await data.Specializations().UpdateAsync(data.Cardiology.Id, Request("Cardiology", "Updated"));

        Assert.Equal("Updated", dto.Description);
    }

    [Fact]
    public async Task Update_ToAnotherSpecialtysName_ThrowsConflict()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<ConflictException>(() =>
            data.Specializations().UpdateAsync(data.Cardiology.Id, Request("General Medicine")));
    }

    [Fact]
    public async Task Update_Missing_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Specializations().UpdateAsync(999, Request("Anything")));
    }

    [Fact]
    public async Task Delete_SpecialtyWithoutDoctors_RemovesIt()
    {
        using var data = new TestData();

        await data.Specializations().DeleteAsync(data.Cardiology.Id);

        await using var check = data.NewContext();
        Assert.False(await check.Specializations.AnyAsync(s => s.Name == "Cardiology"));
    }

    [Fact]
    public async Task Delete_SpecialtyWithDoctors_ThrowsBadRequest()
    {
        using var data = new TestData();

        await Assert.ThrowsAsync<BadRequestException>(() => data.Specializations().DeleteAsync(data.GeneralMedicine.Id));
    }

    [Fact]
    public async Task Delete_Missing_ThrowsNotFound()
    {
        using var data = new TestData();
        await Assert.ThrowsAsync<NotFoundException>(() => data.Specializations().DeleteAsync(999));
    }
}
