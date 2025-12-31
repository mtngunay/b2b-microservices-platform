using AutoMapper;
using B2B.Application.DTOs;
using B2B.Domain.Entities;

namespace B2B.Application.Mappings;

/// <summary>
/// AutoMapper profile for Role entity mappings.
/// </summary>
public class RoleMappingProfile : Profile
{
    public RoleMappingProfile()
    {
        CreateMap<Role, RoleDto>()
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => 
                src.RolePermissions
                    .Where(rp => rp.Permission != null)
                    .Select(rp => rp.Permission!.Name)
                    .ToList()));
    }
}
