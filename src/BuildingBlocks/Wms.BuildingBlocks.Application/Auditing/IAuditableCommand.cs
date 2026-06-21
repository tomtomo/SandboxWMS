using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.BuildingBlocks.Application.Auditing;

// What: opt-in marker audit (ADR-0022) — command mutasi yang WAJIB ter-audit
// Why: audit-log hanya relevan untuk tindakan sensitif (post GR, dispatch wave), bukan tiap
// command — jadi opt-in EKSPLISIT, bukan reflection atas semua command. Aggregate identity
// (Type/Id) di-SUPPLY oleh command itu sendiri (ADR-0022 "bukan reflection") supaya jejak
// audit tahu objek apa yang dikenai tanpa AuditLogBehavior membongkar struktur tiap command.
// How: extends ICommandBase (audit = sisi WRITE saja); command meng-implement dua getter yang
// memberi AggregateType + AggregateId untuk entri AuditLogEntry.
public interface IAuditableCommand : ICommandBase
{
    string AggregateType { get; }

    string AggregateId { get; }
}
