using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecruiterAi.Domain.Entities;

namespace RecruiterAi.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.ToTable("positions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Description).IsRequired();
        b.Property(x => x.Country).HasMaxLength(100);
        b.Property(x => x.SeniorityLevel).HasMaxLength(50);

        // List<string> → jsonb. Npgsql serializes through System.Text.Json automatically.
        b.Property(x => x.RequiredSkills).HasColumnType("jsonb");
        b.Property(x => x.NiceToHaveSkills).HasColumnType("jsonb");

        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
    }
}
