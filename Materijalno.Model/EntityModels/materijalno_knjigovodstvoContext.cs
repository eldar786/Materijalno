using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

// Code scaffolded by EF Core assumes nullable reference types (NRTs) are not used or disabled.
// If you have enabled NRTs for your project, then un-comment the following line:
// #nullable disable

namespace Materijalno.Model.EntityModels
{
    public partial class materijalno_knjigovodstvoContext : DbContext
    {
        public materijalno_knjigovodstvoContext()
        {
        }

        public materijalno_knjigovodstvoContext(DbContextOptions<materijalno_knjigovodstvoContext> options)
            : base(options)
        {
        }

        public virtual DbSet<SifarnikSkladista> SifarnikSkladista { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseSqlServer("Server=DB-SRV-01;Database=materijalno_knjigovodstvo;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SifarnikSkladista>(entity =>
            {
                entity.ToTable("sifarnik_skladista");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.DevizniRacun).HasColumnName("devizni_racun");

                entity.Property(e => e.Kljnaz).HasColumnName("kljnaz");

                entity.Property(e => e.MjestoAdresa)
                    .HasColumnName("mjesto_adresa")
                    .HasMaxLength(50);

                entity.Property(e => e.NazivOrg)
                    .HasColumnName("naziv_org")
                    .HasMaxLength(50);

                entity.Property(e => e.NazivSdk)
                    .HasColumnName("naziv_sdk")
                    .HasMaxLength(50);

                entity.Property(e => e.Opstina)
                    .HasColumnName("opstina")
                    .HasMaxLength(50);

                entity.Property(e => e.PozBr).HasColumnName("poz_br");

                entity.Property(e => e.PozBrD).HasColumnName("poz_br_d");

                entity.Property(e => e.ZiroRacun).HasColumnName("ziro_racun");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
