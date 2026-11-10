using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Materijalno.Model.EntityModels
{
    public partial class materijalno_knjigovodstvoContext
    {
        public DbSet<MaterialDocument> MaterialDocuments { get; set; }
        public DbSet<DocumentCounter> DocumentCounters { get; set; }
        public DbSet<ApplicationSetting> ApplicationSettings { get; set; }
        public DbSet<FinancialBatch> FinancialBatches { get; set; }
        public DbSet<FinancialExportLog> FinancialExportLogs { get; set; }
        public DbSet<YearClose> YearCloses { get; set; }
        private int _postingScopes;

        internal IDisposable AllowPosting()
        {
            _postingScopes++;
            return new PostingScope(() => _postingScopes--);
        }

        private sealed class PostingScope : IDisposable
        {
            private Action _close;
            public PostingScope(Action close) { _close = close; }
            public void Dispose() { var close = _close; _close = null; close?.Invoke(); }
        }

        public override int SaveChanges() => SaveChanges(true);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ChangeTracker.DetectChanges();
            foreach (var entry in ChangeTracker.Entries<Mat>().Where(e => e.Entity.IsDraft && (e.State == EntityState.Added || e.State == EntityState.Modified)).ToList())
            {
                if (entry.Entity.Ident <= 0) entry.Entity.Ident = null;
                if (entry.Entity.Kljnaz <= 0) entry.Entity.Kljnaz = null;
                if (entry.Entity.Kljnaz1 <= 0) entry.Entity.Kljnaz1 = null;
            }
            foreach (var entry in ChangeTracker.Entries<Inv>().Where(e => e.Entity.IsDraft && (e.State == EntityState.Added || e.State == EntityState.Modified)).ToList())
            {
                if (entry.Entity.Ident <= 0) entry.Entity.Ident = null;
                if (entry.Entity.Kljnaz <= 0) entry.Entity.Kljnaz = null;
            }
            ChangeTracker.DetectChanges();
            if (_postingScopes == 0)
            {
                if (ChangeTracker.Entries<Mat>().Any(e =>
                    (e.State == EntityState.Added && !e.Entity.IsDraft) ||
                    ((e.State == EntityState.Modified || e.State == EntityState.Deleted) &&
                     (!e.Entity.IsDraft || !e.OriginalValues.GetValue<bool>(nameof(global::Materijalno.Model.EntityModels.Mat.IsDraft))))))
                    throw new InvalidOperationException("Promet se snima kroz provjerenu komandu Snimi, Izmjena ili Brisanje.");
                if (ChangeTracker.Entries<Inv>().Any(e =>
                    (e.State == EntityState.Added && !e.Entity.IsDraft) ||
                    ((e.State == EntityState.Modified || e.State == EntityState.Deleted) &&
                     (!e.Entity.IsDraft || !e.OriginalValues.GetValue<bool>(nameof(global::Materijalno.Model.EntityModels.Inv.IsDraft))))))
                    throw new InvalidOperationException("Inventura se snima kroz provjerenu komandu Snimi.");
            }
            ValidateMasterChanges();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        private void ValidateMasterChanges()
        {
            foreach (var entry in ChangeTracker.Entries<SifarnikMaterijala>().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList())
            {
                var material = entry.Entity;
                if (material.Ident.GetValueOrDefault() <= 0 || string.IsNullOrWhiteSpace(material.Nazmat) || string.IsNullOrWhiteSpace(material.Jedm) ||
                    !material.Konto1.HasValue || !SifarnikKonta.Any(a => a.Sifkonta == material.Konto1))
                    throw new InvalidOperationException("Unesite pozitivnu šifru, naziv, jedinicu mjere i postojeći konto materijala.");
            }
            foreach (var entry in ChangeTracker.Entries<SifarnikMaterijala>().Where(e => e.State == EntityState.Deleted).ToList())
                if (Mat.IgnoreQueryFilters().Any(m => m.Ident == entry.Entity.Ident) ||
                    Inv.IgnoreQueryFilters().Any(i => i.Ident == entry.Entity.Ident) ||
                    SifarnikMaterijalSkladisteKonto.Any(m => m.Sifmat == entry.Entity.Ident))
                    throw new InvalidOperationException("Materijal ima promet ili kontne veze i ne može se obrisati.");
            foreach (var entry in ChangeTracker.Entries<SifarnikSkladista>().Where(e => e.State == EntityState.Deleted).ToList())
                if (Mat.IgnoreQueryFilters().Any(m => m.Kljnaz == entry.Entity.Kljnaz || m.Kljnaz1 == entry.Entity.Kljnaz) ||
                    Inv.IgnoreQueryFilters().Any(i => i.Kljnaz == entry.Entity.Kljnaz) ||
                    SifarnikMaterijalSkladisteKonto.Any(m => m.Sifskla == entry.Entity.Kljnaz))
                    throw new InvalidOperationException("Skladište ima promet ili kontne veze i ne može se obrisati.");
            foreach (var entry in ChangeTracker.Entries<SifarnikKonta>().Where(e => e.State == EntityState.Deleted).ToList())
                if (Mat.IgnoreQueryFilters().Any(m => m.Konto1 == entry.Entity.Sifkonta || m.Kontosklad == entry.Entity.Sifkonta || m.Kontosklad1 == entry.Entity.Sifkonta) ||
                    Nalmat.Any(n => n.Sintstav == entry.Entity.Sifkonta) ||
                    SifarnikMaterijalSkladisteKonto.Any(m => m.Sifkonta == entry.Entity.Sifkonta))
                    throw new InvalidOperationException("Konto je korišten i ne može se obrisati.");
            foreach (var entry in ChangeTracker.Entries<SifarnikMaterijalSkladisteKonto>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList())
            {
                var v = entry.Entity;
                if (!TabelaMaterijala.Any(m => m.Ident == v.Sifmat) ||
                    !SifarnikSkladista.Any(m => m.Kljnaz == v.Sifskla) ||
                    !SifarnikKonta.Any(m => m.Sifkonta == v.Sifkonta))
                    throw new InvalidOperationException("Materijal, skladište i konto moraju postojati u šifarnicima.");
                if (SifarnikMaterijalSkladisteKonto.Any(m => m.Id != v.Id && m.Sifmat == v.Sifmat && m.Sifskla == v.Sifskla))
                    throw new InvalidOperationException("Za taj materijal i skladište već postoji konto.");
            }
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Mat>(e =>
            {
                e.HasQueryFilter(m => !m.IsDraft && !m.IsArchived);
                e.Property(m => m.IsDraft).HasColumnName("is_draft");
                e.Property(m => m.IsArchived).HasColumnName("is_archived");
                e.Property(m => m.BookYear).HasColumnName("book_year");
                e.Property(m => m.DocumentId).HasColumnName("document_id");
                e.Property(m => m.TransferPairId).HasColumnName("transfer_pair_id");
                e.Property(m => m.LineToken).HasColumnName("line_token");
                e.Property(m => m.FoxRecno).HasColumnName("fox_recno");
                e.Property(m => m.RowVersion).HasColumnName("row_version").IsRowVersion();
                e.Property(m => m.Kolic).HasColumnType("decimal(18,2)");
                e.Property(m => m.Nc).HasColumnType("decimal(17,9)");
                e.Property(m => m.Vrijed).HasColumnType("decimal(18,2)");
                e.Property(m => m.Troskovi).HasColumnType("decimal(18,2)");
                e.Property(m => m.Trospe).HasColumnType("decimal(18,2)");
                e.Property(m => m.Porppp).HasColumnType("decimal(18,2)");
                e.Property(m => m.Brdok).HasMaxLength(100);
                e.Property(m => m.Brnar).HasMaxLength(100);
                e.HasIndex(m => m.LineToken).IsUnique();
                e.HasOne<MaterialDocument>().WithMany().HasForeignKey(m => m.DocumentId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Inv>(e =>
            {
                e.HasQueryFilter(m => !m.IsDraft && !m.IsArchived);
                e.Property(m => m.IsDraft).HasColumnName("is_draft");
                e.Property(m => m.IsArchived).HasColumnName("is_archived");
                e.Property(m => m.InventoryDate).HasColumnName("inventory_date");
                e.Property(m => m.InventorySessionId).HasColumnName("inventory_session_id");
                e.Property(m => m.FoxRecno).HasColumnName("fox_recno");
                e.Property(m => m.RowVersion).HasColumnName("row_version").IsRowVersion();
                e.Property(m => m.Kolic).HasColumnType("decimal(18,2)");
                e.Property(m => m.Nc).HasColumnType("decimal(17,9)");
                e.Property(m => m.Vrijed).HasColumnType("decimal(18,2)");
                e.Property(m => m.Troskovi).HasColumnType("decimal(18,2)");
                e.Property(m => m.Trospe).HasColumnType("decimal(18,2)");
                e.Property(m => m.Porppp).HasColumnType("decimal(18,2)");
            });
            modelBuilder.Entity<TabelaMaterijala>(e =>
            {
                e.HasKey(m => m.Id);
                e.HasAlternateKey(m => m.Ident);
                e.Property(m => m.Id).HasColumnName("Id");
                e.Property(m => m.Nazmat).HasMaxLength(220);
                e.Property(m => m.Jedm).HasMaxLength(12);
            });
            modelBuilder.Entity<SifarnikMaterijala>().Property(m => m.Nazmat).HasMaxLength(220);
            modelBuilder.Entity<SifarnikMaterijala>().Property(m => m.Jedm).HasMaxLength(12);
            modelBuilder.Entity<SifarnikKonta>().Property(m => m.Nazkont).HasMaxLength(220);
            modelBuilder.Entity<SifarnikSkladista>().Property(m => m.NazivOrg).HasMaxLength(100);
            modelBuilder.Entity<SifarnikSkladista>().Property(m => m.MjestoAdresa).HasMaxLength(100);
            modelBuilder.Entity<Nalmat>(e =>
            {
                e.Property(m => m.LineNumber).HasColumnName("line_number");
                e.Property(m => m.BatchId).HasColumnName("batch_id");
                e.Property(m => m.FoxRecno).HasColumnName("fox_recno");
                e.Property(m => m.Analst).HasMaxLength(50);
                e.Property(m => m.Devsta).HasMaxLength(10);
                e.Property(m => m.Bracuna).HasMaxLength(100);
                e.Property(m => m.Brdokst).HasMaxLength(100);
                e.Property(m => m.Kurs).HasColumnType("decimal(18,4)");
                e.Property(m => m.Dug1st).HasColumnType("decimal(18,2)");
                e.Property(m => m.Pot1st).HasColumnType("decimal(18,2)");
                foreach (string name in new[] { "Stodug1", "Stopot1", "Devdugst", "Stodevd", "Devpotst", "Stodevp" })
                    e.Property(name).HasColumnType("decimal(18,2)");
                e.HasOne<FinancialBatch>().WithMany().HasForeignKey(m => m.BatchId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<MaterialDocument>(e =>
            {
                e.ToTable("material_document"); e.HasKey(m => m.Id);
                e.Property(m => m.Id).HasColumnName("id");
                e.Property(m => m.Status).HasColumnName("status");
                e.Property(m => m.BookYear).HasColumnName("book_year");
                e.Property(m => m.Number).HasColumnName("number");
                e.Property(m => m.Warehouse).HasColumnName("warehouse");
                e.Property(m => m.CreatedAt).HasColumnName("created_at");
            });
            modelBuilder.Entity<DocumentCounter>(e =>
            {
                e.ToTable("document_counter"); e.HasKey(m => new { m.Name, m.BookYear });
                e.Property(m => m.Name).HasColumnName("name");
                e.Property(m => m.BookYear).HasColumnName("book_year");
                e.Property(m => m.LastNumber).HasColumnName("last_number");
            });
            modelBuilder.Entity<ApplicationSetting>(e =>
            {
                e.ToTable("app_settings"); e.HasKey(m => m.KeyName);
                e.Property(m => m.KeyName).HasColumnName("key_name"); e.Property(m => m.Value).HasColumnName("value");
            });
            modelBuilder.Entity<FinancialBatch>(e =>
            {
                e.ToTable("financial_batch"); e.HasKey(m => m.Id);
                e.Property(m => m.Id).HasColumnName("id");
                e.Property(m => m.SourceDocumentId).HasColumnName("source_document_id");
                e.Property(m => m.SourceStatus).HasColumnName("source_status");
                e.Property(m => m.Brfak).HasColumnName("brfak");
                e.Property(m => m.Activity).HasColumnName("activity");
                e.Property(m => m.BookYear).HasColumnName("book_year");
                e.Property(m => m.Number).HasColumnName("number");
                e.Property(m => m.DocumentDate).HasColumnName("document_date");
                e.Property(m => m.State).HasColumnName("state");
                e.Property(m => m.ContentHash).HasColumnName("content_hash");
                e.Property(m => m.Error).HasColumnName("error");
                e.Property(m => m.AttemptCount).HasColumnName("attempt_count");
                e.Property(m => m.CreatedAt).HasColumnName("created_at");
                e.Property(m => m.ExportedAt).HasColumnName("exported_at");
                e.Property(m => m.RowVersion).HasColumnName("row_version").IsRowVersion();
                e.HasOne<MaterialDocument>().WithMany().HasForeignKey(m => m.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<FinancialExportLog>(e =>
            {
                e.ToTable("financial_export_log"); e.HasKey(m => m.Id);
                e.Property(m => m.Id).HasColumnName("id"); e.Property(m => m.BatchId).HasColumnName("batch_id");
                e.Property(m => m.CreatedAt).HasColumnName("created_at"); e.Property(m => m.State).HasColumnName("state"); e.Property(m => m.Error).HasColumnName("error");
            });
            modelBuilder.Entity<YearClose>(e =>
            {
                e.ToTable("year_close"); e.HasKey(m => m.Id);
                e.Property(m => m.Id).HasColumnName("id"); e.Property(m => m.BookYear).HasColumnName("book_year");
                e.Property(m => m.NextYear).HasColumnName("next_year"); e.Property(m => m.CreatedAt).HasColumnName("created_at");
                e.Property(m => m.Source).HasColumnName("source"); e.Property(m => m.Quantity).HasColumnName("quantity").HasColumnType("decimal(38,2)"); e.Property(m => m.Value).HasColumnName("value").HasColumnType("decimal(38,2)");
            });
        }
    }
}
