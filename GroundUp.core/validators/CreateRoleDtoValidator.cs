using FluentValidation;
using GroundUp.core.dtos;
using GroundUp.core.entities;

namespace GroundUp.core.validators
{
    public class CreateRoleDtoValidator : AbstractValidator<CreateRoleDto>
    {
        public CreateRoleDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Role name is required")
                .MaximumLength(100).WithMessage("Role name cannot exceed 100 characters")
                .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Role name can only contain letters, numbers, underscores and hyphens");

            RuleFor(x => x.Description)
                .MaximumLength(255).WithMessage("Description cannot exceed 255 characters");

            RuleFor(x => x.RoleType)
                .IsInEnum().WithMessage("Invalid role type");

            When(x => x.RoleType == RoleType.Workspace, () => {
                RuleFor(x => x.WorkspaceId)
                    .NotEmpty().WithMessage("Workspace ID is required for workspace roles");
            });
        }
    }
}