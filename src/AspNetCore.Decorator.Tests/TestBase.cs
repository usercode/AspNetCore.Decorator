// Copyright (c) usercode
// https://github.com/usercode/AspNetCore.Decorator
// MIT License

using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Decorator.Tests;
public class TestBase
{
    protected static IServiceProvider ConfigureProvider(Action<IServiceCollection> configure)
    {
        IServiceCollection services = new ServiceCollection();

        configure(services);

        return services.BuildServiceProvider();
    }
}
