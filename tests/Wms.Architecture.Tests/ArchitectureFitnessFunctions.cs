using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

    // FF #7 — no business `throw` di *.Domain modul (no-throw-for-business, ADR-0019).
    // CATATAN: "minimum viable grep" (ADR-0019, dilabeli jujur) — scan SUMBER untuk statement
    // `throw` di luar komentar, BUKAN analisis Roslyn (itu roadmap). throw direservasi untuk
    // programmer-error; nilai legit masa depan ditambah ke ProgrammerErrorAllowList sebagai
    // keputusan SADAR, bukan diam-diam.
    [Fact]
    public void Ff7_module_domain_has_no_business_throw()
    {
        var violations = new List<string>();

        foreach (var module in ModuleNames)
        {
            var domainDir = Path.Combine(RepoRoot(), "src", "Modules", module, $"Wms.{module}.Domain");
            if (!Directory.Exists(domainDir))
                continue;

            foreach (var file in EnumerateDomainSources(domainDir))
            {
                var lines = File.ReadAllLines(file);
                for (var line = 0; line < lines.Length; line++)
                {
                    var code = StripLineComment(lines[line]);
                    if (!Regex.IsMatch(code, @"\bthrow\b"))
                        continue;
                    if (ProgrammerErrorAllowList.Any(code.Contains))
                        continue;
                    violations.Add($"{Path.GetFileName(file)}:{line + 1}: {lines[line].Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "business `throw` di *.Domain — gunakan Result (ADR-0019):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // allow-list throw programmer-error yang sah di domain (kosong: domain murni Result saat ini)
    private static readonly string[] ProgrammerErrorAllowList = [];

    private static IEnumerable<string> EnumerateDomainSources(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    // buang komentar baris (// …) supaya kata "throw" di komentar tak ke-flag (minimum viable)
    private static string StripLineComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }

    // naik dari base-dir test sampai ketemu Wms.sln (anchor repo root utk scan sumber)
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Wms.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new InvalidOperationException("Wms.sln tak ditemukan dari base dir test.");
    }

    private static string Describe(Assembly asm, string violation, TestResult result)
    {
        var offenders = result.FailingTypeNames is { } names ? string.Join(", ", names) : "(none)";
        return $"{asm.GetName().Name} {violation}: {offenders}";
    }

    // FF #11 — contract-coverage: tiap integration event published (`*.Contracts`) WAJIB
    // punya channel terdeklarasi di docs/architecture/asyncapi.yaml (directional; ADR-0023).
    // What: contract-coverage fitness function (ADR-0023) — penjaga drift doc <-> kode.
    // Why: katalog AsyncAPI hanya otoritatif kalau dijaga executable; tanpa FF ini sebuah
    // contract baru bisa lahir tanpa channel (seam EDA tak terdokumentasi). Directional
    // only (published ⊆ declared) — reverse-coverage (tiap channel punya contract) =
    // known gap karena placeholder sengaja belum punya tipe (ADR-0023).
    // How: BUKAN NetArchTest murni — ia membaca artefak YAML eksternal (di luar kapabilitas
    // type-dependency NetArchTest), tapi REUSE harness Architecture.Tests. Parse channels
    // `address` via YamlDotNet → reflect tipe published (marker: `const string LogicalName`)
    // → assert tiap LogicalName ada di set address.
    [Fact]
    public void Ff11_published_contracts_have_declared_channel()
    {
        var declared = DeclaredChannelAddresses();
        Assert.True(declared.Count > 0, "asyncapi.yaml tak punya channel address — katalog kosong/parse gagal.");

        var published = PublishedIntegrationEvents();
        // sanity: minimal GRConfirmedV1 harus ke-discover — cegah FF lulus secara vacuous
        // (mis. konvensi marker berubah → reflection nol → assert palsu hijau).
        Assert.True(published.Count > 0,
            "tak ada tipe published ter-discover di *.Contracts (cek konvensi `const string LogicalName`).");

        var orphans = published
            .Where(evt => !declared.Contains(evt.LogicalName))
            .Select(evt => $"{evt.TypeName} → '{evt.LogicalName}' (tak ada channel di asyncapi.yaml)")
            .ToList();

        Assert.True(orphans.Count == 0,
            "contract published tanpa channel terdeklarasi (ADR-0023):"
            + Environment.NewLine + string.Join(Environment.NewLine, orphans));
    }

    // assembly `Wms.<Module>.Contracts` untuk modul yang punya layer Contracts (data-driven)
    private static IEnumerable<Assembly> ContractsAssemblies() =>
        ModuleLayers
            .Where(m => m.Value.Contains("Contracts"))
            .Select(m => Load($"Wms.{m.Key}.Contracts"));

    // tipe published = tipe yang mendeklarasikan `public const string LogicalName` (POLA
    // binding ADR-0023). Payload bersarang tanpa const (mis. ReceivedLineV1) bukan channel.
    private static List<(string TypeName, string LogicalName)> PublishedIntegrationEvents()
    {
        var events = new List<(string, string)>();
        foreach (var assembly in ContractsAssemblies())
        {
            foreach (var type in assembly.GetTypes().Where(t => t is { IsPublic: true, IsClass: true }))
            {
                var field = type.GetField("LogicalName",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string)
                    && field.GetRawConstantValue() is string logicalName)
                {
                    events.Add((type.FullName ?? type.Name, logicalName));
                }
            }
        }
        return events;
    }

    // parse channels.*.address dari asyncapi.yaml (CamelCase: C# Address → yaml `address`;
    // IgnoreUnmatchedProperties → abaikan info/operations/components/dst).
    private static HashSet<string> DeclaredChannelAddresses()
    {
        var path = Path.Combine(RepoRoot(), "docs", "architecture", "asyncapi.yaml");
        Assert.True(File.Exists(path), $"asyncapi.yaml tak ditemukan di {path}.");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        var catalog = deserializer.Deserialize<AsyncApiCatalog>(File.ReadAllText(path));

        return catalog?.Channels?.Values
            .Select(channel => channel.Address)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!)
            .ToHashSet() ?? [];
    }

    // model parse minimal — hanya channels.*.address yang relevan untuk contract-coverage
    private sealed class AsyncApiCatalog
    {
        public Dictionary<string, AsyncApiChannel> Channels { get; set; } = new();
    }

    private sealed class AsyncApiChannel
    {
        public string? Address { get; set; }
    }
}
