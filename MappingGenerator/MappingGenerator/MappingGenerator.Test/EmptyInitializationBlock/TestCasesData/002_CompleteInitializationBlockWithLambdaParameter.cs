﻿using System;
using System.Linq.Expressions;

namespace MappingGenerator.Test.EmptyInitializationBlock.TestCasesData
{
    public class UserDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }

    public class UserEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }

    public class Mapper
    {
        public Expression<Func<UserEntity, UserDTO>> Map = (UserEntity entity) => new UserDTO(){};
    }
}
