using System;

namespace Materijalno.Model.EntityModels
{
    public sealed class MaterialDocument
    {
        public Guid Id { get; set; }
        public string Status { get; set; }
        public int BookYear { get; set; }
        public string Number { get; set; }
        public int Warehouse { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class DocumentCounter
    {
        public string Name { get; set; }
        public int BookYear { get; set; }
        public int LastNumber { get; set; }
    }

    public sealed class ApplicationSetting
    {
        public string KeyName { get; set; }
        public string Value { get; set; }
    }

    public sealed class FinancialBatch
    {
        public Guid Id { get; set; }
        public Guid? SourceDocumentId { get; set; }
        public string SourceStatus { get; set; }
        public string Brfak { get; set; }
        public int Activity { get; set; }
        public int BookYear { get; set; }
        public int Number { get; set; }
        public DateTime DocumentDate { get; set; }
        public string State { get; set; }
        public string ContentHash { get; set; }
        public string Error { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExportedAt { get; set; }
        public byte[] RowVersion { get; set; }
    }

    public sealed class FinancialExportLog
    {
        public long Id { get; set; }
        public Guid BatchId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string State { get; set; }
        public string Error { get; set; }
    }

    public sealed class YearClose
    {
        public int Id { get; set; }
        public int BookYear { get; set; }
        public int NextYear { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Source { get; set; }
        public decimal Quantity { get; set; }
        public decimal Value { get; set; }
    }
}
