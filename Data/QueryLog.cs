public class QueryLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Term { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}