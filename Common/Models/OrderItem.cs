namespace Common.Models
{
    public class OrderItem
    {
        public string ProductId { get; set; }
        public string CategoryId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
