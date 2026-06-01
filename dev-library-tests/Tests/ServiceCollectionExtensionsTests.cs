using Microsoft.Extensions.DependencyInjection;
using dev_library;
using dev_refined;
using dev_refined.Clients;

namespace dev_library_tests.Tests;

public class ServiceCollectionExtensionsTests
{
    // ─── AddDevLibraryRepositories ────────────────────────────────────────────

    [Fact]
    public void AddDevLibraryRepositories_RegistersIAppChannelRepository()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryRepositories("Server=dummy;");
        Assert.Contains(services, sd => sd.ServiceType == typeof(IAppChannelRepository));
    }

    [Fact]
    public void AddDevLibraryRepositories_RegistersIGuildRepository()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryRepositories("Server=dummy;");
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGuildRepository));
    }

    [Fact]
    public void AddDevLibraryRepositories_RegistersIFitnessRepository()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryRepositories("Server=dummy;");
        Assert.Contains(services, sd => sd.ServiceType == typeof(IFitnessRepository));
    }

    [Fact]
    public void AddDevLibraryRepositories_RegistersIJobRepository()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryRepositories("Server=dummy;");
        Assert.Contains(services, sd => sd.ServiceType == typeof(dev_library.Data.IJobRepository));
    }

    [Fact]
    public void AddDevLibraryRepositories_AllRepositoriesAreSingletons()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryRepositories("Server=dummy;");

        var repoTypes = new[]
        {
            typeof(IAppChannelRepository),
            typeof(IGuildRepository),
            typeof(IFitnessRepository),
            typeof(dev_library.Data.IJobRepository),
        };

        foreach (var type in repoTypes)
        {
            var descriptor = services.First(sd => sd.ServiceType == type);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }
    }

    // ─── AddDevLibraryClients ─────────────────────────────────────────────────

    [Fact]
    public void AddDevLibraryClients_RegistersIBattleNetClient()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryClients();
        Assert.Contains(services, sd => sd.ServiceType == typeof(IBattleNetClient));
    }

    [Fact]
    public void AddDevLibraryClients_RegistersIWoWAuditClient()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryClients();
        Assert.Contains(services, sd => sd.ServiceType == typeof(IWoWAuditClient));
    }

    [Fact]
    public void AddDevLibraryClients_RegistersIDiscordClient()
    {
        var services = new ServiceCollection();
        services.AddDevLibraryClients();
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDiscordClient));
    }
}
