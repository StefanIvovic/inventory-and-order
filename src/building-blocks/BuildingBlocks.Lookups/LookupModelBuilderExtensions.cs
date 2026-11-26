using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Lookups;

public static class LookupModelBuilderExtensions
{
    public const string TypesTable = "lookup_types";
    public const string ValuesTable = "lookup_values";
    public const string TypeCodeIndex = "ux_lookup_types_code";
    public const string ValueCodeIndex = "ux_lookup_values_type_code";
    public const string ValueNameIndex = "ix_lookup_values_type_name";

    public static ModelBuilder ApplyLookups(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        var codeCheck = $"\"Code\" ~ '{LookupType.CodePattern}'";

        modelBuilder.Entity<LookupType>(type =>
        {
            type.ToTable(TypesTable, table => table.HasCheckConstraint("ck_lookup_types_code", codeCheck));
            type.HasKey(t => t.Id);
            type.Property(t => t.Id).ValueGeneratedNever();
            type.Property(t => t.Code).IsRequired().HasMaxLength(LookupType.CodeMaxLength);
            type.Property(t => t.Name).IsRequired().HasMaxLength(LookupType.NameMaxLength);
            type.HasIndex(t => t.Code).IsUnique().HasDatabaseName(TypeCodeIndex);
        });

        modelBuilder.Entity<LookupValue>(value =>
        {
            value.ToTable(ValuesTable, table => table.HasCheckConstraint("ck_lookup_values_code", codeCheck));
            value.HasKey(v => v.Id);
            value.Property(v => v.Id).ValueGeneratedNever();
            value.Property(v => v.Code).IsRequired().HasMaxLength(LookupType.CodeMaxLength);
            value.Property(v => v.Name).IsRequired().HasMaxLength(LookupType.NameMaxLength);
            value.HasOne<LookupType>().WithMany().HasForeignKey(v => v.LookupTypeId).OnDelete(DeleteBehavior.Restrict);
            value.HasIndex(v => new { v.LookupTypeId, v.Code }).IsUnique().HasDatabaseName(ValueCodeIndex);
            value.HasIndex(v => new { v.LookupTypeId, v.Name, v.Id }).HasDatabaseName(ValueNameIndex);
        });

        return modelBuilder;
    }
}
