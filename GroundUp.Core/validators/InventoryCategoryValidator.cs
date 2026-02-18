using FluentValidation;
using GroundUp.Core.dtos;

namespace GroundUp.Core.validators
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
