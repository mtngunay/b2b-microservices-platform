using AutoMapper;
using B2B.Application.DTOs;
using B2B.Domain.Entities;

namespace B2B.Application.Mappings;

/// <summary>
/// AutoMapper profile for Permission entity mappings.
/// </summary>
public class PermissionMappingProfile : Profile
{
    public PermissionMappingProfile()
    {
        CreateMap<Permission, PermissionDto>();
    }
}
