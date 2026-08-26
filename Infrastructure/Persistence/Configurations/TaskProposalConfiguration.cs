using DoIt.Api.Domain.Entities;
using DoIt.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DoIt.Api.Infrastructure.Persistence.Configurations;

public sealed class TaskProposalConfiguration : IEntityTypeConfiguration<TaskProposal>
{
    public void Configure(EntityTypeBuilder<TaskProposal> builder)
    {
        builder.ToTable("TaskProposals");
        builder.HasKey(proposal => proposal.Id);
        builder.Property(proposal => proposal.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(proposal => proposal.Title).HasMaxLength(220).IsRequired();
        builder.Property(proposal => proposal.Description).HasMaxLength(2000);
        builder.Property(proposal => proposal.Scope).HasMaxLength(32);
        builder.Property(proposal => proposal.TaskType).HasMaxLength(32);
        builder.Property(proposal => proposal.Importance).HasMaxLength(32);
        builder.Property(proposal => proposal.Complexity).HasMaxLength(32);
        builder.Property(proposal => proposal.Obligation).HasMaxLength(32);
        builder.Property(proposal => proposal.AssignmentMode).HasMaxLength(32);
        builder.Property(proposal => proposal.AssigneeIdsJson).HasMaxLength(4000);
        builder.Property(proposal => proposal.ScheduleJson).HasMaxLength(4000);
        builder.HasIndex(proposal => new { proposal.TargetUserId, proposal.Status, proposal.CreatedAt });
        builder.HasIndex(proposal => new { proposal.ProposerUserId, proposal.CreatedAt });
        builder.HasOne(proposal => proposal.ProposerUser)
            .WithMany()
            .HasForeignKey(proposal => proposal.ProposerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(proposal => proposal.TargetUser)
            .WithMany()
            .HasForeignKey(proposal => proposal.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
