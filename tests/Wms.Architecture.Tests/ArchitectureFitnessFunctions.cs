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
    // Outbound = full-5 sejak Phase 03c. MasterData = full-6 sejak Phase 04a; Auth = full-6 sejak Phase 04b
    // (supporting authority: gRPC read-API + cache-aside). Grpc bukan "internal layer" (boleh di-cross-ref
    // antar-modul, FF#3) — .proto read-API sinkron (ADR-0006/0009).
    private static readonly Dictionary<string, string[]> ModuleLayers = new()
    {
        ["Inbound"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts"],
        ["Inventory"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts"],
        ["Outbound"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts"],
        ["MasterData"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts", "Grpc"],
        ["Auth"] = ["Domain", "Application", "Infrastructure", "Api", "Contracts", "Grpc"],
    };

    // "internals" = layer selain Contracts; Contracts = published language yang BOLEH di-cross-ref.
    private static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure", "Api"];

    private static readonly string[] ModuleNames = [.. ModuleLayers.Keys];

    // Collapsed modules (blueprint §3 right-sizing): pure-consumer 1-project TANPA layer suffix (assembly
    // "Wms.<Module>"). Reporting = collapsed pertama (ADR-0017), Notification menyusul (Phase 04d). FF#1
    // (no cloud SDK) + FF#3 (hanya ref *.Contracts/*.Grpc modul lain) tetap berlaku; FF#2/#5/#8
    // (layer-spesifik) tak relevan (tak ada layer terpisah).
    private static readonly string[] CollapsedModules = ["Reporting", "Notification"];

    private static Assembly[] CollapsedModuleAssemblies() =>
        [.. CollapsedModules.Select(module => Load($"Wms.{module}"))];

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
        foreach (var asm in BuildingBlocks.Concat(ModuleAssemblies()).Concat(CollapsedModuleAssemblies()))
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
        // Honor layer yang dideklarasikan (data-driven) — modul Contracts-only (mis. Outbound di
        // Phase 03b) tak punya Domain; jangan paksa Load assembly yang belum lahir.
        var domains = ModuleLayers
            .Where(m => m.Value.Contains("Domain"))
            .Select(m => Load($"Wms.{m.Key}.Domain"))
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

        // collapsed modules (mis. Reporting): pure-consumer 1-project — hanya boleh ref *.Contracts modul
        // lain (aturan FF#3 sama). Tiap modul full = "lain"; collapsed tak punya internal yang di-ref balik.
        foreach (var collapsed in CollapsedModules)
        {
            string[] otherInternals = ModuleLayers
                .SelectMany(other => InternalLayers
                    .Where(layer => other.Value.Contains(layer))
                    .Select(layer => $"Wms.{other.Key}.{layer}"))
                .ToArray();

            var asm = Load($"Wms.{collapsed}");
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(otherInternals)
                .GetResult();

            Assert.True(result.IsSuccessful,
                Describe(asm, "collapsed module depend ke internal modul lain", result));
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

        // full modul: domain = assembly terpisah Wms.<Module>.Domain
        foreach (var module in ModuleNames)
            CollectDomainThrows(
                Path.Combine(RepoRoot(), "src", "Modules", module, $"Wms.{module}.Domain"), violations);

        // collapsed modul (Reporting/Notification): domain = subfolder Wms.<Module>/Domain (UF-10 — tutup
        // coverage-illusion; FF#7 sebelumnya hanya iterate ModuleNames → modul collapsed luput dari scan).
        foreach (var module in CollapsedModules)
            CollectDomainThrows(
                Path.Combine(RepoRoot(), "src", "Modules", module, $"Wms.{module}", "Domain"), violations);

        Assert.True(violations.Count == 0,
            "business `throw` di *.Domain — gunakan Result (ADR-0019):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // scan SUMBER domain (di luar komentar) untuk statement `throw` business — dipakai full & collapsed module
    private static void CollectDomainThrows(string domainDir, List<string> violations)
    {
        if (!Directory.Exists(domainDir))
            return;

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

    // FF #8 — `*.Api` tak menyentuh DbContext / EF Core (reader-delegation; ADR-0011).
    // What: boundary READ terisolasi dari persistence — service gRPC & endpoint REST di *.Api delegasi
    // ke read-port (IMasterDataReader, cache-aside) / MediatR, BUKAN inject DbContext. Menjaga gRPC
    // read-API MasterData (& REST tiap modul) bebas EF: *.Api tak boleh meng-query persistence langsung.
    // How: NetArchTest — tiap modul ber-layer Api tak boleh depend ke namespace Microsoft.EntityFrameworkCore.
    [Fact]
    public void Ff8_module_api_does_not_depend_on_dbcontext()
    {
        string[] forbidden = ["Microsoft.EntityFrameworkCore"];

        var apis = ModuleLayers
            .Where(module => module.Value.Contains("Api"))
            .Select(module => Load($"Wms.{module.Key}.Api"));

        foreach (var asm in apis)
        {
            var result = Types.InAssembly(asm)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(asm, "depend ke EF Core (DbContext) di *.Api (FF#8)", result));
        }
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

    // ── Phase 04.5 hardening FFs (kunci konvensi load-bearing-tapi-unguarded; semua pass vs current) ──

    // FF #10 — tak ada DbContext standalone di Wms.BuildingBlocks.Infrastructure (ADR-0010; InfrastructureModel.cs:10)
    // What: infra tables (outbox/inbox/dead_letter/audit_log) di-MAP via extension ke DbContext MODUL, BUKAN
    // context kernel sendiri — cegah kontaminasi PK lintas-service & jaga DB-per-service (ADR-0010). Tutup
    // governance-overstatement (FF#10 di-klaim roadmap tapi absen). How: reflect assembly; assert nol tipe
    // yang rantai base-nya memuat "DbContext" (string-match → tak butuh reference EF Core di test).
    [Fact]
    public void Ff10_buildingblocks_infrastructure_has_no_standalone_dbcontext()
    {
        var infrastructure = Load("Wms.BuildingBlocks.Infrastructure");
        var allTypes = infrastructure.GetTypes();
        // sanity: cegah vacuous pass bila assembly gagal load / kosong (konsisten FF#11)
        Assert.True(allTypes.Length > 0, "Wms.BuildingBlocks.Infrastructure nol tipe — assembly load gagal?");
        var dbContexts = allTypes
            .Where(InheritsDbContext)
            .Select(type => type.FullName ?? type.Name)
            .ToList();

        Assert.True(dbContexts.Count == 0,
            "InfrastructureDbContext standalone DILARANG (FF#10, ADR-0010 — infra tables di-map ke DbContext modul):"
            + Environment.NewLine + string.Join(Environment.NewLine, dbContexts));
    }

    private static bool InheritsDbContext(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.Name == "DbContext")
                return true;
        return false;
    }

    // FF (error-code format) — tiap Error.Code match ^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$ (ADR-0019)
    // What: Error.Code = kontrak transport publik (RFC7807 extensions errorCode + gRPC trailer) — bentuk
    // {snake}.{snake} di-standardize agar client branch by kode stabil. Guard SHAPE saja (BUKAN prefix==
    // aggregate — itu over-constrain; catalog sah spt 'auth.invalid_credentials' beda prefix). How: source-
    // scan src untuk Error.<Type>("<code>", …) inline + catalog → validasi tiap code literal.
    [Fact]
    public void Ff_error_codes_follow_transport_format()
    {
        var codePattern = new Regex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$");
        var callPattern = new Regex("Error\\.(?:Validation|NotFound|Conflict|Unauthorized|Unexpected)\\(\"([^\"]+)\"");
        var violations = new List<string>();
        var scanned = 0;

        foreach (var file in EnumerateSourceFiles(Path.Combine("src")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = callPattern.Match(StripLineComment(lines[i]));
                if (!match.Success)
                    continue;
                scanned++;
                if (!codePattern.IsMatch(match.Groups[1].Value))
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: '{match.Groups[1].Value}' bukan format {{snake}}.{{snake}} (ADR-0019)");
            }
        }

        // sanity: cegah vacuous pass bila regex/konvensi drift → nol Error.<Type>("…") ter-scan
        Assert.True(scanned > 0, "tak ada Error.<Type>(\"…\") ter-scan — regex/konvensi drift?");
        Assert.True(violations.Count == 0,
            "Error.Code langgar format transport (ADR-0019):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // FF (LogicalName format) — tiap LogicalName match ^[a-z][a-z0-9_]+\.[a-z0-9_]+\.v[0-9]+$ (ADR-0023)
    // What: LogicalName = identitas broker stabil {module}.{event}.v{N} + SemVer; FF#11 jaga channel-existence,
    // FF ini jaga FORMAT-nya. How: reuse PublishedIntegrationEvents() (reflect const LogicalName).
    [Fact]
    public void Ff_logical_names_follow_versioned_format()
    {
        var pattern = new Regex(@"^[a-z][a-z0-9_]+\.[a-z0-9_]+\.v[0-9]+$");
        var published = PublishedIntegrationEvents();
        Assert.True(published.Count > 0, "tak ada tipe published ter-discover (cek konvensi const LogicalName).");

        var violations = published
            .Where(evt => !pattern.IsMatch(evt.LogicalName))
            .Select(evt => $"{evt.TypeName} → '{evt.LogicalName}' bukan format {{module}}.{{event}}.v{{N}} (ADR-0023)")
            .ToList();

        Assert.True(violations.Count == 0,
            "LogicalName langgar format (ADR-0023):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // FF (Inbox idempotency) — tiap tipe yang declare `const string HandlerType` WAJIB call HasProcessedAsync
    // + MarkProcessed (Idempotent Receiver, ADR-0005). What: composite inbox key (event_id, handler_type) =
    // load-bearing cegah silent double-processing; ceremony per-handler sebelumnya unguarded. How: source-scan
    // src/Modules untuk file ber-`const string HandlerType` → assert mengandung kedua call.
    [Fact]
    public void Ff_inbox_consumers_guard_idempotency()
    {
        var violations = new List<string>();
        var guarded = 0;
        foreach (var file in EnumerateSourceFiles(Path.Combine("src", "Modules")))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("const string HandlerType"))
                continue;
            guarded++;
            if (!text.Contains("HasProcessedAsync") || !text.Contains("MarkProcessed"))
                violations.Add($"{Path.GetFileName(file)}: declare HandlerType tapi tak lengkap HasProcessedAsync+MarkProcessed (ADR-0005)");
        }

        // sanity: cegah vacuous pass bila konvensi HandlerType berubah → nol consumer ter-scan
        Assert.True(guarded > 0, "tak ada tipe ber-`const string HandlerType` ter-scan — konvensi drift?");
        Assert.True(violations.Count == 0,
            "Inbox idempotency guard tidak lengkap (ADR-0005):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // FF (dispatcher fail-loud) — tiap *IntegrationEventDispatcher: cek IsFailure → throw (rail DLQ load-bearing,
    // ADR-0005/0029). What: dispatcher membungkus consumer; Result.Failure WAJIB jadi throw agar
    // ConsumerDeadLetterPipeline tangkap → DLQ (bukan di-swallow). BUKAN extract helper (consumer sah-divergen).
    // How: tiap baris `.IsFailure` (di luar komentar) → throw dalam 2 baris berikutnya.
    [Fact]
    public void Ff_integration_event_dispatchers_throw_on_failure()
    {
        var dispatchers = EnumerateSourceFiles(Path.Combine("src", "Modules"))
            .Where(path => path.EndsWith("IntegrationEventDispatcher.cs", StringComparison.Ordinal))
            .ToList();
        Assert.True(dispatchers.Count > 0, "tak ada *IntegrationEventDispatcher ter-scan — cek konvensi nama.");

        var violations = new List<string>();
        foreach (var file in dispatchers)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!StripLineComment(lines[i]).Contains(".IsFailure"))
                    continue;
                var window = string.Join(" ", lines.Skip(i).Take(3));
                if (!window.Contains("throw"))
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: cek IsFailure tanpa throw dalam 2 baris (ADR-0029)");
            }
        }

        Assert.True(violations.Count == 0,
            "dispatcher tak fail-loud pada consumer failure (ADR-0029):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // FF (REST host security) — host (non-collapsed) yang Map<Module>Endpoints WAJIB AddWmsJwtBearer +
    // AddHttpContextCurrentUser (ADR-0016/0012). What: cegah host baru ship surface REST unauthenticated.
    // Pure-consumer (collapsed: Reporting/Notification) EXEMPT — REST-nya seed/inbox read, authZ deferred 07a.
    // How: scan src/Hosts/Local/*/Program.cs; abaikan MapDefaultEndpoints (health Aspire) via negative lookahead.
    [Fact]
    public void Ff_rest_hosts_require_jwt_and_current_user()
    {
        var hostsRoot = Path.Combine(RepoRoot(), "src", "Hosts", "Local");
        var mapPattern = new Regex(@"Map(?!Default)\w+Endpoints\(\)");
        var exemptHosts = CollapsedModules.Select(module => $"Wms.{module}.Host.Local").ToHashSet();
        var violations = new List<string>();

        foreach (var program in Directory.EnumerateFiles(hostsRoot, "Program.cs", SearchOption.AllDirectories))
        {
            var hostName = Path.GetFileName(Path.GetDirectoryName(program)!);
            if (exemptHosts.Contains(hostName))
                continue;

            var text = File.ReadAllText(program);
            if (!mapPattern.IsMatch(text))
                continue; // bukan REST host (hanya MapDefaultEndpoints / pure messaging)

            if (!text.Contains("AddWmsJwtBearer()") || !text.Contains("AddHttpContextCurrentUser()"))
                violations.Add($"{hostName}: Map*Endpoints REST tanpa AddWmsJwtBearer()/AddHttpContextCurrentUser() (ADR-0016/0012)");
        }

        Assert.True(violations.Count == 0,
            "REST host kurang setup auth (ADR-0016/0012):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // enumerasi sumber .cs di bawah <relativeDir> (exclude bin/obj) — dipakai FF source-scan Phase 04.5
    private static IEnumerable<string> EnumerateSourceFiles(string relativeDir) =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), relativeDir), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
