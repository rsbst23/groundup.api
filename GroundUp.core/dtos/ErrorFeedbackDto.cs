namespace GroundUp.core.dtos
{
    public class ErrorFeedbackDto
    {
        public int Id { get; set; }
        public required string Feedback { get; set; }
        public string? Email { get; set; }
        public string? Context { get; set; }
        public required ErrorDetailsDto Error { get; set; }
        public string? Url { get; set; }
        public string? UserAgent { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    public class ErrorDetailsDto
    {
        public string? Message { get; set; }
        public string? Name { get; set; }
        public string? Stack { get; set; }
        public string? ComponentStack { get; set; }
        // Additional properties can be added as needed
    }
}