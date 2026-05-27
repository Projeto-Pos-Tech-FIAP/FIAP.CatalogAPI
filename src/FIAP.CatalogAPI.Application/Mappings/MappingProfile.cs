using AutoMapper;
using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Domain.Entities;

namespace FIAP.CatalogAPI.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Game, GameOutputDto>()
            .ForMember(dest => dest.Genres, opt =>
                opt.MapFrom(src => src.GameGenres
                    .Where(gg => gg.Genre != null)
                    .Select(gg => gg.Genre!)));

        CreateMap<Genre, GenreOutputDto>();
    }
}
