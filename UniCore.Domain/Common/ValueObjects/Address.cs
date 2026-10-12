using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Common.ValueObjects
{
    // A postal address. Used by Campus now and by PersonProfile later.
    // Stored as columns of the owning table (EF owned type), not as its own table.
    public sealed class Address : ValueObject
    {
        public const int StreetMaxLength = 200;
        public const int CityMaxLength = 100;
        public const int StateMaxLength = 100;
        public const int PostalCodeMaxLength = 20;
        public const int CountryMaxLength = 100;

        private Address()
        {
        }

        public string Street { get; private set; } = string.Empty;
        public string City { get; private set; } = string.Empty;
        public string? State { get; private set; }
        public string? PostalCode { get; private set; }
        public string Country { get; private set; } = string.Empty;

        public static Result<Address> Create(string? street, string? city, string? state, string? postalCode, string? country)
        {
            var cleanStreet = street?.Trim();
            if (string.IsNullOrEmpty(cleanStreet) || cleanStreet.Length > StreetMaxLength)
            {
                return AddressErrors.StreetInvalid;
            }

            var cleanCity = city?.Trim();
            if (string.IsNullOrEmpty(cleanCity) || cleanCity.Length > CityMaxLength)
            {
                return AddressErrors.CityInvalid;
            }

            var cleanState = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
            if (cleanState is not null && cleanState.Length > StateMaxLength)
            {
                return AddressErrors.StateInvalid;
            }

            var cleanPostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
            if (cleanPostalCode is not null && cleanPostalCode.Length > PostalCodeMaxLength)
            {
                return AddressErrors.PostalCodeInvalid;
            }

            var cleanCountry = country?.Trim();
            if (string.IsNullOrEmpty(cleanCountry) || cleanCountry.Length > CountryMaxLength)
            {
                return AddressErrors.CountryInvalid;
            }

            return new Address
            {
                Street = cleanStreet,
                City = cleanCity,
                State = cleanState,
                PostalCode = cleanPostalCode,
                Country = cleanCountry
            };
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
            yield return State;
            yield return PostalCode;
            yield return Country;
        }
    }
}
