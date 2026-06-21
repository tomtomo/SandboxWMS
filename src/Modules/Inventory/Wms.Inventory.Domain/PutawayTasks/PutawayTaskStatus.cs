namespace Wms.Inventory.Domain;

// What: lifecycle state PutawayTask — minimal walking-skeleton (Phase 01c)
// Why: skeleton hanya membuat task (Assigned). Completed (+ transisi Stock OnHand→
// Available) menyusul di Phase 03b.
public enum PutawayTaskStatus
{
    Assigned
}
