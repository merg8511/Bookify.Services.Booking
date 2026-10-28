using System.Reflection;
using Bookify.Services.Booking.Application.Abstractions.Messaging;
using Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories;
using Bookify.Services.Booking.Infrastructure.Persistence;

namespace Bookify.Services.Booking.Architecture.Tests.Conventions;

public sealed class ApplicationConventionTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(ICommandHandler<>).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(BookingDbContext).Assembly;

    private static readonly Assembly DomainAssembly =
        Assembly.Load("Bookify.Services.Booking.Domain");

    private const string RepositoryInterfaceNamespace =
        "Bookify.Services.Booking.Application.Abstractions.Persistence.Repositories";

    private const string RepositoryImplementationNamespace =
        "Bookify.Services.Booking.Infrastructure.Persistence.Repositories";

    [Fact]
    public void CqrsHandlers_ShouldFollowNamingAndLayerConventions()
    {
        // ARRANGE
        Type[] handlers = GetCqrsHandlers();

        // ASSERT
        Assert.NotEmpty(handlers);

        foreach (Type handler in handlers)
        {
            Type[] contracts = GetHandlerContracts(handler);

            // Un handler atiende un solo contrato CQRS.
            Type contract = Assert.Single(contracts);

            Type genericDefinition =
                contract.GetGenericTypeDefinition();

            string expectedSuffix =
                genericDefinition == typeof(IQueryHandler<,>)
                    ? "QueryHandler"
                    : "CommandHandler";

            Assert.EndsWith(
                expectedSuffix,
                handler.Name,
                StringComparison.Ordinal);

            Assert.True(
                handler.IsPublic,
                $"{handler.FullName} must be public.");

            Assert.True(
                handler.IsSealed,
                $"{handler.FullName} must be sealed.");

            Assert.StartsWith(
                "Bookify.Services.Booking.Application.",
                handler.Namespace,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CqrsHandlers_ShouldExposeTheirContractHandleAsync()
    {
        // ARRANGE
        Type[] handlers = GetCqrsHandlers();

        // ASSERT
        Assert.NotEmpty(handlers);

        foreach (Type handler in handlers)
        {
            Type contract =
                Assert.Single(GetHandlerContracts(handler));

            Type requestType =
                contract.GetGenericArguments()[0];

            // ACT
            MethodInfo? method = handler.GetMethod(
                "HandleAsync",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types:
                [
                    requestType,
                    typeof(CancellationToken)
                ],
                modifiers: null);

            // ASSERT
            Assert.NotNull(method);

            MethodInfo? contractMethod =
                contract.GetMethod("HandleAsync");

            Assert.NotNull(contractMethod);

            Assert.Equal(
                contractMethod.ReturnType,
                method.ReturnType);
        }
    }

    [Fact]
    public void RepositoryContracts_ShouldLiveInApplication()
    {
        // ARRANGE
        Type[] repositoryContracts =
            GetRepositoryContracts();

        // ASSERT
        Assert.NotEmpty(repositoryContracts);

        foreach (Type contract in repositoryContracts)
        {
            Assert.True(
                contract.IsInterface,
                $"{contract.FullName} must be an interface.");

            Assert.True(
                contract.IsPublic,
                $"{contract.FullName} must be public.");

            Assert.False(
                contract.IsGenericType,
                $"{contract.FullName} must be a specific repository contract.");

            Assert.Equal(
                RepositoryInterfaceNamespace,
                contract.Namespace);

            Assert.StartsWith(
                "I",
                contract.Name,
                StringComparison.Ordinal);

            Assert.EndsWith(
                "Repository",
                contract.Name,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RepositoryImplementations_ShouldBeInternalAndUseBookingDbContext()
    {
        // ARRANGE
        Type[] repositoryContracts =
            GetRepositoryContracts();

        Type[] infrastructureTypes =
            InfrastructureAssembly.GetTypes();

        // ASSERT
        Assert.NotEmpty(repositoryContracts);

        foreach (Type contract in repositoryContracts)
        {
            // ACT
            Type implementation = Assert.Single(infrastructureTypes, type => type.IsClass && !type.IsAbstract && contract.IsAssignableFrom(type));

            // ASSERT
            Assert.Equal(
                contract.Name[1..],
                implementation.Name);

            Assert.Equal(
                RepositoryImplementationNamespace,
                implementation.Namespace);

            Assert.True(
                implementation.IsSealed,
                $"{implementation.FullName} must be sealed.");

            Assert.True(
                implementation.IsNotPublic,
                $"{implementation.FullName} must be internal.");

            ConstructorInfo[] constructors =
                implementation.GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);

            Assert.Contains(
                constructors,
                constructor =>
                    constructor.GetParameters().Any(
                        parameter =>
                            parameter.ParameterType ==
                            typeof(BookingDbContext)));
        }
    }

    [Fact]
    public void CoreLayers_ShouldNotImplementPersistenceRepositories()
    {
        // ARRANGE
        Type[] repositoryContracts =
            GetRepositoryContracts();

        Type[] coreTypes = DomainAssembly
            .GetTypes()
            .Concat(ApplicationAssembly.GetTypes())
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract)
            .ToArray();

        // ACT
        Type[] violations = coreTypes
            .Where(type =>
                repositoryContracts.Any(
                    contract =>
                        contract.IsAssignableFrom(type)))
            .ToArray();

        // ASSERT
        Assert.True(
            violations.Length == 0,
            "Repository implementations were found in core layers: " +
            string.Join(
                ", ",
                violations.Select(type => type.FullName)));
    }

    private static Type[] GetCqrsHandlers()
    {
        return ApplicationAssembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                GetHandlerContracts(type).Length > 0)
            .ToArray();
    }

    private static Type[] GetHandlerContracts(Type type)
    {
        return type
            .GetInterfaces()
            .Where(IsCqrsHandlerContract)
            .ToArray();
    }

    private static bool IsCqrsHandlerContract(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        Type definition =
            type.GetGenericTypeDefinition();

        return definition == typeof(ICommandHandler<>) ||
               definition == typeof(ICommandHandler<,>) ||
               definition == typeof(IQueryHandler<,>);
    }

    private static Type[] GetRepositoryContracts()
    {
        return ApplicationAssembly
            .GetTypes()
            .Where(type =>
                type.IsInterface &&
                type.Namespace == RepositoryInterfaceNamespace &&
                type.Name.StartsWith('I') &&
                type.Name.EndsWith(
                    "Repository",
                    StringComparison.Ordinal))
            .ToArray();
    }
}
