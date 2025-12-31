using AutoMapper;
using B2B.Application.DTOs;
using B2B.Domain.Aggregates;

namespace B2B.Application.Mappings;

/// <summary>
/// AutoMapper profile for User entity mappings.
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => 
                src.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role!.Name)
                    .ToList()))
            .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => 
                src.UserPermissions
                    .Where(up => up.Permission != null)
                    .Select(up => up.Permission!.Name)
                    .ToList()));
    }
}
