namespace Chemical_Neon.Models
{
    public class VendingMachine
    {
        public string? MachineId { get; set; }
        public string? ApiKey { get; set; } // Only needed internally
        public string? LockedBySessionId { get; set; }
        public DateTime? LockExpiration { get; set; }
        public decimal CurrentBalance { get; set; }
    }
}
