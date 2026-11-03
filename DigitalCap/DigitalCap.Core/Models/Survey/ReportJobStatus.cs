namespace DigitalCap.Core.Models.Survey
{
    /// Background job state for Validate All (same as digital-cap ValidationJobStatus).
    public class ValidationJobStatus
    {
        public string Status { get; set; } = "Processing";
        public string? Result { get; set; }
        public string? Error { get; set; }
    }

    /// Background job state for the Export All PDF (same idea as digital-cap MergeJobStatus).
    public class PdfJobStatus
    {
        public string Status { get; set; } = "Processing";
        public byte[]? Bytes { get; set; }
        public string? FileName { get; set; }
        public string? Error { get; set; }
    }
}
