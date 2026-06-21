using System.Reflection;
using NetArchTest.Rules;

namespace Wms.Architecture.Tests;

// What: Fitness Functions (Evolutionary Architecture — Ford et al.; ADR-0003)
// Why: Dependency Rule + boundary microservices ditegakkan sebagai test yang FAIL
// build — "konvensi yang tak di-test akan luntur". Inilah penjaga blueprint §4.
// How: NetArchTest mem-parse metadata assembly (Mono.Cecil); tiap [Fact] = satu
// aturan struktural. Assembly di-load by-name dari output test (via project reference).
public class ArchitectureFitnessFunctions
{
    private static Assembly Load(string name) => Assembly.Load(name);

    private static readonly Assembly[] BuildingBlocks =
    [
        Load("Wms.BuildingBlocks.Domain"),
        Load("Wms.BuildingBlocks.Application"),
        Load("Wms.BuildingBlocks.Infrastructure"),
        Load("Wms.BuildingBlocks.Web"),
    ];

    // satu modul di Phase 01 (Inbound); tambah modul baru di sini saat lahir.
    private static readonly string[] ModuleNames = ["Inbound"];

    private static readonly Assembly[] ModuleAssemblies =
    [
        Load("Wms.Inbound.Domain"),
        Load("Wms.Inbound.Application"),
        Load("Wms.Inbound.Infrastructure"),
        Load("Wms.Inbound.Api"),
        Load("Wms.Inbound.Contracts"),
    ];

    private static readonly Assembly[] PlatformAssemblies =
    [
        Load("Wms.Platform.Hosting"),
        Load("Wms.Platform.Local"),
    ];

    // SDK cloud = namespace spesifik vendor. CATATAN: "Google.Cloud" (bukan "Google"),
    // supaya Google.Protobuf (runtime gRPC, sah di *.Grpc — ADR-0009) tidak ikut terjaring.
    private static readonly string[] CloudSdkNamespaces =
        ["Azure", "Microsoft.Azure", "Google.Cloud", "Amazon"];

    // FF #1 — tak ada SDK cloud di Modules.* & BuildingBlocks.* (SDK hanya di Platform.<Cloud> + Hosts).
    [Fact]
    public void Ff1_modules_and_buildingblocks_have_no_cloud_sdk()
    {
        foreach (var asm in BuildingBlocks.Concat(ModuleAssemblies))
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(CloudSdkNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(asm, "depend ke SDK cloud", result));
        }
    }

    // FF #2 — *.Domain nol framework (no EF / no mediator / no ASP.NET).
    [Fact]
    public void Ff2_domain_has_no_framework_dependency()
    {
        string[] forbidden = ["Microsoft.EntityFrameworkCore", "MediatR", "Microsoft.AspNetCore"];
        Assembly[] domains = [Load("Wms.BuildingBlocks.Domain"), Load("Wms.Inbound.Domain")];

        foreach (var asm in domains)
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(asm, "depend ke framework", result));
        }
    }

    // FF #3 — modul tak me-reference Domain/Application/Infrastructure/Api modul lain
    // (hanya boleh lewat *.Contracts / *.Grpc).
    [Fact]
    public void Ff3_module_does_not_reference_other_modules_internals()
    {
        foreach (var module in ModuleNames)
        {
            string[] otherInternals = ModuleNames
                .Where(m => m != module)
                .SelectMany(m => new[]
                {
                    $"Wms.{m}.Domain", $"Wms.{m}.Application",
                    $"Wms.{m}.Infrastructure", $"Wms.{m}.Api",
                })
                .ToArray();

            if (otherInternals.Length == 0)
                continue; // Phase 01: cuma 1 modul → guard siap, aktif saat modul ke-2 lahir.

            Assembly[] moduleAsms =
            [
                Load($"Wms.{module}.Domain"), Load($"Wms.{module}.Application"),
                Load($"Wms.{module}.Infrastructure"), Load($"Wms.{module}.Api"),
            ];
            foreach (var asm in moduleAsms)
            {
                var result = Types.InAssembly(asm)
                    .ShouldNot().HaveDependencyOnAny(otherInternals)
                    .GetResult();

                Assert.True(result.IsSuccessful, Describe(asm, "depend ke internal modul lain", result));
            }
        }
    }

    // FF #4 — BuildingBlocks tak me-reference Modules / Platform (kernel tak kenal konsumennya).
    [Fact]
    public void Ff4_buildingblocks_does_not_reference_modules_or_platform()
    {
        string[] forbidden = [.. ModuleNames.Select(m => $"Wms.{m}"), "Wms.Platform"];

        foreach (var asm in BuildingBlocks)
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(asm, "depend ke Modules/Platform", result));
        }
    }

    // FF #5 — dependency rule intra-modul: Domain ⊅ Application/Infrastructure/Api;
    // Application ⊅ Infrastructure/Api (dependensi mengalir hanya menuju Domain).
    [Fact]
    public void Ff5_intra_module_dependency_rule()
    {
        foreach (var m in ModuleNames)
        {
            var domain = Types.InAssembly(Load($"Wms.{m}.Domain"))
                .ShouldNot().HaveDependencyOnAny(
                    $"Wms.{m}.Application", $"Wms.{m}.Infrastructure", $"Wms.{m}.Api")
                .GetResult();
            Assert.True(domain.IsSuccessful, Describe(Load($"Wms.{m}.Domain"), "langgar dependency rule", domain));

            var application = Types.InAssembly(Load($"Wms.{m}.Application"))
                .ShouldNot().HaveDependencyOnAny($"Wms.{m}.Infrastructure", $"Wms.{m}.Api")
                .GetResult();
            Assert.True(application.IsSuccessful, Describe(Load($"Wms.{m}.Application"), "langgar dependency rule", application));
        }
    }

    // FF #6 — Platform.* tak me-reference Modules.* (adapter cuma implement port abstrak).
    [Fact]
    public void Ff6_platform_does_not_reference_modules()
    {
        string[] forbidden = [.. ModuleNames.Select(m => $"Wms.{m}")];

        foreach (var asm in PlatformAssemblies)
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(asm, "depend ke Modules", result));
        }
    }

    private static string Describe(Assembly asm, string violation, TestResult result)
    {
        var offenders = result.FailingTypeNames is { } names ? string.Join(", ", names) : "(none)";
        return $"{asm.GetName().Name} {violation}: {offenders}";
    }
}
