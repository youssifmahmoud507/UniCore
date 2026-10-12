using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Common.ValueObjects
{
    public static class AddressErrors
    {
        public static readonly Error StreetInvalid = Error.Validation("ADDRESS_STREET_INVALID", "Street is required and must be at most 200 characters.");
        public static readonly Error CityInvalid = Error.Validation("ADDRESS_CITY_INVALID", "City is required and must be at most 100 characters.");
        public static readonly Error StateInvalid = Error.Validation("ADDRESS_STATE_INVALID", "State must be at most 100 characters.");
        public static readonly Error PostalCodeInvalid = Error.Validation("ADDRESS_POSTAL_CODE_INVALID", "Postal code must be at most 20 characters.");
        public static readonly Error CountryInvalid = Error.Validation("ADDRESS_COUNTRY_INVALID", "Country is required and must be at most 100 characters.");
    }
}
