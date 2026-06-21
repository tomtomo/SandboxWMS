namespace Wms.Inbound.Domain;

// What: tipe discrepancy terkompilasi dari dua sumbu independen (ADR-0013)
// Why: tiap entry discrepancy berasal dari SATU sumbu — kuantitas (ShortDelivery/OverDelivery)
// ATAU kondisi (WrongItem/QcHold). Satu SKU bisa memunculkan beberapa entry (mis. OverDelivery
// + QcHold). Tipe ini juga menentukan ResolutionAction default per SOP (overview §A4).
public enum DiscrepancyType
{
    ShortDelivery,
    OverDelivery,
    WrongItem,
    QcHold
}
