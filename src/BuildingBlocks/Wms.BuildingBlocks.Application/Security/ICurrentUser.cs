namespace Wms.BuildingBlocks.Application.Security;

// What: Port — principal aktif (ADR-0027 SYSTEM actor convention)
// Why: identitas pelaku dibutuhkan lintas-jalur — HTTP (dari JWT), consumer bus, background
// job, seeder, s2s — untuk menstempel IAuditable & audit-log. Sebagai port di Application
// (nol transport/SDK), ia di-implement berbeda per origin: adapter HttpContext (Web) untuk
// jalur HTTP, SystemCurrentUser untuk origin mesin. Konsumen (interceptor, AuditLogBehavior)
// tak peduli dari mana identitas datang — hanya bergantung abstraksi ini (DIP).
// How: satu properti UserId yang TAK PERNAH null; nilai di-resolve via CurrentUserResolver
// (SYSTEM saat tak ada request context, anonymous saat HTTP tak terotentikasi, else userId).
public interface ICurrentUser
{
    string UserId { get; }
}
