using System.Reflection;
using NetArchTest.Rules;

namespace Wms.Architecture.Tests;

// What: Fitness Functions (Evolutionary Architecture — Ford et al.; ADR-0003)
// Why: Dependency Rule + boundary microservices ditegakkan sebagai test yang FAIL
// build — "konvensi yang tak di-test akan luntur". Inilah penjaga blueprint §4.
// How: NetArchTest mem-parse metadata assembly (Mono.Cecil); tiap [Fact] = satu aturan
// struktural. Modul mendeklarasikan layer yang SUDAH ada (data-driven) supaya tumbuh
// bertahap tanpa menyentuh tiap FF — Inventory belum punya Api/Contracts di Phase 01c.
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

    // Single source: tiap modul → layer (project) yang sudah lahir. Tambah modul/layer di sini.
    private static readonly Dictionary<string, string[]> ModuleLayers = new()
    {
        ["Inbound"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts"],
        ["Inventory"] = ["Domain", "Application", "Infrastructure"],
    };

    // "internals" = layer selain Contracts; Contracts = published language yang BOLEH di-cross-ref.
    private static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure", "Api"];

    private static readonly string[] ModuleNames = [.. ModuleLayers.Keys];

    private static Assembly[] ModuleAssemblies() =>
        [.. ModuleLayers.SelectMany(m => m.Value.Select(layer => Load($"Wms.{m.Key}.{layer}")))];

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
        foreach (var asm in BuildingBlocks.Concat(ModuleAssemblies()))
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
        var domains = ModuleNames
            .Select(m => Load($"Wms.{m}.Domain"))
            .Prepend(Load("Wms.BuildingBlocks.Domain"));

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
        foreach (var (module, layers) in ModuleLayers)
        {
            string[] otherInternals = ModuleLayers
                .Where(other => other.Key != module)
                .SelectMany(other => InternalLayers
                    .Where(layer => other.Value.Contains(layer))
                    .Select(layer => $"Wms.{other.Key}.{layer}"))
                .ToArray();

            if (otherInternals.Length == 0)
                continue; // modul tunggal → guard siap, aktif saat modul ke-2 lahir.

            foreach (var layer in layers)
            {
                var asm = Load($"Wms.{module}.{layer}");
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
        foreach (var (m, layers) in ModuleLayers)
        {
            if (layers.Contains("Domain"))
            {
                string[] forbidden = [.. new[] { "Application", "Infrastructure", "Api" }
                    .Where(layers.Contains).Select(l => $"Wms.{m}.{l}")];
                if (forbidden.Length > 0)
                {
                    var domain = Types.InAssembly(Load($"Wms.{m}.Domain"))
                        .ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
                    Assert.True(domain.IsSuccessful, Describe(Load($"Wms.{m}.Domain"), "langgar dependency rule", domain));
                }
            }

            if (layers.Contains("Application"))
            {
                string[] forbidden = [.. new[] { "Infrastructure", "Api" }
                    .Where(layers.Contains).Select(l => $"Wms.{m}.{l}")];
                if (forbidden.Length > 0)
                {
                    var application = Types.InAssembly(Load($"Wms.{m}.Application"))
                        .ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
                    Assert.True(application.IsSuccessful, Describe(Load($"Wms.{m}.Application"), "langgar dependency rule", application));
                }
            }
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
