namespace Wms.Inbound.Domain;

// What: alasan sebuah line ditolak (tidak masuk inventory) saat Confirm
// Why: payload GRConfirmed memisahkan receivedLines (masuk Stock) dari rejectedLines; reason
// menjelaskan ASAL penolakan — WrongItem→ReturnToSupplier, OverDelivery excess→RejectExcess
// (metadata return-to-vendor; flow detail di luar scope, overview §A "Implikasi").
public enum RejectionReason
{
    ReturnToSupplier,
    RejectExcess
}
