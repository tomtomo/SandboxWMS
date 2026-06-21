namespace Wms.Inbound.Domain;

// What: aksi resolusi SPV atas sebuah discrepancy (overview §A4 default SOP)
// Why: setiap discrepancy harus di-resolve sebelum Confirm; action menentukan turunan payload
// GRConfirmed (AcceptPartial/SendToQC → receivedLines; RejectExcess/ReturnToSupplier → rejectedLines).
// Default per type: ShortDelivery→AcceptPartial, OverDelivery→RejectExcess,
// WrongItem→ReturnToSupplier, QcHold→SendToQC.
public enum ResolutionAction
{
    AcceptPartial,
    RejectExcess,
    ReturnToSupplier,
    SendToQC
}
