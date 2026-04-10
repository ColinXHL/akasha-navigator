using AkashaNavigator.Core;
using AkashaNavigator.Core.Interfaces;
using AkashaNavigator.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AkashaNavigator.Tests.Architecture;

public class ServiceRegistrationTests
{
    [Fact]
    public void ConfigureAppServices_ResolvesSameProfileManagerInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IProfileManager>();
        var second = provider.GetRequiredService<IProfileManager>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureAppServices_ResolvesSamePluginHostInstanceEachTime()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IPluginHost>();
        var second = provider.GetRequiredService<IPluginHost>();

        Assert.Same(first, second);
    }

    [Fact]
    public void ConfigureAppServices_RegistersOverlayManagerAsSingletonByType()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IOverlayManager));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(OverlayManager), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_RegistersPanelManagerAsSingletonByType()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();

        var descriptor = services.Single(d => d.ServiceType == typeof(IPanelManager));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(PanelManager), descriptor.ImplementationType);
    }

    [Fact]
    public void ConfigureAppServices_ShouldResolveHotkeyManager()
    {
        var services = new ServiceCollection();
        services.ConfigureAppServices();
        using var provider = services.BuildServiceProvider();

        var manager = provider.GetRequiredService<HotkeyManager>();
        Assert.NotNull(manager);
    }
}
