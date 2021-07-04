﻿using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNet.Hosting.SystemWeb.DependencyInjection
{
    internal sealed class WebObjectActivator : IWebObjectActivator
    {
        private readonly ConcurrentDictionary<Type, ObjectFactory> unresolvedTypes;
        private readonly IServiceProvider serviceProvider;

        public WebObjectActivator(IServiceProvider serviceProvider)
            : this(serviceProvider, new ConcurrentDictionary<Type, ObjectFactory>())
        {
        }

        internal WebObjectActivator(IServiceProvider serviceProvider, ConcurrentDictionary<Type, ObjectFactory> unresolvedTypes)
        {
            this.serviceProvider = serviceProvider;
            this.unresolvedTypes = unresolvedTypes;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
            {
                return this;
            }

            if (serviceType == typeof(IServiceScopeFactory))
            {
                return this;
            }

            if (unresolvedTypes.TryGetValue(serviceType, out var objectFactory))
            {
                return objectFactory(serviceProvider, Array.Empty<object>());
            }

            if (this.serviceProvider.GetService(serviceType) is object service)
            {
                return service;
            }

            objectFactory = unresolvedTypes.GetOrAdd(serviceType, st =>
            {
                if (serviceType.IsAbstract || serviceType.IsInterface)
                {
                    return new ObjectFactory((sp, args) => null);
                }

                if (serviceType.IsPublic)
                {
                    try
                    {
                        return ActivatorUtilities.CreateFactory(serviceType, Array.Empty<Type>());
                    }
                    catch
                    {
                    }
                }

                return new ObjectFactory((sp, args) => Activator.CreateInstance(
                    serviceType,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.CreateInstance,
                    null,
                    null,
                    null));
            });

            return objectFactory(serviceProvider, Array.Empty<object>());
        }

        public IServiceScope CreateScope() => new ServiceScrope(this);

        private sealed class ServiceScrope : IServiceScope
        {
            private readonly IServiceScope scope;

            public ServiceScrope(WebObjectActivator webObjectActivator)
            {
                scope = webObjectActivator.serviceProvider.CreateScope();
                ServiceProvider = new WebObjectActivator(scope.ServiceProvider, webObjectActivator.unresolvedTypes);
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose() => scope.Dispose();
        }
    }
}
