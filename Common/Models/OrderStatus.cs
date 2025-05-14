namespace Common.Models
{
    public enum OrderStatus
    {
        Created,
        PaymentProcessing,
        PaymentCompleted,
        PaymentFailed,
        InventoryAllocated,
        Shipped,
        Delivered,
        Cancelled
    }
}
