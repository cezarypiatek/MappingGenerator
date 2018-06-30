using System;
using System.Collections.Generic;
using System.Text;

namespace MappingGenerator.Test.TestCases
{
    public class UserDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }

    class Class1
    {
        public void CreateUser(int age)
        {
            var firstName = "Cezary";
            var lastName = "Piatek";

            var x = new UserDTO()

            {
                FirstName = firstName,
                LastName = lastName,
                Age = age
            };
        }
    }
}
