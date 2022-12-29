// Copyright (c) usercode
// https://github.com/usercode/AspNetCore.Decorator
// MIT License

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetCore.Decorator.Tests;

public class DecoratorTests : TestBase
{
    [Fact]
    public void CanDecorateType()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IDecoratedService, Decorated>();

            services.Decorate<IDecoratedService, Decorator>();
        });

        var instance = provider.GetRequiredService<IDecoratedService>();

        var decorator = Assert.IsType<Decorator>(instance);

        Assert.IsType<Decorated>(decorator.Inner);
    }

    [Fact]
    public void CanDecorateMultipleLevels()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IDecoratedService, Decorated>();

            services.Decorate<IDecoratedService, Decorator>();
            services.Decorate<IDecoratedService, Decorator>();
        });

        var instance = provider.GetRequiredService<IDecoratedService>();

        var outerDecorator = Assert.IsType<Decorator>(instance);
        var innerDecorator = Assert.IsType<Decorator>(outerDecorator.Inner);
        _ = Assert.IsType<Decorated>(innerDecorator.Inner);
    }

    [Fact]
    public void CanDecorateDifferentServices()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IDecoratedService, Decorated>();
            services.AddSingleton<IDecoratedService, OtherDecorated>();

            services.Decorate<IDecoratedService, Decorator>();
        });

        var instances = provider
            .GetRequiredService<IEnumerable<IDecoratedService>>()
            .ToArray();

        Assert.Equal(2, instances.Length);
        Assert.All(instances, x => Assert.IsType<Decorator>(x));
    }

    [Fact]
    public void ShouldReplaceExistingServiceDescriptor()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDecoratedService, Decorated>();

        services.Decorate<IDecoratedService, Decorator>();

        var descriptor = services.GetDescriptor<IDecoratedService>();

        Assert.Equal(typeof(IDecoratedService), descriptor.ServiceType);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    [Fact]
    public void CanDecorateExistingInstance()
    {
        var existing = new Decorated();

        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IDecoratedService>(existing);

            services.Decorate<IDecoratedService, Decorator>();
        });

        var instance = provider.GetRequiredService<IDecoratedService>();

        var decorator = Assert.IsType<Decorator>(instance);
        var decorated = Assert.IsType<Decorated>(decorator.Inner);

        Assert.Same(existing, decorated);
    }

    [Fact]
    public void CanInjectServicesIntoDecoratedType()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddSingleton<IService, SomeRandomService>();
            services.AddSingleton<IDecoratedService, Decorated>();

            services.Decorate<IDecoratedService, Decorator>();
        });

        var validator = provider.GetRequiredService<IService>();

        var instance = provider.GetRequiredService<IDecoratedService>();

        var decorator = Assert.IsType<Decorator>(instance);
        var decorated = Assert.IsType<Decorated>(decorator.Inner);

        Assert.Same(validator, decorated.InjectedService);
    }

    [Fact]
    public void CanInjectServicesIntoDecoratingType()
    {
        var serviceProvider = ConfigureProvider(services =>
        {
            services.AddSingleton<IService, SomeRandomService>();
            services.AddSingleton<IDecoratedService, Decorated>();

            services.Decorate<IDecoratedService, Decorator>();
        });

        var validator = serviceProvider.GetRequiredService<IService>();

        var instance = serviceProvider.GetRequiredService<IDecoratedService>();

        var decorator = Assert.IsType<Decorator>(instance);

        Assert.Same(validator, decorator.InjectedService);
    }

    [Fact]
    public void DisposableServicesAreDisposed()
    {
        var provider = ConfigureProvider(services =>
        {
            services.AddScoped<IDisposableService, DisposableService>();
            services.Decorate<IDisposableService, DisposableServiceDecorator>();
        });

        DisposableServiceDecorator decorator;
        using (var scope = provider.CreateScope())
        {
            var disposable = scope.ServiceProvider.GetRequiredService<IDisposableService>();
            decorator = Assert.IsType<DisposableServiceDecorator>(disposable);
        }

        Assert.True(decorator.WasDisposed);
        Assert.True(decorator.Inner.WasDisposed);
    }   

    [Fact]
    public void DecorateConcreateTypes()
    {
        var sp = ConfigureProvider(sc =>
        {
            sc
                .AddTransient<IService, SomeRandomService>()
                .AddTransient<DecoratedService>()
                .Decorate<DecoratedService, Decorator2>();
        });

        var result = sp.GetService<DecoratedService>() as Decorator2;

        Assert.NotNull(result);
        var inner = Assert.IsType<DecoratedService>(result.Inner);
        Assert.NotNull(inner.Dependency);
    }

    public interface IDecoratedService { }

    public class DecoratedService
    {
        public DecoratedService(IService dependency)
        {
            Dependency = dependency;
        }

        public IService Dependency { get; }
    }

    public class Decorator2 : DecoratedService
    {
        public Decorator2(DecoratedService decoratedService)
            : base(null)
        {
            Inner = decoratedService;
        }

        public DecoratedService Inner { get; }
    }

    public interface IService { }

    private class SomeRandomService : IService { }

    public class Decorated : IDecoratedService
    {
        public Decorated(IService injectedService = null)
        {
            InjectedService = injectedService;
        }

        public IService InjectedService { get; }
    }

    public class Decorator : IDecoratedService
    {
        public Decorator(IDecoratedService inner, IService injectedService = null)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            InjectedService = injectedService;
        }

        public IDecoratedService Inner { get; }

        public IService InjectedService { get; }
    }

    public class OtherDecorated : IDecoratedService { }

    private interface IDisposableService : IDisposable
    {
        bool WasDisposed { get; }
    }

    private class DisposableService : IDisposableService
    {
        public bool WasDisposed { get; private set; }

        public virtual void Dispose()
        {
            WasDisposed = true;
        }
    }

    private class DisposableServiceDecorator : IDisposableService
    {
        public DisposableServiceDecorator(IDisposableService inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IDisposableService Inner { get; }

        public bool WasDisposed { get; private set; }

        public void Dispose() => WasDisposed = true;
    }
}

internal static class ServiceCollectionExtensions
{
    public static ServiceDescriptor GetDescriptor<T>(this IServiceCollection services)
    {
        return services.GetDescriptors<T>().Single();
    }

    public static ServiceDescriptor[] GetDescriptors<T>(this IServiceCollection services)
    {
        return services.GetDescriptors(typeof(T));
    }

    public static ServiceDescriptor[] GetDescriptors(this IServiceCollection services, Type serviceType)
    {
        return services.Where(x => x.ServiceType == serviceType).ToArray();
    }
}

