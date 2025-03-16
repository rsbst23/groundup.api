using FluentValidation;
using GroundUp.core.dtos;

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

            When(x => x.IsClientRole, () => {
                RuleFor(x => x.ClientId)
                    .NotEmpty().WithMessage("Client ID is required for client roles");
            });
        }
    }
}
