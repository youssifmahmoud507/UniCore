using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    public static class FinanceErrors
    {
        // ----- money -----
        public static readonly Error AmountRequired = Error.Validation("FINANCE_AMOUNT_REQUIRED", "An amount is required.");
        public static readonly Error AmountOutOfRange = Error.Validation("FINANCE_AMOUNT_OUT_OF_RANGE", "The amount is outside the allowed range.");
        public static readonly Error AmountPrecisionInvalid = Error.Validation("FINANCE_AMOUNT_PRECISION_INVALID", "An amount can have at most 2 decimal places.");
        public static readonly Error AmountMustBePositive = Error.Validation("FINANCE_AMOUNT_MUST_BE_POSITIVE", "The amount must be greater than zero.");
        public static readonly Error PercentInvalid = Error.Validation("FINANCE_PERCENT_INVALID", "A percentage must be between 0 and 100.");

        // ----- fee definition -----
        public static readonly Error FeeDefinitionNotFound = Error.NotFound("FEE_DEFINITION_NOT_FOUND", "The fee definition was not found.");
        public static readonly Error FeeDefinitionDataInvalid = Error.Validation("FEE_DEFINITION_DATA_INVALID", "The fee definition data is invalid.");
        public static readonly Error FeeCodeInvalid = Error.Validation("FEE_CODE_INVALID", "The fee code must be 2 to 30 characters: letters A-Z, digits and underscore.");
        public static readonly Error FeeCodeAlreadyExists = Error.Conflict("FEE_CODE_ALREADY_EXISTS", "A fee with this code already exists for this university.");
        public static readonly Error FeeDefinitionInactive = Error.BusinessRule("FEE_DEFINITION_INACTIVE", "The fee definition is not active.");

        // ----- fee structure -----
        public static readonly Error FeeStructureNotFound = Error.NotFound("FEE_STRUCTURE_NOT_FOUND", "The fee structure was not found.");
        public static readonly Error FeeStructureDataInvalid = Error.Validation("FEE_STRUCTURE_DATA_INVALID", "The fee structure data is invalid.");
        public static readonly Error FeeStructureNotDraft = Error.BusinessRule("FEE_STRUCTURE_NOT_DRAFT", "Only a draft fee structure can be changed.");
        public static readonly Error FeeStructureNotActive = Error.BusinessRule("FEE_STRUCTURE_NOT_ACTIVE", "Only an active fee structure can be archived.");
        public static readonly Error FeeStructureEmpty = Error.BusinessRule("FEE_STRUCTURE_EMPTY", "A fee structure needs at least one item before it can be activated.");
        public static readonly Error FeeStructureTooManyItems = Error.BusinessRule("FEE_STRUCTURE_TOO_MANY_ITEMS", "A fee structure cannot have that many items.");
        public static readonly Error FeeStructureScopeTaken = Error.Conflict("FEE_STRUCTURE_SCOPE_TAKEN", "Another active fee structure already covers this program and semester.");
        public static readonly Error FeeItemNotFound = Error.NotFound("FEE_ITEM_NOT_FOUND", "The fee structure item was not found.");
        public static readonly Error FeeItemDuplicate = Error.Conflict("FEE_ITEM_DUPLICATE", "This fee is already in the fee structure.");
    }


}
