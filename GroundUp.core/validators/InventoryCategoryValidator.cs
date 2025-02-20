using FluentValidation;
using GroundUp.core.dtos;

namespace GroundUp.core.validators
{
    public class InventoryCategoryValidator : AbstractValidator<InventoryCategoryDto>
    {
        public InventoryCategoryValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Category name is required.")
                .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.");
        }
    }
}
