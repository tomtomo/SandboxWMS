namespace Wms.BuildingBlocks.Domain.Auditing;

// What: IAuditable — marker contract untuk field audit standar (overview §Konvensi)
// Why: createdBy/createdAt/modifiedBy/modifiedAt adalah metadata infrastruktur yang
// SAMA di semua aggregate — tak ditulis eksplisit per-aggregate (overview), tapi diisi
// seragam oleh satu EF SaveChanges interceptor (ADR-0027 actor → IAuditable). Marker di
// Domain (bukan Infrastructure) supaya aggregate boleh meng-implement-nya tanpa menyeret
// framework — interface POCO murni, nol EF/ASP.NET (FF#2 aman).
// How: getter-only — aggregate meng-implement dengan private setter; interceptor menstempel
// nilai lewat EF change-tracker (entry.Property(...).CurrentValue), menjaga enkapsulasi
// domain (tak ada public setter yang membuka mutasi liar dari luar).
public interface IAuditable
{
    string? CreatedBy { get; }

    DateTimeOffset CreatedAt { get; }

    string? ModifiedBy { get; }

    DateTimeOffset ModifiedAt { get; }
}
