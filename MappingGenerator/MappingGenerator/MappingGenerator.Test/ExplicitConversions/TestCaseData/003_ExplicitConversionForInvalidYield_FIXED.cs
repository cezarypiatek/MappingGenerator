using System.Collections.Generic;

namespace MappingGenerator.Test.ExplicitConversions.TestCaseData
{
    public class TestMapper
    {
        public AddressDTO Address { get; set; }

        public IEnumerable<AddressDTO> DoSomething(AddressEntity address)
        {
            yield return new AddressDTO()
            {
                FlatNo = address.FlatNo,
                BuildtingNo = address.BuildtingNo,
                Street = address.Street,
                ZipCode = address.ZipCode,
                City = address.City
            };
        }
    }

    public class AddressDTO
    {
        public string FlatNo { get; set; }
        public string BuildtingNo { get; set; }
        public string Street { get; set; }
        public string ZipCode { get; set; }
        public string City { get; set; }
    }

    public class AddressEntity
    {
        public string FlatNo { get; set; }
        public string BuildtingNo { get; set; }
        public string Street { get; set; }
        public string ZipCode { get; set; }
        public string City { get; set; }
    }
}