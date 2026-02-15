using MexClub.Application.Interfaces;
using MexClub.Domain.Interfaces;
using MexClub.Infrastructure.Data;
using MexClub.Infrastructure.Repositories;
using MexClub.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MexClub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MexClubDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("MexClubDb")
                    ?? throw new InvalidOperationException("Connection string 'MexClubDb' not found.")
            )
        );

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
