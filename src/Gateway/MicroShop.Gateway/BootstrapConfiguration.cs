using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Transforms;

namespace MicroShop.Gateway;

public static class BootstrapConfiguration
{
    private const string DefaultProductServiceAddress = "http://localhost:5245/";
    private const string DefaultOrderServiceAddress = "http://localhost:5075/";

    public static void AddYarp(IServiceCollection services, IConfiguration configuration)
    {
        var productServiceAddress = GetServiceAddress(
            configuration,
            "PRODUCT_SERVICE_URL",
            "Gateway:ProductServiceUrl",
            "ReverseProxy:Clusters:product-cluster:Destinations:product:Address",
            DefaultProductServiceAddress);
        var orderServiceAddress = GetServiceAddress(
            configuration,
            "ORDER_SERVICE_URL",
            "Gateway:OrderServiceUrl",
            "ReverseProxy:Clusters:order-cluster:Destinations:order:Address",
            DefaultOrderServiceAddress);
        ValidateServiceAddress("Product Service", productServiceAddress);
        ValidateServiceAddress("Order Service", orderServiceAddress);

        var proxyConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:product-cluster:Destinations:product:Address"] = productServiceAddress,
                ["ReverseProxy:Clusters:order-cluster:Destinations:order:Address"] = orderServiceAddress
            })
            .Build();

        services.AddCors(options =>
        {
            var origins = configuration.GetSection("Gateway:CorsOrigins").Get<string[]>()
                ?? ["http://localhost:4200"];
            options.AddPolicy("frontend", policy =>
            {
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services
            .AddReverseProxy()
            .LoadFromConfig(proxyConfiguration.GetSection("ReverseProxy"))
            .AddTransforms(transformBuilderContext =>
            {
                transformBuilderContext.AddRequestTransform(context =>
                {
                    if (context.HttpContext.Request.Headers.TryGetValue(
                            "traceparent",
                            out var traceParent))
                    {
                        context.ProxyRequest.Headers.Remove("traceparent");
                        context.ProxyRequest.Headers.TryAddWithoutValidation(
                            "traceparent",
                            traceParent.ToString());
                    }

                    return ValueTask.CompletedTask;
                });
            });
    }

    private static string GetServiceAddress(
        IConfiguration configuration,
        string environmentKey,
        string gatewayKey,
        string proxyKey,
        string fallback)
    {
        return (configuration[environmentKey]
            ?? configuration[gatewayKey]
            ?? configuration[proxyKey]
            ?? fallback).TrimEnd('/') + "/";
    }

    private static void ValidateServiceAddress(string serviceName, string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} Gateway destination must be an absolute HTTP or HTTPS URL.",
                    serviceName));
        }
    }
}
